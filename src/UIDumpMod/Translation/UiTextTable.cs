using System.Collections.Generic;
using System.Text.Json;

namespace UIDumpMod.Translation
{
    /// <summary>
    /// ui_texts 翻译表（与 AbyssMod 共用同一份 zh_Hans.json）+ 路径匹配规则。
    ///
    /// 为什么需要它：AbyssMod 按「运行时 Transform 全路径」精确查表，但表里大量条目的 key
    /// 是从 UI 预制体静态提取的「预制体相对路径」，运行时路径前面多了场景根、实例名还带 (Clone)，
    /// 永远对不上。这里补两条规则把它们救活：
    ///
    ///   exact  组：key 以 "Engine/" 开头或含 "/View/Canvas/"（运行时风格）→ 整串匹配
    ///   suffix 组：其余 key（预制体相对风格）→ 作为运行时路径的「后缀」匹配
    ///   两组都先把路径里每段的 "(Clone)" 剥掉再比。
    ///
    /// 每个路径下的文本查找顺序：原文 → Trim 后原文（TMP getter 常带尾随换行）→ {n} 模板。
    ///
    /// 另有一层「全局值级表」：json 里 key 为 "__global__" 的条目按值匹配，不依赖路径。
    /// 数据源是游戏自带的本地化语言表（TextData，从 CDN 的 textdata bundle 提取），
    /// 覆盖所有弹窗/通知/倒计时格式串 —— 同一条日文无论出现在哪个 UI 都能命中。
    /// </summary>
    public sealed class UiTextTable
    {
        public const string GlobalKey = "__global__";

        private sealed class PathEntry
        {
            public Dictionary<string, string> Texts;
            public List<TemplatePattern> Patterns; // 无模板时为 null
        }

        private readonly Dictionary<string, PathEntry> _exact = new Dictionary<string, PathEntry>();
        private readonly Dictionary<string, PathEntry> _suffix = new Dictionary<string, PathEntry>();
        private PathEntry _global; // 值级兕底，与路径无关

        public int ExactPathCount => _exact.Count;
        public int SuffixPathCount => _suffix.Count;
        public int PatternPathCount { get; private set; }
        public int GlobalCount => _global?.Texts.Count ?? 0;

        /// <summary>从 ui_texts/zh_Hans.json 的内容构建。格式：{ "路径": { "日文": "中文", ... }, ... }</summary>
        public static UiTextTable FromJson(string json)
        {
            var table = new UiTextTable();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            foreach (var kv in parsed)
            {
                var key = kv.Key;
                if (key == GlobalKey)
                {
                    table._global = MakeEntry(kv.Value);
                    if (table._global.Patterns != null) table.PatternPathCount++;
                    continue;
                }
                if (key.Contains('*')) continue; // 通配符 key 是 AbyssMod 自己的语法，不归我们管
                var entry = MakeEntry(kv.Value);
                if (entry.Patterns != null) table.PatternPathCount++;
                (IsRuntimeStyle(key) ? table._exact : table._suffix)[key] = entry;
            }
            return table;
        }

        private static PathEntry MakeEntry(Dictionary<string, string> texts)
        {
            var entry = new PathEntry { Texts = texts };
            foreach (var t in texts)
                if (TemplatePattern.HasPlaceholder(t.Key))
                    (entry.Patterns ??= new List<TemplatePattern>()).Add(new TemplatePattern(t.Key, t.Value));
            return entry;
        }

        private static bool IsRuntimeStyle(string key) => key.StartsWith("Engine/") || key.Contains("/View/Canvas/");

        /// <summary>"Popup_SoundCaution(Clone)/Box/Text" → "Popup_SoundCaution/Box/Text"</summary>
        public static string StripClone(string path)
        {
            const string Clone = "(Clone)";
            var segs = path.Split('/');
            for (int i = 0; i < segs.Length; i++)
                if (segs[i].EndsWith(Clone)) segs[i] = segs[i].Substring(0, segs[i].Length - Clone.Length);
            return string.Join("/", segs);
        }

        /// <param name="runtimePath">运行时 Transform 全路径（父→子，"/" 分隔）。</param>
        public bool TryTranslate(string runtimePath, string text, out string result)
        {
            result = null;
            if (runtimePath == null) return false;
            var norm = StripClone(runtimePath);

            // 1) exact：剥 (Clone) 后的路径，以及原始路径
            if (TryEntry(_exact, norm, text, out result)) return true;
            if (TryEntry(_exact, runtimePath, text, out result)) return true;

            // 2) suffix：完整路径，再逐级砍掉最前面一段
            //    "A/B/C" → 依次试 "A/B/C"、"B/C"、"C"（以及末尾 "/" 后的空串，无害）
            if (TryEntry(_suffix, norm, text, out result)) return true;
            for (int slash = norm.IndexOf('/'); slash >= 0; slash = norm.IndexOf('/', slash + 1))
                if (TryEntry(_suffix, norm.Substring(slash + 1), text, out result)) return true;
            return false;
        }

        /// <summary>值级兕底：不依赖路径，按原文/模板直接匹配（数据源：TextData 本地化表）。</summary>
        public bool TryTranslateGlobal(string text, out string result)
        {
            result = null;
            var g = _global;
            if (g == null) return false;
            if (g.Texts.TryGetValue(text, out result)) return true;
            if (g.Texts.TryGetValue(text.Trim(), out result)) return true;
            if (g.Patterns != null)
                foreach (var p in g.Patterns)
                    if (p.TryTranslate(text, out result)) return true;
            return false;
        }

        private static bool TryEntry(Dictionary<string, PathEntry> group, string key, string text, out string result)
        {
            result = null;
            if (!group.TryGetValue(key, out var e)) return false;
            if (e.Texts.TryGetValue(text, out result)) return true;
            if (e.Texts.TryGetValue(text.Trim(), out result)) return true;
            if (e.Patterns != null)
                foreach (var p in e.Patterns)
                    if (p.TryTranslate(text, out result)) return true;
            return false;
        }
    }
}
