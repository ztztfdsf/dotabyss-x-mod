# dotabyss-x-mod

ドットアビスX for Android（Dot Abyss X / DMM GAMES）的民间简体中文整合 mod 仓库，非官方、与 EXNOA / FANZAGAMES / DMM 无关。

> ⚠️ 本仓库仅为技术存档与版本延续。使用第三方客户端登录线上服务存在账号风险，请自行评估。游戏本体请在 [DMM GAMES](https://games.dmm.com/) 正版渠道下载。

**历史原发布处（1.14.4 时代，作者 anosu/Jitsu）**：<https://github.com/anosu/DMM-Mod/releases/tag/mod-dotabyss-v30> · 镜像站条目 <https://release.ntr.best/>（该线已停更）

## 这是什么

对官方 APK 的整包重打包（repack），基于社区 mod 作者 Jitsu 的 AbyssMod v1.1.4（MelonLoader/LemonLoader 注入方案），随官方版本升级维护。**本仓库发行版只包含翻译功能**：

- 官方 `jp.co.fanzagames.dotabyss_x_a` → 共存包名 `jp.co.fanzagames.dotabyss_x_a.mod`（可与官方包并装）
- arm64-v8a only · **Android 7.1+（minSdk 25 / targetSdk 36）**
- 进程内挂载 .NET 运行时（LemonLoader = MelonLoader 安卓移植），加载两个 mod：
  - **AbyssMod.dll**（原作者 Jitsu v1.1.4）：Harmony 补丁，剧情/UI/MasterData 翻译核心
  - **UIDumpMod.dll**（本仓库自研，`src/UIDumpMod/`）：运行时 UI 兜底翻译层，修复预制体路径与运行时路径失配导致的数千条翻译失效
- 登录 WebView UA 与官方包对齐（消除 `.mod` 包名暴露）
- 游戏本体 `libil2cpp.so` / `libunity.so` / `global-metadata.dat` 与官方包逐字节一致，无代码注入、无其他功能改动

## 仓库结构

```
src/UIDumpMod/    UIDumpMod 源码（.NET 10，含构建说明与架构文档）
tools/            leak2table.py — LEAK 日志 → ui_texts 补译维护工具
docs/             UPDATE_SOP.md（版本更新六步流程）
                  AbyssMod.decompiled.cs（原作者 mod 的反编译参考，只读）
```

接手维护请先读 [`src/UIDumpMod/README.md`](src/UIDumpMod/README.md)（架构与踩坑记录）和 [`docs/UPDATE_SOP.md`](docs/UPDATE_SOP.md)。

## 翻译状态

AbyssMod 的翻译 CDN（`anosu/dotabyss-translation`）已停止更新（上游死于 2026-10-19 前后）。**APK 已内置全量简体中文翻译包并人工补翻全部缺口**，首启自动释放到本地并本地优先加载，无需任何额外操作。

| 部分 | 覆盖 |
|---|---|
| 剧情（novels） | 1339 话全覆盖 |
| MasterData 翻译表（static） | 150 张表 / 74000+ 条目（深渊 157 层、商店礼包、活动任务、职业、角色档案等全量） |
| UI 硬编码文本（ui_texts） | 6319 条路径（全 UI 预制体提取 + 运行时泄漏路径滚动修复） |
| 角色名/专有名词（names） | 492 条 |

基础译文来自社区繁中镜像（`0BlueYan0/dotabyss-translation-zh_Hant`，原 anosu 布局）经 OpenCC t2s 转换（只转值不转 key），MasterData/UI 缺口部分为本仓库人工补翻。UI 残留日文的持续修复方法见 `tools/leak2table.py` 头部注释。

## 版本

| 版本 | 基线 | 说明 |
|---|---|---|
| 1.14.6（待发布） | 官方 1.14.6 (versionCode 31) | 纯翻译版：AbyssMod + UIDumpMod + 全量翻译包 |

- 基线从官方 1.14.4 升级到 1.14.6 直连包（`global-metadata.dat` 两版本 md5 相同，旧 interop 代理与 mod DLL 直接沿用）
- 内置启动 Toast 版本号同步为 1.14.6
- 签名：自签名（`CN=AbyssMod Archive, O=ntr.best`）。**与 1.14.4 时代作者的 DMMMODKE 签名不同，无法覆盖安装旧 mod，需先卸载再装**

## 安装

1. 卸载旧版 mod（如签名不同）
2. 安装 Release 中的 APK
3. 首次启动会释放 LemonLoader 运行时与 Mods（约 178MB），耐心等待
4. 需要登录 DMM 账号才能进游戏

## 免责

- 仅供学习交流，请于 24 小时内删除并支持正版
- 使用 mod 客户端登录线上服务产生的任何账号后果自负
- 本仓库不提供游戏本体下载
