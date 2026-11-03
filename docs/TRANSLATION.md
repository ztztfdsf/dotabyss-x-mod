# 翻译机制与数据手册

> 本文回答四件事：译文放在哪、启动时怎么被加载、出问题看什么日志、想更新该走哪条路。
> 版本更新的九步流程见 [`UPDATE_SOP.md`](UPDATE_SOP.md)；译名以 [`GLOSSARY.md`](GLOSSARY.md) 为唯一权威。

---

## 1. 一句话结论

**译文以本地文件为准。云端 CDN 只是可选的热更通道，拉不到就自动回退本地，不影响游戏。**

---

## 2. 文件布局

```
APK 内（随包发布）
assets/LemonLoader/deployment/UserData/AbyssMod/
├── translations/
│   ├── manifest/zh_Hans.json     索引 + 各表摘要
│   ├── names/zh_Hans.json        专名表（界面显示名权威源）
│   ├── ui_texts/zh_Hans.json     UI 文本（含 __global__ 值层）
│   ├── static/zh_Hans.json       MasterData 静态表
│   └── novels/<话ID>/zh_Hans.json  剧情正文
└── ttcuyuanj                     TMP 中文字体 AssetBundle

设备（首次启动由 LemonLoader 释放，之后一直用这份）
/sdcard/Android/data/jp.co.fanzagames.dotabyss_x_a.mod/files/MelonLoader/
├── Mods/AbyssMod/AbyssMod.dll + Utility.dll
├── UserData/AbyssMod/translations/…    ← 运行时实际读取的译文
├── UserData/AbyssMod/ttcuyuanj
└── MelonLoader/Latest.log              ← 排障看这个
```

**权威树在本地工作区**（不入库，体积大）：

| 路径 | 角色 |
|---|---|
| `trans_latest/` | 当前最新译文树（合并后的权威源） |
| `trans/` | 上一发布版基线（从 Release APK 里解出的 v17B 快照） |
| `cloud/translations/` | 由 `trans_latest/` 生成的云端分发形态（manifest 摘要已重算） |

---

## 3. 加载链路

```
启动
 ├─ 读 AbyssMod.cfg
 │    [Translation] Enabled / CDN / Language
 │    [Translation.Cache] Directory / PreferLocalFiles
 │
 ├─① 拉 manifest（CDN，可失败）
 │    ├─ 成功 → 记下各表期望摘要，日志 "Manifest loaded (zh_Hans). Hash: …"
 │    └─ 失败 → 日志 warning，全部摘要视为「无期望值」= 不做校验
 │
 ├─② 逐表加载（names → ui_texts → static → novels）
 │    ├─ 远端拿到 且 摘要相符 → 用远端，并写回本地缓存
 │    ├─ 远端拿到 但 摘要不符 → 日志 "JSON resource validation failed."，退回本地缓存
 │    └─ 远端拿不到          → 日志 "Using stale cache: <表>"
 │
 └─③ 加载字体 ttcuyuanj → "Font loaded: TTCuYuanJ SDF"
```

### 三条策略的真实差异

| 配置 | 行为 |
|---|---|
| `PreferLocalFiles = false`（默认）+ CDN 可达且摘要正确 | 远端优先，本地只做缓存 |
| `PreferLocalFiles = false` + CDN 404（当前上游状态） | 每张表拉一次失败 → 回退本地，游戏正常，日志留 warning |
| `PreferLocalFiles = true` | **本地文件存在就直接用**，跳过校验与拉取（manifest 除外）。纯离线、日志干净 |

维护者自用设备走最后一种即可，不需要云端参与。

---

## 4. 各表的键与摘要算法

摘要算法来自反编译 `Utility.dll` 的 `Utility.Cryptography.StringTableHash.ComputeEntries`：

```csharp
foreach (var (key, value) in entries) {   // entries 已按 StringComparer.Ordinal 排序
    AppendData(utf8(key));   AppendData(0x00);
    AppendData(utf8(value)); AppendData(0x00);
}
// 空串不追加字节，但 0x00 分隔符始终追加
```

