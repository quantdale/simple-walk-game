"""Phase D trailhead finish pass (D-204): register/split/clean/palette-pull. PIL+numpy only."""
import sys, numpy as np
from PIL import Image, ImageFilter
R, O = sys.argv[1], sys.argv[2]
PAL = ["F5EBDD","3D4038","A89B88","858D72","83A878","527A5B","83B8C4","E5BA69","CB866C","DDA8AA","5A3E2B","3F6248","8E5240","C4BCAC","7A8070"]
P = np.array([[int(h[i:i+2],16) for i in (0,2,4)] for h in PAL], float)
def pal_pull(rgb, k=0.25):
    f = rgb.reshape(-1,3).astype(float)
    d = ((f[:,None,:]-P[None])**2).sum(-1); n = P[d.argmin(1)]
    return np.clip(f*(1-k)+n*k,0,255).reshape(rgb.shape).astype(np.uint8)
def load(n): return np.asarray(Image.open(f"{R}/{n}.jpg").convert("RGB"))
def grain(rgb, amp=0.04, seed=7):
    g = np.random.default_rng(seed).normal(0,1,rgb.shape[:2]); g = np.asarray(Image.fromarray(((g*40)+128).clip(0,255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)),float)-128
    return np.clip(rgb*(1+amp*g[...,None]/40),0,255).astype(np.uint8)
plates = {"S0":"TH-ENV-S0-01","S4":"TH-ENV-BASE-01"}
pl = {}
for k,n in plates.items():
    a = pal_pull(load(n)); pl[k]=a
    Image.fromarray(grain(a)).save(f"{O}/trailhead_ground_{k}.png")

S=1.25
ROI={ # polygons in 576x1024 preview coords (scaled x1.25 to 720x1280); hand-placed per stage
 "B0":[[(95,340),(255,340),(255,450),(95,450)],[(330,330),(520,330),(520,470),(330,470)],[(200,595),(285,595),(285,635),(200,635)],
       [(340,620),(435,620),(435,675),(340,675)],[(470,505),(520,505),(520,535),(470,535)],[(10,450),(120,450),(120,625),(10,625)],[(0,795),(220,795),(220,935),(0,935)]],
 "B1":[[(145,360),(435,360),(435,610),(145,610)],[(10,395),(120,395),(120,615),(10,615)],[(0,755),(225,755),(225,950),(0,950)]],
 "B2":[[(225,355),(365,355),(430,670),(145,670),(150,490)],[(15,390),(120,390),(120,600),(15,600)],[(0,770),(215,770),(215,930),(0,930)],[(120,675),(315,675),(315,785),(120,785)]],
 "B3":[[(235,315),(355,315),(460,560),(430,690),(160,690),(125,500)],[(15,395),(120,395),(120,600),(15,600)],[(5,665),(220,665),(220,815),(5,815)]],
 "B4":[[(235,315),(355,315),(465,520),(440,690),(165,690),(110,515)],[(15,390),(120,390),(120,600),(15,600)],[(5,615),(190,615),(190,770),(5,770)]],
}
from PIL import ImageDraw
PLATE={"B0":"S0","B1":"S0","B2":"S4","B3":"S4","B4":"S4"}
def layer(n, st):
    m = Image.new("L",(720,1280),0); d=ImageDraw.Draw(m)
    for poly in ROI[st]: d.polygon([(x*S,y*S) for x,y in poly], fill=255)
    roi = np.asarray(m.filter(ImageFilter.MaxFilter(9)))>0
    base = load(plates[PLATE[st]]).astype(float); img = load(n).astype(float)
    d = np.abs(np.asarray(Image.fromarray(img.astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5)),float)-np.asarray(Image.fromarray(base.astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5)),float)).sum(-1)
    k = Image.fromarray(((d>70)&roi).astype(np.uint8)*255)
    k = k.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MinFilter(5))  # close
    k = k.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))  # open (drop specks)
    m = Image.fromarray((np.asarray(k)*(np.asarray(m)>0)).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    return Image.fromarray(np.dstack([pal_pull(load(n),0.2), np.asarray(m)]),"RGBA")
for st,n in [("B0","TH-BR-B0-01"),("B1","TH-BR-B1-01"),("B2","TH-BR-B2-02"),("B3","TH-BR-B3-01"),("B4","TH-BR-B4-01")]:
    L=layer(n,st); bb=L.getchannel("A").getbbox(); L.save(f"{O}/trailhead_structures_{st}.png")
    open(f"{O}/trailhead_structures_{st}.offset","w").write("%d %d %d %d"%bb)
m = load("MIRA-BODY-01").astype(float); bg = np.median(np.concatenate([m[:8].reshape(-1,3),m[-8:].reshape(-1,3)]),0)
d = np.abs(m-bg).sum(-1); fg = (d>30).astype(np.uint8)*255
mk = Image.fromarray(fg).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
inv = Image.fromarray(255-np.asarray(mk)).copy(); ImageDraw.floodfill(inv,(0,0),128)
a = np.where(np.asarray(inv)==128,0,255).astype(np.uint8)
db = np.asarray(Image.fromarray(np.clip(d,0,255).astype(np.uint8)).filter(ImageFilter.BoxBlur(4)))
a[(d<14)&(db<12)] = 0  # enclosed background holes (staff/body gap)
a = np.asarray(Image.fromarray(a).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1)))
rgba = Image.fromarray(np.dstack([pal_pull(m.astype(np.uint8),0.12),a]),"RGBA")
bb = rgba.getchannel("A").getbbox(); body = rgba.crop(bb); body.save(f"{O}/mira_body.png")
w,h = body.size; port = body.crop((int(w*0.15),0,int(w*0.95),int(h*0.40))); port.save(f"{O}/portrait_mira_neutral.png")
print("ok", bb, body.size, port.size)
