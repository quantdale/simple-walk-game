#!/usr/bin/env python3
"""D-204: composite <name>.world.rgba (camera) + <name>.ui.rgba (UI Toolkit, transparent) dumps into
'<name>.unity-editor-capture.png'. Raw RGBA32, bottom-up rows (Unity ReadPixels order). Stdlib only."""
import os, struct, sys, zlib
d = sys.argv[1]
def load(p):
    w, h = map(int, open(p + ".size").read().split()); return w, h, open(p + ".rgba", "rb").read()
def png(path, w, h, data):
    rows = [data[(h - 1 - y) * w * 4:(h - y) * w * 4] for y in range(h)]
    raw = b"".join(b"\x00" + r for r in rows)
    ch = lambda t, x: struct.pack(">I", len(x)) + t + x + struct.pack(">I", zlib.crc32(t + x) & 0xFFFFFFFF)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + ch(b"IDAT", zlib.compress(raw, 9)) + ch(b"IEND", b""))
for f in sorted(os.listdir(d)):
    if not f.endswith(".world.rgba"): continue
    name = f[:-len(".world.rgba")]
    w, h, a = load(os.path.join(d, name + ".world")); _, _, b = load(os.path.join(d, name + ".ui"))
    out = bytearray(a)
    for i in range(0, len(out), 4):
        al = b[i + 3]
        if al:
            for k in range(3): out[i + k] = (b[i + k] * 255 + a[i + k] * (255 - al)) // 255  # UI layer is premultiplied alpha
        out[i + 3] = 255
    png(os.path.join(d, name + ".unity-editor-capture.png"), w, h, bytes(out))
    print(name)
