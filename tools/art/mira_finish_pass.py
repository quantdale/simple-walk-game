"""D-206 Mira finish pass: one locked base (MIRA-BODY-03); each expression contributes ONLY its face (ECC-registered,
feathered ellipse) onto that base, so hat, hair, braid, clothes and framing are identical across portraits."""
import sys, numpy as np, cv2
R, O = sys.argv[1], sys.argv[2]
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
def pal_pull(rgb, k):
    f = rgb.reshape(-1,3).astype(float); d = ((f[:,None,:]-P[None])**2).sum(-1)
    return np.clip(f*(1-k)+P[d.argmin(1)]*k,0,255).reshape(rgb.shape).astype(np.uint8)
ld = lambda n: cv2.cvtColor(cv2.imread(f"{R}/{n}.jpg"), cv2.COLOR_BGR2RGB)
base = ld("MIRA-BODY-03")
# alpha from flat background: flood from border + enclosed flat holes
bg = np.median(np.concatenate([base[:8].reshape(-1,3), base[-8:].reshape(-1,3)]), 0)
d = np.abs(base.astype(float)-bg).sum(-1)
fg = cv2.morphologyEx(((d > 30)*255).astype(np.uint8), cv2.MORPH_CLOSE, np.ones((5,5),np.uint8))
ff = (255-fg).copy(); m = np.zeros((ff.shape[0]+2, ff.shape[1]+2), np.uint8); cv2.floodFill(ff, m, (0,0), 128)
a = np.where(ff == 128, 0, 255).astype(np.uint8)
db = cv2.blur(np.clip(d,0,255).astype(np.uint8), (9,9)); a[(d < 14) & (db < 12)] = 0
a = cv2.GaussianBlur(cv2.erode(a, np.ones((3,3),np.uint8)).astype(float), (0,0), 1.0).astype(np.uint8)
FACE = (int(305*1.25), int(232*1.25), int(78*1.25), int(70*1.25))   # cx, cy, rx, ry (preview coords x1.25)
fm = np.zeros(base.shape[:2], np.float32); cv2.ellipse(fm, (FACE[0], FACE[1]), (FACE[2], FACE[3]), 0, 0, 360, 1, -1)
fm = cv2.GaussianBlur(fm, (0,0), 6)[..., None]
# D-211 hair pass: MIRA-HAIR-01 (same base, loose side wisps smoothed under the hat) is ECC-registered and contributes
# ONLY the hair around the face (head box minus the face ellipse, feathered). Alpha and crop stay from the original base,
# so the D-208 part rects and pivots are unchanged.
import os
if os.path.exists(f"{R}/MIRA-HAIR-01.jpg"):
    hr = cv2.resize(ld("MIRA-HAIR-01"), (base.shape[1], base.shape[0])); Wh = np.eye(2,3,dtype=np.float32)
    _, Wh = cv2.findTransformECC(cv2.cvtColor(base, cv2.COLOR_RGB2GRAY).astype(np.float32), cv2.cvtColor(hr, cv2.COLOR_RGB2GRAY).astype(np.float32),
                                 Wh, cv2.MOTION_AFFINE, (cv2.TERM_CRITERIA_EPS|cv2.TERM_CRITERIA_COUNT, 200, 1e-6), None, 5)
    hr = cv2.warpAffine(hr, Wh, (base.shape[1], base.shape[0]), flags=cv2.INTER_LINEAR|cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REPLICATE)
    hm = np.zeros(base.shape[:2], np.float32); cv2.rectangle(hm, (200, 175), (530, 400), 1, -1)
    hm = cv2.GaussianBlur(hm, (0,0), 10) * (1 - cv2.GaussianBlur(fm[..., 0], (0,0), 3))
    base = (hr*hm[..., None] + base*(1-hm[..., None])).astype(np.uint8)
    print("hair ecc", np.round(Wh,3).tolist())
