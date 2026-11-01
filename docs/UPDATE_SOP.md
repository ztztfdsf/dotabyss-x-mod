# 版本更新 SOP v2（游戏出新版本时照此顺序执行）

> 适用：官方 APK 升级（如 1.15.0 → 1.15.1 / 1.16.x）时的 mod 跟进维护。
> 工作区：`G:\tmp\dotabyss-mod\`（解码树按版本命名 `dl-<ver>.out`，产物 `out/`）。
> **顺序不可颠倒**：每一步的产物是下一步的输入；跳步的代价已在 1.15.0 上付过一次（interop 漏参数 → 两次闪退，多花 6 小时）。

---

## 第 0 步｜前置侦察（只读，不碰设备）

1. **查版本**：经代理查 `api.store.games.dmm.com`（app_id=771484），确认新版本号 / vc / 维护公告
2. **红线确认**：`adb shell pidof jp.co.fanzagames.dotabyss_x_a.mod`
   - 有进程 → 主人在游戏里：**只做本地工作**，不 force-stop、不重启、不装机（尤其深渊探索中）
   - 无进程 → 可自由操作
3. **工作区准备**：`G:\tmp\dotabyss-mod\dl-<ver>.out/`（新目录，不覆盖旧版解码树）

## 第 1 步｜拉官方新包

- 包名 `jp.co.fanzagames.dotabyss_x_a`（**DMM 直连版**，不是 `com.dmm.dmmgames.*` 商店壳包）
- 直连慢（~174KB/s）；走 `vercel.ntr.best` 代理约 **8.9MB/s**
- 单次容易截断（10 秒断流）→ **断点续传补齐**，拉完 `unzip -t` 验完整，记录字节数 + sha256

## 第 2 步｜解码 + 版本差异预检（决定后面三档工作量）

```bash
apktool d -f --no-res -o dl-<ver>.out <apk>   # 只要 smali（翻译/打包用不到 res）
```

- **必比 `global-metadata.dat` md5**（上一版解码树 vs 新版）：
  - **一致** → 老 interop 代理 + 现有 dll 全部沿用，跳过第 6 步的成本（1.14.4→1.14.6 就是这种）
  - **不一致** → interop 必须重生成（第 6 步），且 AbyssMod 需二进制手术
- 记录：`lib/arm64-v8a/libil2cpp.so` 大小、`assets/bin/Data/Managed/Metadata/global-metadata.dat` md5

## 第 3 步｜三路差分（工作量清单，先摸清再动手）

| 路 | 数据源 | 方法 | 产出 |
|---|---|---|---|
| **新剧情** | addressables catalog | 新旧 `catalog_1.json` 差集，抽出 `evs_*` bundle 全名 | 待翻话数 |
| **新 MasterData** | 设备 `files/DownloadCache/*.dat`（MessagePack 全表） | 与上一版 `m_*` 表逐字段 diff | 新表 / 新字段 / 新行 |
| **新 TextData** | 运行时本地化表（46 容器 / 1043 key） | UIDumpMod 探针 dump 后与前版比 | 新 key（值层，最底层源头） |

catalog 解析要点：**长度前缀法**（protobuf 字符串前有长度字节，如 `0f`=15 字节），
勿把下一字段 tag 拼进 ID（曾误判"3 个新剧情"）。
bundle 下载 URL 用 catalog 里该条目的**完整文件名（InternalId）**，裸短名会 403；
资源号 `…/r18/aas/<n>/aa/<bundle>` 的 `<n>` 换版本会变，从旧号起向上扫。

## 第 4 步｜翻译（**顺序：值层 → 剧情 → 字段层 → 收口**）

1. **TextData 值层**（优先级最高：所有弹窗/格式串的总源头，`__global__` 层承载）
   - ⚠️ **必须用 268 日文标记字预筛**，纯汉字日文（`装備一覧` / `自動分解設定` / `確認`）不含假名，
     只筛假名会整批漏掉（1.15.0 漏了 148 条，玩家看到"繁体中文"）
   - 标记字表生成法见 `Translation/JapaneseText.cs` 注释（日文原文≥2 次 ∧ 简体译文从不使用 ∧ 剔除 GB2312 同形字）
2. **新剧情** novels：先对齐 `translations/names/zh_Hans.json`（界面显示名权威表），再逐话翻
   - 工具：`tools/novel_pull.py`（catalog 查 bundle → 下载 → 解包 → 分块编号）
   - 解包：UnityPy 常装不上，用自写最小 UnityFS+LZ4 解析器（TextAsset 结构简单）
3. **MasterData 字段层**：409 张表所有含日文字段
   - 两条口径 **取并集**：①按字段（历史翻过的字段集合）②按值去重（跨表同串）
   - 模板机翻：数字切段做 O(1) 结构匹配（正则回溯会超时），能吃掉 ~80%
   - 专名统一：跑 `tools/glossary.py`，新专名先写回 names 表
4. **收口全量扫漏**（一个都不能少）：
   - 假名残留 = 0
   - **标记字残留 = 0**（值域含 268 字之一即漏网）
   - 简体/t2s 校验（OpenCC 只转值不转 key）
   - 旧写法残留 = 0（`tools/glossary.py --skip` 统计）

## 第 5 步｜翻译表落盘 + 推设备

```bash
# 权威树 → 解码树（烤包）+ 设备（热更）
cp trans-out/ui_texts/zh_Hans.json  dl-<ver>.out/assets/LemonLoader/deployment/UserData/AbyssMod/translations/ui_texts/
cp trans-out/names/zh_Hans.json     dl-<ver>.out/assets/LemonLoader/deployment/UserData/AbyssMod/translations/names/
cp -r trans-out/novels/*            dl-<ver>.out/assets/LemonLoader/deployment/UserData/AbyssMod/translations/novels/
```
- 设备侧同样的相对路径（`/data/media/0/Android/data/<pkg>/files/MelonLoader/UserData/AbyssMod/translations/`）
- **推完必须 md5 逐位比对**（本地 vs 设备），`su -c` 里有变量时注意本地 shell 吞变量（cp 静默失败过）

## 第 6 步｜代码/二进制跟进（仅当 metadata 变了）

1. **interop 重生成**（缺参数 → 闪退，血泪）：
   ```
   Cpp2IL dump 新版 → Il2CppInterop generator
   必须带：--game-assembly=<libil2cpp.so>   # 否则属性无 GC 写屏障 → 写 MasterData 后崩
          --unity=<libunity.so>            # 否则 AssetBundle.LoadFromFile 缺失 → 字体加载失败
          --add-prefix-to ""               # 前缀控制，缺了类型名对不上
   ```
   替换解码树 `assets/LemonLoader/runtime/interop/`（198 个程序集）
2. **AbyssModPatcher 手术**（旧 dll 跑新 interop 会踩错位槽位）：
   ```
   dotnet run src/AbyssModPatcher -c Release -- all <原版 Mods/AbyssMod> <输出>
   ```
   - Utility.dll：拆 Toast 渲染链（25 类型 + 内嵌 so/dex），保留门面签名
   - AbyssMod.dll：挖空 `DisableMosaic` / `BeginNovelLive2DScale` / `EndNovelLive2DScale`（会错杀 spine → CG 黑屏）
   - **输入永远用原作者原版 dll**，不要拿手术过的再手术
3. **UIDumpMod 重编**：`dotnet build src/UIDumpMod -c Release -p:GameTree=<新解码树>`，然后跑 SelfTest：
   ```
   dotnet run --project tests/UIDumpMod.SelfTest -c Release -- <ui_texts 路径>
   ```
   必须 `28 passed, 0 failed`

## 第 7 步｜打包

1. **payload.json 检查**（首启释放清单）：新增目录/顶层目录必须能被释放
   （实测 loader 会枚举整个 `deployment/` 并记进 `.lemonloader-deployment-state`，
   `deploymentFiles` 只覆盖个别文件的 policy；但**新增顶层目录仍要实弹冷装确认**）
2. manifest：包名加 `.mod` 后缀（共存）+ `AuthWebViewKt` UA 正则剥 `.mod`（smali 里要写 `\\.mod`，双层转义）
3. Toast 版本串：AbyssMod.dll `#US` 堆同长替换（字符数必须一致，否则元数据位移）
4. 只留 arm64（删 armeabi-v7a / x86）
5. `zipalign` → `apksigner` 用 `out/modkey.jks`（CN=AbyssMod Archive，密码 `dotabyss`）
   **必须同一把钥匙**，否则不能 `install -r` 覆盖（换钥匙 = 用户数据全丢）
6. **unzip 三项实检**：`deployment/Mods/` 只含预期 mod、零凭据（.jks/keystore）、翻译表条数对得上

## 第 8 步｜装机验证（全绿才发版）

```bash
adb install -r out/dotabyss-<ver>-mod-v<N>.apk
```
`Latest.log` 七件套：
```
Game Version: <ver>
2 Mods loaded                              ← AbyssMod + UIDumpMod
[AbyssMod] MasterData translation plan created. Tables: 151+
[AbyssMod] Static translation bundle loaded. Tables: 152
[AbyssMod] Static translation loaded [ui_texts]. Paths: <数>
[AbyssMod] Font loaded: TTCuYuanJ SDF      ← 缺这行 = interop 漏 --unity
[UIDumpMod] table: exact=… suffix=… patterns=… global=…
ERROR 条数 = 0
```
再过一遍页面：主页 / 队伍 / 商店 / 抽卡 / 剧情 / 深渊 / 娼馆 / 装备弹窗 / 交流，
每页看有无**假名 + 纯汉字日文（标记字）**残留，收 LEAK 日志补表。

### 登录掉线的处理（10002-0）

- `10002-0` = Store SDK `CONFIG_GET_HTTP_STATUS`（拿配置时 HTTP 0）→ 网络层，不是账号问题
- DMM 的 `games.dmm.com` WebView 登录页 **需要真日本出口**；登录成功后 token 落地，游戏全程可直连
- 真日本落地实测可用：平板 FlClash → 极速机场 → **日本东京**（`103.62.49.130`）
  - 吹雪云的"日本"节点多为新加坡落地（`47.130.x` / `3.113.x`），会被地区限制
- 应急取凭据（可选）：设备 `UserData/DUMP_CREDS` 空文件 → UIDumpMod 打印 DMM AppId/ConsumerKey/Secret
  （ConsumerKey=`7dRsiHdNchSZTnp2` R18 / AppId=771484；用后立即删文件并清日志）

## 第 9 步｜发版

1. Release 名称与 tag **必须与新版本号一致**（`v<ver>-v<N>`），别留旧号
2. 上传：后台 curl 挂 Clash 显式代理 `-x http://127.0.0.1:7890`，**优先日本节点**（2 分钟 vs 美国 95 分钟）
3. 上传后对 GitHub digest：本地 sha256 与服务端 digest **逐位一致**；
   下载链路验 `206` + 前缀 md5（代理抖动不必全量下完，服务端 digest 已足以证明完整性）
4. README 版本表 + `docs/GLOSSARY.md` 同步，commit 推送（git 直连 GitHub 超时 → `-c http.proxy=http://127.0.0.1:7890`）
5. Release 说明含：md5 / sha256 / 字节数 / "只含翻译与显示层功能" 声明 / 覆装说明

## 发布前后红线

- ❌ 主人在深渊探索中 → 绝不 force-stop / 重启游戏（曾因强停导致服务端回档 + 楼层计数器 DESYNC）
- ❌ 绝不与官方原版共存安装（`.mod` 包重声明了原包自定义权限，冲突装不上；卸原版会丢存档）
- ❌ 绝不按进程名批量 `taskkill`（`node.exe` / `java.exe` 里有别的东西）
- ❌ 付费 API 额度不用于验证测试
