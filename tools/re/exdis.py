"""Disassemble exported functions of a x64 DLL by name, naming every call target.

usage: exdis.py <dll> <regex> [--max N] [--follow RVA,...]
  regex     matched against the decorated export names (e.g. "cameraSetAntiFlickR")
  --follow  also disassemble these internal functions (hex RVAs; "rva:len" overrides the
            .pdata bounds, needed when a function is split into chained unwind ranges)

Calls to other exports print their (undecorated-ish) name; calls through the IAT print
dll!import; RIP-relative data shows the first 16 bytes. Function bounds come from .pdata.
"""
import bisect
import re
import struct
import sys

from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

path, pattern = sys.argv[1], sys.argv[2]
max_hits = int(sys.argv[sys.argv.index('--max') + 1]) if '--max' in sys.argv else 20
follow = [tuple(int(v, 16) for v in x.split(':')) for x in sys.argv[sys.argv.index('--follow') + 1].split(',')]     if '--follow' in sys.argv else []

b = open(path, 'rb').read()
e = struct.unpack_from('<I', b, 0x3c)[0]
nsec = struct.unpack_from('<H', b, e + 6)[0]
optsz = struct.unpack_from('<H', b, e + 20)[0]
dd = e + 24 + 112
secs = []
for i in range(nsec):
    o = e + 24 + optsz + i * 40
    vs, va, rs, rp = struct.unpack_from('<IIII', b, o + 8)
    secs.append((b[o:o + 8].rstrip(b'\0').decode(), vs, va, rs, rp))


def r2f(rva):
    for _, vs, va, rs, rp in secs:
        if va <= rva < va + max(vs, rs):
            return rva - va + rp


def cstr(rva, limit=300):
    f = r2f(rva)
    if f is None:
        return None
    s = b[f:f + limit].split(b'\0')[0]
    return s.decode('latin1') if len(s) >= 3 and all(32 <= c < 127 for c in s) else None


def short(name):
    m = re.match(r'\?(\w+)@(\w+)@@', name)
    return f'{m.group(2)}::{m.group(1)}' if m else name


# exports
exports = {}
erva = struct.unpack_from('<I', b, dd)[0]
eo = r2f(erva)
nfun, nnames, afun, anames, aords = struct.unpack_from('<IIIII', b, eo + 20)
for i in range(nnames):
    name = cstr(struct.unpack_from('<I', b, r2f(anames) + 4 * i)[0], 1000)
    ordinal = struct.unpack_from('<H', b, r2f(aords) + 2 * i)[0]
    exports[name] = struct.unpack_from('<I', b, r2f(afun) + 4 * ordinal)[0]
by_rva = {}
for n, r in exports.items():
    by_rva.setdefault(r, short(n))

# imports
iat = {}
o = r2f(struct.unpack_from('<I', b, dd + 8)[0])
while True:
    oft, _, _, nm, ft = struct.unpack_from('<IIIII', b, o)
    if not nm:
        break
    dll = cstr(nm)
    t, slot = r2f(oft or ft), ft
    while (v := struct.unpack_from('<Q', b, t)[0]):
        iat[slot] = f"{dll}!#{v & 0xffff}" if v >> 63 else f"{dll}!{cstr((v & 0x7fffffff) + 2)}"
        t += 8
        slot += 8
    o += 20

prva, psz = struct.unpack_from('<II', b, dd + 3 * 8)
funcs = sorted(struct.unpack_from('<II', b, r2f(prva) + i * 12) for i in range(psz // 12))
starts = [f[0] for f in funcs]


def bounds(rva):
    i = bisect.bisect_right(starts, rva) - 1
    if i >= 0 and funcs[i][0] <= rva < funcs[i][1]:
        return funcs[i]
    return (rva, rva + 0x400)


def target_name(t):
    if t in by_rva:
        return by_rva[t]
    f = r2f(t)
    if f is not None and b[f:f + 2] == b'\xff\x25':  # jmp [IAT] thunk
        slot = t + 6 + struct.unpack_from('<i', b, f + 2)[0]
        return iat.get(slot, f'sub_{t:x}')
    if f is not None and b[f] == 0xE9:  # jmp rel32 thunk
        t2 = t + 5 + struct.unpack_from('<i', b, f + 1)[0]
        return by_rva.get(t2, f'sub_{t2:x}')
    return f'sub_{t:x}'


md = Cs(CS_ARCH_X86, CS_MODE_64)
md.detail = True


def dump(title, start, length=None):
    beg, end = (start, start + length) if length else bounds(start)
    print(f'== {title} @ {beg:x}-{end:x}')
    fo = r2f(beg)
    for ins in md.disasm(b[fo:fo + (end - beg)], beg):
        note = ''
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                tgt = ins.address + ins.size + op.mem.disp
                if tgt in iat:
                    note = iat[tgt]
                elif (s := cstr(tgt)):
                    note = repr(s[:80])
                elif (f := r2f(tgt)) is not None:
                    note = f'[{tgt:x}] {b[f:f + 16].hex()}'
            elif ins.mnemonic.startswith(('call', 'j')) and op.type == X86_OP_IMM and ins.mnemonic in ('call', 'jmp'):
                note = target_name(op.imm)
        print(f'  {ins.address:08x}  {ins.mnemonic:7} {ins.op_str:50} {note}')


rx = re.compile(pattern)
hits = [(n, r) for n, r in exports.items() if rx.search(n)]
for n, r in hits[:max_hits]:
    dump(n, r)
for r, *n in follow:
    dump(by_rva.get(r, f'sub_{r:x}'), r, n[0] if n else None)