g0 = cv2.cvtColor(base, cv2.COLOR_RGB2GRAY).astype(np.float32)
out = {"neutral": base}
for k, n in [("glad","MIRA-EXPR-GLAD-01"),("worried","MIRA-EXPR-WORRIED-01"),("proud","MIRA-EXPR-PROUD-01"),("celebration","MIRA-EXPR-CELEB-01")]:
    e = ld(n); W = np.eye(2,3,dtype=np.float32)
    _, W = cv2.findTransformECC(g0, cv2.cvtColor(e, cv2.COLOR_RGB2GRAY).astype(np.float32), W, cv2.MOTION_AFFINE,
                                (cv2.TERM_CRITERIA_EPS|cv2.TERM_CRITERIA_COUNT, 200, 1e-6), None, 5)
    er = cv2.warpAffine(e, W, (base.shape[1], base.shape[0]), flags=cv2.INTER_LINEAR|cv2.WARP_INVERSE_MAP)
    out[k] = (er*fm + base*(1-fm)).astype(np.uint8)
    print(k, "ecc", np.round(W,3).tolist())
ys, xs = np.where(a > 8); x0, x1, y0, y1 = xs.min(), xs.max()+1, ys.min(), ys.max()+1
def decontaminate(rgb, alpha):
    """D-214: remove the cream key colour from edge pixels (un-mix: C = (I - (1-a)·bg) / a) and bleed edge colours into the
    fully transparent area, so bilinear filtering / compression never pulls cream into the silhouette (hat brim, arm)."""
    f = rgb.astype(np.float32); al = (alpha.astype(np.float32) / 255.0)[..., None]
    semi = (al > 0.02) & (al < 0.98)
    un = np.clip((f - (1 - al) * bg[None, None, :]) / np.maximum(al, 0.02), 0, 255)
    f = np.where(semi, un, f)
    known = (alpha >= 250).astype(np.float32)
    acc = f * known[..., None]; wsum = known.copy()
    for r in (2, 4, 8, 16, 32, 64):   # push-pull style fill of the transparent area from the opaque interior
        k = 2 * r + 1
        acc_b = cv2.blur(acc, (k, k)); w_b = cv2.blur(wsum, (k, k))
        fill = (wsum < 0.5) & (w_b > 1e-4)
        acc[fill] = acc_b[fill] / w_b[fill][..., None]; wsum[fill] = 1.0
    f = np.where((alpha < 6)[..., None], acc, f)
    return np.clip(f, 0, 255).astype(np.uint8)

# D-214 choke: the generator draws a light key-coloured rim just outside the ink line. Within 4 px of the transparent area,
# pixels close to the key colour lose alpha (the ink line itself is dark, so it stays).
# D-214 pockets: small enclosed patches of the flat key colour (between hat brim, hair, braid and scarf) are background too.
_kd = np.abs(base.astype(np.float32) - bg[None, None, :]).sum(-1)
_n, _lab, _st, _ = cv2.connectedComponentsWithStats((_kd < 30).astype(np.uint8), 8)
pockets = np.zeros(a.shape, np.uint8)
for _i in range(1, _n):
    if 20 <= _st[_i][4] <= 5000 and _kd[_lab == _i].mean() < 16: pockets[_lab == _i] = 1
a = np.where(cv2.dilate(pockets, np.ones((3, 3), np.uint8)) > 0, 0, a).astype(np.uint8)
print("key pockets removed:", int(pockets.sum()), "px")
dist = cv2.distanceTransform((a > 0).astype(np.uint8), cv2.DIST_L2, 3)
key_d = np.minimum(np.abs(base.astype(np.float32) - bg[None, None, :]).sum(-1),       # cream key
                   np.abs(base.astype(np.float32) - 255.0).sum(-1))                     # and the generator's white halo
ring = dist <= 5.0
a = np.where(ring, (a * np.clip((key_d - 30) / 60.0, 0, 1)).astype(np.uint8), a)
a = cv2.GaussianBlur(a, (0, 0), 0.6)

for k, img in out.items():
    rgba = np.dstack([decontaminate(pal_pull(img, .12), a), a])[y0:y1, x0:x1]
    if k == "neutral": cv2.imwrite(f"{O}/mira_body.png", cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGRA))
    h, w = rgba.shape[:2]
    bust = rgba[0:int(h*0.42), int(w*0.12):int(w*0.92)]   # same box for every expression
    cv2.imwrite(f"{O}/portrait_mira_{k}.png", cv2.cvtColor(bust, cv2.COLOR_RGBA2BGRA))
