using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UIDumpMod.Translation
{
    /// <summary>
    /// 碎片级翻译：处理「运行时把多个片段拼成一串」的文本，整串查表永远 miss。
    ///
    /// 典型场景是深渊事件选项按钮：
    ///   "&lt;color=#..&gt;アビスコード獲得&lt;/color&gt;\n&lt;color=#..&gt;HP40%減少&lt;/color&gt;"
    /// 做法：按 color 标签切开 → 每段文本单独翻译 → 标签原样保留 → 重新拼接。
    /// 任意一段含日文却翻不出时整体放弃（宁可报 [LEAK] 也不输出半中半日）。
    /// 数据来源：MasterData m_nether_floor_event_parts 的效果字段 + UI 格式串。
    /// 新的碎片句式加到 Glossary（整段精确匹配）或 Rules（正则）即可。
    /// </summary>
    public static class FragmentTranslator
    {
        private static readonly Regex ColorTag = new Regex("(<color=[^>]*>|</color>)", RegexOptions.Compiled);

        /// <summary>整段精确匹配。</summary>
        private static readonly Dictionary<string, string> Glossary = new Dictionary<string, string>
        {
            ["アビスコード獲得"] = "获得深渊代码",
            ["アイテム獲得"] = "获得道具",
            ["ナゾの鍵"] = "谜之钥匙",
        };

        /// <summary>代码系名称（恩恵ゲージ句式用）。</summary>
        private static readonly Dictionary<string, string> CodeNames = new Dictionary<string, string>
        {
            ["インパクト"] = "冲击", ["ラッシュ"] = "突进", ["セーフ"] = "安全", ["リスク"] = "风险",
        };

        /// <summary>正则规则，按顺序尝试（顺序即优先级）。build 拿到匹配结果返回译文。</summary>
        private static readonly (Regex rx, Func<Match, string> build)[] Rules =
        {
            (Rx(@"^アビスコイン(\d+)個?獲得$"), m => $"获得{m.Groups[1].Value}深渊币"),
            (Rx(@"^アビスコイン(\d+)個消費$"), m => $"消耗{m.Groups[1].Value}个深渊币"),
            (Rx(@"^HP(\d+)%回復$"),            m => $"HP恢复{m.Groups[1].Value}%"),
            (Rx(@"^HP(\d+)%減少$"),            m => $"HP减少{m.Groups[1].Value}%"),
            (Rx(@"^侵食([+-]?\d+)$"),          m => $"浸蚀{m.Groups[1].Value}"),
            (Rx(@"^(インパクト|ラッシュ|セーフ|リスク)コード系統の恩恵ゲージが$"),   m => CodeNames[m.Groups[1].Value] + "代码系恩惠槽"),
            (Rx(@"^し、(インパクト|ラッシュ|セーフ|リスク)コード系統の恩恵ゲージが$"), m => "，" + CodeNames[m.Groups[1].Value] + "代码系恩惠槽"),
            (Rx(@"^(\d+)減少$"),               m => $"减少{m.Groups[1].Value}"),
            (Rx(@"^(\d+)上昇$"),               m => $"上升{m.Groups[1].Value}"),
            (Rx(@"^しました。$"),              _ => "。"),
            (Rx("^し、$"),                     _ => "，"),
            (Rx("^,$"),                        _ => "，"),
        };

        private static Regex Rx(string pattern) => new Regex(pattern, RegexOptions.Compiled);

        public static bool TryTranslate(string text, out string result)
        {
            result = null;
            if (text == null || (text.IndexOf('<') < 0 && !JapaneseText.NeedsTranslation(text))) return false;

            var sb = new StringBuilder();
            foreach (var part in ColorTag.Split(text)) // 捕获组 → 标签本身也在结果里
            {
                if (part.Length == 0) continue;
                if (part.StartsWith("<")) { sb.Append(part); continue; }
                if (!TryFragment(part, out var zh)) return false;
                sb.Append(zh);
            }
            var built = sb.ToString();
            if (built == text) return false; // 什么都没翻
            result = built;
            return true;
        }

        /// <summary>
        /// 翻译单个片段：先查词表/规则，都不中时——不含日文的片段（空白/数字/已是中文）原样通过，含日文则失败。
        /// 规则必须先于日文判定：「10上昇」「侵食+3」「,」这类片段不含假名也不含日式字形，
        /// 先判定会被当成「无需翻译」直接漏过去。
        /// </summary>
        private static bool TryFragment(string fragment, out string result)
        {
            var core = fragment.Trim();
            result = fragment;
            if (core.Length == 0) return true;
            if (Glossary.TryGetValue(core, out var zh)) { result = zh; return true; }
            foreach (var (rx, build) in Rules)
            {
                var m = rx.Match(core);
                if (m.Success) { result = build(m); return true; }
            }
            return !JapaneseText.NeedsTranslation(core);
        }
    }
}
