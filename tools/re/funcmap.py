"""Find the functions that reference a string and summarize them (called imports, strings, immediates).

Works on any x64 PE without building a full xref cache: string references are found with a
vectorized scan for RIP-relative disp32 values, and function bounds come from .pdata.

usage: funcmap.py <exe-or-dll> <regex> [--max N] [--dis]
  regex  Python regex matched against ASCII strings of the image (e.g. "^OSC - ")
  --dis  also print the full disassembly of each function
"""
import bisect
import re
import struct
import sys

import numpy as np
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

path, pattern = sys.argv[1], sys.argv[2]
max_hits = int(sys.argv[sys.argv.index('--max') + 1]) if '--max' in sys.argv else 1000
want_dis = '--dis' in sys.argv

b = open(path, 'rb').read()
e = struct.unpack_from('<I', b, 0x3c)[0]
nsec = struct.unpack_from('<H', b, e + 6)[0]
optsz = struct.unpack_from('<H', b, e + 20)[0]
dd = e + 24 + 112
secs = []
for i in range(nsec):
    o = e + 24 + optsz + i * 40
    name = b[o:o + 8].rstrip(b'\0').decode()
    vs, va, rs, rp = struct.unpack_from('<IIII', b, o + 8)
    secs.append((name, vs, va, rs, rp))


def r2f(rva):
    for _, vs, va, rs, rp in secs:
        if va <= rva < va + max(vs, rs):
            return rva - va + rp


def f2r(off):
    for _, vs, va, rs, rp in secs:
        if rp <= off < rp + rs:
            return off - rp + va


def cstr(rva, limit=200):
    f = r2f(rva)
    if f is None:
        return None
    s = b[f:f + limit].split(b'\0')[0]
    if len(s) >= 3 and all(32 <= c < 127 or c in (9, 10) for c in s):
        return s.decode('latin1')
    return None


# imports: IAT slot rva -> "dll!name"
iat = {}
irva = struct.unpack_from('<I', b, dd + 8)[0]
o = r2f(irva)
while True:
    oft, _, _, nm, ft = struct.unpack_from('<IIIII', b, o)
    if not nm:
        break
    dll = cstr(nm)
    t = r2f(oft or ft)
    slot = ft
    while True:
        v = struct.unpack_from('<Q', b, t)[0]
        if not v:
            break
        iat[slot] = f"{dll}!#{v & 0xffff}" if v >> 63 else f"{dll}!{cstr((v & 0x7fffffff) + 2, 400)}"
        t += 8
        slot += 8
    o += 20

# function table from .pdata
prva, psz = struct.unpack_from('<II', b, dd + 3 * 8)
funcs = []
po = r2f(prva)
for i in range(psz // 12):
    beg, end, _ = struct.unpack_from('<III', b, po + i * 12)
    funcs.append((beg, end))
funcs.sort()
starts = [f[0] for f in funcs]


def func_of(rva):
    i = bisect.bisect_right(starts, rva) - 1
    if i >= 0 and funcs[i][0] <= rva < funcs[i][1]:
        return funcs[i]
    # code without its own .pdata entry (e.g. a leaf function): use the gap between entries
    if 0 <= i < len(funcs) - 1:
        return (funcs[i][1], funcs[i + 1][0])


text = [s for s in secs if s[0] == '.text'][0]
code = np.frombuffer(b, dtype=np.uint8, count=text[3], offset=text[4])
code_rva0 = text[2]


def disp32_hits(target):
    hits = []
    for shift in range(4):
        n = (len(code) - shift) // 4
        v = code[shift:shift + n * 4].view('<i4').astype(np.int64)
        pos = np.arange(n, dtype=np.int64) * 4 + shift + code_rva0
        hits.extend((pos[(pos + 4 + v) == target]).tolist())
    return sorted(hits)


md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True


def summarize(fbeg, fend):
    fo = r2f(fbeg)
    calls, strs, out = [], [], []
    for ins in md.disasm(b[fo:fo + (fend - fbeg)], fbeg):
        note = ''
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                tgt = ins.address + ins.size + op.mem.disp
                if tgt in iat:
                    note = iat[tgt]
                    if ins.mnemonic in ('call', 'jmp'):
                        calls.append(note)
                else:
                    s = cstr(tgt)
                    if s:
                        note = repr(s[:90])
                        strs.append(s)
            elif ins.mnemonic in ('call', 'jmp') and op.type == X86_OP_IMM:
                # follow a jmp [IAT] thunk
                tf = r2f(op.imm)
                if tf is not None and b[tf:tf + 2] == b'\xff\x25':
                    tgt = op.imm + 6 + struct.unpack_from('<i', b, tf + 2)[0]
                    if tgt in iat:
                        note = iat[tgt]
                        calls.append(note)
                if not note:
                    note = f'sub_{op.imm:x}'
                    if ins.mnemonic == 'call':
                        calls.append(note)
        out.append(f'  {ins.address:08x}  {ins.mnemonic:6} {ins.op_str:48} {note}')
    return calls, strs, out


rx = re.compile(pattern)
targets = []
for m in re.finditer(rb'[\x20-\x7e]{4,}\x00', b):
    s = m.group()[:-1].decode()
    if rx.search(s):
        r = f2r(m.start())
        if r is not None:
            targets.append((r, s))

done = set()
for r, s in targets[:max_hits]:
    for h in disp32_hits(r):
        f = func_of(h)
        if not f or f in done:
            continue
        done.add(f)
        calls, strs, out = summarize(*f)
        print(f'== func {f[0]:08x}-{f[1]:08x} (ref {h:08x} -> {s!r})')
        print('   strings:', ' | '.join(dict.fromkeys(x[:70] for x in strs)))
        print('   calls  :', ', '.join(dict.fromkeys(c for c in calls if not c.startswith('sub_')))[:2000])
        if want_dis:
            print('\n'.join(out))
