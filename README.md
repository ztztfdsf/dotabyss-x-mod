# dotabyss-x-mod

ドットアビスX for Android（Dot Abyss X / パズルRPG, DMM GAMES）的民间整合 mod 仓库，非官方、与 EXNOA / FANZAGAMES / DMM 无关。

> ⚠️ 本仓库仅为技术存档与版本延续。使用第三方客户端登录线上服务存在账号风险，请自行评估。游戏本体请在 [DMM GAMES](https://games.dmm.com/) 正版渠道下载。

## 这是什么

对官方 APK 的整包重打包（repack），基于社区 mod 作者 Jitsu 的 AbyssMod v1.1.4（MelonLoader/LemonLoader 注入方案），随官方版本升级维护：

- 官方 `jp.co.fanzagames.dotabyss_x_a` → 共存包名 `jp.co.fanzagames.dotabyss_x_a.mod`（可与官方包并装）
- arm64-v8a only
- 进程内挂载 .NET 运行时（LemonLoader = MelonLoader 安卓移植），加载 `AbyssMod.dll`（Harmony 补丁：剧情/UI 翻译、动态马赛克开关、语音中断、标题动画、Live2D 缩放等）
- 游戏本体 `libil2cpp.so` / `libunity.so` / `global-metadata.dat` 与官方包逐字节一致，无代码注入

## 版本

| Release | 基线 | 说明 |
|---|---|---|
| v1.14.6 | 官方 1.14.6 (versionCode 31) | 见下 |

### v1.14.6 变更

- 基线从官方 1.14.4 升级到 1.14.6 直连包（`global-metadata.dat` 两版本 md5 相同，旧 interop 代理与 mod DLL 直接沿用）
- 内置启动 Toast 版本号同步为 1.14.6
- 签名：自签名（`CN=AbyssMod Archive, O=ntr.best`）。**与 1.14.4 时代作者的 DMMMODKE 签名不同，无法覆盖安装旧 mod，需先卸载再装**；同样因为签名不同，本仓库各版本之间如更换签名链也需卸载重装

## 翻译状态

AbyssMod 的翻译 CDN（`anosu/dotabyss-translation`）已停止更新（上游死于 2026-10-19 前后）。本包运行时翻译加载策略：

- CDN 404 时自动回退本地缓存（`.../files/MelonLoader/UserData/AbyssMod/translations/`）
- 可在 `AbyssMod.cfg` 中设置 `PreferLocalFiles = true` 强制本地优先
- 简体中文本地翻译包可由社区繁中镜像（`0BlueYan0/dotabyss-translation-zh_Hant`，原 anosu 布局）经 OpenCC t2s 转换得到；1.14.6 新增的 3 个剧情（evs_102000406017 / evs_102000407014 / evs_102000605016）暂无翻译

## 安装

1. 卸载旧版 mod（如签名不同）
2. 安装 Release 中的 APK
3. 首次启动会释放 LemonLoader 运行时与 Mods（约 178MB），耐心等待
4. 需要登录 DMM 账号才能进游戏

## 免责

- 仅供学习交流，请于 24 小时内删除并支持正版
- 使用 mod 客户端登录线上服务产生的任何账号后果自负
- 本仓库不提供游戏本体下载
