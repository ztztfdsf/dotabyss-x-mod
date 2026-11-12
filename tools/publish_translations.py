#!/usr/bin/env python3
"""把权威译文树 trans_latest/ 发布到仓库 translations/（游戏 CDN 拉取源）。

用法：
    python tools/publish_translations.py           # 同步 + 校验
    python tools/publish_translations.py --check   # 只校验不写

流程：
    1. 权威树 manifest 重算（build_manifest.py）
    2. trans_latest/ -> translations/ 全量镜像（删除多余文件，保证与权威树一致）
    3. check_manifest.py 校验镜像结果

游戏侧加载链：AbyssMod 启动拉 <CDN>/manifest/zh_Hans.json 比哈希，
不一致就拉新覆盖本地（cfg 的 PreferLocalFiles = false 时）。
发布后 git push，设备下次重启游戏自动更新，无需重装 APK。
"""
import argparse
import filecmp
import os
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(REPO, "trans_latest")
DST = os.path.join(REPO, "translations")

def run(script, *args):
    r = subprocess.run([sys.executable, os.path.join(REPO, "tools", script), *args],
                       stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                       encoding="utf-8", errors="replace")
    if r.stdout:
        print(r.stdout.strip())
    if r.returncode != 0:
        print(r.stderr or "", file=sys.stderr)
        sys.exit(r.returncode)

def mirror(src, dst):
    """单向镜像：src 覆盖 dst，删除 dst 多余文件。返回 (复制数, 删除数)。
    文本统一 LF 行尾（与 git 远端逐字节一致，md5 可自验）。"""
    copied = deleted = 0
    for root, dirs, files in os.walk(src):
        rel = os.path.relpath(root, src)
        target = os.path.join(dst, rel) if rel != "." else dst
        os.makedirs(target, exist_ok=True)
        for f in files:
            s, d = os.path.join(root, f), os.path.join(target, f)
            data = open(s, "rb").read().replace(b"\r\n", b"\n")
            old = open(d, "rb").read() if os.path.exists(d) else None
            if old != data:
                open(d, "wb").write(data)
                copied += 1
    for root, dirs, files in os.walk(dst):
        rel = os.path.relpath(root, src) if False else os.path.relpath(root, dst)
        source = os.path.join(src, rel) if rel != "." else src
        for f in files:
            d, s = os.path.join(root, f), os.path.join(source, f)
            if not os.path.exists(s):
                os.remove(d)
                deleted += 1
    return copied, deleted

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="只校验，不写 translations/")
    a = ap.parse_args()

    if not os.path.isdir(SRC):
        sys.exit(f"权威树不存在: {SRC}")

    print("① 重算 manifest …")
    run("build_manifest.py", "--root", SRC)

    if a.check:
        print("② --check：跳过镜像")
    else:
        print("② 镜像 trans_latest/ -> translations/ …")
        copied, deleted = mirror(SRC, DST)
        print(f"   复制 {copied} 个，删除 {deleted} 个多余文件")

    print("③ 校验 translations/ …")
    run("check_manifest.py", "--root", DST)
    print("OK：发布就绪，git add translations/ && git commit && git push 后设备下次启动自动更新")

if __name__ == "__main__":
    main()
