"""Minimal Inno Setup 6.x dumper: decompress setup header and data chunks (LZMA1)."""
import lzma, struct, sys, os, zlib

path, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
data = open(path, 'rb').read()

def lzma1_raw(buf):
    p = buf[0]
    lc, rem = p % 9, p // 9
    lp, pb = rem % 5, rem // 5
    dict_size = struct.unpack('<I', buf[1:5])[0]
    d = lzma.LZMADecompressor(lzma.FORMAT_RAW, filters=[{
        'id': lzma.FILTER_LZMA1, 'lc': lc, 'lp': lp, 'pb': pb, 'dict_size': dict_size}])
    try:
        return d.decompress(buf[5:])
    except lzma.LZMAError as e:
        print('  lzma error', e)
        return b''

tbl = data.find(b'rDlPtS')
fields = struct.unpack('<8I', data[tbl + 12: tbl + 12 + 32])
version, total, off_exe, sz_exe, crc_exe, off0, off1, tcrc = fields
print('offset table @', tbl, dict(version=version, total=total, off0=off0, off1=off1))

# setup.0: 64 byte version id, then blocks
print('setup.0 id:', data[off0:off0 + 64].rstrip(b'\0'))
pos = off0 + 64
for blk in range(2):
    crc, stored, compressed = struct.unpack('<IIB', data[pos:pos + 9])
    pos += 9
    raw, end = b'', pos + stored
    while pos < end:
        n = min(4096, end - pos - 4)
        raw += data[pos + 4: pos + 4 + n]
        pos += 4 + n
    dec = lzma1_raw(raw) if compressed else raw
    open(os.path.join(out, f'setup0_block{blk}.bin'), 'wb').write(dec)
    print(f'block{blk}: stored={stored} -> {len(dec)} bytes')

# setup.1: chunks starting with zlb\x1a
starts = []
i = off1
while True:
    i = data.find(b'zlb\x1a', i)
    if i < 0:
        break
    starts.append(i)
    i += 4
starts.append(len(data))
for k in range(len(starts) - 1):
    s, e = starts[k], starts[k + 1]
    dec = lzma1_raw(data[s + 4:e])
    if len(dec) > 0:
        fn = os.path.join(out, f'chunk_{s:010d}.bin')
        open(fn, 'wb').write(dec)
        print(f'chunk @{s}: {e - s} -> {len(dec)} bytes')
