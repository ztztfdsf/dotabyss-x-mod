#!/usr/bin/env python3
"""novel_pull.py — 版本更新 SOP 第⑤步：新剧情 bundle 拉取→解包→提取日文原文→（人工翻译）→合并校验。

用法（工作区根目录，如 G:/tmp/dotabyss-mod）:
  # 1) 从设备拉新旧两版 catalog，diff 出新增剧情 ID
  python repo/tools/novel_pull.py diff cat1146.bin cat1150.bin
  # 2) 从 CDN 拉取新增 bundle（资源号 aas/<N> 用 probe 子命令探测；1.14.6=12738, 1.15.0=13238）
  python repo/tools/novel_pull.py fetch cat1150.bin new1150/bundles --aas 13238 --ids evs_10200070101,hmn_10300100001
  # 3) 解包 TextAsset 为纯文本
  python repo/tools/novel_pull.py extract new1150/bundles new1150/txt
  # 4) 生成分块翻译工作台（chunks/ 日文 + zh/ 空译文，人工翻译 zh/c*.txt，格式 idx|说话人|译文）
  python repo/tools/novel_pull.py chunks new1150/txt new1150/chunks
  # 5) 翻译完成后合并成 novels/<id>/zh_Hans.json（假名残留/条目数校验）
  python repo/tools/novel_pull.py merge new1150 new1150/out_novels
  # 6) 合入权威翻译树（translations/novels/）
  python repo/tools/novel_pull.py install new1150/out_novels trans-out/novels

注意:
  - 分块解析必须用「长度前缀法」提取 ID（防 protobuf tag 拼接乌龙），本工具已内置。
  - 正文提取只取 CSV 第 3 字段: 日文正文不含 ASCII 逗号（用全角、），尾部是角色ID/voice/chara_N 字段。
  - 译文格式: idx|说话人|译文 或 idx||译文；idx 必须与 chunks 完全对齐，merge 会全量校验。
"""
import argparse, hashlib, json, os, re, struct, sys, urllib.request
from concurrent.futures import ThreadPoolExecutor

MSG_TAGS = ('message', 'messageTextUnder', 'l2dmessage')
KANA = re.compile('[\u3040-\u309F\u30A0-\u30FF]')
PROXY = {'https': 'http://127.0.0.1:7890'}
UA = {'User-Agent': 'UnityPlayer/6000.3.8f1 (UnityWebRequest/1.0, libcurl/8.10.1-DEV)'}
CDN = 'https://api.abyss-prod-r18.dotabyss.dmmgames.com/resources/android/r18/aas/%d/aa/%s'


def ids_from_catalog(path):
    """长度前缀法提取剧情 ID（0f 00 00 00 = 15 字节字符串），防 protobuf tag 拼接。"""
    d = open(path, 'rb').read()
    out = set()
    for m in re.finditer(rb'[a-z]{2,4}_[0-9]{6,14}', d):
        s = m.start()
        if s < 4:
            continue
        L = struct.unpack('<I', d[s-4:s])[0]
        if 6 <= L <= 40 and re.fullmatch(rb'[a-z]{2,4}_[0-9]+', d[s:s+L]):
            out.add(d[s:s+L].decode())
    return out


def bundle_names(path):
    d = open(path, 'rb').read()
    return set(m.group(1).decode() for m in re.finditer(rb'([A-Za-z0-9_\-]+_[0-9a-f]{32}\.bundle)', d))


def cmd_diff(a):
    old, new = ids_from_catalog(a.old), ids_from_catalog(a.new)
    added = sorted(new - old)
    print('old %d  new %d  added %d' % (len(old), len(new), len(added)))
    from collections import Counter
    print(dict(Counter(x.split('_')[0] for x in added)))
    for x in added:
        print(' ', x)
    return added


def cmd_probe(a):
    """探测新版本资源号: probe cat1150.bin --start 12739 --end 13600"""
    names = bundle_names(a.catalog)
    newest = sorted(names)[-1]
    op = urllib.request.build_opener(urllib.request.ProxyHandler(PROXY))

    def test(n):
        url = CDN % (n, newest)
        try:
            r = op.open(urllib.request.Request(url, headers={**UA, 'Range': 'bytes=0-7'}), timeout=15)
            return n, r.status
        except Exception as e:
            return n, getattr(e, 'code', 0)
    for n in range(a.start, a.end):
        _, s = test(n)
        if s in (200, 206):
            print('AAS number:', n)
            return n
    print('not found in range')


