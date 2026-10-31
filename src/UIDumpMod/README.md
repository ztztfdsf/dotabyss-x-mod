# UIDumpMod — 运行时 UI 兜底翻译层

AbyssMod（翻译核心）的补漏组件：把「表里有译文但路径对不上」的 UI 文本在运行时修复。

## 解决什么问题

AbyssMod 的 `ui_texts` 表按**运行时 Transform 路径**做 key 精确匹配，但存在两类失配：

| 失配类型 | 例子 | 后果 |
|---|---|---|
| 预制体相对路径 vs 运行时路径 | 表 key `Scene_NetherTop/NetherMenu/...`，运行时 `NetherTop/UICanvas/RootUI/UIGroup/Scene_NetherTop/...` | 静态提取的 5000+ 条翻译**从未生效** |
| `(Clone)` 实例化后缀 | `Popup_SoundCaution(Clone)` | 同上 |

## 代码结构

```
Core.cs                    入口（MelonMod）：加载翻译表、挂 hook、定时全场景扫描
TextHooks.cs               Harmony 补丁（TMP/Legacy 的 set_text + OnEnable，postfix）+ 处理管道
Translation/               ↓ 纯 C#，不依赖 Unity/MelonLoader，PC 端可直接自测
  UiTextTable.cs           ui_texts 表 + 路径匹配规则（exact / suffix / {n} 模板）
  FragmentTranslator.cs    color 标签拼接文本的碎片级翻译（深渊事件选项等）
  JapaneseText.cs          「是否还含日文」判定
  TemplatePattern.cs       {n} 占位符模板
tests/UIDumpMod.SelfTest/  自测（内置用例 + 真实表覆盖率统计），改逻辑先过它
```

## 工作原理

```
TMP_Text.set_text ──prefix──> AbyssMod 翻译（原逻辑）
       │                        │ 翻不出（路径失配）
       └──postfix──> TextHooks.Handle()
                        ├─ 文本含日文？否 → 跳过
                        ├─ UiTextTable.TryTranslate(path, text)
                        │    ├─ StripClone: 剥每段的 (Clone) 后缀
                        │    ├─ exact 整串匹配（Engine/… 运行时风格 key）
                        │    ├─ suffix 逐级后缀匹配（预制体相对 key）
                        │    └─ {n} 模板正则（数字/日期等动态文本）
                        ├─ FragmentTranslator.TryTranslate（color 标签碎片拼接文本）
                        ├─ 命中 → 回写 .text → 日志 [FIXED]
                        └─ 未命中 → 日志 [LEAK]（滚动补译的输入）
```

另有一个每 120 帧（约 2 秒）的全场景 `FindObjectsOfType<TextMeshProUGUI>` 扫描，
兜住不走 `set_text`/`OnEnable` 的文本。

## 数据来源

**不引入新数据文件**——直接读 AbyssMod 的缓存目录：

```
{UserData}/AbyssMod/translations/ui_texts/zh_Hans.json
```

## 日志标记

- `[FIXED] path|text` — 兜底命中（前 20 条逐条打印，之后每 100 条一条）
- `[LEAK] tag|path|text` — 仍翻不出的日文。**这是维护循环的输入**：拿 `Latest.log`
  跑仓库根目录 `tools/leak2table.py`，自动补译 + 列出人工待办。

## 构建

需要 .NET 10 SDK。引用当前游戏版本解码树里的 interop 代理（见 csproj 注释），默认按工作区约定路径，
换机器/换版本用 `-p:GameTree=` 覆盖：

```bash
dotnet run --project tests/UIDumpMod.SelfTest -c Release   # ① 自测
dotnet build src/UIDumpMod -c Release                      # ② 编 mod
# 产物 bin/Release/net10.0/UIDumpMod.dll → APK 的 assets/LemonLoader/deployment/Mods/UIDumpMod/
#             （或直接 adb push 到设备 files/MelonLoader/Mods/UIDumpMod/ 热更）
```

## 维护注意事项（踩过的坑）

1. **多行文本**：TMP `.text` getter 可能返回带尾随 `\n` 或两行拼接的文本——查表已做 Trim
   重试；补译多行文本时必须以**完整多行字符串**作 key。
2. **日文判定**：假名正则故意排除 `ー` 与 `・`（已译中文会用到）；日式字形汉字表里
   **不要加简中也用的字**（会/断/昇…），否则已译文本被误报 [LEAK]。
3. **碎片规则先于日文判定**：`10上昇`、`侵食+3` 这类片段不含假名/日式字形，靠正则规则
   先行匹配，不能先判日文（自测有对应用例）。
4. **重入保护**：自己回写 `.text` 会再触发 hook，`_writing` 标志防递归。
5. **`(Clone)` 后缀**是 7 个字符（含括号）。
6. **interop 版本必须对齐**：csproj 引用「当前游戏版本重新生成的」interop 代理编译；
   拿旧版本 interop 编译出的 dll 在新版本上会槽位错位、运行时崩溃（v16 的教训）。
