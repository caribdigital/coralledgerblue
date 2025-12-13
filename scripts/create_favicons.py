from pathlib import Path
from PIL import Image, ImageDraw

colors = [(46, 227, 255), (0, 197, 161)]
background = (5, 12, 26)
logo_dir = Path("C:/Projects/CoralLedgerBlue/src/CoralLedger.Blue.Web/wwwroot/images/logos/favicons")
logo_dir.mkdir(parents=True, exist_ok=True)

def draw_circle_gradient(img, draw):
    size = img.width
    cx = cy = size // 2
    radius = int(size * 0.4)
    for i in range(radius, 0, -1):
        ratio = i / radius
        r = int(colors[0][0] * ratio + colors[1][0] * (1 - ratio))
        g = int(colors[0][1] * ratio + colors[1][1] * (1 - ratio))
        b = int(colors[0][2] * ratio + colors[1][2] * (1 - ratio))
        draw.ellipse((cx - i, cy - i, cx + i, cy + i), fill=(r, g, b, 255))
    return radius, cx, cy

def draw_accent(draw, radius, cx, cy, size):
    width = max(1, size // 16)
    draw.arc([cx - radius * 0.7, cy - radius * 0.3, cx + radius * 0.7, cy + radius * 0.5],
             start=200, end=340, fill=(244, 251, 255), width=width)
    draw.arc([cx - radius * 0.6, cy - radius * 0.1, cx + radius * 0.6, cy + radius * 0.9],
             start=120, end=220, fill=(18, 168, 195), width=max(2, size // 12))
    highlight = int(radius * 0.25)
    draw.ellipse([cx - highlight, cy - highlight * 0.6, cx - highlight * 0.2, cy],
                 fill=(244, 251, 255))

for size, filename in [(16, "favicon-16x16.png"), (32, "favicon-32x32.png"), (180, "apple-touch-icon.png")]:
    img = Image.new("RGBA", (size, size), background)
    draw = ImageDraw.Draw(img)
    radius, cx, cy = draw_circle_gradient(img, draw)
    draw_accent(draw, radius, cx, cy, size)
    img.save(logo_dir / filename)

ico_sizes = [(16, 16), (32, 32), (64, 64)]
base = Image.open(logo_dir / "favicon-32x32.png")
icons = [base.resize(size, Image.LANCZOS) for size in ico_sizes]
icons[0].save(logo_dir / "favicon.ico", format="ICO", sizes=ico_sizes)
