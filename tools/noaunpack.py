"""极简 MessagePack 解码器（纯标准库）+ MasterData 结构探针。

NoaMessagePack 把每张表存成 {表名: [行, 行, ...]}，每行是 {字段id: 值, ...}。
字段id 是数字，字段名要靠值形态推测（字符串=名字类字段）。
"""
import struct
import sys

sys.stdout.reconfigure(encoding="utf-8")


class Reader:
    __slots__ = ("b", "p")

    def __init__(self, b):
        self.b = b
        self.p = 0

    def u8(self):
        v = self.b[self.p]
        self.p += 1
        return v

    def take(self, n):
        v = self.b[self.p:self.p + n]
        self.p += n
        return v

    def unpack(self, fmt, n):
        v = struct.unpack_from(fmt, self.b, self.p)[0]
        self.p += n
        return v

    def read(self):
        c = self.u8()
        if c <= 0x7F:
            return c
        if c >= 0xE0:
            return c - 0x100
        if 0x80 <= c <= 0x8F:
            return {self.read(): self.read() for _ in range(c & 0x0F)}
        if 0x90 <= c <= 0x9F:
            return [self.read() for _ in range(c & 0x0F)]
        if 0xA0 <= c <= 0xBF:
            return self.take(c & 0x1F).decode("utf-8", "ignore")
        if c == 0xC0:
            return None
        if c == 0xC2:
            return False
        if c == 0xC3:
            return True
        if c == 0xC4:
            return self.take(self.u8())
        if c == 0xC5:
            return self.take(self.unpack(">H", 2))
        if c == 0xC6:
            return self.take(self.unpack(">I", 4))
        if c == 0xCA:
            return self.unpack(">f", 4)
        if c == 0xCB:
            return self.unpack(">d", 8)
        if c == 0xCC:
            return self.u8()
        if c == 0xCD:
            return self.unpack(">H", 2)
        if c == 0xCE:
            return self.unpack(">I", 4)
        if c == 0xCF:
            return self.unpack(">Q", 8)
        if c == 0xD0:
            return self.unpack(">b", 1)
        if c == 0xD1:
            return self.unpack(">h", 2)
        if c == 0xD2:
            return self.unpack(">i", 4)
        if c == 0xD3:
            return self.unpack(">q", 8)
        if c == 0xD9:
            return self.take(self.u8()).decode("utf-8", "ignore")
        if c == 0xDA:
            return self.take(self.unpack(">H", 2)).decode("utf-8", "ignore")
        if c == 0xDB:
            return self.take(self.unpack(">I", 4)).decode("utf-8", "ignore")
        if c == 0xDC:
            return [self.read() for _ in range(self.unpack(">H", 2))]
        if c == 0xDD:
            return [self.read() for _ in range(self.unpack(">I", 4))]
        if c == 0xDE:
            n = self.unpack(">H", 2)
            return {self.read(): self.read() for _ in range(n)}
        if c == 0xDF:
            n = self.unpack(">I", 4)
            return {self.read(): self.read() for _ in range(n)}
        raise ValueError(f"未支持的 msgpack 类型 {c:#x} @ {self.p-1:#x}")


def load(path):
    data = open(path, "rb").read()
    return Reader(data).read()


if __name__ == "__main__":
    md = load(sys.argv[1] if len(sys.argv) > 1 else r"G:\tmp\dotabyss-mod\_md\masterdata.dat")
    print("顶层类型", type(md).__name__, "表数", len(md))
    names = sorted(md)
    print("前 20 张表:", names[:20])
    for t in ("m_abilities", "m_drops", "m_drop_groups", "m_drop_lottery_steps", "m_items"):
        if t not in md:
            print(f"\n{t}: 不存在")
            continue
        rows = md[t]
        print(f"\n### {t}  行数 {len(rows)}")
        for r in rows[:2]:
            print("   ", r)
