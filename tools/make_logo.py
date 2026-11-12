# Renders the app logo: python tools/make_logo.py <out-dir>  ->  app.ico, logo-256.png, logo-512.png, preview.png
"""Renders the PS2 Manager logo (library spines + disc) at any size with supersampling."""
import sys
from PIL import Image, ImageDraw, ImageFilter

SS = 4  # supersampling factor


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def gradient(size, c1, c2, diagonal=True):
    w, h = size
    g = Image.new("RGBA", size)
    px = g.load()
    for y in range(h):
        for x in range(w):
            t = ((x + y) / (w + h - 2)) if diagonal else y / (h - 1)
            px[x, y] = lerp(c1, c2, t)
    return g


def render(size, simple=False):
    S = 256 * SS
    k = S / 256
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    def r(*v):
        return [int(a * k) for a in v]

    # Tile
    tile = Image.new("L", (S, S), 0)
    ImageDraw.Draw(tile).rounded_rectangle(r(8, 8, 248, 248), radius=int(56 * k), fill=255)
    img.paste(gradient((S, S), (40, 45, 60, 255), (17, 19, 25, 255)), (0, 0), tile)
    ImageDraw.Draw(img).rounded_rectangle(r(8, 8, 248, 248), radius=int(56 * k),
                                          outline=(70, 78, 100, 255), width=max(1, int(3 * k)))

    d = ImageDraw.Draw(img)
    # Library spines (game cases on a shelf)
    spines = [(40, (58, 66, 88)), (70, (74, 84, 112)), (100, (92, 104, 138))] if not simple \
        else [(44, (70, 80, 108)), (82, (92, 104, 138))]
    w = 24 if not simple else 30
    for x, col in spines:
        d.rounded_rectangle(r(x, 52, x + w, 204), radius=int(6 * k), fill=col + (255,))
        if not simple:  # label stripe
            lay = Image.new("RGBA", (S, S), (0, 0, 0, 0))
            ImageDraw.Draw(lay).rounded_rectangle(r(x + 5, 66, x + w - 5, 74), radius=int(3 * k), fill=(255, 255, 255, 70))
            img.alpha_composite(lay)
            d = ImageDraw.Draw(img)

    # Disc shadow
    cx, cy, R = 156, 148, 76
    sh = Image.new("L", (S, S), 0)
    ImageDraw.Draw(sh).ellipse(r(cx - R - 2, cy - R + 6, cx + R + 6, cy + R + 10), fill=150)
    sh = sh.filter(ImageFilter.GaussianBlur(10 * k))
    img.paste((0, 0, 0, 255), (0, 0), sh)

    # Disc body: blue -> violet
    disc = Image.new("L", (S, S), 0)
    ImageDraw.Draw(disc).ellipse(r(cx - R, cy - R, cx + R, cy + R), fill=255)
    img.paste(gradient((S, S), (86, 156, 255, 255), (139, 92, 246, 255)), (0, 0), disc)

    d = ImageDraw.Draw(img)
    # Sheen wedge
    sheen = Image.new("L", (S, S), 0)
    ImageDraw.Draw(sheen).pieslice(r(cx - R, cy - R, cx + R, cy + R), 200, 250, fill=70)
    ImageDraw.Draw(sheen).pieslice(r(cx - R, cy - R, cx + R, cy + R), 20, 50, fill=35)
    img.paste((255, 255, 255, 255), (0, 0), sheen)
    d = ImageDraw.Draw(img)
    if not simple:  # data rings
        lay = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        for rr, a in ((60, 40), (46, 28)):
            ImageDraw.Draw(lay).ellipse(r(cx - rr, cy - rr, cx + rr, cy + rr), outline=(255, 255, 255, a), width=max(1, int(2 * k)))
        img.alpha_composite(lay)
        d = ImageDraw.Draw(img)
    # Hub and hole
    d.ellipse(r(cx - 26, cy - 26, cx + 26, cy + 26), fill=(230, 236, 255, 255))
    d.ellipse(r(cx - 13, cy - 13, cx + 13, cy + 13), fill=(20, 22, 30, 255))

    return img.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    out = sys.argv[1]
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    frames = [render(s, simple=s <= 32) for s in sizes]
    frames[-1].save(out + "/logo-256.png")
    render(512).save(out + "/logo-512.png")
    frames[-1].save(out + "/app.ico", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
    # preview sheet on dark and light backgrounds
    sheet = Image.new("RGBA", (760, 300), (32, 32, 36, 255))
    x = 10
    for f in frames:
        sheet.paste(f, (x, 20), f)
        x += f.size[0] + 12
    light = Image.new("RGBA", (760, 150), (243, 243, 243, 255))
    x = 10
    for f in frames[:-1]:
        light.paste(f, (x, 10), f)
        x += f.size[0] + 12
    sheet.paste(light, (0, 150))
    sheet.save(out + "/preview.png")
