# 签名（APK 签名线）

本仓库的发行版用**自签名**。密钥文件不入库（`.gitignore` 排除 `keys/`、`*.jks`、`*.keystore`）。

## 两条签名线

| 线 | 证书 DN | 证书 SHA-256 | 生效版本 | 状态 |
|---|---|---|---|---|
| 旧线 | `CN=AbyssMod Archive, O=ntr.best, C=JP` | `774a4955c36d823c501c7a5d8ef54a1686c9aeee2c69294d9b787dab8d8c370d` | v14 ～ v18 | **密钥文件已丢失，不可用于新版本** |
| 新线 v2 | `CN=AbyssMod Archive v2, O=ntr.best, C=JP` | `8fb9c84540bd6210c41bfe35dd6ec54f24c1588f207630bb72ef4c87c8bdf8b1` | 下一版起 | 有效（2048-bit RSA / SHA256withRSA / 2026-11-03 ～ 2056-10-26） |

旧线为什么失效：2026-11-02 一次磁盘事故把 `modkey.jks` 连同整个构建树清零。之后做了穷尽取证（G 盘 1.9 TB 裸盘按 JKS magic 与证书 DER 双路扫描、C/D/E 全盘按 PKCS12 形态普查并用密码实开、仓库 30 个提交全历史、设备端 `/data` 与原 `/sdcard` 备份、卷影副本），确认数据块已被覆盖，无法恢复。私钥无法从 APK 内的证书/签名反推（RSA-2048）。

## 新线 v2 的物料

三处各放一份，内容逐字节相同（`sha256(modkey-v2.jks) = a04efb0b791ecb0f068fbce11b2b888d4594cff87fce383f357b7afb3e5f8f26`）：

```
G:\tmp\dotabyss-mod\keys\            ← 主副本（工作区）
G:\工具等其他文件存放处\安卓mod工具\keys\   ← 副副本
D:\备份-dotabyss签名钥\                ← 异盘副本（关键，G 盘出过事故）
```

每个目录里同时有：

| 文件 | 用途 |
|---|---|
| `modkey-v2.jks` | PKCS12 keystore，别名 `abyssmod` |
| `abyssmod-v2-key.pem` | 私钥（PKCS#8 明文 PEM），keystore 格式损坏时的重建源 |
| `abyssmod-v2-cert.pem` | 单张证书 |
| `abyssmod-v2-certchain.pem` | 证书链 |
| `*.b64.txt` | 以上四者的 base64 文本形态（便于贴到别处/跨工具传输） |

密码与别名见本地 `keys/README.txt`（**不入库**）。

## 重签命令

```bash
# 对齐 + 签名（v1/v2/v3 全开，minSdk 25 起）
zipalign -p -f 4 <unsigned>.apk <aligned>.apk
apksigner sign \
  --ks keys/modkey-v2.jks --ks-key-alias abyssmod \
  --ks-pass pass:<见本地 README> --key-pass pass:<同> \
  --v1-signing-enabled true --v2-signing-enabled true --v3-signing-enabled true \
  --out dotabyss-<ver>-mod.apk <aligned>.apk

# 验收：证书指纹必须是 8fb9c84540bd6210c41bfe35dd6ec54f24c1588f207630bb72ef4c87c8bdf8b1
apksigner verify --print-certs dotabyss-<ver>-mod.apk
```

## 换线对用户的影响（必须写进 Release 说明）

APK 签名是包身份的一部分，**签名不同 = 系统拒绝覆盖安装**（`INSTALL_FAILED_UPDATE_INCOMPATIBLE`）。这与 root 无关，任何安装器都绕不过（MT 管理器的"签名校验"功能是给应用自身的自检用的，不是系统安装校验）。

所以新线首发时用户必须：

1. 卸载旧 mod（服务端存档、角色、进度、氪金全部不受影响）
2. 卸载前想保住本地资源的，先把
   `/sdcard/Android/data/jp.co.fanzagames.dotabyss_x_a.mod/files/`（约 7 GB 游戏资源）
   复制到 `/sdcard/backup/` 之类的位置；装好新包后拷回，并用 root `chown` 到新 uid、`restorecon`
3. 装新包 → 首启释放 LemonLoader（约 178 MB）→ 重新登录 DMM

## 换线的技术替代（维护者参考，非用户路径）

设备已 root（KernelSU + Zygisk Next + LSPosed IT，支持 Android 16）时，可装 **CorePatch** 模块在 `system_server` 层关闭签名校验，从而允许签名不一致的覆盖升级（省掉卸载与资源搬运）。

- CorePatch 4.7（2025-06-14）初步支持 A16；4.8 起可破解 A13+ 的 V3 签名认证；4.9（2026-04-06）修 A17 崩溃
- 启用方式：LSPosed 里勾选「系统框架」作用域 → 重启设备 → `pm install -r`
- 风险：设备 `veritymode=enforcing`、bootloader locked 的环境下 hook `system_server` 有不确定性；失败通常表现为安装仍报签名冲突（退回卸载重装），极端情况需重启或停用模块

本仓库的发版流程**不依赖**该方案（用户侧不假设 root）。
