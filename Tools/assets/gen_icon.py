from PIL import Image, ImageDraw, ImageFilter, ImageFont
import math, os, sys
S = 1024
out = sys.argv[1]
img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
# background: vertical gradient navy -> deep violet with a radial glow
bg = Image.new('RGBA', (S, S))
px = bg.load()
for y in range(S):
    for x in range(S):
        k = y / S
        r = int(12 + 30 * k); g = int(16 + 10 * k); b = int(38 + 50 * k)
        d = math.hypot(x - S * 0.42, y - S * 0.42) / S
        glow = max(0.0, 1 - d * 1.9)
        r = min(255, int(r + 255 * 0.45 * glow * 1.0)); g = min(255, int(g + 122 * 0.45 * glow)); b = min(255, int(b + 26 * 0.45 * glow))
        px[x, y] = (r, g, b, 255)
mask = Image.new('L', (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=200, fill=255)
img.paste(bg, (0, 0), mask)
d = ImageDraw.Draw(img)
# court lines
d.line([(140, 760), (884, 760)], fill=(255, 255, 255, 60), width=10)
d.line([(512, 690), (512, 830)], fill=(255, 255, 255, 60), width=6)
# ball with speed trail
trail = Image.new('RGBA', (S, S), (0, 0, 0, 0)); tdraw = ImageDraw.Draw(trail)
for i in range(14):
    k = i / 14
    rad = 70 * (0.35 + 0.65 * k)
    cx = 520 + 330 * k; cy = 520 - 300 * k
    tdraw.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], fill=(34, 195, 255, int(18 + 60 * k)))
trail = trail.filter(ImageFilter.GaussianBlur(10))
img.alpha_composite(trail)
# racket (rotated): draw on separate layer then rotate
rk = Image.new('RGBA', (S, S), (0, 0, 0, 0))
r = ImageDraw.Draw(rk)
r.rounded_rectangle([470, 600, 556, 900], radius=36, fill=(150, 92, 44, 255))      # handle
r.rounded_rectangle([470, 640, 556, 680], radius=10, fill=(34, 195, 255, 255))     # accent band
r.ellipse([300, 210, 726, 650], fill=(32, 20, 12, 255))                            # edge
r.ellipse([316, 226, 710, 634], fill=(255, 122, 26, 255))                          # rubber
r.ellipse([360, 262, 500, 380], fill=(255, 196, 140, 255))                         # highlight
r.ellipse([372, 274, 488, 368], fill=(255, 150, 70, 255))
rk = rk.rotate(-28, resample=Image.BICUBIC, center=(512, 560))
shadow = rk.copy().split()[3].point(lambda a: int(a * 0.5))
sh = Image.new('RGBA', (S, S), (0, 0, 0, 255)); sh.putalpha(shadow)
sh = sh.filter(ImageFilter.GaussianBlur(18))
img.alpha_composite(sh, (18, 26)); img.alpha_composite(rk)
d = ImageDraw.Draw(img)
bx, by, br = 850, 220, 82
d.ellipse([bx - br, by - br, bx + br, by + br], fill=(255, 250, 240, 255))
d.arc([bx - br + 14, by - br + 14, bx + br - 14, by + br - 14], 200, 320, fill=(255, 150, 40, 255), width=10)
d.ellipse([bx - 45, by - 55, bx - 10, by - 25], fill=(255, 255, 255, 255))
img.save(out)
print('icon ok', img.size)
