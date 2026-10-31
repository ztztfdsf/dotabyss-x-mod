using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using UIDumpMod.Translation;

namespace UIDumpMod
{
    /// <summary>
    /// Harmony 补丁 + 文本处理管道。
    ///
    /// 挂点：TMP_Text.text setter、TextMeshProUGUI.OnEnable，以及 Legacy UGUI Text 的同名两处，全部 postfix。
    /// AbyssMod 在 set_text 的 prefix 里先翻；我们在 postfix 里只处理它翻完仍含日文的文本。
    ///
    /// 日志：
    ///   [FIXED] path|原文         兜底翻译成功（前 20 条逐条打，之后每 100 条打 1 条）
    ///   [LEAK]  tag|path|原文     仍翻不出的日文（同一 tag+path+文本只报一次）→ tools/leak2table.py 的输入
    /// </summary>
    public static class TextHooks
    {
        private static readonly HashSet<string> ReportedLeaks = new HashSet<string>();
        private static int _fixedCount;

        /// <summary>重入保护：我们自己回写 .text 会再次触发 setter hook。</summary>
        private static bool _writing;

        // ---------- Harmony 挂载 ----------

        public static void Apply(HarmonyLib.Harmony harmony, MelonLogger.Instance log)
        {
            int ok = 0, fail = 0;
            void Patch(string name, System.Reflection.MethodBase target, string postfix)
            {
                try { harmony.Patch(target, postfix: new HarmonyMethod(typeof(TextHooks), postfix)); ok++; }
                catch (Exception e) { fail++; log.Warning($"patch failed [{name}]: {e.Message}"); }
            }
            Patch("TMP.set_text",     AccessTools.PropertySetter(typeof(TMP_Text), "text"),     nameof(TmpSetText));
            Patch("TMPUGUI.OnEnable", AccessTools.Method(typeof(TextMeshProUGUI), "OnEnable"), nameof(TmpOnEnable));
            Patch("Legacy.set_text",  AccessTools.PropertySetter(typeof(Text), "text"),         nameof(LegacySetText));
            Patch("Legacy.OnEnable",  AccessTools.Method(typeof(Text), "OnEnable"),            nameof(LegacyOnEnable));
            log.Msg($"UIDumpMod patched: {ok} ok, {fail} failed");
        }

        // Harmony 按参数名注入 __instance，方法必须 public static
        public static void TmpSetText(TMP_Text __instance)       => Handle(__instance, "TMP.set_text");
        public static void TmpOnEnable(TextMeshProUGUI __instance) => Handle(__instance, "TMPUGUI.OnEnable");
        public static void LegacySetText(Text __instance)        => Handle(__instance, "Legacy.set_text");
        public static void LegacyOnEnable(Text __instance)       => Handle(__instance, "Legacy.OnEnable");

        // ---------- 处理管道 ----------

        /// <summary>取文本 → 仍含日文？→ 查表 / 碎片翻译 → 回写，失败则报 [LEAK]。永不抛异常。</summary>
        public static void Handle(Component comp, string tag)
        {
            if (_writing || comp == null) return;
            try
            {
                var tmp = comp as TMP_Text;
                var legacy = comp as Text;
                var text = tmp != null ? tmp.text : legacy?.text;
                if (!JapaneseText.NeedsTranslation(text)) return;

                var path = PathOf(comp.transform);
                string zh = null;
                bool ok = (Core.Table != null && Core.Table.TryTranslate(path, text, out zh))
                          || (Core.Table != null && Core.Table.TryTranslateGlobal(text, out zh))
                          || FragmentTranslator.TryTranslate(text, out zh);
                if (ok) Write(tmp, legacy, zh, path, text);
                else ReportLeak(tag, path, text);
            }
            catch { }
        }

        private static void Write(TMP_Text tmp, Text legacy, string zh, string path, string original)
        {
            _writing = true;
            try
            {
                if (tmp != null) tmp.text = zh;
                else if (legacy != null) legacy.text = zh;
                _fixedCount++;
                if (_fixedCount <= 20 || _fixedCount % 100 == 0)
                    MelonLogger.Msg("[FIXED] " + path + "|" + original.Replace("\n", "\\n"));
            }
            finally { _writing = false; }
        }

        private static void ReportLeak(string tag, string path, string text)
        {
            if (!ReportedLeaks.Add(tag + "|" + path + "|" + text)) return;
            var shown = text.Replace("\n", "\\n").Replace("\r", "\\r");
            if (shown.Length > 300) shown = shown.Substring(0, 300) + "…";
            MelonLogger.Msg("[LEAK] " + tag + "|" + path + "|" + shown);
        }

        /// <summary>Transform 全路径，父→子，"/" 分隔（与 AbyssMod 的 ui_texts key 同格式）。</summary>
        public static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
