#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""重算 translations/ 下所有表的摘要，与 manifest 比对。

摘要算法与 AbyssMod 的 Utility.Cryptography.StringTableHash.ComputeEntries 一致：
  逐条 md5.Update(utf8(key)); Update(0x00); Update(utf8(value)); Update(0x00)
  - 扁平表（names / novels）：key = 条目键
  - ui_texts：key = path + "\u0001" + 日文原文
  - static：  key = 表名 + "\u0001" + 字段名 + "\u0001" + 日文原文
  空串不追加字节，但 0x00 分隔符始终追加

用法: python tools/check_manifest.py [--lang zh_Hans] [--root translations]
退出码: 0 全绿 / 1 有不一致
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


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--lang", default="zh_Hans")
    ap.add_argument("--root", default="translations")
    args = ap.parse_args()
    root, lang = args.root, args.lang

    man = load(os.path.join(root, "manifest", f"{lang}.json"))
    bad = 0

    names = load(os.path.join(root, "names", f"{lang}.json"))
    h = md5_entries([(k, names[k]) for k in sorted(names)])
    print(f"names     {h} vs {man['names']}  {'OK' if h == man['names'] else 'FAIL'}")
    bad += h != man["names"]

    ui = load(os.path.join(root, "ui_texts", f"{lang}.json"))
    h = md5_entries([(p + "\u0001" + jp, ui[p][jp]) for p in sorted(ui) for jp in sorted(ui[p])])
    print(f"ui_texts  {h} vs {man['ui_texts']}  {'OK' if h == man['ui_texts'] else 'FAIL'}")
    bad += h != man["ui_texts"]

    st = load(os.path.join(root, "static", f"{lang}.json"))
    h = md5_entries([(t + "\u0001" + f + "\u0001" + k, st[t][f][k])
                     for t in sorted(st) for f in sorted(st[t]) for k in sorted(st[t][f])])
    print(f"static    {h} vs {man['static']}  {'OK' if h == man['static'] else 'FAIL'}")
    bad += h != man["static"]

    novels_bad = 0
    for d, expect in man["novels"].items():
        p = os.path.join(root, "novels", d, f"{lang}.json")
        if not os.path.exists(p):
            print("MISSING", d)
            novels_bad += 1
            continue
        data = load(p)
        if md5_entries([(k, data[k]) for k in sorted(data)]) != expect:
            print("MISMATCH", d)
            novels_bad += 1
    print(f"novels    {len(man['novels'])} 话，失败 {novels_bad}")
    bad += novels_bad

    extra = [d for d in os.listdir(os.path.join(root, "novels")) if d not in man["novels"]]
    if extra:
        print(f"未登记 novels: {len(extra)} {extra[:5]}")
        bad += len(extra)

    print("\n结论:", "✅ 全绿" if bad == 0 else f"❌ {bad} 处不一致")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