def cmd_fetch(a):
    d = open(a.catalog, 'rb').read()
    ids = set(a.ids.split(',')) if a.ids else None
    os.makedirs(a.outdir, exist_ok=True)
    targets = {}
    for i in ids or []:
        m = re.search(rb'([A-Za-z0-9_\-]*_' + i.encode() + rb'\.txt_[0-9a-f]{32}\.bundle)', d)
        if m:
            targets[i] = m.group(1).decode()
        else:
            print('MISSING bundle for', i)
    op = urllib.request.build_opener(urllib.request.ProxyHandler(PROXY))

    def get(kv):
        k, v = kv
        try:
            r = op.open(urllib.request.Request(CDN % (a.aas, v), headers=UA), timeout=60)
            data = r.read()
            open(os.path.join(a.outdir, k + '.bundle'), 'wb').write(data)
            return k, len(data), data[:7] == b'UnityFS'
        except Exception as e:
            return k, getattr(e, 'code', str(e)), False
    with ThreadPoolExecutor(6) as ex:
        res = list(ex.map(get, targets.items()))
    ok = [r for r in res if r[2]]
    print('fetched %d/%d' % (len(ok), len(res)), [r for r in res if not r[2]])


# ---- UnityFS/LZ4 最小解包（TextAsset 专用，无第三方依赖） ----
def lz4_block(src, size):
    dst = bytearray(); i = 0; n = len(src)
    while i < n:
        tok = src[i]; i += 1
        lit = tok >> 4
        if lit == 15:
            while True:
                b = src[i]; i += 1; lit += b
                if b != 255: break
        dst += src[i:i+lit]; i += lit
        if i >= n: break
        off = src[i] | (src[i+1] << 8); i += 2
        ml = tok & 15
        if ml == 15:
            while True:
                b = src[i]; i += 1; ml += b
                if b != 255: break
        ml += 4
        st = len(dst) - off
        for k in range(ml): dst.append(dst[st+k])
    return bytes(dst[:size])


def decomp(data, flags, usize):
    c = flags & 0x3F
    if c == 0: return data
    if c in (2, 3): return lz4_block(data, usize)
    raise ValueError('compression %d unsupported' % c)


def cstr(d, p):
    e = d.index(b'\0', p); return d[p:e].decode(), e + 1


def unityfs(path):
    d = open(path, 'rb').read()
    sig, p = cstr(d, 0)
    assert sig == 'UnityFS', sig
    ver = struct.unpack('>I', d[p:p+4])[0]; p += 4
    _, p = cstr(d, p); _, p = cstr(d, p)
    size, csz, usz, flags = struct.unpack('>QIII', d[p:p+20]); p += 20
    if ver >= 7: p = (p + 15) & ~15
    bi = d[len(d)-csz:] if flags & 0x80 else d[p:p+csz]; p += csz if not flags & 0x80 else 0
    bi = decomp(bi, flags, usz)
    if flags & 0x200: p = (p + 15) & ~15
    q = 16
    nb = struct.unpack('>I', bi[q:q+4])[0]; q += 4
    out = bytearray()
    for _ in range(nb):
        u, c, f = struct.unpack('>IIH', bi[q:q+10]); q += 10
        out += decomp(d[p:p+c], f, u); p += c
    return bytes(out)


def extract_text(raw, name):
    key = name.encode(); best = None; start = 0
    while True:
        i = raw.find(key, start)
        if i < 0: break
        start = i + 1
        if i < 4: continue
        L = struct.unpack('<I', raw[i-4:i])[0]
        if L != len(key): continue
        j = (i + L + 3) & ~3
        if j + 4 > len(raw): continue
        sl = struct.unpack('<I', raw[j:j+4])[0]
        if 0 < sl <= len(raw) - j - 4:
            cand = raw[j+4:j+4+sl]
            if best is None or len(cand) > len(best): best = cand
    return best


def cmd_extract(a):
    os.makedirs(a.outdir, exist_ok=True)
    ok = 0
    for f in sorted(os.listdir(a.indir)):
        if not f.endswith('.bundle'): continue
        name = f[:-7]
        raw = unityfs(os.path.join(a.indir, f))
        t = extract_text(raw, name + '.txt') or extract_text(raw, name)
        if t is None:
            print('FAIL', name); continue
        open(os.path.join(a.outdir, name + '.txt'), 'wb').write(t); ok += 1
    print('extracted', ok)


def parse_messages(txt_path):
    per = []
    for l in open(txt_path, encoding='utf-8-sig'):
        p = l.rstrip('\r\n').split(',')
        if p[0] in MSG_TAGS and len(p) > 2 and p[2]:
            per.append(p[2])  # 正文只取第3字段；日文正文无 ASCII 逗号
    return per


