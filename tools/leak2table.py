#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""leak2table.py — 把 UIDumpMod 的 [LEAK] 日志转成 ui_texts 翻译表条目。

维护循环(收到泄漏 → 补译 → 验证):
  1. 游玩时 UIDumpMod 把翻不出来的日文打进 Latest.log:
       [LEAK] TMPUGUI.OnEnable|Engine/View/.../Text|キャンセル
  2. 本脚本解析日志, 归一化路径(剥 (Clone)), 对每条 (path, text):
       - 已有译文/模板能命中的 → 标记 resolved, 不动
       - 未命中的 → 列出来, 并按内置术语表(RULES/GLOSSARY)自动补
       - 仍无法补的 → 输出到待办清单, 人工翻完写回 GLOSSARY 再跑一遍
  3. 结果合并进 ui_texts/zh_Hans.json (只在对应 path 下加 key, 不覆盖已有)

用法:
  python leak2table.py <leaklog...> --table <ui_texts/zh_Hans.json> [--names <names.json>]
  python leak2table.py --check <leaklog> --table <...>     # 只核对不写入

注意:
  - 多行文本必须以完整多行字符串作 key (日志里被 \\n 转义的要还原)。
  - 日文判定与 mod 内一致: 排除全角长音符ー与中点・。
"""
import argparse
import json
import re
import sys

LEAK_RE = re.compile(r'\[LEAK\] ([^|]+)\|([^|]+)\|(.+)$')
PLACEHOLDER = re.compile(r'\{(\d+)\}')

# 内置日文判定: 与 UIDumpMod.Core.Japanese 一致
def has_jp(s: str) -> bool:
    return bool(re.search(r'[\u3040-\u309F\u30A1-\u30FA\u30FD\u30FE\u31F0-\u31FF\uFF66-\uFF9D]', s))

# 数字/日期动态文本模板: 日文原文模板 → 中文模板
RULES = [
    (re.compile(r'^次のフロア（(\d+)フロア目）$'),          '次のフロア（{0}フロア目）',          '下一楼层（第{0}层）'),
    (re.compile(r'^(\d+)フロア目$'),                       '{0}フロア目',                       '第{0}层'),
    (re.compile(r'^あと(\d+)時間(\d+)分$'),                'あと{0}時間{1}分',                  '剩余{0}小时{1}分'),
    (re.compile(r'^あと(\d+)時間$'),                       'あと{0}時間',                       '剩余{0}小时'),
    (re.compile(r'^あと(\d+)日$'),                         'あと{0}日',                         '剩余{0}天'),
    (re.compile(r'^(\d{4}/\d{2}/\d{2} \d{2}:\d{2})まで$'), '{0}まで',                           '{0}截止'),
    (re.compile(r'^(\d{2}/\d{2} \d{2}:\d{2})まで$'),       '{0}まで',                           '{0}截止'),
    (re.compile(r'^(\d+:\d+)まで$'),                       '{0}まで',                           '{0}截止'),
    (re.compile(r'^ゲートキー<color=#4cf37b>(.*)</color>を使用して探索を続行しますか？$'),
     'ゲートキー<color=#4cf37b>{0}</color>を使用して探索を続行しますか？', '是否使用门之钥<color=#4cf37b>{0}</color>继续探索？'),
    (re.compile(r'^(インパクト|ラッシュ|セーフ|リスク)コード系統の恩恵ゲージが<color=#4cf37b>1上昇</color>しました。$'),
     '{0}コード系統の恩恵ゲージが<color=#4cf37b>1上昇</color>しました。', '__CODE_GAUGE__'),
    (re.compile(r'^(アビスコイン) ×(\d+)$'), '{0} ×{1}', '深渊币 ×{1}'),
]

# 静态术语表: 日文 → 中文 (界面高频短词, 持续积累)
GLOSSARY = {
    'ミッション': '任务', 'ミッションパス': '任务通行证', 'デイリー': '每日', 'ウィークリー': '每周',
    '一括受け取り': '一键领取', '受け取り': '领取', '受け取る': '领取', '受け取らない': '不领取',
    'キャンセル': '取消', 'スキップ': '跳过', 'レアスキップ': '稀有跳过', 'スキップ確認': '跳过确认',
    'スキップフロア確認': '跳过楼层确认', '確認して次へ': '确认并继续', '次へ': '继续',
    'オプション': '选项', 'ソート': '排序', 'レアリティ': '稀有度', 'キャラ一覧': '角色一览',
    'お気に入り登録': '收藏', '登録済み': '已登记', 'クエスト': '冒险', 'キャンペーン': '活动',
    'イベント': '活动', 'イベントは終了しました': '活动已结束', 'ショップ': '商店', 'ボス': '首领',
    'フロア': '楼层', 'アイテム': '道具', 'アイテム一覧': '道具一览', '獲得アイテム': '获得道具',
    'アビスコード': '深渊代码', 'アビスコード図鑑': '深渊代码图鉴', 'アビスコード所持数': '深渊代码持有数',
    'ゲートキー': '门之钥', 'ゲートキーの消費': '门之钥消耗', 'ゲートキー獲得まで': '至获得门之钥',
    'アビスジェム': '深渊宝石', 'プレイヤー経験値': '玩家经验值', 'アビスコイン': '深渊币',
    '獲得アビスコイン：': '获得深渊币：', '獲得ナゾの鍵：': '获得谜之钥匙：', 'ナゾの鍵': '谜之钥匙',
    '深淵ミッション': '深渊任务', '連続踏破フロア': '连续踏破楼层', '最大到達フロア': '最大到达楼层',
    'パーティ状況': '队伍状况', '現在の探索中パーティー': '当前探索中的队伍',
    '中断する': '中断', '帰還する': '返回', '続行する': '继续', '再開する': '重新开始', '行わない': '不进行',
    '現在地へ': '前往当前位置', 'リロール': '重掷', '残り': '剩余', 'MAXまで': '至MAX',
    'ドロップ無し。': '无掉落。', 'リザルト': '结算', '探索ログ': '探索日志', '勢力クエスト': '势力关卡',
    '厄災の迎撃': '灾厄迎击', '凱旋コイン': '凯旋币', '依頼の更新まで': '至委托刷新',
    '現在依頼はありません': '当前没有委托', 'メインストーリークエスト': '主线剧情关卡',
    'クエスト出現まで': '至关卡出现', 'クエスト出現中!': '关卡出现中！', 'クエスト発生率': '关卡触发率',
    '報酬MAXまで': '至报酬MAX', '会心ダメ': '暴击伤害', '贈る': '赠送', 'ニュース': '新闻', 'プレゼント': '礼物',
    'インパクトコード': '冲击代码', 'ラッシュコード': '突进代码', 'セーフコード': '安全代码',
    'リスクコード': '风险代码', 'バフ戦力': '增益战力', 'フォースチェイン：': '连锁技：',
    '付与ステータス一覧': '已赋予状态一览', '付与されている効果はありません。': '当前没有已赋予的效果。',
    'エリアマップ': '区域地图', 'セール中': '促销中', 'エルドラーナ': '艾尔德拉娜',
    'インパクト': '冲击', 'ラッシュ': '突进', 'セーフ': '安全', 'リスク': '风险',
    'ゲートキー<color=#4cf37b></color>を使用して探索を続行しますか？': '是否使用门之钥继续探索？',  # 过渡态: 计数填充前的空占位
    'このゲームでは音声が再生されます。': '本游戏会播放语音。',
    '本作品はフィクションです。<br>実在の人物、団体、事件などには一切関係ありません。':
        '本作品纯属虚构，<br>与现实中的人物、团体、事件等毫无关联。',
    '以下のアイテムを受け取りました。': '已收到以下道具。',
    'アイテムを消費してアビリティを解放': '消耗道具解锁能力',
    '拠点に送るアイテムを選択してください': '请选择要送往据点的道具',
    '拠点に送ったアイテムは、探索に失敗しても獲得できます。': '送往据点的道具即使探索失败也可获得。',
    'チケットを消費して10フロア分の探索を': '消耗跳过券，跳过10层的探索',
    '10フロア分の探索をスキップすることができます。': '可跳过10层的探索。',
    'スキップ終了後、スキップしたフロアに応じた報酬とアビスコードを獲得出来ます。':
        '跳过结束后，将根据跳过的层数获得相应的报酬与深渊代码。',
    'クリアしたことのあるフロアのみ探索をスキップすることが出来ます': '仅可跳过曾经通关过的楼层',
    'スキップする場合、ゲートキーの消費倍率は1に固定されます。': '跳跃时，门之钥的消耗倍率固定为1。',
    '複数のゲートキーを消費して、スコアとドロップに倍率をかけることができます。':
        '可消耗多个门之钥，为得分和掉落附加倍率。',
    '10フロア分のドロップ数': '10层的掉落数', '10フロア分のスコア計算': '10层的得分计算',
    '厄災を討伐することで厄災Lvが上昇します。': '讨伐灾厄可提升灾厄Lv。',
    'YYYY/MM/DD hh:ssまで': 'YYYY/MM/DD hh:ss截止',
}

# 编辑器占位假文本: 真数据到达前的 dummy, 不值得翻译
DUMMY = ['あいうえお', 'アイテム名１２３４', 'マナクリスタル名', 'ボススキル名', 'ステージ名１５文字',
         '二つ名二つ名', '名前は最大表示', 'アビリティ名は最大', 'クエスト名クエスト名',
         'アビスコード名', '一二三四五六七八九', 'テキストテキスト', 'ダミー']


def norm_path(p: str) -> str:
    """剥掉每段路径的 (Clone) 后缀 — 与 UIDumpMod.Core.Normalize 一致。"""
    return '/'.join(s[:-7] if s.endswith('(Clone)') else s for s in p.lstrip('/').split('/'))


def build_pattern(template: str) -> re.Pattern:
    out, seen, last = ['^'], set(), 0
    for m in PLACEHOLDER.finditer(template):
        out.append(re.escape(template[last:m.start()]))
        g = 'p' + m.group(1)
        out.append(f'(?P<{g}>.+?)' if g not in seen else f'(?P={g})')
        seen.add(g)
        last = m.end()
    out.append(re.escape(template[last:]))
    out.append('$')
    return re.compile(''.join(out))


def resolve(table: dict, path: str, text: str):
    """在表里按 mod 同样规则找译文: 后缀路径 + 精确 key + 模板。"""
    segs = norm_path(path).split('/')
    for i in range(len(segs)):
        cand = '/'.join(segs[i:])
        d = table.get(cand)
        if not d:
            continue
        if text in d:
            return d[text]
        if text.strip() in d:
            return d[text.strip()]
        for jp, zh in d.items():
            if PLACEHOLDER.search(jp) and build_pattern(jp).match(text):
                return zh
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('logs', nargs='*', help='Latest.log 或导出的 [LEAK] 行文件')
    ap.add_argument('--table', required=True, help='ui_texts/zh_Hans.json')
    ap.add_argument('--names', help='names/zh_Hans.json (人名回退)')
    ap.add_argument('--check', action='store_true', help='只核对不写入')
    args = ap.parse_args()

    table = json.load(open(args.table, encoding='utf-8'))
    names = json.load(open(args.names, encoding='utf-8')) if args.names else {}

    pairs = set()
    for fn in args.logs:
        for line in open(fn, encoding='utf-8', errors='replace'):
            m = LEAK_RE.search(line.rstrip('\n'))
            if m:
                pairs.add((norm_path(m.group(2)), m.group(3).replace('\\n', '\n')))

    added, todo = 0, []
    for path, text in sorted(pairs):
        if any(d in text for d in DUMMY) or not has_jp(text):
            continue
        if resolve(table, path, text) is not None:
            continue  # 已可解析(可能上一轮已入表)
        val = GLOSSARY.get(text)
        key = text
        if val is None:
            for rx, tk, tv in RULES:
                m = rx.match(text)
                if m:
                    key, val = tk, tv.format(*m.groups())
                    break
        if val == '__CODE_GAUGE__':
            m = re.match(r'^(インパクト|ラッシュ|セーフ|リスク)コード系統', text)
            key = '{0}コード系統の恩恵ゲージが<color=#4cf37b>1上昇</color>しました。'.format(m.group(1))
            val = GLOSSARY.get(m.group(1), m.group(1)) + '代码系恩惠槽<color=#4cf37b>上升1</color>。'
        if val is None:
            val = names.get(text)
        if val is None:
            todo.append((path, text))
            continue
        table.setdefault(path, {})[key] = val
        added += 1

    if not args.check:
        json.dump(table, open(args.table, 'w', encoding='utf-8'), ensure_ascii=False)
    print(f'leak pairs: {len(pairs)}  auto-added: {added}  todo: {len(todo)}  total paths: {len(table)}')
    for p, t in todo:
        print(f'  TODO {p[-55:]} | {t!r}')


if __name__ == '__main__':
    sys.exit(main())
