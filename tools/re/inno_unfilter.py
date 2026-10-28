"""Undo Inno Setup 5.2+ CALL/JMP (E8/E9) address transform on a carved file."""
import sys
src, dst = sys.argv[1], sys.argv[2]
p = bytearray(open(src, 'rb').read())
i, n = 0, len(p)
while i < n - 4:
    if p[i] in (0xE8, 0xE9):
        i += 1
        left = 0x10000 - (i % 0x10000)
        if left < 4:          # operand crosses a 64 KiB block: left untouched
            i += left
            continue
        if p[i + 3] in (0x00, 0xFF):
            addr = (i + 4) & 0xFFFFFF
            rel = (p[i] | p[i + 1] << 8 | p[i + 2] << 16) - addr
            rel &= 0xFFFFFF
            if rel & 0x800000:
                p[i + 3] ^= 0xFF
            p[i], p[i + 1], p[i + 2] = rel & 0xFF, (rel >> 8) & 0xFF, (rel >> 16) & 0xFF
        i += 4
    else:
        i += 1
open(dst, 'wb').write(p)
