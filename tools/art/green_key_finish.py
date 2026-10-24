"""D-223: chroma-green key cutout + D-214-style decontaminate/choke.
Usage: green_key_finish.py <in.jpg|png> <out.png> [--max-edge 280]
Prints key_fringe_ratio (neon-green in semitransparent rim). Exit 2 if > 0.04."""
import sys, numpy as np, cv2
inp, out = sys.argv[1], sys.argv[2]
max_edge = 280
if "--max-edge" in sys.argv:
    max_edge = int(sys.argv[sys.argv.index("--max-edge")+1])
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070","B89BC8"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
def pal_pull(rgb, k=0.38):
    f = rgb.reshape(-1,3).astype(float); d = ((f[:,None,:]-P[None])**2).sum(-1)
    return np.clip(f*(1-k)+P[d.argmin(1)]*k,0,255).reshape(rgb.shape).astype(np.uint8)
def grain(rgb, amp=0.035, seed=21):
    g = cv2.GaussianBlur(np.random.default_rng(seed).normal(0,1,rgb.shape[:2]),(0,0),0.8)
    return np.clip(rgb.astype(float)*(1+amp*g[...,None]),0,255).astype(np.uint8)
img = cv2.cvtColor(cv2.imread(inp), cv2.COLOR_BGR2RGB)
hsv = cv2.cvtColor(img, cv2.COLOR_RGB2HSV)
green = ((hsv[...,0] > 35) & (hsv[...,0] < 95) & (hsv[...,1] > 45) & (hsv[...,2] > 40)).astype(np.uint8)
fg = ((1 - green) * 255).astype(np.uint8)
fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, np.ones((7,7),np.uint8))
ff = (255-fg).copy(); m = np.zeros((ff.shape[0]+2, ff.shape[1]+2), np.uint8); cv2.floodFill(ff, m, (0,0), 128)
a = np.where(ff == 128, 0, 255).astype(np.uint8)
# Hard erode then choke neon
a = cv2.erode(a, np.ones((5,5),np.uint8))
bg = np.array([0.,255.,0.])
dist = cv2.distanceTransform((a > 0).astype(np.uint8), cv2.DIST_L2, 3)
key_d = np.abs(img.astype(np.float32) - bg[None,None,:]).sum(-1)
a = np.where(dist <= 10.0, (a * np.clip((key_d - 70) / 100.0, 0, 1)).astype(np.uint8), a)
# Drop any remaining high-sat green in the rim
neon = (hsv[...,0] > 35) & (hsv[...,0] < 95) & (hsv[...,1] > 90) & (hsv[...,2] > 60)
a[neon & (dist <= 16)] = 0
a = cv2.GaussianBlur(a.astype(float), (0,0), 0.5).astype(np.uint8)
# Binary-ish cleanup: crush mushy mid-alphas on the rim
a = np.where(a < 48, 0, np.where(a > 210, 255, a)).astype(np.uint8)
def decontaminate(rgb, alpha):
    f = rgb.astype(np.float32); al = (alpha.astype(np.float32) / 255.0)[..., None]
    semi = (al > 0.02) & (al < 0.98)
    un = np.clip((f - (1 - al) * bg[None, None, :]) / np.maximum(al, 0.02), 0, 255)
    f = np.where(semi, un, f)
    known = (alpha >= 250).astype(np.float32)
    acc = f * known[..., None]; wsum = known.copy()
    for r in (2, 4, 8, 16, 32, 64):
        k = 2 * r + 1
        acc_b = cv2.blur(acc, (k, k)); w_b = cv2.blur(wsum, (k, k))
        fill = (wsum < 0.5) & (w_b > 1e-4)
        acc[fill] = acc_b[fill] / w_b[fill][..., None]; wsum[fill] = 1.0
    f = np.where((alpha < 8)[..., None], acc, f)
    # Force near-transparent rim to interior colour (kills residual neon)
    rim = (alpha > 0) & (alpha < 180)
    f[rim] = acc[rim]
    return np.clip(f, 0, 255).astype(np.uint8)
styled = grain(pal_pull(img, 0.42))
clean = decontaminate(styled, a)
# Final: zero RGB where alpha==0; where alpha low, pull toward clean opaque neighbour already done
clean = np.where(a[...,None] == 0, 0, clean)
ys, xs = np.where(a > 8)
x0,x1,y0,y1 = int(xs.min()), int(xs.max()+1), int(ys.min()), int(ys.max()+1)
rgba = np.dstack([clean, a])[y0:y1, x0:x1]
h, w = rgba.shape[:2]
if max(h, w) > max_edge:
    s = max_edge / max(h, w)
    rgba = cv2.resize(rgba, (max(4,int(w*s)//4*4), max(4,int(h*s)//4*4)), interpolation=cv2.INTER_AREA)
ph = ((rgba.shape[0]+3)//4)*4; pw = ((rgba.shape[1]+3)//4)*4
pad = np.zeros((ph,pw,4), np.uint8); pad[:rgba.shape[0], :rgba.shape[1]] = rgba
# Key-fringe metric: neon green OR hot magenta in semitransparent band only
al = pad[...,3]; rgb = pad[...,:3]
hsv2 = cv2.cvtColor(pad[...,:3], cv2.COLOR_RGB2HSV)
edge_m = (al > 12) & (al < 220)
neon2 = (hsv2[...,0] > 35) & (hsv2[...,0] < 95) & (hsv2[...,1] > 100) & (hsv2[...,2] > 80)
mag = (rgb[...,0] > 160) & (rgb[...,2] > 160) & (rgb[...,1] < 110) & (hsv2[...,1] > 60)
fring = edge_m & (neon2 | mag)
ratio = float(fring.sum()) / max(1, int(edge_m.sum()))
# D-225: kill enclosed key pockets the border flood-fill never reached (hue-symmetric for RGB/BGR order)
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from key_pocket_clean import clean as _pocket_clean
pad, _pocket_px = _pocket_clean(pad); print('key_pocket_px', _pocket_px)
cv2.imwrite(out, cv2.cvtColor(pad, cv2.COLOR_RGBA2BGRA))
print(f"wrote {out} size={pad.shape[:2]} key_fringe_ratio={ratio:.4f} edge_px={int(edge_m.sum())} fringe_px={int(fring.sum())}")
sys.exit(2 if ratio > 0.04 else 0)
