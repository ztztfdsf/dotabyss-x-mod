# dotabyss-x-mod

ドットアビスX for Android（Dot Abyss X / DMM GAMES）的民间简体中文整合 mod 仓库，非官方、与 EXNOA / FANZAGAMES / DMM 无关。

> ⚠️ 本仓库仅为技术存档与版本延续。使用第三方客户端登录线上服务存在账号风险，请自行评估。游戏本体请在 [DMM GAMES](https://games.dmm.com/) 正版渠道下载。

**历史原发布处（1.14.4 时代，作者 anosu/Jitsu）**：<https://github.com/anosu/DMM-Mod/releases/tag/mod-dotabyss-v30> · 镜像站条目 <https://release.ntr.best/>（该线已停更）

## 这是什么

对官方 APK 的整包重打包（repack），基于社区 mod 作者 Jitsu 的 AbyssMod v1.1.4（MelonLoader/LemonLoader 注入方案），随官方版本升级维护。**本仓库发行版只包含翻译功能**：

- 官方 `jp.co.fanzagames.dotabyss_x_a` → 共存包名 `jp.co.fanzagames.dotabyss_x_a.mod`（可与官方包并装）
- arm64-v8a only · **Android 7.1+（minSdk 25 / targetSdk 36）**
- 进程内挂载 .NET 运行时（LemonLoader = MelonLoader 安卓移植），加载两个 mod：
  - **AbyssMod.dll + Utility.dll**（原作者 Jitsu v1.1.4，无源码）：Harmony 补丁，剧情/UI/MasterData 翻译核心。
    原作者二进制按 1.14 interop 编译，在 1.15.0 新 interop 下有三处崩溃/错杀路径，由
    **AbyssModPatcher**（`src/AbyssModPatcher/`）在构建时做 IL 手术：拆除 Toast 渲染链、
    挖空错杀 spine CG 的三个渲染 hook。翻译逻辑零改动。
  - **UIDumpMod.dll**（本仓库自研，`src/UIDumpMod/`）：运行时 UI 兜底翻译层，修复预制体
    路径与运行时路径失配导致的数千条翻译失效
- 登录 WebView UA 与官方包对齐（消除 `.mod` 包名暴露）
- 游戏本体 `libil2cpp.so` / `libunity.so` / `global-metadata.dat` 与官方包逐字节一致，无代码注入、无其他功能改动

## 翻译是怎么加载的（重要）

**译文以本地为准，不依赖任何在线服务。**

```
APK 内置  assets/LemonLoader/deployment/UserData/AbyssMod/translations/
   ↓ 首启由 LemonLoader 释放到
设备      /sdcard/Android/data/<包名>/files/MelonLoader/UserData/AbyssMod/translations/
   ↓ AbyssMod 启动时读取（本地优先）
游戏内生效
```

- 首启后完全离线可用，不联网、不校验、不拉取。
- `AbyssMod.cfg` 里的 `[Translation] CDN` 是**可选的热更通道**，默认指向的上游仓库已停止服务（404）。
  拉不到时 AbyssMod 会回退到本地文件继续工作，只在日志里留一条 warning，不影响使用。
  想把本地锁死为一等公民，把 `[Translation.Cache] PreferLocalFiles = true` 即可（本地文件存在时跳过清单校验直接使用）。
- 所以「云端翻译仓库」在本项目里是**可选的第二分发通道**（给不想重装 APK 的场景做增量热更），不是运行必需。
  维护者自用的设备走本地，不必改 CDN。

细节（文件布局、摘要算法、热更与排障流程）见 [`docs/TRANSLATION.md`](docs/TRANSLATION.md)。

## 仓库结构

```
src/UIDumpMod/        UIDumpMod 源码（.NET 10；Core + TextHooks + Translation/ 纯逻辑层）
src/AbyssModPatcher/  对原作者 AbyssMod/Utility 二进制做 IL 手术的工具（修复新 interop 下的崩溃点）
tests/UIDumpMod.SelfTest/  翻译逻辑自测（PC 端可跑，改逻辑先过它）
tools/  novel_pull.py     — 版本更新时剧情拉取/解包/合并流水线（SOP ⑤）
        leak2table.py     — LEAK 日志 → ui_texts 补译维护工具
        glossary.py       — 专有名词一致性校验/统一工具
        glossary-fix.json — 人工确认的译名统一表
        jp_lint.py        — 假名/纯汉字日文/繁体残留全量扫描（收口门禁）
        build_manifest.py — 重算翻译表摘要并写回 manifest
        check_manifest.py — 校验整棵译文树与 manifest 是否一致
        il2dis.py         — il2cpp 元数据便捷查询
docs/   GLOSSARY.md       — 专有名词对照表（唯一权威译名来源）
        glossary.json     — 同上，机器可读版
        UPDATE_SOP.md     — 版本更新九步流程（含 interop 重生成与二进制手术）
        TRANSLATION.md    — 翻译加载机制 / 数据格式 / 排障手册
        SIGNING.md        — 签名线、密钥位置与备份、重签命令、换线影响
        AbyssMod.decompiled.cs（原作者 mod 的反编译参考，只读）
```

接手维护请先读 [`src/UIDumpMod/README.md`](src/UIDumpMod/README.md)（架构与踩坑记录）、[`docs/UPDATE_SOP.md`](docs/UPDATE_SOP.md)（**版本更新九步顺序流程，出新版本照此执行**）、[`docs/TRANSLATION.md`](docs/TRANSLATION.md)（**翻译数据怎么加载、怎么热更、怎么排障**）和 [`docs/GLOSSARY.md`](docs/GLOSSARY.md)（**译名唯一权威来源，改任何名字前先查这里**）。改翻译逻辑前先跑 `dotnet run --project tests/UIDumpMod.SelfTest`，全部通过再装机。

## 翻译状态（1.15.0 当前树）

| 部分 | 规模 | 说明 |
|---|---|---|
| 剧情（novels） | **1,377 话 / 60,415 条** | 1.14.6 基线 1,339 话 + 1.15.0 新增 38 话（活动主线 8 话 + 亨丽埃塔/千寻个人与 H 剧情 30 文件），新增部分人工翻译 |
| MasterData 翻译表（static） | **152 表 / 18,500 条** | 深渊 157 层、商店礼包、活动任务、职业、角色档案、寝室名、句签名、图鉴等全量 |
| UI 文本（ui_texts） | **6,322 路径 / 7,498 条** | 其中 `__global__` 值层 **870 条**（游戏本地化 TextData 表，弹窗/格式串的总源头） |
| 角色名/专有名词（names） | **507 条** | 译名统一规则见 [`docs/GLOSSARY.md`](docs/GLOSSARY.md) |

收口门禁（发布前必跑，全 0 才算翻完）：

```
python tools/jp_lint.py lint --root <translations>     → 假名 0 / 纯汉字日文 0 / 繁体残留 0
python tools/check_manifest.py --root <translations>   → manifest 摘要全部一致
dotnet run --project tests/UIDumpMod.SelfTest           → 35 passed, 0 failed
```

基础译文来自社区繁中镜像（`0BlueYan0/dotabyss-translation-zh_Hant`，原 anosu 布局）经 OpenCC t2s 转换（只转值不转 key），MasterData/UI 缺口部分为本仓库人工补翻。UI 残留日文的持续修复方法见 `tools/leak2table.py` 头部注释。

> **斯卡雷特（スカーレット）剧情修复（v14）**：上游镜像把该角色 6 话（`107801*`，共 12 个文件 594 行）的译文整段填成了一部中国网络小说的正文，内容与日文原文完全无关。本仓库已将这 594 行**从日文原文全部重译**并替换，同时全量扫描确认其余 1327 话无同类问题。
>
> **专有名词统一（v14）**：同一角色在旧包里存在多种译名（如 `スカーレット` 既有「斯卡蕾特」又有「斯卡雷特」、`アリシア` 有「艾利西亚/阿莉西亚」）。已在本地建立 [`docs/GLOSSARY.md`](docs/GLOSSARY.md) 作为翻译时的唯一权威译名表（配套校验工具 `tools/glossary.py`），并全库统一 1400+ 处。规则：界面显示名以 `names` 表为准，正文/表/UI 必须与之一致。

## 版本

| 版本 | 基线 | 说明 |
|---|---|---|
| （下一版，未发布） | 官方 1.15.0 (vc 32) | 译文树重组：static 23 处日文汉字漏转修正（`永続→永续` 等）；ui_texts 补齐运行时路径差异条目，路径 6,570→**6,322 归一后 7,498 条**（含 `__global__` 772→**870**）；manifest 摘要首次按 AbyssMod 真实算法生成（此前一直是过期值，日志 `JSON resource validation failed` 源于此）。**签名方案待定，见下方已知限制** |
| 1.15.0 · mod v18 | 官方 1.15.0 (versionCode 32) | **纯汉字日文根治**：日文标记字表 50→268（全表反推生成法），TextData 漏翻的 148 条纯汉字日文入 `__global__`（角色筛选/装备弹窗/灾厄防卫/商店/娼馆营业/深渊事件效果/武器种类）；ui_texts 6816→**6868**；UIDumpMod **1.12.0**（仅本地构建，未发布） |
| 1.15.0 · mod v17B | 官方 1.15.0 (versionCode 32) | **当前对外版本**。修复进入 H 剧情 CG 黑屏（AbyssMod 旧 interop 渲染 hook 错杀 spine 模型，手术 no-op 定罪修复）；MasterData 全表清扫至 **152 表/18497 条**；UI 路径增至 **6570**；UIDumpMod 1.9.0 纯翻译版（Toast 已拆除） |
| 1.15.0 · mod v17 | 官方 1.15.0 (versionCode 32) | 游戏本体升级 1.15.0；interop 代理按新 metadata 重生成（修复闪退/字体丢失）；payload.json 修复；**38 话新剧情人工全译**（活动主线 + 亨丽埃塔/千寻）；Toast 改 UIDumpMod 自绘原版视觉（旧渲染链在新 interop 下 coreclr abort） |
| 1.15.0 · mod v15/v16 | 官方 1.15.0 (versionCode 32) | v15 首版（已损坏勿用）；v16 修复闪退+字体但 Toast 渲染链崩溃（已被 v17 取代） |
| 1.14.6 · mod v14 | 官方 1.14.6 (versionCode 31) | 纯翻译版：AbyssMod + UIDumpMod + 全量翻译包 + 斯卡雷特剧情重译 + 全库专名统一 |
| 1.14.6 · mod v13 | 官方 1.14.6 (versionCode 31) | 含上游斯卡雷特剧情的坏译文与旧译名（已被 v14 取代，保留存档） |

- 基线从官方 1.14.4 升级到 1.14.6 直连包，再到 1.15.0（1.15.0 的 `global-metadata.dat` 有变化，但差异为纯增量：+134/-10 类型，经 il2cpp 双版本 diff 验证旧 interop 代理与 mod DLL 兼容复用）
- 内置启动 Toast 版本号同步为 1.15.0
- 签名：自签名（`CN=AbyssMod Archive, O=ntr.best`）。v14～v18 同签名线可互相覆盖安装；**与 1.14.4 时代作者的 DMMMODKE 签名不同，无法覆盖安装旧 mod，需先卸载再装**

## 已知限制

- **下一版起换签名线（已定）**：v14～v18 使用的自签名密钥文件在 2026-11-02 的一次磁盘事故中损毁，经穷尽取证（G 盘 1.9 TB 裸盘扫描、全盘 PKCS12 普查、仓库全历史、设备端检索）确认不可恢复。
  旧线证书：`CN=AbyssMod Archive, O=ntr.best, C=JP`，SHA-256 `774a4955…370d`。
  新线 v2 证书：`CN=AbyssMod Archive v2`，SHA-256 `8fb9c845…f8b1`（已生成并三处备份）。
  **影响**：新线首发包与 v18 及更早版本签名不同，**无法覆盖安装**，需先卸载旧 mod 再装。
  - 服务端存档、角色、进度、氪金**不受影响**
  - 本地游戏资源（`/sdcard/Android/data/<包名>/files/`，约 7 GB）会随卸载删除；想保住就在卸载前复制到其他目录，装好后拷回
  - 客户端设置、缓存重建
  细节与重签命令见 [`docs/SIGNING.md`](docs/SIGNING.md)。
- 上游翻译 CDN（`anosu/dotabyss-translation`）已停止服务（HTTP 404），仓库内所有 release 均不依赖它。
- 第三方客户端登录线上服务存在账号风险，见顶部免责。

## 安装

1. 卸载旧版 mod（如签名不同）
2. 安装 Release 中的 APK
3. 首次启动会释放 LemonLoader 运行时与 Mods（约 178MB），耐心等待
4. 需要登录 DMM 账号才能进游戏

## 免责

- 仅供学习交流，请于 24 小时内删除并支持正版
- 使用 mod 客户端登录线上服务产生的任何账号后果自负
- 本仓库不提供游戏本体下载
