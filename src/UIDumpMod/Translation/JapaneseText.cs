using System.Text.RegularExpressions;

namespace UIDumpMod.Translation
{
    /// <summary>
    /// 判断一段文本「是否还需要翻译」（= 是否仍含日文）。
    /// 纯 C#，不依赖 Unity —— 可以在 PC 上直接跑自测（tests/UIDumpMod.SelfTest）。
    /// </summary>
    public static class JapaneseText
    {
        /// <summary>
        /// 假名范围：平假名、片假名（含小写扩展）、半角片假名。
        /// 故意排除 ー(30FC) 与 ・(30FB)：它们在已译中文里也常见（如「冲击释放・真」），算进来会误报。
        /// </summary>
        private static readonly Regex Kana = new Regex(
            @"[\u3040-\u309F\u30A1-\u30FA\u30FD\u30FE\u31F0-\u31FF\uFF66-\uFF9D]",
            RegexOptions.Compiled);

        /// <summary>
        /// 日式字形汉字（补假名的盲区）：纯汉字的日文 UI 标签（総合戦力/推奨戦力/現在…）不含假名。
        /// 这些字形是日文新字体，简中译文里不会出现，所以出现即判为日文。
        /// 注意：不要加入 会/断/昇 这类简中也用的字，否则已译文本会被误报 [LEAK]。
        /// </summary>
        private static readonly char[] JapaneseOnlyKanji =
            "戦奨円満検絵価帰斉勲圧広遠間変険県続読済現際従様対処売歩沢淵減復費個獲図鑑値伝覚".ToCharArray();

        public static bool NeedsTranslation(string s)
            => !string.IsNullOrEmpty(s) && (Kana.IsMatch(s) || s.IndexOfAny(JapaneseOnlyKanji) >= 0);
    }
}
