"""D-225: remove enclosed chroma-key pockets (opaque/semi #00FF00 islands the border flood-fill never reached)
and de-green the 2px ring around them. Usage: key_pocket_clean.py <rgba.png>... (in place). Prints removed px."""
import sys, numpy as np, cv2
def key_mask(bgra):
    hsv = cv2.cvtColor(bgra[..., :3], cv2.COLOR_BGR2HSV)
    return (hsv[...,0] >= 40) & (hsv[...,0] <= 80) & (hsv[...,1] >= 150) & (hsv[...,2] >= 150) & (bgra[...,3] > 12)
def clean(bgra):
    k = key_mask(bgra)
    if not k.any(): return bgra, 0
    ring = cv2.dilate(k.astype(np.uint8), np.ones((5,5), np.uint8)) > 0
    out = bgra.copy()
    out[k, 3] = 0
    b, g, r = [out[...,i].astype(int) for i in range(3)]
    greenish = ring & ~k & (g > r + 25) & (g > b + 25)
    out[greenish, 3] = (out[greenish, 3] * 0.35).astype(np.uint8)            # choke the halo
    cap = np.maximum(r, b) + 10
    out[...,1] = np.where(ring, np.minimum(g, cap), g).astype(np.uint8)       # despill
    return out, int(k.sum())
if __name__ == "__main__":
    for p in sys.argv[1:]:
        im = cv2.imread(p, cv2.IMREAD_UNCHANGED); o, n = clean(im)
        if n: cv2.imwrite(p, o)
        print(n, p)
