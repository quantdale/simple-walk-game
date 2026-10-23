"""D-205 finish pass v2: dense-flow registration to the master plate + hysteresis alpha cutouts. OpenCV/numpy/PIL (free)."""
import sys, json, numpy as np, cv2
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
    f = dis.calc(ga, gb, None)
    return cv2.GaussianBlur(f, (0,0), sigma)
def warp(img, flow):
    h, w = flow.shape[:2]; gx, gy = np.meshgrid(np.arange(w), np.arange(h))
    return cv2.remap(img, (gx+flow[...,0]).astype(np.float32), (gy+flow[...,1]).astype(np.float32), cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT)
def residual(a, b):
    f = smooth_flow(a, b, 15); m = np.hypot(f[...,0], f[...,1])[350:1050, 40:680]  # banks + river (static geometry)
    return float(np.median(m)), float(np.percentile(m, 95))
master = load("TH-ENV-BASE-01")
s0raw = load("TH-ENV-S0-01")
before = residual(master, s0raw)
def affine_from_flow(a, b):
    f = smooth_flow(a, b, 8); h, w = f.shape[:2]
    ys, xs = np.mgrid[300:1000:20, 40:680:20]; src = np.stack([xs.ravel(), ys.ravel()], 1).astype(np.float32)
    dst = src + f[ys.ravel(), xs.ravel()]
    M, _ = cv2.estimateAffinePartial2D(src, dst, method=cv2.RANSAC, ransacReprojThreshold=3)
    return M
M0 = affine_from_flow(master, s0raw)
s0 = cv2.warpAffine(s0raw, M0, (master.shape[1], master.shape[0]), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REFLECT)
after = residual(master, s0)
# Shared static riverbed (style guide §2.3.4): hand-traced shoreline on the master camera; inside it every stage
# uses the master's water geometry, re-graded to the stage-0 murky ramp (Lab mean/std transfer from S0 water).
SHORE = [(0,232),(60,228),(150,230),(190,236),(230,232),(280,240),(330,252),(360,258),(360,385),(320,392),(290,405),(240,420),(200,425),(160,410),(120,390),(80,370),(40,345),(0,320)]
river = np.zeros(master.shape[:2], np.uint8); cv2.fillPoly(river, [np.int32([(x*2, y*2) for x, y in SHORE])], 255)
cv2.imwrite(f"{O}/riverbed_mask.png", river)
inner = cv2.erode(river, np.ones((21,21),np.uint8)) > 0
L0 = cv2.cvtColor(s0, cv2.COLOR_RGB2LAB).astype(float); L4 = cv2.cvtColor(master, cv2.COLOR_RGB2LAB).astype(float)
mu0, sd0 = L0[inner].mean(0), L0[inner].std(0); mu4, sd4 = L4[inner].mean(0), L4[inner].std(0)
murky = cv2.cvtColor(np.clip((L4-mu4)/sd4*sd0+mu0, 0, 255).astype(np.uint8), cv2.COLOR_LAB2RGB)
fa = cv2.GaussianBlur(river.astype(float)/255, (0,0), 3)[..., None]
s0 = (murky*fa + s0*(1-fa)).astype(np.uint8)
plates = {"S0": s0, "S4": master}
for k, v in plates.items(): cv2.imwrite(f"{O}/trailhead_ground_{k}.png", cv2.cvtColor(grain(pal_pull(v, .25)), cv2.COLOR_RGB2BGR))
SCALE = 2484/720  # master paint canvas width / shipped width (style guide §2.2)
rep = {"S0_affine_to_master": M0.tolist(), "plate_S0_vs_master_px_shipped": {"before": before, "after": after},
       "plate_S0_vs_master_px_at_master": {"before": [x*SCALE for x in before], "after": [x*SCALE for x in after]}}
