"""D-222 Canopy Grove finish: S0 registered to BASE, timber/walkway cutouts, orchids chroma-key. OpenCV/numpy."""
import sys, json, numpy as np, cv2
R, O = sys.argv[1], sys.argv[2]
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
def pal_pull(rgb, k):
    f = rgb.reshape(-1,3).astype(float); d = ((f[:,None,:]-P[None])**2).sum(-1)
    return np.clip(f*(1-k)+P[d.argmin(1)]*k,0,255).reshape(rgb.shape).astype(np.uint8)
def grain(rgb, amp=0.04, seed=13):
    g = cv2.GaussianBlur(np.random.default_rng(seed).normal(0,1,rgb.shape[:2]),(0,0),0.8)
    return np.clip(rgb.astype(float)*(1+amp*g[...,None]),0,255).astype(np.uint8)
def load(n): return cv2.cvtColor(cv2.imread(f"{R}/{n}.jpg"), cv2.COLOR_BGR2RGB)
dis = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
def smooth_flow(a, b, sigma=25):
    ga, gb = [cv2.equalizeHist(cv2.cvtColor(x, cv2.COLOR_RGB2GRAY)) for x in (a, b)]
    return cv2.GaussianBlur(dis.calc(ga, gb, None), (0,0), sigma)
def warp(img, flow):
    h, w = flow.shape[:2]; gx, gy = np.meshgrid(np.arange(w), np.arange(h))
    return cv2.remap(img, (gx+flow[...,0]).astype(np.float32), (gy+flow[...,1]).astype(np.float32), cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT)
import os; os.makedirs(O, exist_ok=True)
master = load("CG-ENV-BASE-01"); s0raw = load("CG-ENV-S0-01")
f = smooth_flow(master, s0raw, 8)
ys, xs = np.mgrid[200:1200:20, 20:700:20]; src = np.stack([xs.ravel(), ys.ravel()], 1).astype(np.float32)
M0, _ = cv2.estimateAffinePartial2D(src, src + f[ys.ravel(), xs.ravel()], method=cv2.RANSAC, ransacReprojThreshold=3)
s0 = cv2.warpAffine(s0raw, M0, (720, 1280), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REFLECT)
s0 = warp(s0, smooth_flow(master, s0, 40))
cv2.imwrite(f"{O}/canopy_ground_S0.png", cv2.cvtColor(grain(pal_pull(s0, .25)), cv2.COLOR_RGB2BGR))
cv2.imwrite(f"{O}/canopy_ground_S4.png", cv2.cvtColor(grain(pal_pull(master, .25)), cv2.COLOR_RGB2BGR))
def lab(x): return cv2.cvtColor(cv2.GaussianBlur(x,(0,0),1.5), cv2.COLOR_RGB2LAB).astype(float)
ROI = {
 "T0": [(80,520),(640,480),(700,900),(40,950)],
 "W3": [(40,380),(420,360),(480,900),(20,920)],
}
SRC = {"T0": "CG-TIMBER-T0-01", "W3": "CG-WALK-T3-01"}
PLATE = {"T0": s0, "W3": master}
rep = {}
for g, n in SRC.items():
    plate_ref = PLATE[g]; raw = load(n)
    img = warp(raw, smooth_flow(plate_ref, raw, 40))
    roi = np.zeros((1280,720), np.uint8); cv2.fillPoly(roi, [np.int32(ROI[g])], 255)
    d = np.sqrt(((lab(img)-lab(plate_ref))**2).sum(-1))
    thr_s, thr_w = (38, 20) if g == "T0" else (32, 18)
    strong = ((d > thr_s) & (roi > 0)).astype(np.uint8); weak = ((d > thr_w) & (roi > 0)).astype(np.uint8)
    n_, lbl, stats, _ = cv2.connectedComponentsWithStats(weak, 8)
    keep = np.zeros(n_, bool); keep[np.unique(lbl[strong > 0])] = True; keep[0] = False
    keep &= stats[:, cv2.CC_STAT_AREA] > 200
    if keep.any(): keep &= stats[:, cv2.CC_STAT_AREA] >= max(1200, 0.06 * stats[keep, cv2.CC_STAT_AREA].max())
    m = keep[lbl].astype(np.uint8)*255
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(11,11)))
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(5,5)))
    inv = (m == 0).astype(np.uint8); n2, l2, s2, _ = cv2.connectedComponentsWithStats(inv, 4)
    small = np.zeros(n2, bool); small[1:] = s2[1:, cv2.CC_STAT_AREA] < 400; m[small[l2]] = 255
    m = cv2.erode(m, np.ones((3,3),np.uint8))
    a = cv2.GaussianBlur(m.astype(float), (0,0), 1.6)
    a *= cv2.GaussianBlur(cv2.erode(roi, np.ones((31,31),np.uint8)).astype(float)/255, (0,0), 10)
    col = pal_pull(img, .2)
    rgba = np.dstack([col, a.clip(0,255).astype(np.uint8)])
    name = "canopy_timber_T0.png" if g == "T0" else "canopy_walkway_W3.png"
    cv2.imwrite(f"{O}/{name}", cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGRA))
    rep[g] = {"alpha_px": int((a > 128).sum())}
# orchids chroma key
orch = load("CG-ORCHIDS-01")
hsv = cv2.cvtColor(orch, cv2.COLOR_RGB2HSV)
# green key: hue ~60, high sat
green = ((hsv[...,0] > 35) & (hsv[...,0] < 95) & (hsv[...,1] > 60) & (hsv[...,2] > 40)).astype(np.uint8)
fg = (1 - green) * 255
fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, np.ones((5,5),np.uint8))
ff = (255-fg).copy(); mask = np.zeros((ff.shape[0]+2, ff.shape[1]+2), np.uint8); cv2.floodFill(ff, mask, (0,0), 128)
a = np.where(ff == 128, 0, 255).astype(np.uint8)
a = cv2.GaussianBlur(cv2.erode(a, np.ones((3,3),np.uint8)).astype(float), (0,0), 0.8).astype(np.uint8)
ys, xs = np.where(a > 8); x0,x1,y0,y1 = xs.min(), xs.max()+1, ys.min(), ys.max()+1
# decontaminate vs green
bg = np.array([0,255,0], float)
f = orch.astype(np.float32); al = (a.astype(np.float32)/255)[...,None]
semi = (al>0.02)&(al<0.98); un = np.clip((f-(1-al)*bg)/(np.maximum(al,0.02)),0,255)
f = np.where(semi, un, f)
styled = grain(pal_pull(np.clip(f,0,255).astype(np.uint8), .3))
rgba = np.dstack([styled, a])[y0:y1, x0:x1]
# pad to 4
ph = ((rgba.shape[0]+3)//4)*4; pw = ((rgba.shape[1]+3)//4)*4
pad = np.zeros((ph,pw,4), np.uint8); pad[:rgba.shape[0], :rgba.shape[1]] = rgba
cv2.imwrite(f"{O}/orchids.png", cv2.cvtColor(pad, cv2.COLOR_RGBA2BGRA))
rep["orchids"] = {"bbox": [int(x0),int(y0),int(x1),int(y1)], "size": list(pad.shape[:2])}
json.dump(rep, open(f"{O}/registration.json","w"), indent=1); print(json.dumps(rep))
