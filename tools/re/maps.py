"""Recover std::map initializer lists from libdev static ctors. usage: maps.py <libdev_unfiltered.dll> (run dis.py once first to build the xref cache)"""
import pickle, struct, re, json, sys
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_MEM, X86_OP_IMM, X86_OP_REG, X86_REG_RIP

b = open(sys.argv[1], 'rb').read()
refs = pickle.load(open(sys.argv[1] + '.xref.pkl', 'rb'))
md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True
def r2f(r):
    if 0x1000 <= r < 0x1c0000: return r - 0x1000 + 0x400
    if 0x1c0000 <= r < 0x20e000: return r - 0x1c0000 + 0x1bf000
    return None

def func_start(site):
    # walk back to int3 padding
    f = r2f(site)
    while not (b[f - 1] == 0xcc and b[f - 2] == 0xcc):
        f -= 1
    return f - 0x400 + 0x1000

def run(start, stop):
    """straight-line symbolic exec: track xmm regs, r11/rbp offsets, and stack memory."""
    xmm, mem, regs = {}, {}, {'rsp': 0}
    out = []
    code = b[r2f(start):r2f(stop) + 16]
    for ins in md.disasm(code, start):
        ops = ins.operands
        def addr(op):
            base = ins.reg_name(op.mem.base) if op.mem.base else None
            if base not in regs: return None
            return regs[base] + op.mem.disp
        if ins.mnemonic == 'sub' and ins.op_str.startswith('rsp, '):
            regs['rsp'] = 0
        elif ins.mnemonic in ('mov', 'lea') and ops[0].type == X86_OP_REG and ins.reg_name(ops[0].reg) in ('r11', 'rbp') and ops[1].type in (X86_OP_REG, X86_OP_MEM):
            if ops[1].type == X86_OP_REG and ins.reg_name(ops[1].reg) == 'rsp':
                regs[ins.reg_name(ops[0].reg)] = 0
            elif ins.mnemonic == 'lea' and ops[1].type == X86_OP_MEM and ins.reg_name(ops[1].mem.base) == 'rsp':
                regs[ins.reg_name(ops[0].reg)] = ops[1].mem.disp
        elif ins.mnemonic in ('movdqa', 'movdqu', 'movups', 'movaps') and ops[0].type == X86_OP_REG and ops[1].type == X86_OP_MEM and ops[1].mem.base == X86_REG_RIP:
            t = ins.address + ins.size + ops[1].mem.disp
            xmm[ins.reg_name(ops[0].reg)] = b[r2f(t):r2f(t) + 16]
        elif ins.mnemonic in ('movdqa', 'movdqu', 'movups', 'movaps') and ops[0].type == X86_OP_MEM and ops[1].type == X86_OP_REG:
            a = addr(ops[0]); v = xmm.get(ins.reg_name(ops[1].reg))
            if a is not None and v is not None:
                for i in range(16): mem[a + i] = v[i]
        elif ins.mnemonic == 'mov' and ops[0].type == X86_OP_MEM and ops[1].type == X86_OP_IMM:
            a = addr(ops[0])
            if a is not None:
                v = struct.pack('<Q', ops[1].imm & 0xffffffffffffffff)[:ops[0].size]
                for i in range(len(v)): mem[a + i] = v[i]
        if ins.address >= stop:
            break
    return mem

def ints(mem, lo, hi):
    return [struct.unpack('<i', bytes(mem.get(lo + 4 * i + k, 0) for k in range(4)))[0] for i in range((hi - lo) // 4)]

simple = {}
for site in sorted(refs.get(0x104df0, [])):
    start = func_start(site)
    # find the map global and begin/end of initializer list
    ins = list(md.disasm(b[r2f(start):r2f(site) + 5], start))
    tgt = None
    for x in ins:
        if x.mnemonic == 'lea' and x.op_str.startswith('rcx, [rip'):
            tgt = x.address + x.size + x.operands[1].mem.disp
    mem = run(start, site)
    # initializer list: begin at [rsp+0x20], end at [rsp+0x28] were set by lea rax,[rsp+X]; recover from code
    leas = [x for x in ins if x.mnemonic == 'lea' and x.op_str.startswith('rax, [r')]
    offs = []
    for x in leas:
        bn = x.reg_name(x.operands[1].mem.base)
        base = 0 if bn == 'rsp' else None
        if bn == 'r11':
            # r11 is set as lea r11,[rsp+N]
            for y in ins:
                if y.mnemonic == 'lea' and y.op_str.startswith('r11, [rsp'):
                    base = y.operands[1].mem.disp
        if base is not None: offs.append(base + x.operands[1].mem.disp)
    if len(offs) >= 2:
        lo, hi = min(offs[-2:]), max(offs[-2:])
        vals = ints(mem, lo, hi)
        simple[hex(tgt)] = dict(zip(vals[0::2], vals[1::2]))

nested = {}
for site in sorted(refs.get(0x105080, [])):
    start = site - 0x30
    for back in range(0x30, 0x10, -1):
        ins = list(md.disasm(b[r2f(site - back):r2f(site) + 5], site - back))
        if ins and ins[-1].address == site: break
    key = sub = None
    for x in ins:
        if x.mnemonic == 'mov' and x.operands[0].type == X86_OP_MEM and x.operands[1].type == X86_OP_IMM: key = x.operands[1].imm
        if x.mnemonic == 'lea' and x.op_str.startswith(('rdx, [rip', 'edx, [rip')): sub = x.address + x.size + x.operands[1].mem.disp
    nested.setdefault(hex(func_start(site)), []).append((key, hex(sub) if sub else None))

json.dump({'simple': simple, 'nested': nested}, open(sys.argv[1] + '.maps.json', 'w'), indent=1)
for k, v in simple.items(): print('map', k, v)
for k, v in nested.items(): print('nested in fn', k, v)
