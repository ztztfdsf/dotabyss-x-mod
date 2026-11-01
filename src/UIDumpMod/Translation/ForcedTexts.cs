using System.Collections.Generic;

namespace UIDumpMod.Translation
{
    /// <summary>
    /// 明确要翻的「非日文残留」：游戏 UI 里混进来的英文标题。
    ///
    /// 为什么单独一层：正常管道只处理被判为「含日文」的文本（假名 / 日式汉字），
    /// 而系统弹窗标题 "System Message" 是纯英文，永远进不了管道，于是中文界面里
    /// 孤零零挂着一句英文。这里用白名单精确匹配（区分大小写、只比整串），
    /// 不做任何通配，避免误伤游戏里本来就该显示英文的内容。
    ///
    /// 新增条目前先确认：① 它在游戏里确实是硬编码残留 ② 不是图片/品牌名。
    /// </summary>
    public static class ForcedTexts
    {
        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>
        {
            ["System Message"] = "系统消息",
        };

        public static bool TryGet(string text, out string result)
        {
            result = null;
            if (string.IsNullOrEmpty(text)) return false;
            if (Values.TryGetValue(text, out result)) return true;
            return Values.TryGetValue(text.Trim(), out result);
        }
    }
}
