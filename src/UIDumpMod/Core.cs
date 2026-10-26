// UIDumpMod — 运行时 UI 兜底翻译层（AbyssMod 的补漏组件）
//
// 解决的问题:
//   AbyssMod 的 ui_texts 翻译表按「运行时 Transform 路径」精确匹配。
//   但静态从 UI 预制体提取的翻译条目, key 是预制体相对路径
//   (如 "Scene_NetherTop/NetherMenu/..."), 与运行时路径
//   (如 "NetherTop/UICanvas/RootUI/UIGroup/Scene_NetherTop/..." + "(Clone)" 后缀)
//   对不上, 导致数千条翻译从未生效, 界面残留日文。
//
// 本 mod 在 AbyssMod 的 prefix 翻译之后(postfix)出手, 把漏网的日文按
// 「路径后缀对齐」规则再翻一次, 翻译数据直接复用同一份 ui_texts 表。
//
// 日志标记:
//   [FIXED] path|text —— 兜底命中并替换成功
//   [LEAK]  path|text —— 仍然无法翻译的日文(收日志滚动补译的输入)
//
// 数据流与维护方法见同目录 README.md。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using MelonLoader;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(UIDumpMod.Core), "UIDumpMod", "1.4.0", "ztztfdsf")]
[assembly: MelonGame("EXNOA LLC.", "ドットアビスX for Android")]

namespace UIDumpMod
{
    public class Core : MelonMod
    {
        // ---------- 配置 ----------

        /// <summary>判定「还需要翻译」的日文字符范围。故意排除全角长音ー与中点・(避免误伤已译中文)。</summary>
        private static readonly Regex Japanese = new Regex(
            @"[\u3040-\u309F\u30A1-\u30FA\u30FD\u30FE\u31F0-\u31FF\uFF66-\uFF9D]");

        /// <summary>{0} 占位符, 与 AbyssMod 的 UiTextPattern 模板语法一致。</summary>
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);

        /// <summary>全场景兜底扫描间隔(帧)。120 帧 ≈ 2 秒。</summary>
        private const int SweepInterval = 120;

        /// <summary>防重: 已报告过的 [LEAK] (tag|path|text)。</summary>
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>重入保护: 我们自己 set_text 触发 hook 时跳过。</summary>
        private static bool _applying;

        // ---------- 翻译表 ----------
        //
        // 从 AbyssMod 的缓存目录读同一份 ui_texts/zh_Hans.json, 按 key 风格分两组:
        //   _exactPaths  — 运行时风格 key(以 "Engine/" 开头或含 "/View/Canvas/"), 直接整串匹配
        //   _suffixPaths — 预制体相对 key(其余), 作为归一化运行时路径的后缀匹配
        //   _patterns    — 含 {0} 占位符的模板条目(数字/日期等动态文本)

        private static Dictionary<string, Dictionary<string, string>> _exactPaths;
        private static Dictionary<string, Dictionary<string, string>> _suffixPaths;
        private static readonly Dictionary<string, List<Pattern>> _patterns = new Dictionary<string, List<Pattern>>();
        private static int _fixedCount;

        private sealed class Pattern
        {
            public readonly Regex Source;
            public readonly string Template;

            public Pattern(string sourceTemplate, string translatedTemplate)
            {
                Source = new Regex(Build(sourceTemplate), RegexOptions.Compiled | RegexOptions.CultureInvariant);
                Template = translatedTemplate;
            }

            /// <summary>把 "{0}フロア目" 之类的模板编译成 "^第?(&lt;p0&gt;.+?)…$" 捕获正则。</summary>
            private static string Build(string t)
            {
                var sb = new StringBuilder("^");
                var seen = new HashSet<string>();
                int last = 0;
                foreach (Match m in Placeholder.Matches(t))
                {
                    sb.Append(Regex.Escape(t.Substring(last, m.Index - last)));
                    var g = "p" + m.Groups[1].Value;
                    if (seen.Add(g)) sb.Append("(?<" + g + ">.+?)");
                    else sb.Append("\\k<" + g + ">");
                    last = m.Index + m.Length;
                }
                sb.Append(Regex.Escape(t.Substring(last))).Append('$');
                return sb.ToString();
            }

