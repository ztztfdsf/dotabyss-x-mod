# UIDumpMod — 运行时 UI 兜底翻译层

AbyssMod（翻译核心）的补漏组件：把「表里有译文但路径对不上」的 UI 文本在运行时修复。

## 解决什么问题

AbyssMod 的 `ui_texts` 表按**运行时 Transform 路径**做 key 精确匹配，但存在两类失配：

| 失配类型 | 例子 | 后果 |
|---|---|---|
| 预制体相对路径 vs 运行时路径 | 表 key `Scene_NetherTop/NetherMenu/...`，运行时 `NetherTop/UICanvas/RootUI/UIGroup/Scene_NetherTop/...` | 静态提取的 5000+ 条翻译**从未生效** |
| `(Clone)` 实例化后缀 | `Popup_SoundCaution(Clone)` | 同上 |

## 工作原理

```
TMP_Text.set_text ──prefix──> AbyssMod 翻译（原逻辑）
       │                        │ 翻不出（路径失配）
       └──postfix──> UIDumpMod.Handle()
                        ├─ 文本含日文？否 → 跳过
                        ├─ TryFallback(path, text)
                        │    ├─ Normalize: 剥 (Clone) 后缀
                        │    ├─ exact 整串匹配（Engine/… 风格 key）
                        │    ├─ suffix 逐级后缀匹配（预制体相对 key）
                        │    └─ {0} 模板正则（数字/日期等动态文本）
                        ├─ 命中 → 回写 tmp.text → 日志 [FIXED]
                        └─ 未命中 → 日志 [LEAK]（滚动补译的输入）
```

另有一个每 120 帧（约 2 秒）的全场景 `FindObjectsOfType<TextMeshProUGUI>` 扫描，
兜住不走 `set_text`/`OnEnable` 的文本。

## 数据来源

**不引入新数据文件**——直接读 AbyssMod 的缓存目录：

```
{UserData}/AbyssMod/translations/ui_texts/zh_Hans.json
```

表里按 key 风格自动分组：`Engine/` 开头或含 `/View/Canvas/` 的进 exact 组，
其余进 suffix 组，含 `{0}` 的进模板组。

## 日志标记

- `[FIXED] path|text` — 兜底命中（前 20 条逐条打印，之后每 100 条一条）
- `[LEAK] tag|path|text` — 仍翻不出的日文。**这是维护循环的输入**：拿 `Latest.log`
  跑 `tools/leak2table.py`（见仓库根目录 `tools/`），自动补译 + 列出人工待办。

## 构建

需要 .NET 10 SDK。引用路径默认指向仓库同级约定目录（apktool 解码树），可用命令行参数覆盖：

```bash
dotnet build -c Release
# 换机器/换路径：
dotnet build -c Release -p:LoaderDir="<LemonLoader>/runtime/loader/net6" -p:InteropDir="<LemonLoader>/runtime/interop"
```

产物 `bin/Release/net10.0/UIDumpMod.dll` 放进 APK 的
`assets/LemonLoader/deployment/Mods/UIDumpMod/`（或直接 push 到设备
`files/MelonLoader/Mods/UIDumpMod/` 热更新）。

## 维护注意事项（踩过的坑）

1. **多行文本**：TMP `.text` getter 可能返回带尾随 `\n` 或两行拼接的文本，
   单行日志解析会截断导致 key 配不上——`TryDict` 已做 Trim 重试；
   补译多行文本时必须以**完整多行字符串**作 key。
2. **日文判定**：正则故意排除全角长音符 `ー` 与中点 `・`，否则已译中文
   （如 `冲击释放・真`）会被误报 LEAK。
3. **重入保护**：自己回写 `tmp.text` 会再触发 hook，`_applying` 标志防递归。
4. **`(Clone)` 后缀**是 7 个字符（含括号），Normalize 剥的是 7 不是 6。
