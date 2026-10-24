"""D-222 Rowan finish: chroma-key ROWAN-FINISH-01, palette pull, grain, decontaminate, portrait. OpenCV/numpy."""
import sys, os, numpy as np, cv2
R, O = sys.argv[1], sys.argv[2]
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
INK = np.array([0x5A, 0x3E, 0x2B], float)
def pal_pull(rgb, k):
    f = rgb.reshape(-1,3).astype(float); d = ((f[:,None,:]-P[None])**2).sum(-1)
    return np.clip(f*(1-k)+P[d.argmin(1)]*k,0,255).reshape(rgb.shape).astype(np.uint8)
def grain(rgb, amp=0.045, seed=17):
    g = cv2.GaussianBlur(np.random.default_rng(seed).normal(0,1,rgb.shape[:2]),(0,0),0.9)
    return np.clip(rgb.astype(float)*(1+amp*g[...,None]),0,255).astype(np.uint8)
def reink(rgb, alpha):
    lab = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).astype(float)
    dark = (lab[...,0] < 55) & (alpha > 40)
    edge = cv2.Canny(cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY), 40, 120) > 0
    mask = ((dark & edge) | ((lab[...,0] < 40) & (alpha > 80))).astype(np.float32)
    mask = cv2.GaussianBlur(mask, (0,0), 0.8)[..., None]
    return np.clip(rgb.astype(float)*(1-0.72*mask) + INK[None,None,:]*(0.72*mask),0,255).astype(np.uint8)
os.makedirs(O, exist_ok=True)
# prefer chroma green finish; fall back to cream body
path = f"{R}/ROWAN-FINISH-01.jpg"
base = cv2.cvtColor(cv2.imread(path), cv2.COLOR_BGR2RGB)
hsv = cv2.cvtColor(base, cv2.COLOR_RGB2HSV)
green = ((hsv[...,0] > 35) & (hsv[...,0] < 95) & (hsv[...,1] > 50) & (hsv[...,2] > 40)).astype(np.uint8)
fg = ((1 - green) * 255).astype(np.uint8)
fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, np.ones((5,5),np.uint8))
ff = (255-fg).copy(); m = np.zeros((ff.shape[0]+2, ff.shape[1]+2), np.uint8); cv2.floodFill(ff, m, (0,0), 128)
a = np.where(ff == 128, 0, 255).astype(np.uint8)
a = cv2.GaussianBlur(cv2.erode(a, np.ones((3,3),np.uint8)).astype(float), (0,0), 1.0).astype(np.uint8)
bg = np.array([0.,255.,0.])
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
dist = cv2.distanceTransform((a > 0).astype(np.uint8), cv2.DIST_L2, 3)
key_d = np.abs(base.astype(np.float32) - bg[None,None,:]).sum(-1)
ring = dist <= 7.0
a = np.where(ring, (a * np.clip((key_d - 40) / 80.0, 0, 1)).astype(np.uint8), a)
a = cv2.GaussianBlur(a, (0, 0), 0.6)
ys, xs = np.where(a > 8); x0, x1, y0, y1 = xs.min(), xs.max()+1, ys.min(), ys.max()+1
styled = grain(pal_pull(reink(base, a), 0.42))
rgba = np.dstack([decontaminate(styled, a), a])[y0:y1, x0:x1]
# pad to multiples of 4
ph = ((rgba.shape[0]+3)//4)*4; pw = ((rgba.shape[1]+3)//4)*4
pad = np.zeros((ph, pw, 4), np.uint8); pad[:rgba.shape[0], :rgba.shape[1]] = rgba
cv2.imwrite(f"{O}/rowan_body.png", cv2.cvtColor(pad, cv2.COLOR_RGBA2BGRA))
h, w = pad.shape[:2]
bust = pad[0:int(h*0.40), int(w*0.08):int(w*0.92)]
bh = ((bust.shape[0]+3)//4)*4; bw = ((bust.shape[1]+3)//4)*4
bp = np.zeros((bh,bw,4), np.uint8); bp[:bust.shape[0], :bust.shape[1]] = bust
cv2.imwrite(f"{O}/portrait_rowan_neutral.png", cv2.cvtColor(bp, cv2.COLOR_RGBA2BGRA))
print("rowan", pad.shape, "portrait", bp.shape, "alpha%", int((pad[...,3]>8).mean()*100))
