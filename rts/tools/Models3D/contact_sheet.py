# rts/tools/Models3D/contact_sheet.py
# Assembles render images into one labelled JPEG contact sheet (plain Python 3 with Pillow, not Blender).
# Usage: python3 rts/tools/Models3D/contact_sheet.py <out.jpg> <columns> <cell px> <label=image.png> [<label=image.png> ...]
# Each argument gives a caption and an image; images are resized to the cell size and laid out row by row.
import sys

from PIL import Image, ImageDraw

# Command-line arguments: output path, column count, cell size in pixels, then caption=image pairs.
out, cols, cell = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
# List of (caption, image path) pairs. Range: at least one.
items = [a.split("=", 1) for a in sys.argv[4:]]
# Pixel height of the caption strip under each cell.
cap_h = 14
# Number of rows needed. Range: >= 1.
rows = (len(items) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cell, rows * (cell + cap_h)), (30, 30, 34))
draw = ImageDraw.Draw(sheet)
for i, (cap, path) in enumerate(items):
    # Cell position in pixels.
    x, y = (i % cols) * cell, (i // cols) * (cell + cap_h)
    sheet.paste(Image.open(path).convert("RGB").resize((cell, cell), Image.LANCZOS), (x, y))
    draw.text((x + 3, y + cell + 1), cap, fill=(230, 230, 230))
sheet.save(out, quality=82)
print("wrote", out)
