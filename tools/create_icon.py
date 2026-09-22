"""Build SmartFoldering's geometric Windows icon from local shapes."""

from pathlib import Path

from PIL import Image, ImageDraw


SIZE = 1024
ROOT = Path(__file__).resolve().parents[1]


def render() -> Image.Image:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    fill = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = fill.load()
    for y in range(SIZE):
        for x in range(SIZE):
            blend = (x + y) / (2 * (SIZE - 1))
            pixels[x, y] = (
                round(15 + 24 * blend),
                round(39 + 38 * blend),
                round(64 + 47 * blend),
                255,
            )
    mask = Image.new("L", (SIZE, SIZE))
    ImageDraw.Draw(mask).rounded_rectangle((42, 42, 982, 982), radius=230, fill=255)
    image.paste(fill, (0, 0), mask)
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((200, 345, 824, 765), radius=86, fill=(239, 249, 255, 255))
    draw.rounded_rectangle((225, 280, 505, 435), radius=53, fill=(239, 249, 255, 255))
    draw.rounded_rectangle((290, 505, 734, 688), radius=49, fill=(31, 80, 110, 255))
    for y in (544, 593, 642):
        draw.rounded_rectangle((360, y, 664, y + 19), radius=9, fill=(145, 234, 215, 255))
    return image


if __name__ == "__main__":
    icon = render()
    destination = ROOT / "Assets" / "smartfoldering.ico"
    icon.save(destination, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    icon.resize((256, 256), Image.Resampling.LANCZOS).save(ROOT / "Assets" / "smartfoldering.png")
    print(destination)
