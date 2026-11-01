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
        /// 日式字形汉字（补假名的盲区）：纯汉字的日文 UI 标签（総合戦力/装備一覧/自動分解設定…）不含假名。
        /// 这些字形是日文新字体，简中译文里不会出现，所以出现即判为日文。
        /// 生成方法：扫描全部翻译表，提取「日文原文中出现≥2次且简体译文从不使用」的汉字，
        /// 再剔除 GB2312 一级字库内的简繁同形字（知/白/北…），防止已译文本被误报 [LEAK]。
        /// 重新生成脚本见 new1150（工具链 ：trad_markers_clean.json）。
        /// </summary>
        private static readonly char[] JapaneseOnlyKanji =
            "仮伝価係個値備傭傾働償優円凍処凱別剣効動勢勲協単収員問喚喪営団囲図圧報場増売変奨姉婦実対導層師帰広張強後従復応恵愛態憩憶戦戻払択拠挙捗捜採揃換撃敗敵斉昇時曜書枠査棄棹検業楽様標機歩歴毎気氷汎決沢況浄淵済減満準滅潔災為無熱猟獄獣獲現環異畳発県確種積穏窮範簡約紋純細紹終絆経結給絵継続緊総緒線編練績罠職脅脈興舎舗華衝補製複覇見規覚覧親観計討訓記訪設許訳証評試話該詳認誕誠誤説読調請講護負貨販貴買費資賊賞購贈軍輩輸連週進遅運過達遠適遷選遺還釈針鉱銃録鍛鍵鎧鏡鑑長門閉開間関閲闇闘険隊階際隠難霊響頂項順須預頑領頭頼額願類風飢餓館馬騎験驚鳴ﾀﾞﾒ".ToCharArray();

        public static bool NeedsTranslation(string s)
            => !string.IsNullOrEmpty(s) && (Kana.IsMatch(s) || s.IndexOfAny(JapaneseOnlyKanji) >= 0);
    }
}