def cmd_chunks(a):
    os.makedirs(a.chunks, exist_ok=True)
    os.makedirs(os.path.join(a.chunks, '..', 'zh'), exist_ok=True)
    order = []; seen = set(); perfile = {}
    files = sorted(os.listdir(a.txt))
    for f in files:
        k = f[:-4]; perfile[k] = parse_messages(os.path.join(a.txt, f))
        for t in perfile[k]:
            if t not in seen: seen.add(t); order.append((k, t))
    N = 120
    for c in range(0, len(order), N):
        with open(os.path.join(a.chunks, 'c%02d.txt' % (c // N)), 'w', encoding='utf-8') as w:
            for i, (k, t) in enumerate(order[c:c+N]):
                w.write('%d|%s|%s\n' % (c + i, k, t.replace('\n', '\\n')))
    json.dump(perfile, open(os.path.join(a.chunks, '..', 'perfile.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    json.dump([t for _, t in order], open(os.path.join(a.chunks, '..', 'src_list.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    print('unique %d lines -> %d chunks' % (len(order), (len(order) + N - 1) // N))
    print('next: translate zh/c%02d.txt .. format: idx|说话人|译文' % 0)


def cmd_merge(a):
    root = a.workdir
    have = {}
    zhdir = os.path.join(root, 'zh')
    for f in sorted(os.listdir(zhdir)):
        for l in open(os.path.join(zhdir, f), encoding='utf-8'):
            l = l.rstrip('\n')
            if not l.strip(): continue
            p = l.split('|')
            idx = int(p[0]); text = p[-1]
            have[idx] = text
    src = json.load(open(os.path.join(root, 'src_list.json'), encoding='utf-8'))
    missing = [i for i in range(len(src)) if i not in have]
    assert not missing, '缺译文行: %s' % missing[:10]
    perfile = json.load(open(os.path.join(root, 'perfile.json'), encoding='utf-8'))
    order = []; seen = set()
    files = sorted(os.listdir(os.path.join(root, 'txt')))
    for f in files:
        for t in parse_messages(os.path.join(root, 'txt', f)):
            if t not in seen: seen.add(t); order.append(t)
    assert order == src, 'src_list 与 txt 不一致，重跑 chunks'
    byfile = {}
    for i, t in enumerate(order):
        for fid, texts in perfile.items():
            pass
    # 直接按文件重走一遍对齐
    byfile = {}
    gi = 0
    seen2 = set()
    for f in files:
        k = f[:-4]
        texts = parse_messages(os.path.join(root, 'txt', f))
        d = byfile.setdefault(k, {})
        for t in texts:
            if t not in seen2:
                seen2.add(t); d[t] = have[gi]; gi += 1
            else:
                d[t] = have[src.index(t)]
    assert gi == len(src)
    outdir = a.outdir
    n = 0
    for fid, d in byfile.items():
        os.makedirs(os.path.join(outdir, fid), exist_ok=True)
        json.dump(d, open(os.path.join(outdir, fid, 'zh_Hans.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        n += 1
    kana = [(fid, v[:30]) for fid, d in byfile.items() for v in d.values() if KANA.search(v) and '<user>' not in v]
    print('wrote %d files, lines %d, kana-residue %d' % (n, sum(len(d) for d in byfile.values()), len(kana)))
    if kana:
        print(kana[:5]); sys.exit(1)


def cmd_install(a):
    import shutil
    n = 0
    for d in os.listdir(a.srcdir):
        dst = os.path.join(a.dstdir, d)
        assert not os.path.exists(dst), '已存在: ' + dst
        shutil.copytree(os.path.join(a.srcdir, d), dst); n += 1
    print('installed %d novel dirs -> %s (total %d)' % (n, a.dstdir, len(os.listdir(a.dstdir))))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('diff'); s.add_argument('old'); s.add_argument('new'); s.set_defaults(fn=cmd_diff)
    s = sub.add_parser('probe'); s.add_argument('catalog'); s.add_argument('--start', type=int, default=12739); s.add_argument('--end', type=int, default=13600); s.set_defaults(fn=cmd_probe)
    s = sub.add_parser('fetch'); s.add_argument('catalog'); s.add_argument('outdir'); s.add_argument('--aas', type=int, required=True); s.add_argument('--ids'); s.set_defaults(fn=cmd_fetch)
    s = sub.add_parser('extract'); s.add_argument('indir'); s.add_argument('outdir'); s.set_defaults(fn=cmd_extract)
    s = sub.add_parser('chunks'); s.add_argument('txt'); s.add_argument('chunks'); s.set_defaults(fn=cmd_chunks)
    s = sub.add_parser('merge'); s.add_argument('workdir'); s.add_argument('outdir'); s.set_defaults(fn=cmd_merge)
    s = sub.add_parser('install'); s.add_argument('srcdir'); s.add_argument('dstdir'); s.set_defaults(fn=cmd_install)
    a = ap.parse_args()
    a.fn(a)


if __name__ == '__main__':
    main()
