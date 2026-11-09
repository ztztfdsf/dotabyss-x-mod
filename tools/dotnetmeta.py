"""极简 ECMA-335 元数据读取器：从 il2cpp interop 程序集里还原 类型 → 字段/方法 映射。

只实现需要的表：Module / TypeRef / TypeDef / FieldPtr / Field / MethodPtr / MethodDef。
Il2CppInterop 生成的 DLL 是标准 PE + CLI metadata，字段名与方法名都在 #Strings 堆里。

用法:
    python dotnetmeta.py <dll> list                      # 列出所有类型
    python dotnetmeta.py <dll> find <正则>                # 类型名匹配则打印其字段/方法
"""
import re
import struct
import sys

sys.stdout.reconfigure(encoding="utf-8")


class Meta:
    def __init__(self, path):
        self.buf = open(path, "rb").read()
        self._pe()

    # ---------- PE ----------
    def _pe(self):
        b = self.buf
        e_lfanew = struct.unpack_from("<I", b, 0x3C)[0]
        assert b[e_lfanew:e_lfanew + 4] == b"PE\0\0"
        coff = e_lfanew + 4
        nsec = struct.unpack_from("<H", b, coff + 2)[0]
        opt_size = struct.unpack_from("<H", b, coff + 16)[0]
        opt = coff + 20
        magic = struct.unpack_from("<H", b, opt)[0]
        dd = opt + (96 if magic == 0x10B else 112)
        self.sections = []
        sec = opt + opt_size
        for i in range(nsec):
            o = sec + i * 40
            name = b[o:o + 8].rstrip(b"\0").decode("ascii", "ignore")
            vsize, vaddr, rawsize, rawptr = struct.unpack_from("<IIII", b, o + 8)
            self.sections.append((vaddr, vsize, rawptr, rawsize, name))
        # DataDirectory[14] = CLI header
        cli_rva, cli_size = struct.unpack_from("<II", b, dd + 14 * 8)
        cli = self.rva2off(cli_rva)
        md_rva, md_size = struct.unpack_from("<II", b, cli + 8)
        self._metadata(self.rva2off(md_rva))

    def rva2off(self, rva):
        for vaddr, vsize, rawptr, rawsize, _ in self.sections:
            if vaddr <= rva < vaddr + max(vsize, rawsize):
                return rawptr + (rva - vaddr)
        raise ValueError(f"rva {rva:#x} 不在任何节内")

    # ---------- metadata root ----------
    def _metadata(self, off):
        b = self.buf
        assert struct.unpack_from("<I", b, off)[0] == 0x424A5342  # BSJB
        ver_len = struct.unpack_from("<I", b, off + 12)[0]
        p = off + 16 + ver_len
        p += 2  # flags
        nstreams = struct.unpack_from("<H", b, p)[0]
        p += 2
        self.streams = {}
        for _ in range(nstreams):
            s_off, s_size = struct.unpack_from("<II", b, p)
            p += 8
            end = b.index(b"\0", p)
            name = b[p:end].decode("ascii", "ignore")
            p = (end + 4) & ~3
            self.streams[name] = (off + s_off, s_size)
        self._tables()

    # ---------- #~ ----------
    def _tables(self):
        b = self.buf
        off, _ = self.streams["#~"]
        self.str_base, _ = self.streams["#Strings"]
        heap_sizes = b[off + 6]
        self.str_sz = 4 if heap_sizes & 1 else 2
        self.guid_sz = 4 if heap_sizes & 2 else 2
        self.blob_sz = 4 if heap_sizes & 4 else 2
        valid = struct.unpack_from("<Q", b, off + 8)[0]
        p = off + 24
        self.rows = {}
        for i in range(64):
            if valid >> i & 1:
                self.rows[i] = struct.unpack_from("<I", b, p)[0]
                p += 4
        self.table_off = {}
        cur = p
        for i in range(64):
            if i in self.rows:
                self.table_off[i] = cur
                if i > 0x06:      # 只实现到 MethodDef，后面的表不用定位
                    continue
                cur += self.rows[i] * self._row_size(i)

    # ---------- 索引宽度 ----------
    def _idx(self, table):
        return 4 if self.rows.get(table, 0) > 0xFFFF else 2

    def _coded(self, tables, bits):
        mx = max(self.rows.get(t, 0) for t in tables)
        return 4 if mx >= (1 << (16 - bits)) else 2

    def _row_size(self, t):
        s, g, bl = self.str_sz, self.guid_sz, self.blob_sz
        if t == 0x00:  # Module
            return 2 + s + g + g + g
        if t == 0x01:  # TypeRef
            return self._coded([0x00, 0x1A, 0x23, 0x01], 2) + s + s
        if t == 0x02:  # TypeDef
            return 4 + s + s + self._coded([0x02, 0x01, 0x1B], 2) + self._idx(0x04) + self._idx(0x06)
        if t == 0x03:  # FieldPtr
            return self._idx(0x04)
        if t == 0x04:  # Field
            return 2 + s + bl
        if t == 0x05:  # MethodPtr
            return self._idx(0x06)
        if t == 0x06:  # MethodDef
            return 4 + 2 + 2 + s + bl + self._idx(0x08)
        raise NotImplementedError(f"表 0x{t:02x} 未实现")

    # ---------- 读取 ----------
    def string(self, idx):
        p = self.str_base + idx
        end = self.buf.index(b"\0", p)
        return self.buf[p:end].decode("utf-8", "ignore")

    def _read_row(self, t, n, cols):
        """cols: [(name, kind)] kind = 'u1'|'u2'|'u4'|'str'|'blob'|'idx:<t>'|'coded:<t1,t2,...>:<bits>'"""
        row_off = self.table_off[t] + n * self._row_size(t)
        out = {}
        p = row_off
        for name, kind in cols:
            if kind.startswith("idx:"):
                sz = self._idx(int(kind[4:], 16))
                out[name] = struct.unpack_from("<H" if sz == 2 else "<I", self.buf, p)[0]
                p += sz
            elif kind.startswith("coded:"):
                body = kind[6:]
                tbls, bits = body.rsplit(":", 1)
                tl = [int(x, 16) for x in tbls.split(",")]
                sz = self._coded(tl, int(bits))
                out[name] = struct.unpack_from("<H" if sz == 2 else "<I", self.buf, p)[0]
                p += sz
            elif kind == "str":
                out[name] = self.string(struct.unpack_from("<H" if self.str_sz == 2 else "<I",
                                                           self.buf, p)[0])
                p += self.str_sz
            elif kind in ("blob", "guid"):
                p += self.blob_sz if kind == "blob" else self.guid_sz
            else:
                sz = int(kind[1:])
                out[name] = struct.unpack_from("<H" if sz == 2 else "<I", self.buf, p)[0]
                p += sz
        return out

    def types(self):
        """返回 [(fullname, fields[], methods[])]"""
        td_rows = self.rows.get(0x02, 0)
        fd_rows = self.rows.get(0x04, 0)
        md_rows = self.rows.get(0x06, 0)
        tcols = [("Flags", "u4"), ("Name", "str"), ("Namespace", "str"),
                 ("Extends", "coded:02,01,1B:2"),
                 ("FieldList", "idx:04"), ("MethodList", "idx:06")]
        rows = [self._read_row(0x02, i, tcols) for i in range(td_rows)]
        fcols = [("Flags", "u2"), ("Name", "str"), ("Sig", "blob")]
        mcols = [("Rva", "u4"), ("ImplFlags", "u2"), ("Flags", "u2"),
                 ("Name", "str"), ("Sig", "blob"), ("ParamList", "idx:08")]
        out = []
        for i, r in enumerate(rows):
            f_start = r["FieldList"]
            f_end = rows[i + 1]["FieldList"] if i + 1 < len(rows) else fd_rows + 1
            m_start = r["MethodList"]
            m_end = rows[i + 1]["MethodList"] if i + 1 < len(rows) else md_rows + 1
            fields = [self._read_row(0x04, j - 1, fcols)["Name"] for j in range(f_start, f_end)]
            methods = [self._read_row(0x06, j - 1, mcols)["Name"] for j in range(m_start, m_end)]
            ns = r["Namespace"]
            full = f"{ns}.{r['Name']}" if ns else r["Name"]
            out.append((full, fields, methods))
        return out


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    m = Meta(sys.argv[1])
    cmd = sys.argv[2]
    ts = m.types()
    if cmd == "list":
        print(f"{len(ts)} 个 TypeDef")
        for full, f, me in ts:
            print(f"{full}  ({len(f)} 字段 / {len(me)} 方法)")
    elif cmd == "find":
        pat = re.compile(sys.argv[3], re.I)
        n = 0
        for full, f, me in ts:
            if pat.search(full):
                n += 1
                print(f"\n### {full}")
                if f:
                    print("  字段: " + ", ".join(f))
                if me:
                    print("  方法: " + ", ".join(me[:40]) + (" …" if len(me) > 40 else ""))
        print(f"\n匹配 {n} 个类型")
    return 0


if __name__ == "__main__":
    sys.exit(main())
