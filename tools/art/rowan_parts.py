"""D-222: split Rowan into head, fern, body cut-outs for bone rig. OpenCV/numpy."""
import sys, json, numpy as np, cv2
M, O = sys.argv[1:3]
b = cv2.imread(M, cv2.IMREAD_UNCHANGED)
pad = (-b.shape[0]) % 4
if pad: b = np.pad(b, ((0,pad),(0,0),(0,0)), mode='constant')
padw = (-b.shape[1]) % 4
if padw: b = np.pad(b, ((0,0),(0,padw),(0,0)), mode='constant')
H, W = b.shape[:2]; alpha = b[..., 3]
def poly(pts): m = np.zeros((H, W), np.uint8); cv2.fillPoly(m, [np.int32(pts)], 255); return m
# estimate head from upper opaque bbox
ys, xs = np.where(alpha[:int(H*0.42)] > 8)
cx = int(xs.mean()); cy = int(ys.mean())
# head through neck; fern sprig on viewer's right of bun; body rest
hx0, hx1 = max(0, cx-int(W*0.28)), min(W, cx+int(W*0.32))
hy1 = int(H*0.32)
parts = {
 "head": (poly([(hx0,0),(hx1,0),(hx1,hy1),(int(cx+W*0.05),int(H*0.36)),(int(cx-W*0.05),int(H*0.36)),(hx0,hy1)]), (cx, int(H*0.30))),
 "fern": (poly([(int(cx+W*0.12),int(H*0.02)),(int(cx+W*0.38),int(H*0.02)),(int(cx+W*0.40),int(H*0.22)),(int(cx+W*0.18),int(H*0.24))]), (int(cx+W*0.22), int(H*0.12))),
}
out = {"canvas": [W, H], "parts": {}}
used = np.zeros((H, W), np.uint8)
for name, (m, piv) in parts.items():
    m = cv2.dilate(m & (alpha > 0).astype(np.uint8)*255, np.ones((5,5),np.uint8)) & ((alpha > 0)*255).astype(np.uint8)
    used |= m
    ys, xs = np.where(m > 0)
    if len(xs)==0: continue
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()+1), int(ys.min()), int(ys.max()+1)
    p = b.copy(); p[..., 3] = np.minimum(alpha, m)
    pw, ph = ((x1-x0+3)//4)*4, ((y1-y0+3)//4)*4
    rgb = cv2.copyMakeBorder(p[y0:y1, x0:x1,:3], 0, ph-(y1-y0), 0, pw-(x1-x0), cv2.BORDER_REPLICATE)
    aa = cv2.copyMakeBorder(p[y0:y1, x0:x1,3], 0, ph-(y1-y0), 0, pw-(x1-x0), cv2.BORDER_CONSTANT, value=0)
    crop = np.dstack([rgb, aa])
    cv2.imwrite(f"{O}/rowan_part_{name}.png", crop)
    out["parts"][name] = {"x": x0, "y": y0, "w": int(x1-x0), "h": int(y1-y0), "pivot": list(piv), "tex_w": pw, "tex_h": ph}
core = cv2.erode(used, np.ones((7,7),np.uint8))
body = b.copy(); body[..., 3] = np.where(core > 0, 0, alpha)
cv2.imwrite(f"{O}/rowan_part_body.png", body)
out["parts"]["body"] = {"x": 0, "y": 0, "w": W, "h": H, "pivot": [W//2, H]}
json.dump(out, open(f"{O}/parts.json","w"), indent=1); print(json.dumps(out))
