# 版本更新 SOP（游戏出新版本时照此执行）

> 适用：官方 APK 升级（如 1.14.6 → 1.14.7+）时的 mod 跟进维护。
> 工作区：见仓库根目录 `docs/WORKSPACE.md`（本地为 `G:\tmp\dotabyss-mod\`）。

## ① 拉官方新包

- 取 **DMM 直连版**（包名 `jp.co.fanzagames.dotabyss_x_a`），**不是**商店壳包（`com.dmm.dmmgames.*`）
- 版本查询：经代理查 `api.store.games.dmm.com`（app_id=771484）

## ② 塞 MOD（装配）

1. apktool 解码新官方包
2. **先比对 `global-metadata.dat` md5**：
   - 与上一版**一致** → 旧 interop 代理（198 个）与 mod DLL 直接沿用，把上一版
     `assets/LemonLoader/` 整树搬进新解码树即可
   - **不一致** → 类型/方法有变化，需评估 AbyssMod 与 interop 代理兼容性（可能要重新生成代理）
3. 复用上一版的改动：manifest 包名 `.mod` 后缀（共存）、UA 加固、
   翻译包（deployment/UserData）、Mods（AbyssMod + UIDumpMod）
4. Toast 版本号打点：AbyssMod.dll `#US` 堆里 `Mod 加载成功，版本: 1.14.6`
   做同长度替换（注意保持字符数一致，元数据才不位移）
5. zipalign + `modkey.jks` 自签（密码 `dotabyss`）

## ③ 查新剧情

拉新旧两版 addressables catalog 做差集：
- 设备路径 `.../files/com.unity.addressables/` 或 CDN `resources/android/r18/aas/<hash>/aa/catalog_1.json`
- **必须用长度前缀法解析**（protobuf 里字符串前有长度字节，如 `0f`=15 字节），
  勿把下一个字段的 tag 字节拼进 ID（曾因此误判"3 个未翻译新剧情"，实为乌龙）
- diff 出新增 `evs_*` ID

## ④ 查新页面/新文本

- MasterData（411 张 `m_*` 表，从设备 `DownloadCache` 拉或运行时 dump）与旧版 diff
- ui_texts：装机后跑 UIDumpMod 收 `[LEAK]`（新界面自然漏出来）

## ⑤ 翻译

- 新剧情：从官方 CDN 拉对应 bundle（裸奔可直连，无签名），UnityPy/解包取日文原文
- 繁中镜像（`0BlueYan0/dotabyss-translation-zh_Hant`）若已更新 → OpenCC t2s
  （**只转 JSON 值，不转 key**——key 是日文原文用于匹配）
- 镜像没更新的部分人工翻
- UI 残留：`tools/leak2table.py` 维护循环（见 WORKSPACE.md）

## ⑥ 基准测试（全绿才发版）

装机实测，检查 MelonLoader `Latest.log`：

```
Game Version: <新版本号>
2 Mods loaded                        ← AbyssMod + UIDumpMod（仅翻译层）
[AbyssMod] MasterData translation plan created. Tables: 151+
[AbyssMod] Static translation bundle loaded. Tables: 152
[AbyssMod] Static translation loaded [ui_texts]. Paths: <数>
[AbyssMod] Font loaded: TTCuYuanJ SDF
[UIDumpMod] table: exact=... suffix=... patterns=...
[LEAK] 条数 ≈ 0                      ← 仅允许编辑器占位符（あいうえお 等）
```

加全功能页面过一遍（主页/队伍/商店/抽卡/冒险/剧情/深渊）+ 翻译显示确认。

## ⑦ 重大版本附加：interop 重生成 + 原作者二进制手术

当 `global-metadata.dat` 有实质变化（类型增减）时，除上述六步外还需要：

1. **重生成 interop 代理**（否则旧代理槽位错位，mod 一调用就 coreclr abort）：
   Cpp2IL dump 新版 → Il2CppInterop generator（LemonLoader 自带 fork）生成，
   关键参数：`--game-assembly=<libil2cpp.so> --unity=<libunity.so>`，缺前者
   生成的属性没有 GC 写屏障（写 MasterData 会崩），缺后者 AssetBundle 方法不还原
   （字体加载失败）。生成后替换解码树 `runtime/interop/`（198 个程序集）。
2. **原作者二进制手术**（AbyssMod/Utility 按 1.14 interop 编译，新 interop 下部分路径崩溃）：
   ```
   dotnet run src/AbyssModPatcher -c Release -- all <原版 Mods/AbyssMod 目录> <输出目录>
   ```
   自动完成：Utility.dll 拆 Toast 渲染链（保留门面签名）；AbyssMod.dll 挖空
   DisableMosaic/Begin/EndNovelLive2DScale 三个渲染 hook（错杀 spine CG）。
   **输入永远用原作者原版 dll**，不要拿已手术过的再手术。
3. **UIDumpMod 也要用新 interop 重编**（csproj 默认引用当前解码树，见 -p:GameTree）。
4. 冒烟重点：进一次 H 剧情（spine CG 显示）、等一次 MasterData 写完不崩、字体加载。

## 发布

1. unzip 复核 APK：deployment/Mods/ 只含 AbyssMod + UIDumpMod，无凭据/调试残留
2. 旧 Release 三删（asset → release → tag）→ 建 Release（说明含 md5/sha256/大小 +
   "先卸载旧签名版"提示 + "只含翻译功能"声明）
3. README 同步更新版本表，commit 推送
4. 下载链路实测（206/200 + digest 比对）
