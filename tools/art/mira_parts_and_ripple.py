"""D-208: split Mira (locked base) into animatable cut-out parts, and a water ripple strip from the shared riverbed. OpenCV/numpy."""
import sys, json, numpy as np, cv2
M, O, PLATE, RIVER = sys.argv[1:5]
b = cv2.imread(M, cv2.IMREAD_UNCHANGED); H, W = b.shape[:2]
def poly(pts): m = np.zeros((H, W), np.uint8); cv2.fillPoly(m, [np.int32(pts)], 255); return m
alpha = b[..., 3]
staff = poly([(0,150),(80,150),(80,700),(125,700),(125,1000),(60,1000),(0,700)])
parts = {
 "head":       (poly([(60,0),(510,0),(510,262),(300,268),(210,262),(60,262)]) & ~staff, (300, 262)),   # pivot: neck
 "braid":      (poly([(305,262),(372,262),(378,470),(330,486),(300,400)]), (338, 266)),               # pivot: top of braid
 "scarf_tail": (poly([(420,305),(548,395),(548,500),(485,500),(445,420),(438,360)]), (425, 315)),     # pivot: scarf knot
}
out = {"canvas": [W, H], "parts": {}}
used = np.zeros((H, W), np.uint8)
for name, (m, piv) in parts.items():
    m = cv2.dilate(m & (alpha > 0).astype(np.uint8)*255, np.ones((5,5),np.uint8)) & ((alpha > 0)*255).astype(np.uint8)
    used |= m
    ys, xs = np.where(m > 0); x0, x1, y0, y1 = xs.min(), xs.max()+1, ys.min(), ys.max()+1
    p = b.copy(); p[..., 3] = np.minimum(alpha, m)
    cv2.imwrite(f"{O}/mira_part_{name}.png", p[y0:y1, x0:x1])
    out["parts"][name] = {"x": int(x0), "y": int(y0), "w": int(x1-x0), "h": int(y1-y0), "pivot": list(piv)}
core = cv2.erode(used, np.ones((7,7),np.uint8))     # body keeps a 3 px overlap under each part so no gap opens
body = b.copy(); body[..., 3] = np.where(core > 0, 0, alpha)
cv2.imwrite(f"{O}/mira_part_body.png", body)
out["parts"]["body"] = {"x": 0, "y": 0, "w": W, "h": H, "pivot": [W//2, H]}
# water ripple strip: bright strokes of the master water, inside the shared riverbed
pl = cv2.imread(PLATE); rv = cv2.imread(RIVER, 0) > 0
g = cv2.cvtColor(pl, cv2.COLOR_BGR2GRAY).astype(float); loc = cv2.GaussianBlur(g, (0,0), 9)
hi = np.clip((g - loc - 12) * 14, 0, 255); hi[~cv2.erode(rv.astype(np.uint8), np.ones((41,41),np.uint8)).astype(bool)] = 0
hi = cv2.GaussianBlur(hi, (0,0), 0.8)
ys, xs = np.where(hi > 8); x0, x1, y0, y1 = xs.min(), xs.max()+1, ys.min(), ys.max()+1
rgba = np.zeros((H2 := pl.shape[0], pl.shape[1], 4), np.uint8); rgba[..., :3] = (232, 246, 250); rgba[..., 3] = hi.astype(np.uint8)  # BGR of a pale river highlight
cv2.imwrite(f"{O}/water_ripple.png", rgba[y0:y1, x0:x1])
out["ripple"] = {"x": int(x0), "y": int(y0), "w": int(x1-x0), "h": int(y1-y0), "canvas": [pl.shape[1], pl.shape[0]]}
json.dump(out, open(f"{O}/parts.json", "w"), indent=1); print(json.dumps(out))
