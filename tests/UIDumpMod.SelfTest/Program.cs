// UIDumpMod 翻译逻辑自测（纯 PC 端，不需要设备/游戏）
//
//   dotnet run --project tests/UIDumpMod.SelfTest                       # 只跑内置用例
//   dotnet run --project tests/UIDumpMod.SelfTest -- <ui_texts/zh_Hans.json>  # 再加真实表覆盖率统计
//
// 改了 src/UIDumpMod/Translation/ 下任何文件后先跑一遍，全部 PASS 再编 mod 装机。
// 新增碎片句式 / 修匹配规则时，把对应的真实游戏文本补进下面的用例。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UIDumpMod.Translation;

int pass = 0, fail = 0;
void Check(string name, string actual, string expected)
{
    if (actual == expected) { pass++; return; }
    fail++;
    Console.WriteLine($"FAIL {name}\n     expected: {expected ?? "(no translation)"}\n     actual:   {actual ?? "(no translation)"}");
}

// ---------- 日文判定 ----------
Check("kana", JapaneseText.NeedsTranslation("スキップ").ToString(), "True");
Check("中点/长音不算日文", JapaneseText.NeedsTranslation("冲击释放・真ー").ToString(), "False");
Check("日式字形汉字", JapaneseText.NeedsTranslation("総合戦力").ToString(), "True");
Check("简中常用字不误判", JapaneseText.NeedsTranslation("会断升").ToString(), "False");

// ---------- 路径匹配规则（内置小表） ----------
var table = UiTextTable.FromJson(JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
{
    ["Engine/View/Canvas/Front/Popup/Text"] = new() { ["確認"] = "确认" },
    ["Scene_NetherTop/Menu/Label"] = new() { ["深淵"] = "深渊", ["あと{0}時間{1}分"] = "剩余{0}小时{1}分" },
    ["Popup_Floor/Text"] = new() { ["{0}フロア目"] = "第{0}层", ["{0}と{0}"] = "{0}和{0}" },
    ["Wild/*/Text"] = new() { ["ワイルド"] = "通配" },
}));
string T(string path, string text) => table.TryTranslate(path, text, out var r) ? r : null;

Check("exact", T("Engine/View/Canvas/Front/Popup/Text", "確認"), "确认");
Check("exact 剥 (Clone)", T("Engine/View/Canvas/Front/Popup(Clone)/Text", "確認"), "确认");
Check("suffix（运行时路径多了场景根）", T("NetherTop/UICanvas/RootUI/Scene_NetherTop/Menu/Label", "深淵"), "深渊");
Check("suffix + (Clone)", T("Root/Scene_NetherTop(Clone)/Menu/Label", "深淵"), "深渊");
Check("尾随换行 Trim 重试", T("X/Scene_NetherTop/Menu/Label", "深淵\n"), "深渊");
Check("双占位符模板", T("X/Scene_NetherTop/Menu/Label", "あと3時間15分"), "剩余3小时15分");
Check("单占位符模板", T("Engine/View/Canvas/Popup_Floor(Clone)/Text", "101フロア目"), "第101层");
Check("重复占位符必须相同", T("A/Popup_Floor/Text", "1と2"), null);
Check("重复占位符相同才命中", T("A/Popup_Floor/Text", "1と1"), "1和1");
Check("路径不匹配", T("Other/Label", "深淵"), null);
Check("通配符 key 不归 UIDumpMod", T("Wild/x/Text", "ワイルド"), null);
Check("前缀不是后缀", T("Scene_NetherTop/Menu/Label/Extra", "深淵"), null);

// ---------- 碎片级翻译（真实游戏文本） ----------
string F(string s) => FragmentTranslator.TryTranslate(s, out var r) ? r : null;
Check("碎片: 代码+HP", F("<color=#FFD700>アビスコード獲得</color>\n<color=#FF4444>HP40%減少</color>"),
      "<color=#FFD700>获得深渊代码</color>\n<color=#FF4444>HP减少40%</color>");
Check("碎片: 深渊币", F("<color=#a>アビスコイン50個獲得</color>"), "<color=#a>获得50深渊币</color>");
Check("碎片: 消耗", F("<color=#a>アビスコイン50個消費</color>"), "<color=#a>消耗50个深渊币</color>");
Check("碎片: 恩恵ゲージ长句",
      F("<color=#a>インパクトコード系統の恩恵ゲージが</color><color=#b>10上昇</color><color=#a>し、セーフコード系統の恩恵ゲージが</color><color=#b>5減少</color><color=#a>しました。</color>"),
      "<color=#a>冲击代码系恩惠槽</color><color=#b>上升10</color><color=#a>，安全代码系恩惠槽</color><color=#b>减少5</color><color=#a>。</color>");
Check("碎片: 逗号拼接", F("<color=#a>HP10%回復</color>,<color=#b>侵食+3</color>"), "<color=#a>HP恢复10%</color>，<color=#b>浸蚀+3</color>");
Check("碎片: 有一段翻不出 → 整体放弃", F("<color=#a>アビスコード獲得</color><color=#b>未知の効果</color>"), null);
Check("碎片: 已是中文 → 不处理", F("获得深渊代码"), null);

Console.WriteLine($"\nbuilt-in: {pass} passed, {fail} failed");

// ---------- 可选：真实表覆盖率 ----------
if (args.Length > 0)
{
    var json = File.ReadAllText(args[0]);
    var real = UiTextTable.FromJson(json);
    var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
    int total = 0, resolved = 0;
    foreach (var (key, texts) in raw)
    {
        if (key.Contains('*')) continue;
        // 模拟运行时路径：exact 组原样；suffix 组前面加场景根（AbyssMod 自己能精确命中的那部分也算在内）
        var path = key.StartsWith("Engine/") || key.Contains("/View/Canvas/") ? key : "SceneRoot/UICanvas/" + key;
        foreach (var (jp, zh) in texts)
        {
            if (TemplatePattern.HasPlaceholder(jp)) continue;
            total++;
            if (real.TryTranslate(path, jp, out var r) && r == zh) resolved++;
        }
    }
    Console.WriteLine($"real table: exact={real.ExactPathCount} suffix={real.SuffixPathCount} patterns={real.PatternPathCount}");
    Console.WriteLine($"real table: {resolved}/{total} plain entries resolvable by UIDumpMod " +
                      $"(其余为含 (Clone) 的完整运行时路径，由 AbyssMod 精确匹配负责)");
}

return fail == 0 ? 0 : 1;
