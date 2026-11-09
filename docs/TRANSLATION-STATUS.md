# 翻译过程表（翻译状态总览）

> 适用范围：ドットアビスX（`jp.co.fanzagames.dotabyss_x_a`）中文汉化
> 数据来源：`trans_latest/`（权威译文树）→ APK 内 `assets/LemonLoader/deployment/UserData/AbyssMod/translations/`
> 最后更新：2026-11-09（对应游戏 1.16.0 / v19d）

---

## 一、总量

| 表 | 文件位置 | 单位 | 数量 |
|---|---|---|---|
| names（专有名词） | `names/zh_Hans.json` | 条 | **511** |
| static（MasterData 静态文本） | `static/zh_Hans.json` | 表 / 条 | **152 表 / 18,870 条** |
| ui_texts（界面文本） | `ui_texts/zh_Hans.json` | 路径 / 条 | **6,724 路径 / 8,018 条** |
| novels（剧情正文） | `novels/<剧本ID>/zh_Hans.json` | 话 / 条 | **1,409 话 / 61,672 条** |
| **合计** | | **条** | **~89,071** |

质量门禁：`tools/jp_lint.py lint --root trans_latest` → 假名残留 **0**、纯汉字日文标记字残留 **0**。

---

## 二、各表怎么算「覆盖」

| 表 | 键构成 | 覆盖判定 |
|---|---|---|
| names | 日文专名 | 与 MasterData 里出现过的专名去重后比对 |
| static | `表名 ▸ 字段 ▸ 日文原文` | 扫描「全部文本表的所有含日文字段」∪「按值去重」，取并集 |
| ui_texts | `节点路径 ▸ 日文原文`（含 `(Clone)` 后缀路径） | 路径精确匹配 + 通配 `*`（仅匹配单段）+ `{0}` 占位符模式 |
| novels | `剧本ID ▸ 日文原文` | 与 catalog 中已下发的剧本 ID 全集比对 |

**已知设计性跳过（不算缺口）**：

| 类别 | 条数 | 原因 |
|---|---|---|
| `m_ng_words.keyword` | 877 | NG 词过滤库，属屏蔽词表，翻译后会失效 |
| `m_dictionary_*_groups.initial` | 28 | 图鉴索引列（单假名），非可读文本 |
| 占位美术文案（`アイテム名１２３４`、`あいうえお…`、`テキストテキスト…`、`一二三四五六…ダミーテキスト`、`スキルネームの長さに…`、`ボススキル名８９０…`、`クエスト名クエスト名…` 等） | 22 | OnEnable 占位，运行时被 `set_text` 覆盖，翻反而会露馅 |

---

## 三、版本进度表

| 版本 | 游戏版本 | 译文内容 | 状态 |
|---|---|---|---|
| v17B | 1.15.0 | 基准全量（旧签名线） | 已发布 |
| — | 1.15.0 | + 907 条 UI 文本、static 23 处漏转修正、39 条 LEAK 补译、519 条 `(Clone)` 路径回填、真 manifest 算法 | 已并入（v19 基础） |
| **v19 / v19d** | **1.16.0** | 见下节 | 打包完成，待装机 |

---

## 四、1.16.0 新增内容（本轮完成）

### 4.1 新增剧情（32 话 / 1,257 条）— 全部已译

| 类别 | ID | 话数 | 内容 |
|---|---|---|---|
| 活动剧情 | `evs_10300050101` `evs_10300050201` | 2 | 镜影回廊 第5届：光之银丝与空舞的水流 |
| 艾什莉日常 | `hmn_10170100001`–`003` | 3 | 立绘日常三段（引退房间/日晒/约定） |
| 法莉丝日常 | `hmn_11170100001`–`003` | 3 | 有翼族身世 + 灭火自卫团 |
| 艾什莉角色剧情 | `hmr_10170100011`–`033` | 9 | 9 段（含 H 场景 3 组） |
| 法莉丝角色剧情 | `hmr_11170100011`–`033` | 9 | 9 段（含 H 场景 3 组） |
| 寝室/家门 | `men_10170100001`–`003`、`men_11170100001`–`003` | 6 | 短篇日常 |

**剧情原文来源链路**（可复现）：
1. 从设备 `/files/com.unity.addressables/*.bin` 拉当前 catalog
2. `tools/novel_pull.py diff <旧catalog> <新catalog>` → 新增剧本 ID
3. `probe` 子命令探测新资源号 → **aas 13618**（1.14.6=12738 / 1.15.0=13238）
4. `fetch` 从 `api.abyss-prod-r18.dotabyss.dmmgames.com/resources/android/r18/aas/13618/aa/<bundle>` 拉 bundle
5. `extract` → CSV 原文；`chunks` → 翻译工作台；`merge` → `zh_Hans.json`

> ⚠️ 必须直连（不带代理）。走 Clash 代理出口 IP 会 403；curl 也会 403，用 Python + `UnityPlayer/6000.3.8f1` UA 才能 206。

### 4.2 新增/修正界面文本

| 项 | 条数 | 说明 |
|---|---|---|
| static 新增（角色/装备/活动/关卡名与说明） | +336 | 1.16.0 MasterData 新增行 |
| names 新增专名 | +4 | アシュリー / ファリス 等 |
| ui_texts「難易度選択」 | +1 | 主线地图难度标题（LEAK 实捕） |

### 4.3 本版 MasterData 缺口审计结论

`_audit_1160.py` 全字段扫描结果：未译 905 条，**全部落在设计性跳过类别**（877 NG 词 + 28 单假名图鉴索引），真实缺口 **0**。

---

## 五、每次版本更新的固定流程

```
① 拉官方新包      root 抠 /data/app/vmdl*.tmp/base.apk
② 解包 + 包名改写  apktool d --no-res ▸ _axml_pkg.py ▸ _patch_arsc.py
③ interop 评估    metadata 版本号 + 类型面 diff（不必重生成就别重生成）
④ catalog diff     tools/novel_pull.py diff
⑤ 剧情拉取翻译     probe → fetch → extract → chunks → 人工译 → merge → install
⑥ 静态表审计       _apply_1160.py（补新增行）+ _audit_1160.py（查真缺口）
⑦ 语法门禁         tools/jp_lint.py lint
⑧ manifest 重算    tools/build_manifest.py
⑨ 烤包             cp trans_latest → deployment/.../translations
⑩ 补 res 缺失      apktool --no-res 会丢约 50 个 res 原始文件，从官方包回填
⑪ 对齐签名         zipalign -p -f 4 → apksigner（v1+v2+v3, keys/modkey-v2.jks）
⑫ 装机验证         Latest.log：Game Version / 198 interop / 2 Mods / Font loaded / LEAK 只剩占位
```

---

## 六、待办 / 已知遗留

| 项 | 状态 |
|---|---|
| v19d 装机（含新译文） | 待主人下线后安装（游戏运行中不可强停） |
| GitHub Release 发布 | 待装机验证后 |
| 1.16.0 之后的增量 | 等下次版本更新 |