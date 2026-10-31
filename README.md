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

## 仓库结构

```
src/UIDumpMod/        UIDumpMod 源码（.NET 10；Core + TextHooks + Translation/ 纯逻辑层）
src/AbyssModPatcher/  对原作者 AbyssMod/Utility 二进制做 IL 手术的工具（修复新 interop 下的崩溃点）
tests/UIDumpMod.SelfTest/  翻译逻辑自测（PC 端可跑，改逻辑先过它）
tools/                novel_pull.py   — 新版本剧情拉取/解包/合并流水线（SOP ⑤）
                      leak2table.py   — LEAK 日志 → ui_texts 补译维护工具
                      glossary.py     — 专有名词一致性校验/统一工具
                      glossary-fix.json — 人工确认的译名统一表
docs/                 GLOSSARY.md     — 专有名词对照表（唯一权威译名来源）
                      glossary.json   — 同上，机器可读版
                      UPDATE_SOP.md   — 版本更新流程（含 interop 重生成与二进制手术）
                      AbyssMod.decompiled.cs（原作者 mod 的反编译参考，只读）
```

接手维护请先读 [`src/UIDumpMod/README.md`](src/UIDumpMod/README.md)（架构与踩坑记录）、[`docs/UPDATE_SOP.md`](docs/UPDATE_SOP.md)（版本更新流程）和 [`docs/GLOSSARY.md`](docs/GLOSSARY.md)（**译名唯一权威来源，改任何名字前先查这里**）。改翻译逻辑前先跑 `dotnet run --project tests/UIDumpMod.SelfTest`，全部通过再装机。

## 翻译状态

AbyssMod 的翻译 CDN（`anosu/dotabyss-translation`）已停止更新（上游死于 2026-10-19 前后）。**APK 已内置全量简体中文翻译包并人工补翻全部缺口**，首启自动释放到本地并本地优先加载，无需任何额外操作。

| 部分 | 覆盖 |
|---|---|
| 剧情（novels） | 1377 话全覆盖（1.14.6 基线 1339 话 + 1.15.0 新增 38 话：活动主线 8 话 + 亨丽埃塔/千寻个人与 H 剧情 30 文件，人工翻译） |
| MasterData 翻译表（static） | 152 张表 / 18497 条目（深渊 157 层、商店礼包、活动任务、职业、角色档案、寝室名、句签名、图鉴等全量） |
| UI 硬编码文本（ui_texts） | 6570 条路径（全 UI 预制体提取 + 运行时泄漏路径滚动修复） |
| 角色名/专有名词（names） | 507 条（译名统一规则见 [`docs/GLOSSARY.md`](docs/GLOSSARY.md)） |

基础译文来自社区繁中镜像（`0BlueYan0/dotabyss-translation-zh_Hant`，原 anosu 布局）经 OpenCC t2s 转换（只转值不转 key），MasterData/UI 缺口部分为本仓库人工补翻。UI 残留日文的持续修复方法见 `tools/leak2table.py` 头部注释。

> **斯卡雷特（スカーレット）剧情修复（v14）**：上游镜像把该角色 6 话（`107801*`，共 12 个文件 594 行）的译文整段填成了一部中国网络小说的正文，内容与日文原文完全无关。本仓库已将这 594 行**从日文原文全部重译**并替换，同时全量扫描确认其余 1327 话无同类问题。
>
> **专有名词统一（v14）**：同一角色在旧包里存在多种译名（如 `スカーレット` 既有「斯卡蕾特」又有「斯卡雷特」、`アリシア` 有「艾莉西亚/阿莉西亚」）。已在本地建立 [`docs/GLOSSARY.md`](docs/GLOSSARY.md) 作为翻译时的唯一权威译名表（配套校验工具 `tools/glossary.py`），并全库统一 1400+ 处。规则：界面显示名以 `names` 表为准，正文/表/UI 必须与之一致。

## 版本

| 版本 | 基线 | 说明 |
|---|---|---|
| 1.15.0 · mod v17B | 官方 1.15.0 (versionCode 32) | **修复进入 H 剧情 CG 黑屏**（AbyssMod 旧 interop 渲染 hook 错杀 spine 模型，手术 no-op 定罪修复）；MasterData 全表清扫至 **152 表/18497 条**（寝室名/句签名/任务/图鉴/商店全量）；UI 路径增至 **6570**；UIDumpMod 1.9.0 纯翻译版（Toast 已拆除） |
| 1.15.0 · mod v17 | 官方 1.15.0 (versionCode 32) | 游戏本体升级 1.15.0；interop 代理按新 metadata 重生成（修复闪退/字体丢失）；payload.json 修复；**38 话新剧情人工全译**（活动主线 + 亨丽埃塔/千寻）；Toast 改 UIDumpMod 自绘原版视觉（旧渲染链在新 interop 下 coreclr abort） |
| 1.15.0 · mod v15/v16 | 官方 1.15.0 (versionCode 32) | v15 首版（已损坏勿用）；v16 修复闪退+字体但 Toast 渲染链崩溃（已被 v17 取代） |
| 1.14.6 · mod v14 | 官方 1.14.6 (versionCode 31) | 纯翻译版：AbyssMod + UIDumpMod + 全量翻译包 + 斯卡雷特剧情重译 + 全库专名统一 |
| 1.14.6 · mod v13 | 官方 1.14.6 (versionCode 31) | 含上游斯卡雷特剧情的坏译文与旧译名（已被 v14 取代，保留存档） |

- 基线从官方 1.14.4 升级到 1.14.6 直连包，再到 1.15.0（1.15.0 的 `global-metadata.dat` 有变化，但差异为纯增量：+134/-10 类型，经 il2cpp 双版本 diff 验证旧 interop 代理与 mod DLL 兼容复用）
- 内置启动 Toast 版本号同步为 1.15.0
- 签名：自签名（`CN=AbyssMod Archive, O=ntr.best`）。v14/v15 同签名线可直接覆盖安装；**与 1.14.4 时代作者的 DMMMODKE 签名不同，无法覆盖安装旧 mod，需先卸载再装**

## 安装

1. 卸载旧版 mod（如签名不同）
2. 安装 Release 中的 APK
3. 首次启动会释放 LemonLoader 运行时与 Mods（约 178MB），耐心等待
4. 需要登录 DMM 账号才能进游戏

## 免责

- 仅供学习交流，请于 24 小时内删除并支持正版
- 使用 mod 客户端登录线上服务产生的任何账号后果自负
- 本仓库不提供游戏本体下载
