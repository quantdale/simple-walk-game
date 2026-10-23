"""D-217 Tavi finish pass: locked base TAVI-BODY-02; face-ellipse expressions; stronger palette pull + paper grain + ink-brown
contour re-ink so he matches Mira/STYLE_GUIDE_A; D-214 decontaminate. OpenCV/numpy (free)."""
import sys, os, numpy as np, cv2
R, O = sys.argv[1], sys.argv[2]
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
INK = np.array([0x5A, 0x3E, 0x2B], float)   # style guide ink brown
def pal_pull(rgb, k):
    f = rgb.reshape(-1,3).astype(float); d = ((f[:,None,:]-P[None])**2).sum(-1)
    return np.clip(f*(1-k)+P[d.argmin(1)]*k,0,255).reshape(rgb.shape).astype(np.uint8)
def grain(rgb, amp=0.045, seed=11):
    g = cv2.GaussianBlur(np.random.default_rng(seed).normal(0,1,rgb.shape[:2]),(0,0),0.9)
    return np.clip(rgb.astype(float)*(1+amp*g[...,None]),0,255).astype(np.uint8)
def reink(rgb, alpha):
    """Soften crisp black outlines toward ink brown and thicken silhouette slightly (hand-inked look)."""
    lab = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).astype(float)
    # dark thin lines (low L, low chroma-ish): pull toward ink
    dark = (lab[...,0] < 55) & (alpha > 40)
    edge = cv2.Canny(cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY), 40, 120) > 0
    mask = ((dark & edge) | ((lab[...,0] < 40) & (alpha > 80))).astype(np.float32)
    mask = cv2.GaussianBlur(mask, (0,0), 0.8)[..., None]
    out = rgb.astype(float)*(1-0.72*mask) + INK[None,None,:]*(0.72*mask)
    return np.clip(out,0,255).astype(np.uint8)
ld = lambda n: cv2.cvtColor(cv2.imread(f"{R}/{n}.jpg"), cv2.COLOR_BGR2RGB)
base = ld("TAVI-BODY-02")
bg = np.median(np.concatenate([base[:8].reshape(-1,3), base[-8:].reshape(-1,3)]), 0)
d = np.abs(base.astype(float)-bg).sum(-1)
fg = cv2.morphologyEx(((d > 30)*255).astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5,5),np.uint8))
ff = (255-fg).copy(); m = np.zeros((ff.shape[0]+2, ff.shape[1]+2), np.uint8); cv2.floodFill(ff, m, (0,0), 128)
a = np.where(ff == 128, 0, 255).astype(np.uint8)
db = cv2.blur(np.clip(d,0,255).astype(np.uint8), (9,9)); a[(d < 14) & (db < 12)] = 0
a = cv2.GaussianBlur(cv2.erode(a, np.ones((3,3),np.uint8)).astype(float), (0,0), 1.0).astype(np.uint8)
# face ellipse — estimate from head bbox of opaque pixels in upper third
ys, xs = np.where(a[:int(a.shape[0]*0.38)] > 8)
cx, cy = int(xs.mean()), int(ys.mean()+0.12*(ys.max()-ys.min()))
rx, ry = int(0.22*(xs.max()-xs.min())), int(0.28*(ys.max()-ys.min()))
FACE = (cx, cy, max(60,rx), max(50,ry)); print("FACE", FACE)
fm = np.zeros(base.shape[:2], np.float32); cv2.ellipse(fm, (FACE[0], FACE[1]), (FACE[2], FACE[3]), 0, 0, 360, 1, -1)
fm = cv2.GaussianBlur(fm, (0,0), 6)[..., None]
HEAD = np.zeros(base.shape[:2], np.uint8); cv2.rectangle(HEAD, (max(0,cx-180), max(0,cy-200)), (min(base.shape[1]-1,cx+180), cy+120), 255, -1)
g0 = cv2.cvtColor(base, cv2.COLOR_RGB2GRAY).astype(np.float32)
out = {"neutral": base}
for k, n in [("curious","TAVI-EXPR-CURIOUS-02"),("glad","TAVI-EXPR-GLAD-02"),("proud","TAVI-EXPR-PROUD-02"),("wistful","TAVI-EXPR-WISTFUL-02")]:
    e = ld(n); W = np.eye(2,3,dtype=np.float32)
    try:
        _, W = cv2.findTransformECC(g0, cv2.cvtColor(e, cv2.COLOR_RGB2GRAY).astype(np.float32), W, cv2.MOTION_AFFINE,
                                    (cv2.TERM_CRITERIA_EPS|cv2.TERM_CRITERIA_COUNT, 200, 1e-6), HEAD, 5)
    except cv2.error as ex: print(k, "ecc fail", ex); W = np.eye(2,3,dtype=np.float32)
    er = cv2.warpAffine(e, W, (base.shape[1], base.shape[0]), flags=cv2.INTER_LINEAR|cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REPLICATE)
    out[k] = (er*fm + base*(1-fm)).astype(np.uint8)
    print(k, "ecc", np.round(W,3).tolist())
ys, xs = np.where(a > 8); x0, x1, y0, y1 = xs.min(), xs.max()+1, ys.min(), ys.max()+1
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
    f = np.where((alpha < 6)[..., None], acc, f)
    return np.clip(f, 0, 255).astype(np.uint8)
_kd = np.minimum(np.abs(base.astype(np.float32) - bg[None, None, :]).sum(-1), np.abs(base.astype(np.float32) - 255.0).sum(-1))
_n, _lab, _st, _ = cv2.connectedComponentsWithStats((_kd < 30).astype(np.uint8), 8)
pockets = np.zeros(a.shape, np.uint8)
for _i in range(1, _n):
    if 20 <= _st[_i][4] <= 5000 and _kd[_lab == _i].mean() < 16: pockets[_lab == _i] = 1
pockets[cv2.dilate((fm[..., 0] > 0.01).astype(np.uint8), np.ones((15, 15), np.uint8)) > 0] = 0
a = np.where(cv2.dilate(pockets, np.ones((3, 3), np.uint8)) > 0, 0, a).astype(np.uint8)
print("key pockets removed:", int(pockets.sum()), "px")
dist = cv2.distanceTransform((a > 0).astype(np.uint8), cv2.DIST_L2, 3)
key_d = np.minimum(np.abs(base.astype(np.float32) - bg[None, None, :]).sum(-1), np.abs(base.astype(np.float32) - 255.0).sum(-1))
ring = dist <= 7.0
a = np.where(ring, (a * np.clip((key_d - 30) / 60.0, 0, 1)).astype(np.uint8), a)
a[(dist <= 12) & (base.min(-1) > 210)] = 0
a = cv2.GaussianBlur(a, (0, 0), 0.6)
os.makedirs(O, exist_ok=True)
for k, img in out.items():
    styled = grain(pal_pull(reink(img, a), 0.48))
    rgba = np.dstack([decontaminate(styled, a), a])[y0:y1, x0:x1]
    if k == "neutral": cv2.imwrite(f"{O}/tavi_body.png", cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGRA))
    h, w = rgba.shape[:2]
    bust = rgba[0:int(h*0.42), int(w*0.10):int(w*0.90)]
    cv2.imwrite(f"{O}/portrait_tavi_{k}.png", cv2.cvtColor(bust, cv2.COLOR_RGBA2BGRA))
print("wrote", O)
