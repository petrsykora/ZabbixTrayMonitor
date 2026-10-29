# Usage: python3 tools/make_app_icon.py Assets/app.ico preview.png  (requires Pillow)
# Erzeugt Assets/app.ico (16-256 px): blaues abgerundetes Quadrat, weiße Monitoring-Kurve, grüner Statuspunkt
import io
import struct
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

def bmp_frame(im):
    # 32-Bit-DIB (BGRA, von unten nach oben) mit AND-Maske, Höhe im Header doppelt (XOR + AND)
    w, h = im.size
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    px = im.load()
    xor = bytearray()
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[x, y]
            xor += bytes((b, g, r, a))
    row = ((w + 31) // 32) * 4
    mask = bytearray()
    for y in range(h - 1, -1, -1):
        bits = bytearray(row)
        for x in range(w):
            if px[x, y][3] == 0:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bits
    return header + bytes(xor) + bytes(mask)

def png_frame(im):
    buf = io.BytesIO()
    im.save(buf, format="PNG")
    return buf.getvalue()

# Kleine Größen als BMP, nur 256 px als PNG: Windows zeigt PNG-komprimierte kleine Frames nicht überall an
# (zB. leere Ikone im Alt+Tab-Umschalter)
def save_ico(path, images):
    frames = [png_frame(im) if im.size[0] >= 256 else bmp_frame(im) for im in images]
    out = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    for im, data in zip(images, frames):
        n = im.size[0]
        out += struct.pack("<BBBBHHII", n % 256, n % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    with open(path, "wb") as f:
        f.write(out + b"".join(frames))

sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
images = [render(n) for n in sizes]
save_ico(sys.argv[1], images)

# Vorschau
preview = Image.new("RGBA", (sum(sizes) + 10 * len(sizes), 256), (240, 240, 240, 255))
x = 0
for im in images:
    preview.paste(im, (x, 256 - im.size[1]), im)
    x += im.size[0] + 10
preview.save(sys.argv[2])