S=1.25
ROI={"B0":[[(90,330),(260,330),(260,455),(90,455)],[(325,325),(525,325),(525,475),(325,475)],[(195,590),(290,590),(290,640),(195,640)],
       [(335,615),(440,615),(440,680),(335,680)],[(465,500),(525,500),(525,540),(465,540)],[(5,445),(125,445),(125,630),(5,630)],[(0,790),(225,790),(225,940),(0,940)]],
 "B1":[[(140,355),(440,355),(440,615),(140,615)],[(5,390),(125,390),(125,620),(5,620)],[(0,750),(230,750),(230,955),(0,955)]],
 "B2":[[(220,350),(370,350),(435,675),(140,675),(145,490)],[(10,385),(125,385),(125,605),(10,605)],[(0,765),(220,765),(220,935),(0,935)],[(115,670),(320,670),(320,790),(115,790)]],
 "B3":[[(230,310),(360,310),(465,560),(435,695),(155,695),(120,500)],[(10,390),(125,390),(125,605),(10,605)],[(0,660),(225,660),(225,820),(0,820)]],
 "B4":[[(230,310),(360,310),(470,520),(445,695),(160,695),(105,515)],[(10,385),(125,385),(125,605),(10,605)],[(0,610),(195,610),(195,775),(0,775)]]}
PL = {"B0":"S0","B1":"S0","B2":"S4","B3":"S4","B4":"S4"}
SRC = {"B0":"TH-BR-B0-01","B1":"TH-BR-B1-01","B2":"TH-BR-B2-02","B3":"TH-BR-B3-01","B4":"TH-BR-B4-01"}
def lab(x): return cv2.cvtColor(cv2.GaussianBlur(x,(0,0),1.5), cv2.COLOR_RGB2LAB).astype(float)
for st, n in SRC.items():
    plate = plates[PL[st]]; raw = load(n)
    img = warp(raw, smooth_flow(plate, raw, 40))          # register the layer to its (registered) plate
    roi = np.zeros(plate.shape[:2], np.uint8)
    for poly in ROI[st]: cv2.fillPoly(roi, [np.int32([(x*S, y*S) for x, y in poly])], 255)
    roi = cv2.dilate(roi, np.ones((15,15),np.uint8))
    d = np.sqrt(((lab(img)-lab(plate))**2).sum(-1))
    hsv = cv2.cvtColor(img, cv2.COLOR_RGB2HSV)
    if PL[st] == "S4": d[((hsv[...,0] > 28) & (hsv[...,0] < 125) & (hsv[...,1] > 32))] = 0   # grass, leaves and water stay with the plate
    strong = ((d > 40) & (roi > 0)).astype(np.uint8); weak = ((d > 24) & (roi > 0)).astype(np.uint8)
    n_, lbl, stats, _ = cv2.connectedComponentsWithStats(weak, 8)
    keep = np.zeros(n_, bool); keep[np.unique(lbl[strong > 0])] = True; keep[0] = False
    keep &= stats[:, cv2.CC_STAT_AREA] > 120
    m = keep[lbl].astype(np.uint8)*255
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(9,9)))
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(5,5)))
    inv = (m == 0).astype(np.uint8); n2, l2, s2, _ = cv2.connectedComponentsWithStats(inv, 4)
    small = np.zeros(n2, bool); small[1:] = s2[1:, cv2.CC_STAT_AREA] < 300; m[small[l2]] = 255   # fill only pin-holes, keep water between posts
    m = cv2.erode(m, np.ones((3,3),np.uint8))                           # 1 px choke
    a = cv2.GaussianBlur(m.astype(float), (0,0), 1.6)                  # feather
    a[roi == 0] = 0
    rgba = np.dstack([pal_pull(img, .2), a.clip(0,255).astype(np.uint8)])
    cv2.imwrite(f"{O}/trailhead_structures_{st}.png", cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGRA))
    rep[st] = {"layer_vs_plate_px_shipped": residual(plate, img)}
json.dump(rep, open(f"{O}/registration.json","w"), indent=1); print(json.dumps(rep))
