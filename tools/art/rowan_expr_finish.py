"""D-224: Rowan expression plates — face-ellipse ECC onto locked ROWAN-FINISH-01; D-214 decontaminate/choke.
Usage: rowan_expr_finish.py <raw-dir> <out-dir>
Writes portrait_rowan_{neutral,glad,wistful,proud,curious}.png (neutral from base)."""
import sys, os, numpy as np, cv2
R, O = sys.argv[1], sys.argv[2]
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
INK = np.array([0x5A, 0x3E, 0x2B], float)
def pal_pull(rgb, k):
    f = rgb.reshape(-1,3).astype(float); d = ((f[:,None,:]-P[None])**2).sum(-1)
    return np.clip(f*(1-k)+P[d.argmin(1)]*k,0,255).reshape(rgb.shape).astype(np.uint8)
def grain(rgb, amp=0.04, seed=19):
    g = cv2.GaussianBlur(np.random.default_rng(seed).normal(0,1,rgb.shape[:2]),(0,0),0.9)
    return np.clip(rgb.astype(float)*(1+amp*g[...,None]),0,255).astype(np.uint8)
def reink(rgb, alpha):
    lab = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).astype(float)
    dark = (lab[...,0] < 55) & (alpha > 40)
    edge = cv2.Canny(cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY), 40, 120) > 0
    mask = ((dark & edge) | ((lab[...,0] < 40) & (alpha > 80))).astype(np.float32)
    mask = cv2.GaussianBlur(mask, (0,0), 0.8)[..., None]
    return np.clip(rgb.astype(float)*(1-0.72*mask) + INK[None,None,:]*(0.72*mask),0,255).astype(np.uint8)
ld = lambda n: cv2.cvtColor(cv2.imread(f"{R}/{n}.jpg"), cv2.COLOR_BGR2RGB)
base = ld("ROWAN-FINISH-01")
hsv = cv2.cvtColor(base, cv2.COLOR_RGB2HSV)
green = ((hsv[...,0] > 35) & (hsv[...,0] < 95) & (hsv[...,1] > 50) & (hsv[...,2] > 40)).astype(np.uint8)
fg = ((1 - green) * 255).astype(np.uint8)
fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, np.ones((5,5),np.uint8))
ff = (255-fg).copy(); m = np.zeros((ff.shape[0]+2, ff.shape[1]+2), np.uint8); cv2.floodFill(ff, m, (0,0), 128)
a = np.where(ff == 128, 0, 255).astype(np.uint8)
a = cv2.GaussianBlur(cv2.erode(a, np.ones((3,3),np.uint8)).astype(float), (0,0), 1.0).astype(np.uint8)
bg = np.array([0.,255.,0.])
ys, xs = np.where(a[:int(a.shape[0]*0.40)] > 8)
cx, cy = int(xs.mean()), int(ys.mean()+0.10*(ys.max()-ys.min()))
rx, ry = int(0.24*(xs.max()-xs.min())), int(0.30*(ys.max()-ys.min()))
FACE = (cx, cy, max(55,rx), max(48,ry)); print("FACE", FACE)
fm = np.zeros(base.shape[:2], np.float32); cv2.ellipse(fm, (FACE[0], FACE[1]), (FACE[2], FACE[3]), 0, 0, 360, 1, -1)
fm = cv2.GaussianBlur(fm, (0,0), 6)[..., None]
HEAD = np.zeros(base.shape[:2], np.uint8)
cv2.rectangle(HEAD, (max(0,cx-160), max(0,cy-180)), (min(base.shape[1]-1,cx+160), cy+110), 255, -1)
g0 = cv2.cvtColor(base, cv2.COLOR_RGB2GRAY).astype(np.float32)
out = {"neutral": base}
for k, n in [("glad","ROWAN-EXPR-GLAD-01"),("wistful","ROWAN-EXPR-WISTFUL-01"),
            ("proud","ROWAN-EXPR-PROUD-01"),("curious","ROWAN-EXPR-CURIOUS-01")]:
    e = ld(n)
    if e.shape[:2] != base.shape[:2]:
        e = cv2.resize(e, (base.shape[1], base.shape[0]), interpolation=cv2.INTER_AREA)
    W = np.eye(2,3,dtype=np.float32)
    try:
        _, W = cv2.findTransformECC(g0, cv2.cvtColor(e, cv2.COLOR_RGB2GRAY).astype(np.float32), W, cv2.MOTION_AFFINE,
                                    (cv2.TERM_CRITERIA_EPS|cv2.TERM_CRITERIA_COUNT, 200, 1e-6), HEAD, 5)
    except cv2.error as ex:
        print(k, "ecc fail", ex); W = np.eye(2,3,dtype=np.float32)
    er = cv2.warpAffine(e, W, (base.shape[1], base.shape[0]), flags=cv2.INTER_LINEAR|cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REPLICATE)
    out[k] = (er*fm + base*(1-fm)).astype(np.uint8)
    print(k, "ecc", np.round(W,3).tolist())
def decontaminate(rgb, alpha):
    f = rgb.astype(np.float32); al = (alpha.astype(np.float32) / 255.0)[..., None]
    semi = (al > 0.02) & (al < 0.98)
    un = np.clip((f - (1 - al) * bg[None, None, :]) / np.maximum(al, 0.02), 0, 255)
    f = np.where(semi, un, f)
    known = (alpha >= 250).astype(np.float32)
    acc = f * known[..., None]; wsum = known.copy()
    for r in (2, 4, 8, 16, 32, 64):
        ksz = 2 * r + 1
        acc_b = cv2.blur(acc, (ksz, ksz)); w_b = cv2.blur(wsum, (ksz, ksz))
        fill = (wsum < 0.5) & (w_b > 1e-4)
        acc[fill] = acc_b[fill] / w_b[fill][..., None]; wsum[fill] = 1.0
    f = np.where((alpha < 6)[..., None], acc, f)
    return np.clip(f, 0, 255).astype(np.uint8)
dist = cv2.distanceTransform((a > 0).astype(np.uint8), cv2.DIST_L2, 3)
key_d = np.abs(base.astype(np.float32) - bg[None,None,:]).sum(-1)
ring = dist <= 7.0
a = np.where(ring, (a * np.clip((key_d - 40) / 80.0, 0, 1)).astype(np.uint8), a)
a = cv2.GaussianBlur(a, (0, 0), 0.6)
ys, xs = np.where(a > 8); x0, x1, y0, y1 = xs.min(), xs.max()+1, ys.min(), ys.max()+1
os.makedirs(O, exist_ok=True)
for k, img in out.items():
    styled = grain(pal_pull(reink(img, a), 0.42))
    rgba = np.dstack([decontaminate(styled, a), a])[y0:y1, x0:x1]
    h, w = rgba.shape[:2]
    bust = rgba[0:int(h*0.40), int(w*0.08):int(w*0.92)]
    bh = ((bust.shape[0]+3)//4)*4; bw = ((bust.shape[1]+3)//4)*4
    bp = np.zeros((bh,bw,4), np.uint8); bp[:bust.shape[0], :bust.shape[1]] = bust
    cv2.imwrite(f"{O}/portrait_rowan_{k}.png", cv2.cvtColor(bp, cv2.COLOR_RGBA2BGRA))
    print(k, bp.shape)
print("wrote", O)