            public bool TryTranslate(string s, out string result)
            {
                var m = Source.Match(s);
                if (!m.Success) { result = null; return false; }
                result = Placeholder.Replace(Template, mm =>
                {
                    var g = m.Groups["p" + mm.Groups[1].Value];
                    return g.Success ? g.Value : mm.Value;
                });
                return !string.IsNullOrEmpty(result);
            }
        }

        // ---------- 生命周期 ----------

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("UIDumpMod initializing");
            LoadTranslations();
            var h = new HarmonyLib.Harmony("com.ztztfdsf.uidump");
            Patches.Apply(h, LoggerInstance);
            LoggerInstance.Msg($"UIDumpMod table: exact={_exactPaths?.Count ?? 0} suffix={_suffixPaths?.Count ?? 0} patterns={_patterns.Count}");
        }

        private int _frame;
        public override void OnLateUpdate()
        {
            _frame++;
            if (_frame % SweepInterval != 0) return;
            try { SweepScene(); } catch { }
        }

        /// <summary>全场景扫描: 兜住不走 set_text/OnEnable 的文本(如粒子标签、纹理换字)。</summary>
        private static void SweepScene()
        {
            foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
            {
                try { if (tmp != null) Handle(tmp, "Sweep"); } catch { }
            }
        }

        // ---------- 数据加载 ----------

        private void LoadTranslations()
        {
            try
            {
                var path = Path.Combine(
                    MelonLoader.Utils.MelonEnvironment.UserDataDirectory,
                    "AbyssMod", "translations", "ui_texts", "zh_Hans.json");
                if (!File.Exists(path))
                {
                    LoggerInstance.Warning("ui_texts json not found: " + path);
                    return;
                }
                var parsed = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path));

                _exactPaths = new Dictionary<string, Dictionary<string, string>>();
                _suffixPaths = new Dictionary<string, Dictionary<string, string>>();
                foreach (var kv in parsed)
                {
                    var key = kv.Key;
                    if (key.Contains('*')) continue; // 通配符 key 交给 AbyssMod 自己处理
                    var target = (key.StartsWith("Engine/") || key.Contains("/View/Canvas/"))
                        ? _exactPaths : _suffixPaths;
                    if (_exactPaths.ContainsKey(key) || _suffixPaths.ContainsKey(key)) continue;
                    target[key] = kv.Value;

                    var pats = new List<Pattern>();
                    foreach (var t in kv.Value)
                        if (Placeholder.IsMatch(t.Key)) pats.Add(new Pattern(t.Key, t.Value));
                    if (pats.Count > 0) _patterns[key] = pats;
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Warning("LoadTranslations failed: " + e.Message);
            }
        }

        // ---------- 路径与匹配 ----------

        public static string PathOf(Transform t)
        {
            if (t == null) return null;
            var stack = new List<string>();
            while (t != null) { stack.Add(t.name); t = t.parent; }
            return string.Join("/", stack);
        }

        /// <summary>"Popup_SoundCaution(Clone)/Box/Text" → "Popup_SoundCaution/Box/Text"</summary>
        public static string Normalize(string path)
        {
            if (path == null) return null;
            var segs = path.Split('/');
            for (int i = 0; i < segs.Length; i++)
                if (segs[i].EndsWith("(Clone)")) segs[i] = segs[i].Substring(0, segs[i].Length - 7);
            return string.Join("/", segs);
        }

        /// <summary>兜底翻译入口: exact 整串 → suffix 逐级后缀。</summary>
        public static bool TryFallback(string path, string text, out string result)
        {
            result = null;
            if (_exactPaths == null || path == null) return false;
            var norm = Normalize(path);
            if (TryDict(_exactPaths, norm, text, out result)) return true;
            if (TryDict(_exactPaths, path, text, out result)) return true;
            // 预制体相对 key 是归一化路径的后缀: 从完整路径逐级砍头尝试
            for (int cut = 0; cut <= norm.Length; cut++)
            {
                var suffix = cut == 0 ? norm : norm.Substring(cut);
                if (TryDict(_suffixPaths, suffix, text, out result)) return true;
                var next = norm.IndexOf('/', cut);
                if (next < 0) break;
                cut = next; // for 循环 cut++ 后正好落在下一个后缀的起点
            }
            return false;
        }

        private static bool TryDict(Dictionary<string, Dictionary<string, string>> paths,
            string key, string text, out string result)
        {
            result = null;
            if (key == null || !paths.TryGetValue(key, out var d)) return false;
            if (d.TryGetValue(text, out result)) return true;
            if (d.TryGetValue(text.Trim(), out result)) return true; // TMP getter 常带尾随换行
            if (_patterns.TryGetValue(key, out var pats))
                foreach (var p in pats)
                    if (p.TryTranslate(text, out result)) return true;
            return false;
        }

        // ---------- 主管道 ----------

        /// <summary>hook 回调统一入口: 日文才处理, 先兜底翻译, 失败则报 [LEAK]。</summary>
        public static void Handle(object comp, string tag)
        {
            try
            {
                if (_applying) return;
                if (!(comp is Component c)) return;
                var text = (comp as TMP_Text)?.text ?? (comp as Text)?.text;
                if (string.IsNullOrEmpty(text) || !Japanese.IsMatch(text)) return;

                var path = PathOf(c.transform);
                if (TryFallback(path, text, out var zh))
                {
                    _applying = true;
                    try
                    {
                        if (comp is TMP_Text tmp) tmp.text = zh;
                        else if (comp is Text legacy) legacy.text = zh;
                        _fixedCount++;
                        if (_fixedCount <= 20 || _fixedCount % 100 == 0)
                            MelonLogger.Msg("[FIXED] " + path + "|" + text.Replace("\n", "\\n"));
                    }
                    finally { _applying = false; }
                    return;
                }

                var dedup = tag + "|" + path + "|" + text;
                if (Reported.Contains(dedup)) return;
                Reported.Add(dedup);
                var shown = text.Replace("\n", "\\n").Replace("\r", "\\r");
                if (shown.Length > 300) shown = shown.Substring(0, 300) + "…";
                MelonLogger.Msg("[LEAK] " + tag + "|" + path + "|" + shown);
            }
            catch { }
        }
    }

    /// <summary>Harmony 补丁挂载: TMP 与 Legacy 两套文本组件的写入与激活时机。</summary>
    public static class Patches
    {
        public static void TmpSetText(TMP_Text __instance, string value) => Core.Handle(__instance, "TMP.set_text");
        public static void TmpOnEnable(TextMeshProUGUI __instance) => Core.Handle(__instance, "TMPUGUI.OnEnable");
        public static void LegacySetText(Text __instance, string value) => Core.Handle(__instance, "Legacy.set_text");
        public static void LegacyOnEnable(Text __instance) => Core.Handle(__instance, "Legacy.OnEnable");

        public static void Apply(HarmonyLib.Harmony h, MelonLogger.Instance log)
        {
            int ok = 0, fail = 0;
            void Try(string what, Action f)
            {
                try { f(); ok++; }
                catch (Exception e) { fail++; log.Warning($"patch failed [{what}]: {e.Message}"); }
            }
            Try("TMP.set_text", () => h.Patch(AccessTools.PropertySetter(typeof(TMP_Text), "text"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(TmpSetText))));
            Try("TMPUGUI.OnEnable", () => h.Patch(AccessTools.Method(typeof(TextMeshProUGUI), "OnEnable"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(TmpOnEnable))));
            Try("Legacy.set_text", () => h.Patch(AccessTools.PropertySetter(typeof(Text), "text"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(LegacySetText))));
            Try("Legacy.OnEnable", () => h.Patch(AccessTools.Method(typeof(Text), "OnEnable"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(LegacyOnEnable))));
            log.Msg($"UIDumpMod patched: {ok} ok, {fail} failed");
        }
    }
}
