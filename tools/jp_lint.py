#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
日文漏网扫描 / 标记字表生成工具
==============================

背景：日文汉字与繁体字形完全相同（装備一覧 / 自動分解設定 / 確認），不含假名时
假名正则抓不到、翻译表里也没词条，于是原样漏到界面上，看起来像"繁体中文"。
2026-11 在 1.15.0 上整批漏了 148 条纯汉字日文，就是因为只按假名判定。

本工具做两件事：

  gen   —— 反推标记字表：扫描全部翻译表，取「日文原文出现 >= N 次 ∧ 简体译文从不使用」
           的汉字，剔除 GB2312 一级字库内的简繁同形字（知/白/北…，防中文误判），
           输出可直接粘进 UIDumpMod `Translation/JapaneseText.cs` 的字符串。

  lint  —— 全量漏网扫描：对每个译文值，报出三类残留
           ① 假名（ぁ-ん / ァ-ヶ）
           ② 标记字（纯汉字日文）
           ③ 繁形字（标记字出现但此处明显是译文）
           并按 --show 打印样例，末尾给总账。零残留才算翻译收口。

用法：
    python tools/jp_lint.py gen  --root <translations 目录>
    python tools/jp_lint.py lint --root <translations 目录> [--show 30]
    python tools/jp_lint.py lint --root <dir> --markers-file docs/markers.json

