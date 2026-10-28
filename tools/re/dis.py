"""Tiny xref helper for libdev.dll. usage: dis.py dll xref <hex-file-offset-of-data>... | func <rva> [n]"""
import struct, sys, pickle, os
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_MEM, X86_REG_RIP

path = sys.argv[1]
b = open(path, 'rb').read()
e = struct.unpack_from('<I', b, 0x3c)[0]
nsec = struct.unpack_from('<H', b, e + 6)[0]
optsz = struct.unpack_from('<H', b, e + 20)[0]
base = struct.unpack_from('<Q', b, e + 24 + 24)[0]
secs = []
for i in range(nsec):
    o = e + 24 + optsz + i * 40
    name = b[o:o + 8].rstrip(b'\0').decode()
    vs, va, rs, rp = struct.unpack_from('<IIII', b, o + 8)
    secs.append((name, vs, va, rs, rp))
def f2r(off):
    for n, vs, va, rs, rp in secs:
        if rp <= off < rp + rs: return off - rp + va
def r2f(rva):
    for n, vs, va, rs, rp in secs:
        if va <= rva < va + max(vs, rs): return rva - va + rp
text = [s for s in secs if s[0] == '.text'][0]
md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True
cache = path + '.xref.pkl'

def build():
    refs = {}
    code = b[text[4]:text[4] + text[3]]
    off = 0
    while off < len(code):
        got = False
        for ins in md.disasm(code[off:off + 0x10000], text[2] + off):
            got = True
            for op in ins.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    refs.setdefault(ins.address + ins.size + op.mem.disp, []).append(ins.address)
            if ins.mnemonic == 'call' and ins.operands and ins.operands[0].type == 2:
                refs.setdefault(ins.operands[0].imm, []).append(ins.address)
            off = ins.address + ins.size - text[2]
        if not got: off += 1
    pickle.dump(refs, open(cache, 'wb'))
    return refs

refs = pickle.load(open(cache, 'rb')) if os.path.exists(cache) else build()

def dis(rva, n):
    fo = r2f(rva)
    for i, ins in enumerate(md.disasm(b[fo:fo + n * 15], rva)):
        if i >= n: break
        extra = ''
        for op in ins.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                t = ins.address + ins.size + op.mem.disp
                tf = r2f(t)
                if tf:
                    s = b[tf:tf + 64]
                    if s[:1].isascii() and s.split(b'\0')[0][:60].isascii() and len(s.split(b'\0')[0]) > 3:
                        extra = '  ; "%s"' % s.split(b'\0')[0][:60].decode('latin1')
                    elif s[1:2] == b'\0' and len(s.split(b'\0\0')[0]) > 6:
                        extra = '  ; L"%s"' % s.split(b'\0\0')[0].decode('latin1', 'ignore').replace('\0', '')[:60]
                    else:
                        extra = '  ; [%x] %s' % (t, s[:16].hex())
        print('%08x  %-8s %s%s' % (ins.address, ins.mnemonic, ins.op_str, extra))

cmd = sys.argv[2]
if cmd == 'xref':
    for a in sys.argv[3:]:
        rva = f2r(int(a, 16))
        print('data', a, 'rva', hex(rva), 'refs', [hex(x) for x in refs.get(rva, [])])
elif cmd == 'xrefrva':
    for a in sys.argv[3:]:
        print(a, [hex(x) for x in refs.get(int(a, 16), [])])
elif cmd == 'func':
    dis(int(sys.argv[3], 16), int(sys.argv[4]) if len(sys.argv) > 4 else 80)
