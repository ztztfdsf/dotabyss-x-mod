#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""按 AbyssMod 的真实算法重算 manifest 摘要并写回。

背景：manifest 里的 names/static/ui_texts/novels 摘要是 AbyssMod 用来校验下载到的表是否
一致的（`Utility.Cryptography.StringTableHash.ComputeEntries`）。历史上手写 manifest 一直
是过期值，导致日志里四条 `JSON resource validation failed` + 回退 stale cache。本脚本按
反编译得到的真实算法生成，保证一次到位。

算法（ILSpy 反编译 Utility.dll 得到）:
    foreach (key, value) in entries:   # 调用方按 StringComparer.Ordinal 排序
        AppendData(utf8(key)); AppendData(0x00); AppendData(utf8(value)); AppendData(0x00)
    空串不追加字节，但 0x00 始终追加

键的构成:
    names / novels  扁平表  -> key 本身
    ui_texts               -> path + "\\u0001" + 日文原文
    static                 -> 表名 + "\\u0001" + 字段名 + "\\u0001" + 日文原文

用法:
    python tools/build_manifest.py --root <translations 目录> [--lang zh_Hans]
    # 只算不写：加 --dry-run
"""
import argparse
import hashlib
import json
import os
import sys

SEP = b"\x00"


def md5_entries(pairs) -> str:
    h = hashlib.md5()
    for k, v in pairs:
        for s in (k, v):
            if s:
                h.update(str(s).encode("utf-8"))
            h.update(SEP)
    return h.hexdigest()


def load(p):
    return json.loads(open(p, encoding="utf-8-sig").read())


def dump(obj, p):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8", newline="\n") as f:
        json.dump(obj, f, ensure_ascii=False, indent=1, sort_keys=True)
        f.write("\n")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", required=True, help="translations 目录（含 names/ui_texts/static/novels/manifest）")
    ap.add_argument("--lang", default="zh_Hans")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    root, lang = args.root, args.lang

    names = load(os.path.join(root, "names", f"{lang}.json"))
    ui = load(os.path.join(root, "ui_texts", f"{lang}.json"))
    static = load(os.path.join(root, "static", f"{lang}.json"))

    h_names = md5_entries([(k, names[k]) for k in sorted(names)])
    h_ui = md5_entries([(p + "\u0001" + jp, ui[p][jp]) for p in sorted(ui) for jp in sorted(ui[p])])
    h_static = md5_entries([(t + "\u0001" + f + "\u0001" + k, static[t][f][k])
                            for t in sorted(static) for f in sorted(static[t])
                            for k in sorted(static[t][f])])

    novels = {}
    nov_root = os.path.join(root, "novels")
    for d in sorted(os.listdir(nov_root)):
        p = os.path.join(nov_root, d, f"{lang}.json")
        if os.path.exists(p):
            data = load(p)
            novels[d] = md5_entries([(k, data[k]) for k in sorted(data)])

    man_path = os.path.join(root, "manifest", f"{lang}.json")
    old = load(man_path) if os.path.exists(man_path) else {}
    man = {
        "names": h_names,
        "static": h_static,
        "ui_texts": h_ui,
        "novels": novels,
        "replacements": old.get("replacements", {}),
    }
    man["hash"] = hashlib.md5(json.dumps(man, ensure_ascii=False, sort_keys=True).encode("utf-8")).hexdigest()

    print(f"names     {len(names):>6} 条      {h_names}")
    print(f"ui_texts  {len(ui):>6} 路径     {h_ui}")
    print(f"static    {len(static):>6} 表       {h_static}")
    print(f"novels    {len(novels):>6} 话")
    print(f"manifest hash = {man['hash']}")

    if args.dry_run:
        print("[dry-run] 未写盘")
        return 0
    dump(man, man_path)
    print(f"已写入 {man_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
