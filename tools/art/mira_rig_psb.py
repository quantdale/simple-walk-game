"""D-212: layered PSB source for Mira's 2D Animation rig (PSD Importer, character mode). Layers = the D-208 cut-out parts
placed on the 576x1092 body canvas, bottom to top: scarf_tail, body, braid, head. psd-tools (MIT), no paid tools.
Usage: mira_rig_psb.py <Art/Trailhead dir> <out.psb>"""
import sys, numpy as np
from PIL import Image
from psd_tools import PSDImage
from psd_tools.api.layers import PixelLayer
from psd_tools.constants import Compression
D, OUT = sys.argv[1:3]
PARTS = {"body": (0, 0, 576, 1091), "braid": (298, 260, 83, 229), "scarf_tail": (418, 303, 133, 200), "head": (79, 0, 434, 271)}
W, H = 576, 1092
psd = PSDImage.new("RGBA", (W, H), color=(0, 0, 0, 0)); psd._record.header.version = 2   # PSB (large document format) for the PSD Importer
for name in ["scarf_tail", "body", "braid", "head"]:
    x, y, w, h = PARTS[name]
    im = Image.open(f"{D}/mira_part_{name}.png").convert("RGBA")
    im = im.crop((0, im.height - h, w, im.height))        # parts are padded at the top/right to multiples of 4
    a = np.array(im); ys, xs = np.where(a[..., 3] > 0)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    psd.append(PixelLayer.frompil(im.crop(box), psd, name, top=y + box[1], left=x + box[0], compression=Compression.RLE))
psd.save(OUT); print(OUT, [(l.name, l.bbox) for l in PSDImage.open(OUT)])