退出码：lint 有残留 -> 1（可挂 CI/收口门禁）
"""
import argparse
import collections
import json
import os
import re
import sys

KANA = re.compile(r"[\u3041-\u3096\u30A1-\u30FA]")
HAN = re.compile(r"[\u4E00-\u9FFF]")

# 设计性跳过的条目：已知永远不翻，不纳入漏网统计（否则门禁永远红）
SKIP_RULES = [
    ("m_tavern_cards", "name"),      # Lv1-100-ド 类占位卡名
    ("あいうえお", None),            # 预制体里的假文占位串
    ("じゅうにもじ", None),
    ("テキスト", None),
]


def is_design_skip(label, jp, zh):
    # 单个字符重复 5 次以上（いいいいいいいいいいい / ああああ 类预制体占位串）
    if len(zh) >= 5 and len(set(zh)) == 1:
        return True
    for tbl, field in SKIP_RULES:
        if field is None:
            if tbl in zh or tbl in jp:
                return True
        elif tbl in label and label.endswith(":" + field):
            return True
    return False


def _walk(label, node, out, depth=0):
    """递归展开 dict-of-dict，收集叶子层的 {日文: 译文}。
    ui_texts = {路径: {日文: 译文}}；static = {表: {字段: {日文: 译文}}}。"""
    if depth > 4 or not isinstance(node, dict):
        return
    for k, v in node.items():
        if isinstance(v, str):
            out.append((f"{label}{k}", v))
        elif isinstance(v, dict):
            # 叶子判断：所有 value 都是 str -> 这一层就是 {日文: 译文}
            if v and all(isinstance(x, str) for x in v.values()):
                for jp, zh in v.items():
                    yield f"{label}{k}", jp, zh
            else:
                yield from _walk(f"{label}{k}:", v, out, depth + 1)


def iter_pairs(root):
    """遍历 translations 树，产出 (来源标签, 日文 key, 译文 value)。"""
    for dirpath, _, files in os.walk(root):
        for f in files:
            if f != "zh_Hans.json":
                continue
            path = os.path.join(dirpath, f)
            rel = os.path.relpath(path, root).replace(os.sep, "/")
            # manifest/ 是 ID → md5 清单，不是译文
            if rel.startswith("manifest/") or "/manifest/" in rel:
                continue
            try:
                data = json.load(open(path, encoding="utf-8"))
            except Exception as e:
                print(f"[warn] 读取失败 {rel}: {e}", file=sys.stderr)
                continue
            if not isinstance(data, dict):
                continue
            sample = next(iter(data.values()), None)
            if isinstance(sample, dict):
                yield from _walk(f"{rel}:", data, [])
            elif isinstance(sample, str):
                for jp, zh in data.items():
                    yield rel, jp, zh


def gb2312_level1():
    """GB2312 一级汉字（常用简体字）：这些字简繁同形，不能当标记字。"""
    out = set()
    for hi in range(0xB0, 0xD8):
        for lo in range(0xA1, 0xFF):
            try:
                out.add(bytes([hi, lo]).decode("gb2312"))
            except Exception:
                pass
    return out


def cmd_gen(args):
    jp_count = collections.Counter()
    zh_chars = set()
    pairs = 0
    for _, jp, zh in iter_pairs(args.root):
        pairs += 1
        for c in jp:
            if HAN.fullmatch(c):
                jp_count[c] += 1
        zh_chars.update(c for c in zh if HAN.fullmatch(c))

    same = gb2312_level1()
    markers = sorted((c for c, n in jp_count.items()
                      if c not in zh_chars and n >= args.min_count and c not in same),
                     key=lambda c: -jp_count[c])
    dropped = sorted(c for c, n in jp_count.items()
                     if c not in zh_chars and n >= args.min_count and c in same)

    print(f"# 语料 {pairs} 条 / 日文汉字 {len(jp_count)} 个 / 简体译文用字 {len(zh_chars)} 个")
    print(f"# 标记字 {len(markers)} 个（剔除 GB2312 同形字 {len(dropped)}: {''.join(dropped)}）")
    print()
    print('            "' + "".join(markers) + '".ToCharArray();')
    if args.out:
        json.dump("".join(markers), open(args.out, "w", encoding="utf-8"), ensure_ascii=False)
        print(f"# 已写出 {args.out}", file=sys.stderr)
    return 0


def load_markers(args):
    if args.markers_file:
        raw = open(args.markers_file, encoding="utf-8").read()
        return set(json.loads(raw) if raw.lstrip().startswith('"') else raw.strip())
    # 默认：直接从源码里读，避免两份字表漂移
    src = args.source
    text = open(src, encoding="utf-8").read()
    m = re.search(r"JapaneseOnlyKanji\s*=\s*\"([^\"]+)\"", text)
    if not m:
        sys.exit(f"无法从 {src} 解析标记字表，请用 --markers-file 指定")
    return set(m.group(1))


def cmd_lint(args):
    markers = load_markers(args)
    kana_hits, marker_hits = [], []
    total = 0
    skipped = 0
    for label, jp, zh in iter_pairs(args.root):
        total += 1
        if is_design_skip(label, jp, zh):
            skipped += 1
            continue
        if KANA.search(zh):
            kana_hits.append((label, jp, zh))
        else:
            hit = sorted({c for c in zh if c in markers})
            if hit:
                marker_hits.append((label, jp, zh, "".join(hit)))

    print(f"扫描 {total} 条译文（标记字表 {len(markers)} 字；设计性跳过 {skipped} 条）")
    print(f"① 假名残留 {len(kana_hits)} 条")
    for label, jp, zh in kana_hits[:args.show]:
        print(f"   {label} | {jp[:24]} → {zh[:40]}")
    print(f"② 纯汉字日文（标记字）残留 {len(marker_hits)} 条")
    for label, jp, zh, hit in marker_hits[:args.show]:
        print(f"   [{hit}] {label} | {jp[:24]} → {zh[:40]}")
    if len(kana_hits) + len(marker_hits) > args.show:
        print("   …（--show 可调大）")
    print()
    print("结论：" + ("✅ 零残留" if not (kana_hits or marker_hits)
                    else f"❌ 需修 {len(kana_hits) + len(marker_hits)} 条"))
    return 0 if not (kana_hits or marker_hits) else 1


def main():
    ap = argparse.ArgumentParser(description="日文漏网扫描 / 标记字表生成")
    sub = ap.add_subparsers(dest="cmd", required=True)

    g = sub.add_parser("gen", help="反推标记字表")
    g.add_argument("--root", required=True, help="translations 目录")
    g.add_argument("--min-count", type=int, default=2, help="日文原文最少出现次数（默认 2）")
    g.add_argument("--out", help="把结果写成 JSON（可选）")
    g.set_defaults(func=cmd_gen)

    l = sub.add_parser("lint", help="全量漏网扫描")
    l.add_argument("--root", required=True, help="translations 目录")
    l.add_argument("--source", default="src/UIDumpMod/Translation/JapaneseText.cs",
                   help="从中读取标记字表的源码文件（默认仓库内路径）")
    l.add_argument("--markers-file", help="改用外部标记字表（JSON 字符串或纯文本）")
    l.add_argument("--show", type=int, default=20, help="每类最多打印条数")
    l.set_defaults(func=cmd_lint)

    args = ap.parse_args()
    sys.exit(args.func(args))


if __name__ == "__main__":
    main()
