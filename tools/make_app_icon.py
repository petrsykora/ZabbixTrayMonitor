# Usage: python3 tools/make_app_icon.py Assets/app.ico preview.png  (requires Pillow)
# Erzeugt Assets/app.ico (16-256 px): blaues abgerundetes Quadrat, weiße Monitoring-Kurve, grüner Statuspunkt
import sys
from PIL import Image, ImageDraw

def render(size):
    s = 8  # Supersampling
    W = size * s
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = round(W * 0.03)
    radius = round(W * 0.22)
    # vertikaler Verlauf von hell- zu dunkelblau
    grad = Image.new("RGBA", (W, W))
    gd = ImageDraw.Draw(grad)
    top, bottom = (40, 120, 230), (12, 60, 150)
    for y in range(W):
        t = y / (W - 1)
        c = tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        gd.line([(0, y), (W, y)], fill=c)
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).rounded_rectangle([pad, pad, W - pad, W - pad], radius=radius, fill=255)
    img.paste(grad, (0, 0), mask)

    # Monitoring-Kurve (Herzschlag); bei kleinen Größen dicker, damit sie sichtbar bleibt
    stroke = max(round(W * (0.085 if size <= 24 else 0.065)), 2 * s)
    pts = [(0.14, 0.56), (0.34, 0.56), (0.43, 0.30), (0.55, 0.78), (0.64, 0.46), (0.70, 0.56), (0.86, 0.56)]
    pts = [(x * W, y * W) for x, y in pts]
    d.line(pts, fill=(255, 255, 255, 255), width=stroke, joint="curve")
    for p in (pts[0], pts[-1]):
        r = stroke / 2
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=(255, 255, 255, 255))

    # grüner Statuspunkt unten rechts mit weißem Rand (ab 24 px, bei 16 px zu klein)
    if size >= 24:
        cx, cy, r = W * 0.78, W * 0.78, W * 0.135
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 255, 255, 255))
        r2 = r * 0.72
        d.ellipse([cx - r2, cy - r2, cx + r2, cy + r2], fill=(46, 160, 67, 255))

    return img.resize((size, size), Image.LANCZOS)

sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
images = [render(n) for n in sizes]
images[-1].save(sys.argv[1], format="ICO", sizes=[(n, n) for n in sizes], append_images=images[:-1])

# Vorschau
preview = Image.new("RGBA", (sum(sizes) + 10 * len(sizes), 256), (240, 240, 240, 255))
x = 0
for im in images:
    preview.paste(im, (x, 256 - im.size[1]), im)
    x += im.size[0] + 10
preview.save(sys.argv[2])
