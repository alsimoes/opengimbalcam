"""Carve PE files from a decompressed solid Inno chunk; name them by export name / OriginalFilename.

usage: carve_pe.py <chunk.bin> <outdir> [name-filter ...]
NOTE: carved files still carry Inno's CALL/JMP filter; run inno_unfilter.py on them before disassembling.
"""
import mmap, os, re, struct, sys

src, out = sys.argv[1], sys.argv[2]
wanted = [w.lower() for w in sys.argv[3:]]
os.makedirs(out, exist_ok=True)
f = open(src, 'rb')
m = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)

def pe_size(o):
    e = struct.unpack_from('<I', m, o + 0x3c)[0]
    if e > 0x1000 or m[o + e:o + e + 4] != b'PE\0\0':
        return None
    nsec = struct.unpack_from('<H', m, o + e + 6)[0]
    optsz = struct.unpack_from('<H', m, o + e + 20)[0]
    magic = struct.unpack_from('<H', m, o + e + 24)[0]
    sec = o + e + 24 + optsz
    end = 0
    for i in range(nsec):
        raw_sz, raw_ptr = struct.unpack_from('<II', m, sec + i * 40 + 16)
        end = max(end, raw_ptr + raw_sz)
    dd = o + e + 24 + (96 if magic == 0x10b else 112)
    cert_off, cert_sz = struct.unpack_from('<II', m, dd + 4 * 8)
    if cert_off:
        end = max(end, cert_off + cert_sz)
    return end

def export_name(blob):
    e = struct.unpack_from('<I', blob, 0x3c)[0]
    nsec = struct.unpack_from('<H', blob, e + 6)[0]
    optsz = struct.unpack_from('<H', blob, e + 20)[0]
    magic = struct.unpack_from('<H', blob, e + 24)[0]
    rva = struct.unpack_from('<I', blob, e + 24 + (96 if magic == 0x10b else 112))[0]
    if not rva:
        return None
    for i in range(nsec):
        vs, va, rs, rp = struct.unpack_from('<IIII', blob, e + 24 + optsz + i * 40 + 8)
        if va <= rva < va + max(vs, rs):
            nrva = struct.unpack_from('<I', blob, rva - va + rp + 12)[0]
            no = nrva - va + rp
            return blob[no:blob.index(b'\0', no)].decode('latin1')
    return None

def name_of(blob):
    try:
        n = export_name(blob)
        if n:
            return n
    except (struct.error, ValueError):
        pass
    mo = re.search(rb'O\0r\0i\0g\0i\0n\0a\0l\0F\0i\0l\0e\0n\0a\0m\0e\0\0\0(?:\0\0)?((?:[^\0]\0)+)', blob)
    if mo:
        return mo.group(1).decode('utf-16le')
    return None

pos, count = 0, 0
while True:
    pos = m.find(b'MZ', pos)
    if pos < 0:
        break
    try:
        sz = pe_size(pos)
    except struct.error:
        sz = None
    if sz and 0x400 < sz < 200_000_000:
        blob = m[pos:pos + sz]
        nm = name_of(blob) or f'unnamed_{pos}.bin'
        if not wanted or any(w in nm.lower() for w in wanted):
            fn = os.path.join(out, f'{pos:010d}_{nm}')
            open(fn, 'wb').write(blob)
            print(pos, sz, nm)
        count += 1
        pos += 0x200  # sizes can be overestimated; do not skip over the next file
    else:
        pos += 2
print('PE files found:', count)
