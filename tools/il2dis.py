#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""libil2cpp.so 反汇编小工具（配 Il2CppDumper 的 dump.cs RVA 使用）

用法：
  python il2dis.py <libil2cpp.so> <RVA(hex)> [--len 0x400] [--calls]

--calls 会把 BL/BLR 目标按 dump.cs RVA 表反查成 "类名::方法名"。
"""
import sys, json, re, struct

from capstone import Cs, CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN
from capstone.arm64 import ARM64_OP_IMM, ARM64_OP_MEM


def elf_segments(buf):
    assert buf[:4] == b"\x7fELF", "not ELF"
    is64 = buf[4] == 2
    assert is64
    e_phoff = struct.unpack_from("<Q", buf, 0x20)[0]
    e_phentsize, e_phnum = struct.unpack_from("<HH", buf, 0x36)
    segs = []
    for i in range(e_phnum):
        off = e_phoff + i * e_phentsize
        p_type = struct.unpack_from("<I", buf, off)[0]
        if p_type != 1:  # PT_LOAD
            continue
        p_offset, p_vaddr = struct.unpack_from("<QQ", buf, off + 0x08)
        p_filesz = struct.unpack_from("<Q", buf, off + 0x20)[0]
        segs.append((p_vaddr, p_offset, p_filesz))
    return segs


def va_to_off(segs, va):
    for vaddr, off, sz in segs:
        if vaddr <= va < vaddr + sz:
            return off + (va - vaddr)
    return None


def load_names(dump_cs):
    """从 dump.cs 抽 RVA → 方法名（只用作 BL 目标注解）。"""
    table = {}
    cur = "?"
    rva_re = re.compile(r"// RVA: (0x[0-9A-Fa-f]+)")
    decl_re = re.compile(r"^\s*(?:public|private|protected|internal|static|virtual|sealed|override|abstract|new|readonly|async|\s)*[\w<>\[\],\.\? ]+\s(\w+)\s*\(")
    for line in open(dump_cs, encoding="utf-8", errors="ignore"):
        if line.startswith("// Namespace:") or re.match(r"^(public|internal|private) .*(class|struct|interface|enum) ", line):
            m = re.search(r"(?:class|struct|interface|enum)\s+([\w\.<>`]+)", line)
            if m:
                cur = m.group(1)
        m = rva_re.search(line)
        if m:
            name = line.strip()
            decl = decl_re.match(line)
            meth = decl.group(1) if decl else name[:40]
            table[int(m.group(1), 16)] = f"{cur}::{meth}"
    return table


def main():
    so, rva = sys.argv[1], int(sys.argv[2], 0)
    length = 0x400
    show_calls = "--calls" in sys.argv
    if "--len" in sys.argv:
        length = int(sys.argv[sys.argv.index("--len") + 1], 0)

    buf = open(so, "rb").read()
    segs = elf_segments(buf)
    off = va_to_off(segs, rva)
    if off is None:
        sys.exit(f"RVA {rva:#x} 不在任何 PT_LOAD 段内")

    names = {}
    if show_calls:
        try:
            names = load_names(r"G:/工具等其他文件存放处/Il2CppDumper/out-1150/dump.cs")
        except Exception as e:
            print(f"# 无法加载符号表: {e}", file=sys.stderr)

    md = Cs(CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN)
    md.detail = show_calls
    code = buf[off:off + length]
    for ins in md.disasm(code, rva):
        line = f"{ins.address:#012x}  {ins.mnemonic:<8} {ins.op_str}"
        if show_calls and ins.mnemonic in ("bl", "blr", "b") and ins.operands:
            op = ins.operands[0]
            if op.type == ARM64_OP_IMM:
                tgt = op.imm
                sym = names.get(tgt)
                line += f"    ; -> {sym}" if sym else f"    ; -> {tgt:#x}"
        print(line)


if __name__ == "__main__":
    main()
