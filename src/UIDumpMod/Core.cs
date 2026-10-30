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
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using MelonLoader;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(UIDumpMod.Core), "UIDumpMod", "1.7.0", "ztztfdsf")]
[assembly: MelonGame("EXNOA LLC.", "ドットアビスX for Android")]

namespace UIDumpMod
{
    public class Core : MelonMod
    {
        // ---------- 配置 ----------

        /// <summary>判定「还需要翻译」的日文字符范围。故意排除全角长音ー与中点・(避免误伤已译中文)。</summary>
        private static readonly Regex Japanese = new Regex(
            @"[\u3040-\u309F\u30A1-\u30FA\u30FD\u30FE\u31F0-\u31FF\uFF66-\uFF9D]");

        /// <summary>
        /// 日式汉字标记(假名盲区补充): 这些字形是日文新字体/繁体, 我们的简中译文里不会出现,
        /// 因此出现即视为日文。纯汉字 UI 标签(総合戦力/推奨戦力/浸食率/現在)靠它兜住。
        /// </summary>
        private static readonly char[] JpMarkerKanji =
            "戦奨円満検絵価帰斉勲圧広遠間変険県続読済現際従様対処売歩沢淵減復費個獲図鑑値伝覚".ToCharArray();

        private static bool NeedsTranslation(string s)
            => Japanese.IsMatch(s) || s.IndexOfAny(JpMarkerKanji) >= 0;

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
            ToastPump.Attach(LoggerInstance);
            // 钩住原版 Toast.Show：AbyssMod 的所有弹窗（启动成功/翻译失败等）从这里进我们的自绘队列
            try
            {
                var utilAsm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Utility");
                var toastT = utilAsm.GetType("Utility.Notifications.Toast");
                var show = AccessTools.Method(toastT, "Show");
                h.Patch(show, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.ToastShow)));
                LoggerInstance.Msg("[TP] Toast.Show hooked");
            }
            catch (Exception e) { LoggerInstance.Warning("[TP] Toast.Show hook failed: " + e.Message); }
        }

        // ---------- 自绘 Toast（原版视觉参数） ----------
        // 1.15.0 重生成 interop 方法表部分错位：Utility 渲染链（ToastTheme/Color ctor/AndroidToastRenderer）必崩，
        // 故面子照抄原版参数、里子全自写；只依赖探针验证过的 UGUI 槽位。
        public override void OnUpdate() => ToastPump.Tick(UnityEngine.Time.unscaledDeltaTime);
        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => ToastPump.ProbeOnce();

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
            stack.Reverse(); // 收集时是子→父, 翻成父→子
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

        // ---------- 碎片级翻译 ----------
        //
        // 深渊事件选项按钮的文本是运行时拼接的: <color=..>增益</color> + 换行 + <color=..>损耗</color>
        // 整串查表永远 miss。这里把 color 标签之间的文本段拆开逐个翻译再重组,
        // 覆盖任意增益/损耗组合(数据源: m_nether_floor_event_parts 的效果字段 + UI 格式串)。

        private static readonly Regex ColorTag = new Regex("(<color=[^>]*>|</color>)", RegexOptions.Compiled);

        /// <summary>碎片词汇表: 精确匹配的片段。</summary>
        private static readonly Dictionary<string, string> FragGlossary = new Dictionary<string, string>
        {
            ["アビスコード獲得"] = "获得深渊代码",
            ["アイテム獲得"] = "获得道具",
            ["ナゾの鍵"] = "谜之钥匙",
        };

        /// <summary>代码系名称(恩恵ゲージ句式用)。</summary>
        private static readonly Dictionary<string, string> CodeNames = new Dictionary<string, string>
        {
            ["インパクト"] = "冲击", ["ラッシュ"] = "突进", ["セーフ"] = "安全", ["リスク"] = "风险",
        };

        /// <summary>碎片规则: 正则模板片段。(顺序即优先级)</summary>
        private static readonly (Regex rx, string tmpl)[] FragRules =
        {
            (new Regex(@"^アビスコイン(\d+)個?獲得$"),          "获得{0}深渊币"),
            (new Regex(@"^アビスコイン(\d+)個消費$"),          "消耗{0}个深渊币"),
            (new Regex(@"^HP(\d+)%回復$"),                     "HP恢复{0}%"),
            (new Regex(@"^HP(\d+)%減少$"),                     "HP减少{0}%"),
            (new Regex(@"^侵食([+-]?\d+)$"),                   "浸蚀{0}"),
            (new Regex(@"^(インパクト|ラッシュ|セーフ|リスク)コード系統の恩恵ゲージが$"), "__CODE__"),
            (new Regex(@"^し、(インパクト|ラッシュ|セーフ|リスク)コード系統の恩恵ゲージが$"), "，__CODE__"),
            (new Regex(@"^(\d+)減少$"),                        "减少{0}"),
            (new Regex(@"^(\d+)上昇$"),                        "上升{0}"),
            (new Regex(@"^しました。$"),                        "。"),
            (new Regex("^し、$"),                               "，"),
            (new Regex("^,$"),                                  "，"),
        };

        /// <summary>翻译单个文本碎片(先词汇表后规则)。不含日文的片段原样返回。</summary>
        private static bool TranslateFragment(string frag, out string result)
        {
            var core = frag.Trim();
            result = frag;
            if (core.Length == 0 || !NeedsTranslation(core)) return true; // 空白/非日文 → 原样
            if (FragGlossary.TryGetValue(core, out var zh)) { result = zh; return true; }
            foreach (var (rx, tmpl) in FragRules)
            {
                var m = rx.Match(core);
                if (!m.Success) continue;
                if (tmpl == "__CODE__" || tmpl == "，__CODE__")
                {
                    var name = CodeNames[m.Groups[1].Value];
                    result = tmpl == "__CODE__" ? name + "代码系恩惠槽" : "，" + name + "代码系恩惠槽";
                }
                else
                {
                    var args = new string[m.Groups.Count - 1];
                    for (int i = 1; i < m.Groups.Count; i++) args[i - 1] = m.Groups[i].Value;
                    result = string.Format(tmpl, args);
                }
                return true;
            }
            return false; // 含日文但翻不出 → 整体放弃
        }

        /// <summary>组合文本碎片翻译: 拆 color 标签, 逐段翻译, 全部成功才重组。</summary>
        public static bool TryFragments(string text, out string result)
        {
            result = null;
            if (text == null || (text.IndexOf('<') < 0 && !NeedsTranslation(text))) return false;
            var sb = new StringBuilder();
            foreach (var part in ColorTag.Split(text))
            {
                if (part.Length == 0) continue;
                if (part.StartsWith("<")) { sb.Append(part); continue; } // 标签原样
                if (!TranslateFragment(part, out var zh)) return false;
                sb.Append(zh);
            }
            var built = sb.ToString();
            if (built == text) return false; // 没有任何变化
            result = built;
            return true;
        }

        // ---------- 主管道 ----------

        /// <summary>hook 回调统一入口: 日文才处理, 先兜底翻译, 失败则报 [LEAK]。</summary>
        public static void Handle(object comp, string tag)
        {
            try
            {
                if (_applying) return;
                if (!(comp is Component c)) return;
                if (comp is TextMeshProUGUI tu) ToastPump.OnAnchor(tu);
                var text = (comp as TMP_Text)?.text ?? (comp as Text)?.text;
                if (string.IsNullOrEmpty(text) || !NeedsTranslation(text)) return;
                var path = PathOf(c.transform);
                if (TryFallback(path, text, out var zh) || TryFragments(text, out zh))
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

        // 原版 Toast.Show 的 postfix：按参数名绑定（kind 是 Utility 的枚举，用 object 接避免类型不匹配）
        public static void ToastShow(string title, string message, object kind, float duration)
            => ToastPump.Show(title, message, Convert.ToInt32(kind), duration);
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