| 表 | 结构 | 摘要用的 key |
|---|---|---|
| `names` | `{ 日文名: 中文名 }` | 日文名本身 |
| `novels/<id>` | `{ 日文原文: 中文 }` | 日文原文本身 |
| `ui_texts` | `{ 运行时路径: { 日文: 中文 } }` | `path` + `\u0001` + 日文 |
| `static` | `{ 表名: { 字段名: { 日文: 中文 } } }` | `表名` + `\u0001` + `字段名` + `\u0001` + 日文 |

`manifest` 中除各表摘要外还有一个 `hash` 字段，是整份 manifest 的版本标识（用于「CDN 有新版本」提示），与校验无关。

```bash
# 改了任何表之后：重算并写回 manifest
python tools/build_manifest.py --root cloud/translations          # --dry-run 只算不写
python tools/check_manifest.py --root cloud/translations          # 全绿才算对
```

> 历史坑：早期 manifest 摘要是手写的过期值，与真实数据永远对不上，导致每次启动都
> `JSON resource validation failed` 并回退 stale cache —— 也就是说热更从未真正生效过。
> 现在由工具生成，链路才是通的。

---

## 5. 两条更新路径

### 5.1 本地（默认、无条件可用）

```bash
# 1) 权威树 → 解码树（打新包时烤进去）
cp trans_latest/*.{…} dl-<ver>.out/assets/LemonLoader/deployment/UserData/AbyssMod/translations/…

# 2) 权威树 → 设备（不重装 APK 的即时生效路径）
adb push trans_latest/ /sdcard/Android/data/<包名>/files/MelonLoader/UserData/AbyssMod/translations/
# 推完必须 md5 逐位比对（本地 vs 设备）
```

### 5.2 云端（可选热更）

只在自己的 CDN 可用、且 manifest 摘要正确时才有意义。

```bash
# 生成 → 推送（自带仓库）
python tools/build_manifest.py --root cloud/translations
cd cloud && git add -A && git commit -m "…" && git push
# 设备侧把 [Translation] CDN 指过去即可
```

> 已知：`anosu/dotabyss-translation` 上游已 404。自建数据仓库的地址见 Release 说明。

---

## 6. 排障对照表

| 日志 | 含义 | 处理 |
|---|---|---|
| `Using 198 pre-generated Android Il2Cpp assemblies.` | interop 正常 | — |
| `Using stale cache: ui_texts/zh_Hans.json` | 远端不可用，用本地 | 正常；想消警告就设 `PreferLocalFiles = true` |
| `JSON resource validation failed.` | 拉到的表与 manifest 摘要不符 | 跑 `build_manifest.py` 重算摘要 |
| `Font loaded: TTCuYuanJ SDF` 缺失 | interop 生成时漏了 `--unity=<libunity.so>` | 按 SOP 第 6 步重生成 |
| `MasterData type not found for translation table: m_xxx` | 该表在游戏侧不存在 / 表名拼错 | 核对 dump 的表名，或删掉多余条目 |
| `[UIDumpMod] [LEAK] TMPUGUI.OnEnable\|<路径>\|<日文>` | 界面上仍有未翻译的日文 | 用 `leak2table.py` 收进 `ui_texts`，再做人工补译 |
| `ERROR` 条数 > 0 | 有异常 | 必须归零再发版 |

---

## 7. 数据健康度验收

发版前四条必须全绿：

```bash
python tools/jp_lint.py lint --root trans_latest --show 20   # 假名 0 / 标记字 0 / 繁体残留 0
python tools/check_manifest.py --root cloud/translations      # 摘要全一致
python tools/glossary.py --root trans_latest                  # 译名一致，无旧写法残留
python tools/glossary.py --root trans_latest --fix            # 有残留时才跑，自动统一为 names 表主译名
dotnet run --project tests/UIDumpMod.SelfTest -- <ui_texts>   # 35 passed, 0 failed
```

装机后按 `UPDATE_SOP.md` 第 8 步核对 `Latest.log` 七件套，再过一遍主要页面（主页 / 队伍 / 商店 / 抽卡 / 剧情 / 深渊 / 娼馆 / 装备弹窗 / 交流），确认无假名与纯汉字日文残留。
