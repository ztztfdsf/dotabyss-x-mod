using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UIDumpMod.Translation
{
    /// <summary>
    /// 带占位符的翻译模板：ui_texts 里形如 "あと{0}時間{1}分" → "剩余{0}小时{1}分" 的条目。
    /// 语法与 AbyssMod 的 UiTextPattern 一致：{n} 匹配任意非空文本并原样回填到译文的同号位置。
    /// </summary>
    public sealed class TemplatePattern
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);

        private readonly Regex _source;
        private readonly string _translated;

        public TemplatePattern(string sourceTemplate, string translatedTemplate)
        {
            _source = new Regex(ToRegex(sourceTemplate), RegexOptions.Compiled | RegexOptions.CultureInvariant);
            _translated = translatedTemplate;
        }

        public static bool HasPlaceholder(string s) => Placeholder.IsMatch(s);

        /// <summary>"{0}フロア目" → "^(?&lt;p0&gt;.+?)フロア目$"；同一占位符第二次出现用反向引用。</summary>
        private static string ToRegex(string template)
        {
            var sb = new StringBuilder("^");
            var seen = new HashSet<string>();
            int last = 0;
            foreach (Match m in Placeholder.Matches(template))
            {
                sb.Append(Regex.Escape(template.Substring(last, m.Index - last)));
                var group = "p" + m.Groups[1].Value;
                sb.Append(seen.Add(group) ? "(?<" + group + ">.+?)" : "\\k<" + group + ">");
                last = m.Index + m.Length;
            }
            sb.Append(Regex.Escape(template.Substring(last))).Append('$');
            return sb.ToString();
        }

        public bool TryTranslate(string text, out string result)
        {
            var m = _source.Match(text);
            if (!m.Success) { result = null; return false; }
            result = Placeholder.Replace(_translated, ph =>
            {
                var g = m.Groups["p" + ph.Groups[1].Value];
                return g.Success ? g.Value : ph.Value; // 译文里有原文没有的占位符 → 原样保留
            });
            return !string.IsNullOrEmpty(result);
        }
    }
}
