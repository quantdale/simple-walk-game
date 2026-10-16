#!/usr/bin/env python3
"""Encode a raw RGBA32 dump (bottom-up rows, Unity ReadPixels order) as PNG. Stdlib only."""
import struct, sys, zlib
src, size_file, out = sys.argv[1], sys.argv[2], sys.argv[3]
w, h = map(int, open(size_file).read().split())
data = open(src, "rb").read()
assert len(data) == w * h * 4, (len(data), w, h)
rows = [data[(h - 1 - y) * w * 4:(h - y) * w * 4] for y in range(h)]  # flip to top-down
raw = b"".join(b"\x00" + r for r in rows)
def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
open(out, "wb").write(png)
print(out, w, h)
