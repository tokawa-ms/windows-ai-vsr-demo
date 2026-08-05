from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ASSETS = Path(__file__).parent / "Assets"
FILES = (
    "Square150x150Logo.png",
    "Square44x44Logo.png",
    "StoreLogo.png",
    "Wide310x150Logo.png",
    "SplashScreen.png",
)
SCALE = 4
BLUE_TOP = (0, 103, 184)
BLUE_BOTTOM = (0, 63, 122)
CYAN = (80, 210, 255)
WHITE = (255, 255, 255)
FONT = Path(r"C:\Windows\Fonts\bahnschrift.ttf")


def draw_gradient(image: Image.Image) -> None:
    pixels = image.load()
    width, height = image.size
    for y in range(height):
        vertical = y / max(height - 1, 1)
        for x in range(width):
            diagonal = x / max(width - 1, 1)
            blend = min(1.0, vertical * 0.72 + diagonal * 0.16)
            pixels[x, y] = tuple(
                round(start + (end - start) * blend)
                for start, end in zip(BLUE_TOP, BLUE_BOTTOM)
            )


def draw_wordmark(draw: ImageDraw.ImageDraw, width: int, height: int) -> None:
    is_compact = width <= 60
    is_square = width / height < 1.4
    font_size = int(height * (0.44 if is_compact else 0.43 if is_square else 0.39))
    font = ImageFont.truetype(str(FONT), font_size)
    text = "VSR"
    bounds = draw.textbbox((0, 0), text, font=font, stroke_width=0)
    text_width = bounds[2] - bounds[0]
    text_height = bounds[3] - bounds[1]
    center_x = width / 2
    center_y = height * (0.47 if is_compact else 0.44)
    draw.text(
        (center_x - text_width / 2, center_y - text_height / 2 - bounds[1]),
        text,
        fill=WHITE,
        font=font,
    )

    line_y = int(height * (0.73 if is_compact else 0.72))
    gap = max(2, int(height * 0.024))
    line_height = max(2, int(height * (0.045 if is_compact else 0.025)))
    total_width = int(text_width * 0.92)
    start_x = int(center_x - total_width / 2)
    segment_widths = (0.18, 0.28, 0.42)
    x = start_x
    for index, proportion in enumerate(segment_widths):
        segment_width = int(total_width * proportion)
        color = WHITE if index == 0 else CYAN
        rise = (2 - index) * line_height
        draw.rounded_rectangle(
            (x, line_y + rise, x + segment_width, line_y + rise + line_height),
            radius=max(1, line_height // 2),
            fill=color,
        )
        x += segment_width + gap


def create_logo(path: Path) -> None:
    with Image.open(path) as source:
        width, height = source.size
    canvas = Image.new("RGB", (width * SCALE, height * SCALE))
    draw_gradient(canvas)
    draw = ImageDraw.Draw(canvas)
    draw_wordmark(draw, width * SCALE, height * SCALE)
    output = canvas.resize((width, height), Image.Resampling.LANCZOS)
    output.save(path, format="PNG", optimize=True)


for filename in FILES:
    create_logo(ASSETS / filename)