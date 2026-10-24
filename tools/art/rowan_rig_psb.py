"""D-222: layered PSB for Rowan's 2D Animation rig. Layers: fern, body, head. psd-tools."""
import sys, numpy as np, json
from PIL import Image
from psd_tools import PSDImage
from psd_tools.api.layers import PixelLayer
from psd_tools.constants import Compression
D, OUT = sys.argv[1:3]
meta = json.load(open(f"{D}/parts.json"))
W, H = meta["canvas"]
psd = PSDImage.new("RGBA", (W, H), color=(0, 0, 0, 0)); psd._record.header.version = 2
for name in ["fern", "body", "head"]:
    p = meta["parts"][name]
    im = Image.open(f"{D}/rowan_part_{name}.png").convert("RGBA")
    im = im.crop((0, 0, p["w"], p["h"]))
    a = np.array(im); ys, xs = np.where(a[..., 3] > 0)
    if len(xs) == 0: continue
    box = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    psd.append(PixelLayer.frompil(im.crop(box), psd, name, top=p["y"] + box[1], left=p["x"] + box[0], compression=Compression.RLE))
psd.save(OUT); print(OUT, [(l.name, l.bbox) for l in PSDImage.open(OUT)])
