"""D-217: split Tavi (locked BODY-02) into cut-out parts for the bone rig: head, bandana_tail, body. OpenCV/numpy."""
import sys, json, numpy as np, cv2
M, O = sys.argv[1:3]
b = cv2.imread(M, cv2.IMREAD_UNCHANGED)
# pad height to multiple of 4
pad = (-b.shape[0]) % 4
if pad:
    b = np.pad(b, ((0,pad),(0,0),(0,0)), mode='constant')
H, W = b.shape[:2]; alpha = b[..., 3]
def poly(pts): m = np.zeros((H, W), np.uint8); cv2.fillPoly(m, [np.int32(pts)], 255); return m
# head through neck (pivot at neck centre); bandana tails on the viewer's right; body is the rest
parts = {
 "head": (poly([(140,0),(500,0),(510,250),(480,300),(360,310),(240,300),(160,250)]), (360, 300)),
 "bandana_tail": (poly([(400,250),(520,280),(528,400),(470,420),(420,360),(405,300)]), (430, 280)),
}
out = {"canvas": [W, H], "parts": {}}
used = np.zeros((H, W), np.uint8)
for name, (m, piv) in parts.items():
    m = cv2.dilate(m & (alpha > 0).astype(np.uint8)*255, np.ones((5,5),np.uint8)) & ((alpha > 0)*255).astype(np.uint8)
    used |= m
    ys, xs = np.where(m > 0); x0, x1, y0, y1 = int(xs.min()), int(xs.max()+1), int(ys.min()), int(ys.max()+1)
    p = b.copy(); p[..., 3] = np.minimum(alpha, m)
    # pad crop to multiples of 4
    pw, ph = ((x1-x0+3)//4)*4, ((y1-y0+3)//4)*4
    crop = np.zeros((ph, pw, 4), np.uint8); crop[:y1-y0, :x1-x0] = p[y0:y1, x0:x1]
    # BORDER_REPLICATE rgb with alpha 0 in pad
    if pw > x1-x0 or ph > y1-y0:
        rgb = cv2.copyMakeBorder(p[y0:y1, x0:x1,:3], 0, ph-(y1-y0), 0, pw-(x1-x0), cv2.BORDER_REPLICATE)
        aa = cv2.copyMakeBorder(p[y0:y1, x0:x1,3], 0, ph-(y1-y0), 0, pw-(x1-x0), cv2.BORDER_CONSTANT, value=0)
        crop = np.dstack([rgb, aa])
    cv2.imwrite(f"{O}/tavi_part_{name}.png", crop)
    out["parts"][name] = {"x": x0, "y": y0, "w": int(x1-x0), "h": int(y1-y0), "pivot": list(piv), "tex_w": pw, "tex_h": ph}
core = cv2.erode(used, np.ones((7,7),np.uint8))
body = b.copy(); body[..., 3] = np.where(core > 0, 0, alpha)
cv2.imwrite(f"{O}/tavi_part_body.png", body)
out["parts"]["body"] = {"x": 0, "y": 0, "w": W, "h": H, "pivot": [W//2, H]}
json.dump(out, open(f"{O}/parts.json","w"), indent=1); print(json.dumps(out))
