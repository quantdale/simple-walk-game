"""D-216 River Intake finish pass (direction A): S0 plate registered to the base plate (RANSAC similarity from dense
flow), shared riverbed re-graded per stage, gate layers G0/G2 cut out against their plate (Lab difference, hysteresis,
ROI), palette pull + grain. Reuses the D-205 helpers. OpenCV/numpy/PIL only (free)."""
import sys, json, numpy as np, cv2
sys.argv = [sys.argv[0], sys.argv[1], sys.argv[2]]
R, O = sys.argv[1], sys.argv[2]
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
def pal_pull(rgb, k):
    f = rgb.reshape(-1,3).astype(float); d = ((f[:,None,:]-P[None])**2).sum(-1)
    return np.clip(f*(1-k)+P[d.argmin(1)]*k,0,255).reshape(rgb.shape).astype(np.uint8)
def grain(rgb, amp=0.04):
    g = cv2.GaussianBlur(np.random.default_rng(7).normal(0,1,rgb.shape[:2]),(0,0),0.8)
    return np.clip(rgb*(1+amp*g[...,None]),0,255).astype(np.uint8)
def load(n): return cv2.cvtColor(cv2.imread(f"{R}/{n}.jpg"), cv2.COLOR_BGR2RGB)
dis = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
def smooth_flow(a, b, sigma=25):
    ga, gb = [cv2.equalizeHist(cv2.cvtColor(x, cv2.COLOR_RGB2GRAY)) for x in (a, b)]
    return cv2.GaussianBlur(dis.calc(ga, gb, None), (0,0), sigma)
def warp(img, flow):
    h, w = flow.shape[:2]; gx, gy = np.meshgrid(np.arange(w), np.arange(h))
    return cv2.remap(img, (gx+flow[...,0]).astype(np.float32), (gy+flow[...,1]).astype(np.float32), cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT)
def residual(a, b):
    f = smooth_flow(a, b, 15); m = np.hypot(f[...,0], f[...,1])[450:1250, 0:720]   # banks, path, river (static geometry)
    return float(np.median(m)), float(np.percentile(m, 95))
