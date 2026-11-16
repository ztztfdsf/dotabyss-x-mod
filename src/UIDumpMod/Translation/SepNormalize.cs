using System.Collections.Generic;

namespace UIDumpMod.Translation
{
    /// <summary>
    /// 分隔符归一化匹配（2026-11-16 好感度弹窗漏翻根因修复）。
    ///
    /// 现象：弹窗正文「キャラクターストーリーが解放されました。&lt;br&gt;閲覧しますか？」在
    /// __global__ 表里存在对应译文，但运行时 set_text 进来的是另一种分隔符形态
    /// （TMP 字面 br 标签 vs 代码拼接的真实换行），字典精确查找 miss，整条漏翻。
    ///
    /// 修法：精确 + Trim + 模板全部 miss 后，把文本的分隔符在两种形态间互换再试一轮。
    /// 互换是双向的（br→换行 与 换行→br），各自带 Trim 变体，最多 4 次字典查找。
    /// </summary>
    public static class SepNormalize
    {
        private const string Br = "<br>";

        internal static bool TryMatch(UiTextTable.PathEntry entry, string text, out string result)
        {
            result = null;
            if (entry == null || text == null) return false;

            if (text.Contains(Br))
            {
                var t = text.Replace(Br, "\n");
                if (entry.Texts.TryGetValue(t, out result)) return true;
                if (entry.Texts.TryGetValue(t.Trim(), out result)) return true;
            }
            if (text.Contains("\n"))
            {
                var t = text.Replace("\n", Br);
                if (entry.Texts.TryGetValue(t, out result)) return true;
                if (entry.Texts.TryGetValue(t.Trim(), out result)) return true;
            }
            return false;
        }
    }
}
