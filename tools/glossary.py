#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
专有名词对照表（glossary）构建 / 校验 / 统一工具
================================================

目的：保证同一个角色/地名在所有剧情、UI、MasterData 里译名一致，不出现
「同一个人在两话里叫不同名字」。

工作方式：
  1. 语料 = translations/novels/*/zh_Hans.json（日文原文为 key，译文为 value）
           + translations/names/zh_Hans.json（mod 自带的专有名词表）
  2. 候选专名 = names 表里的条目 + 从原文中自动抽取的高频片假名串
  3. 对每个候选专名，统计「含有该专名的原文行」里，译文出现了哪些中文候选串，
     按覆盖率判定主译名（canonical）与少数派变体（variant）
  4. --fix 把少数派变体统一替换成主译名（只在该专名出现的那些行里替换，避免误伤）
  5. 输出 glossary.json（机器用）+ GLOSSARY.md（人看）

用法：
    python tools/glossary.py --root <translations 目录>            # 只生成报告
    python tools/glossary.py --root <translations 目录> --fix      # 报告并统一
"""
import argparse
import collections
import json
import os
import re
import sys

KATAKANA = re.compile(r'[\u30a1-\u30f6\u30fc]{2,}')
# 中日文常用标点/标记，排除在候选串之外
NOISE = set('、。，．「」『』（）()！？!?…‥・：；\n\r\t 　<>=[]/\\"\'')


def load_json(path):
    with open(path, encoding='utf-8') as fp:
        return json.load(fp)


SKIP = ()
AUTO_KATAKANA = False

# 人工确认的译名（优先级最高）：地名/称号等 names 表没有精确条目、但正文里大量出现的专名。
# 依据：names 表里同名衍生条目（如 ミレスガルド兵Ａ→米尔斯加德士兵Ａ）+ 正文出现频次。
OVERRIDES = {
    'ミレスガルド': '米雷斯加德',
    'ペルディオン': '佩尔迪翁',
    'ホウライ': '蓬莱',
}


def iter_novels(root):
    """遍历所有剧情文件，返回 (文件名, 路径)。文件名以 SKIP 中任一前缀开头则跳过。"""
    base = os.path.join(root, 'novels')
    for name in sorted(os.listdir(base)):
        if SKIP and name.startswith(SKIP):
            continue
        p = os.path.join(base, name, 'zh_Hans.json')
        if os.path.isfile(p):
            yield name, p


TAG_NOISE = {'br', 'size', 'color', 'b', 'i', 'u', 'user'}


def chinese_candidates(zh, min_len=2, max_len=8):
    """从一句中文译文里切出候选专名串（连续中文，或长度>=3 的拉丁词，排除标签碎片）。"""
    out = set()
    for m in re.finditer(r'[\u4e00-\u9fff]+', zh):
        s = m.group()
        for ln in range(min_len, max_len + 1):
            for i in range(len(s) - ln + 1):
                cand = s[i:i + ln]
                if cand not in NOISE:
                    out.add(cand)
    for m in re.finditer(r'[A-Za-z]{3,}', zh):
        w = m.group()
        if w.lower() not in TAG_NOISE:
            out.add(w)
    return out


def drop_fragments(variants, tol=0.90):
    """去掉被别人包含的碎片候选（如 贝丽/丽莎 之于 贝丽莎）。"""
    items = sorted(variants.items(), key=lambda x: (-len(x[0]), -x[1]))
    kept = []
    for cand, n in items:
        if any(cand != k and cand in k and cnt >= n * tol for k, cnt in kept):
            continue
        kept.append((cand, n))
    kept.sort(key=lambda x: (-x[1], -len(x[0])))
    return kept


def build_stats(root, min_occur=3, support_ratio=0.30):
    """统计每个日文专名在译文里对应的中文候选串。"""
    names = {}
    names_path = os.path.join(root, 'names', 'zh_Hans.json')
    if os.path.isfile(names_path):
        names = load_json(names_path)

    # 候选日文专名：names 表条目 + 高频片假名串
    katakana_count = collections.Counter()
    for _name, path in iter_novels(root):
        for jp in load_json(path):
            for m in KATAKANA.findall(jp):
                katakana_count[m] += 1

    candidates = set(k for k in names if not k.startswith('<'))
    candidates |= set(OVERRIDES)
    if AUTO_KATAKANA:
        for k, c in katakana_count.items():
            if c >= 20 and len(k) >= 3:
                candidates.add(k)

    stats = {}
    for jp_name in sorted(candidates, key=len, reverse=True):
        occ = 0
        variants = collections.Counter()
        for _name, path in iter_novels(root):
            data = load_json(path)
            for jp, zh in data.items():
                if jp_name not in jp or not isinstance(zh, str):
                    continue
                occ += 1
                for cand in chinese_candidates(zh):
                    variants[cand] += 1
        if occ < min_occur:
            continue
        # 去掉碎片候选，只保留覆盖率够高的写法（同一个人被译成几种写法时会同时出现）
        strong_all = drop_fragments(dict(variants))
        floor = max(2, int(occ * support_ratio))
        strong = [(c, n) for c, n in strong_all if n >= floor]
        if not strong:
            continue
        stats[jp_name] = {'occurrences': occ, 'variants': strong}

    return names, stats


def pick_canonical(jp_name, entry, names):
    """选主译名：names 表（游戏名片/名簿显示用的就是它）优先，其次语料多数。

    names 表是 AbyssMod 的 TranslateName 精确查表结果，决定角色名在界面上的显示，
    所以剧情正文必须与它保持一致，否则同一个角色会出现两种译名。
    """
    variants = entry['variants']
    if jp_name in OVERRIDES:
        return OVERRIDES[jp_name], '人工指定'
    table_val = names.get(jp_name)
    if isinstance(table_val, str) and table_val and not re.search(r'[・&＆]', table_val):
        return table_val, 'names表'
    return variants[0][0], '语料多数'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', required=True, help='translations 目录（含 novels/ names/）')
    ap.add_argument('--fix', action='store_true', help='把少数派变体统一为主译名')
    ap.add_argument('--min-occur', type=int, default=3)
    ap.add_argument('--support', type=float, default=0.30)
    ap.add_argument('--report', default=None, help='glossary.json / GLOSSARY.md 输出目录')
    ap.add_argument('--skip', default='', help='统计时跳过的文件名前缀，逗号分隔（如人工重译的剧情）')
    ap.add_argument('--auto-katakana', action='store_true', help='额外把高频片假名串当候选（会混入普通名词）')
    args = ap.parse_args()

    global SKIP, AUTO_KATAKANA
    SKIP = tuple(x for x in args.skip.split(',') if x)
    AUTO_KATAKANA = bool(args.auto_katakana)

    names, stats = build_stats(args.root, args.min_occur, args.support)

    canonical = {}
    inconsistent = []
    for jp_name, entry in stats.items():
        can, source = pick_canonical(jp_name, entry, names)
        canonical[jp_name] = {
            'zh': can,
            'source': source,
            'occurrences': entry['occurrences'],
            'variants': [{'zh': c, 'n': n, 'canonical': c == can}
                         for c, n in entry['variants']],
        }
        others = [(c, n) for c, n in entry['variants'] if c != can]
        if others:
            inconsistent.append((jp_name, can, others, entry['occurrences']))

    print(f'专名条目: {len(canonical)}   存在多写法: {len(inconsistent)}')
    for jp_name, can, others, occ in sorted(inconsistent, key=lambda x: -sum(n for _, n in x[2]))[:40]:
        print(f'  {jp_name} → {can}（共{occ}行）; 其他写法: '
              + ', '.join(f'{c}×{n}' for c, n in others[:4]))

    if args.report:
        os.makedirs(args.report, exist_ok=True)
        with open(os.path.join(args.report, 'glossary.json'), 'w', encoding='utf-8') as fp:
            json.dump(canonical, fp, ensure_ascii=False, indent=2, sort_keys=True)
        print('glossary.json 已写入', args.report)

    if not args.fix:
        return 0

    # 统一：只在含有该日文专名的行里，把少数派写法替换成主译名
    replaced = 0
    touched_files = 0
    for name, path in iter_novels(args.root):
        data = load_json(path)
        changed = False
        for jp, zh in data.items():
            if not isinstance(zh, str):
                continue
            new = zh
            for jp_name, info in canonical.items():
                if jp_name not in jp:
                    continue
                for var in info['variants']:
                    if var['canonical'] or var['zh'] == info['zh']:
                        continue
                    if var['zh'] in new:
                        new = new.replace(var['zh'], info['zh'])
                        replaced += 1
            if new != zh:
                data[jp] = new
                changed = True
        if changed:
            with open(path, 'w', encoding='utf-8') as fp:
                json.dump(data, fp, ensure_ascii=False, indent=4)
            touched_files += 1
    print(f'统一替换: {replaced} 处，涉及 {touched_files} 个文件')
    return 0


if __name__ == '__main__':
    sys.exit(main())