master = load("RI-ENV-BASE-01"); s0raw = load("RI-ENV-S0-01")
before = residual(master, s0raw)
f = smooth_flow(master, s0raw, 8)
ys, xs = np.mgrid[420:1260:20, 20:700:20]; src = np.stack([xs.ravel(), ys.ravel()], 1).astype(np.float32)
M0, _ = cv2.estimateAffinePartial2D(src, src + f[ys.ravel(), xs.ravel()], method=cv2.RANSAC, ransacReprojThreshold=3)
s0 = cv2.warpAffine(s0raw, M0, (720, 1280), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REFLECT)
s0 = warp(s0, smooth_flow(master, s0, 40))           # residual drift: very smooth dense correction
after = residual(master, s0)
# Shared riverbed (style guide §2.3.4): water mask from the master; inside it every stage uses the master's water
# geometry re-graded to the stage-0 murky ramp (Lab mean/std transfer from S0 water), so the crossfade never ghosts.
hsv = cv2.cvtColor(master, cv2.COLOR_RGB2HSV)
w = ((hsv[...,0] > 85) & (hsv[...,0] < 115) & (hsv[...,1] > 30)).astype(np.uint8)*255
w[:240] = 0
w = cv2.morphologyEx(w, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(25,25)))
n_, lbl, st, _ = cv2.connectedComponentsWithStats(w, 8)
river = ((lbl == 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))) * 255).astype(np.uint8)
inner = cv2.erode(river, np.ones((21,21),np.uint8)) > 0
hs0 = cv2.cvtColor(s0, cv2.COLOR_RGB2HSV); s0w = inner & (hs0[...,1] < 90)
L0 = cv2.cvtColor(s0, cv2.COLOR_RGB2LAB).astype(float); L4 = cv2.cvtColor(master, cv2.COLOR_RGB2LAB).astype(float)
mu0, sd0 = L0[s0w].mean(0), L0[s0w].std(0); mu4, sd4 = L4[inner].mean(0), L4[inner].std(0)
murky = cv2.cvtColor(np.clip((L4-mu4)/sd4*sd0+mu0, 0, 255).astype(np.uint8), cv2.COLOR_LAB2RGB)
fa = cv2.GaussianBlur(river.astype(float)/255, (0,0), 3)[..., None]
s0 = (murky*fa + s0*(1-fa)).astype(np.uint8)
cv2.imwrite(f"{O}/riverbed_mask.png", river)
plates = {"S0": s0, "S4": master}
for k, v in plates.items(): cv2.imwrite(f"{O}/intake_ground_{k}.png", cv2.cvtColor(grain(pal_pull(v, .25)), cv2.COLOR_RGB2BGR))
rep = {"S0_affine_to_master": M0.tolist(), "plate_S0_vs_master_px": {"before": before, "after": after}}
ROI = {"G0": [(330,500),(560,470),(720,560),(720,1010),(520,1000),(360,900)], "G2": [(340,470),(720,430),(720,960),(560,930),(380,860)]}
SRC = {"G0": "RI-GT-G0-01", "G2": "RI-GT-G2-01"}; PL = {"G0": "S0", "G2": "S4"}
def lab(x): return cv2.cvtColor(cv2.GaussianBlur(x,(0,0),1.5), cv2.COLOR_RGB2LAB).astype(float)
for g, n in SRC.items():
    plate_ref = master; raw = load(n)
    img = warp(raw, smooth_flow(plate_ref, raw, 40))  # the gate frames were painted over the base plate: register to it
    roi = np.zeros((1280,720), np.uint8); cv2.fillPoly(roi, [np.int32(ROI[g])], 255)
    d = np.sqrt(((lab(img)-lab(plate_ref))**2).sum(-1))
    h2 = cv2.cvtColor(img, cv2.COLOR_RGB2HSV)
    d[((h2[...,0] > 28) & (h2[...,0] < 80) & (h2[...,1] > 40))] *= 0.4   # grass stays with the plate
    strong = ((d > 40) & (roi > 0)).astype(np.uint8); weak = ((d > 22) & (roi > 0)).astype(np.uint8)
    n_, lbl, stats, _ = cv2.connectedComponentsWithStats(weak, 8)
    keep = np.zeros(n_, bool); keep[np.unique(lbl[strong > 0])] = True; keep[0] = False
    keep &= stats[:, cv2.CC_STAT_AREA] > 150
    if keep.any(): keep &= stats[:, cv2.CC_STAT_AREA] >= max(1500, 0.08 * stats[keep, cv2.CC_STAT_AREA].max())   # no speckle islands
    m = keep[lbl].astype(np.uint8)*255
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(9,9)))
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(5,5)))
    inv = (m == 0).astype(np.uint8); n2, l2, s2, _ = cv2.connectedComponentsWithStats(inv, 4)
    small = np.zeros(n2, bool); small[1:] = s2[1:, cv2.CC_STAT_AREA] < 300; m[small[l2]] = 255
    m = cv2.erode(m, np.ones((3,3),np.uint8))
    a = cv2.GaussianBlur(m.astype(float), (0,0), 1.6)
    a *= cv2.GaussianBlur(cv2.erode(roi, np.ones((31,31),np.uint8)).astype(float)/255, (0,0), 10)   # soft ROI edge, never a straight cut
    col = pal_pull(img, .2)
    if g == "G0":   # weathered: pulled toward the forgotten plate's grey ramp
        hs = cv2.cvtColor(col, cv2.COLOR_RGB2HSV).astype(float); hs[...,1] *= 0.6; col = cv2.cvtColor(hs.astype(np.uint8), cv2.COLOR_HSV2RGB)
    rgba = np.dstack([col, a.clip(0,255).astype(np.uint8)])
    cv2.imwrite(f"{O}/intake_gate_{g}.png", cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGRA))
    rep[g] = {"layer_vs_base_px": residual(plate_ref, img), "alpha_px": int((a > 128).sum())}
json.dump(rep, open(f"{O}/registration.json","w"), indent=1); print(json.dumps(rep))
