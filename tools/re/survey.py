"""Survey every call to a libdev function (e.g. uvcExtSet 0x87d0): selector, buffer stores, nearby log strings.

usage: survey.py <libdev_unfiltered.dll> <target-rva-hex>
"""
import pickle,struct,re,sys
from capstone import *
from capstone.x86 import *
b=open(sys.argv[1],'rb').read()
refs=pickle.load(open(sys.argv[1]+'.xref.pkl','rb'))
md=Cs(CS_ARCH_X86,CS_MODE_64); md.detail=True
def r2f(r):
    if 0x1000<=r<0x1c0000: return r-0x1000+0x400
    if 0x1c0000<=r<0x20e000: return r-0x1c0000+0x1bf000
def s_at(r):
    f=r2f(r)
    if f is None: return None
    s=b[f:f+80].split(b'\0')[0]
    return s.decode('latin1') if len(s)>3 and all(32<=c<127 for c in s) else None
target=int(sys.argv[2],16)
for site in sorted(refs.get(target,[])):
    start=site-0x120
    ins=list(md.disasm(b[r2f(start):r2f(site)+0x60],start))
    # resync: keep only instructions that land exactly on site
    idx=[i for i,x in enumerate(ins) if x.address==site]
    if not idx:
        for d in range(1,16):
            ins=list(md.disasm(b[r2f(start+d):r2f(site)+0x60],start+d)); idx=[i for i,x in enumerate(ins) if x.address==site]
            if idx: break
    if not idx: print(hex(site),'resync fail'); continue
    k=idx[0]; before=ins[max(0,k-45):k]; after=ins[k+1:k+14]
    sel=None; stores=[]
    for x in before:
        if x.mnemonic=='mov' and x.op_str.startswith('edx, '): 
            try: sel=int(x.op_str[5:],0)
            except: sel=x.op_str[5:]
        m=re.match(r'(byte|word|dword) ptr \[rsp \+ (0x[0-9a-f]+)\], (0x[0-9a-f]+|\d+)$',x.op_str)
        if x.mnemonic=='mov' and m: stores.append((m.group(1)[0],int(m.group(2),16),int(m.group(3),0)))
    names=[]
    for x in before[-30:]+after:
        for op in x.operands:
            if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
                s=s_at(x.address+x.size+op.mem.disp)
                if s and ('%' in s or 'failed' in s or s[0].islower()): names.append(s)
    print(hex(site),'sel',sel,'stores',[(t,hex(o),hex(v)) for t,o,v in stores[-8:]],'|',' || '.join(names[-3:]))
