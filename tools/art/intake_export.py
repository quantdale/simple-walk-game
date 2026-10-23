"""D-216: copies the River Intake finish-pass outputs into Resources/Art/Intake. Portraits are letterboxed to 460×460
(same box as Mira's), the body is padded to multiples of 4 at the top/right (block compression); padding repeats the edge
colour with alpha 0 (D-214 edge bleed), so filtering never pulls a key colour into the silhouette."""
import sys, glob, os, numpy as np, cv2
S, D = sys.argv[1], sys.argv[2]
def pad(img, top, bottom, left, right):
    rgb = cv2.copyMakeBorder(img[..., :3], top, bottom, left, right, cv2.BORDER_REPLICATE)
    a = cv2.copyMakeBorder(img[..., 3], top, bottom, left, right, cv2.BORDER_CONSTANT, value=0)
    return np.dstack([rgb, a])
for f in sorted(glob.glob(f"{S}/*.png")):
    n = os.path.basename(f)
    if n == "riverbed_mask.png": continue
    img = cv2.imread(f, cv2.IMREAD_UNCHANGED)
    if img.shape[2] == 3: cv2.imwrite(f"{D}/{n}", img); continue
    h, w = img.shape[:2]
    if n.startswith("portrait_"):
        s = 460; sc = min(s / w, s / h); img = cv2.resize(img, (round(w * sc), round(h * sc)), interpolation=cv2.INTER_AREA); h, w = img.shape[:2]
        l = (s - w) // 2; img = pad(img, s - h, 0, l, s - w - l)
    else:
        img = pad(img, (-h) % 4, 0, 0, (-w) % 4)
    cv2.imwrite(f"{D}/{n}", img); print(n, img.shape)
