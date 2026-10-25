"""Offline preview renderer (painter's algorithm, flat Lambert shading) for geometry exported
from the project's real procedural builders. Approximates Unity's look; not a Unity screenshot."""
import numpy as np, sys, math
from PIL import Image, ImageDraw

def load(path):
    d = np.loadtxt(path, dtype=np.float32)
    return d[:, :9].reshape(-1, 3, 3), d[:, 9:13], d[:, 13]

def look_at(eye, target, up):
    f = target - eye; f /= np.linalg.norm(f)
    r = np.cross(up, f); r /= np.linalg.norm(r)   # Unity: right = up x forward (left-handed)
    u = np.cross(f, r)
    return r, u, f

def render(path, out, eye, target, up, fov=38, W=1600, H=720, light=(0.35, 0.85, -0.4), sky=(14, 20, 38), ambient=0.42):
    tris, cols, emis = load(path)
    eye = np.array(eye, np.float32); target = np.array(target, np.float32); up = np.array(up, np.float32)
    r, u, f = look_at(eye, target, up)
    rel = tris - eye
    x = rel @ r; y = rel @ u; z = rel @ f
    tanh = math.tan(math.radians(fov) / 2)
    n = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    area = 0.5 * np.linalg.norm(n, axis=1)
    n /= (np.linalg.norm(n, axis=1, keepdims=True) + 1e-12)
    L = np.array(light, np.float32); L /= np.linalg.norm(L)
    lam = np.clip(n @ L, 0, 1)
    shade = ambient + (1 - ambient) * lam
    shade = np.where(emis > 0.5, 1.15, shade)
    depth = z.mean(axis=1)
    fog = np.clip((depth - 16) / 40, 0, 0.8)[:, None]
    rgb = np.clip(cols[:, :3] * shade[:, None], 0, 1)
    rgb = rgb * (1 - fog) + np.array(sky) / 255.0 * fog
    alpha = cols[:, 3]
    # facing test in camera space (works for clipped polygons too)
    view = np.stack([x, y, z], axis=2)
    cam_n = np.cross(view[:, 1] - view[:, 0], view[:, 2] - view[:, 0])
    front = (cam_n * view[:, 0]).sum(axis=1) < 0   # Unity front faces: clockwise on screen
    near = 0.1
    polys = []
    for i in np.nonzero(front)[0]:
        pts = view[i]
        if (pts[:, 2] > near).all():
            poly = pts
        elif (pts[:, 2] <= near).all():
            continue
        else:  # Sutherland-Hodgman against z = near
            clipped = []
            for k in range(3):
                a, b = pts[k], pts[(k + 1) % 3]
                ina, inb = a[2] > near, b[2] > near
                if ina: clipped.append(a)
                if ina != inb:
                    t = (near - a[2]) / (b[2] - a[2]); clipped.append(a + (b - a) * t)
            poly = np.array(clipped)
        sxy = [(W / 2 + p[0] / (p[2] * tanh) * (H / 2), H / 2 - p[1] / (p[2] * tanh) * (H / 2)) for p in poly]
        big = area[i] > 4.0
        polys.append(((1e6 if big else 0) + depth[i], sxy, i))
    polys.sort(key=lambda q: -q[0])
    img = Image.new('RGB', (W, H), sky)
    dr = ImageDraw.Draw(img, 'RGBA')
    for _, sxy, i in polys:
        c = tuple(int(v * 255) for v in rgb[i]) + (int(alpha[i] * 255),)
        dr.polygon(sxy, fill=c)
    img.save(out)
    print(out, len(polys), 'polygons drawn')

if __name__ == '__main__':
    mode = sys.argv[1]
    if mode == 'match':
        e = math.radians(63); d = 2.62 / math.tan(math.radians(19))
        render(sys.argv[2], sys.argv[3], (-math.cos(e) * d, 0.76 + math.sin(e) * d, 0), (0, 0.76, 0), (0, 0, 1), 38)
    elif mode == 'hero':
        render(sys.argv[2], sys.argv[3], (-3.2, 2.0, -3.6), (0, 0.8, 0), (0, 1, 0), 42)
    else:
        render(sys.argv[2], sys.argv[3], (0, -0.17, -1.25), (0, -0.17, 0), (0, 1, 0), 40, 1400, 900, light=(0.3, 0.6, -0.8), sky=(20, 26, 46), ambient=0.5)
