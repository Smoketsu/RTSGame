# rts/tools/ConceptArt/generate.py: generates concept art JPEGs from a round's shots.json with the OpenAI image API; used by the concept-artist subagent.
# Usage: python3 rts/tools/ConceptArt/generate.py <round folder> [shot id ...] [--scratch DIR]
import argparse
import base64
import io
import json
import os
import re
import sys
import tempfile
import urllib.error
import urllib.request

from PIL import Image

# Image models to try, best first. Unit: model name. The first one the API accepts is used.
MODELS = ["gpt-image-2.6-sunburst", "gpt-image-2.5-sunburst", "gpt-image-2"]
# Image API endpoint. Unit: URL.
URL = "https://api.openai.com/v1/images/generations"
# Image edit endpoint, used for shots that list references. Unit: URL.
EDIT_URL = "https://api.openai.com/v1/images/edits"
# Multipart field name for each reference image on the edit endpoint. Unit: form field name.
IMAGE_FIELD = "image[]"
# Repository root; reference paths are relative to it. Unit: absolute path.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
# Render quality sent to the API. Unit: API enum (low, medium, high).
QUALITY = "medium"
# Longest side of the saved JPEG. Unit: pixels. Range: 1..4096.
MAX_SIDE = 1536
# JPEG quality of the saved file. Unit: Pillow quality. Range: 1..95.
JPEG_QUALITY = 88
# Request timeout, raised from 300 for high-quality shots. Unit: seconds. Range: 1..600.
TIMEOUT = 600


def load_shots(path):
    """Reads shots.json, dropping whole-line // comments. Returns the parsed dict."""
    # Raw file text without comment lines. Unit: text.
    text = "".join(l for l in open(path, encoding="utf-8") if not l.lstrip().startswith("//"))
    return json.loads(text)


def multipart(fields, refs):
    """Builds a multipart/form-data body. Returns (body bytes, content type header)."""
    # Random part separator. Unit: text.
    boundary = "----concept" + os.urandom(8).hex()
    # Body pieces in order. Unit: bytes.
    parts = []
    for k, v in fields.items():
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    for path in refs:
        with open(os.path.join(ROOT, path), "rb") as f:
            parts.append((f'--{boundary}\r\nContent-Disposition: form-data; name="{IMAGE_FIELD}"; '
                          f'filename="{os.path.basename(path)}"\r\nContent-Type: image/jpeg\r\n\r\n').encode()
                         + f.read() + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), "multipart/form-data; boundary=" + boundary


def request(model, prompt, size, refs=(), quality=QUALITY):
    """Calls the API once at the given quality (a shot's own quality field, else QUALITY) (edit endpoint if refs is non-empty). Returns decoded PNG bytes; raises HTTPError on rejection."""
    if refs:
        # Multipart body and its content type header.
        data, ctype = multipart({"model": model, "prompt": prompt, "size": size, "quality": quality, "n": 1}, refs)
        url = EDIT_URL
    else:
        # JSON request body. Unit: dict.
        data, ctype, url = json.dumps({"model": model, "prompt": prompt, "size": size, "quality": quality, "n": 1}).encode(), "application/json", URL
    # HTTP request with the placeholder credential the proxy replaces.
    req = urllib.request.Request(url, data, {"Authorization": "Bearer dummy", "Content-Type": ctype})
    with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
        return base64.b64decode(json.load(r)["data"][0]["b64_json"])


def generate(shot, style, folder, scratch):
    """Generates one shot, trying each model in MODELS on a 400-level rejection. Writes PNG and JPEG."""
    # Full prompt: style text then shot text.
    prompt = style + " " + shot["prompt"]
    # Last API error text, shown if every model fails.
    err = ""
    for model in MODELS:
        try:
            # PNG bytes from the API.
            png = request(model, prompt, shot["size"], shot.get("references") or [], shot.get("quality", QUALITY))
        except urllib.error.HTTPError as e:
            err = e.read().decode(errors="replace")[:500]
            print(f"{shot['id']}: {model} rejected ({e.code}): {err}", file=sys.stderr)
            if e.code in (400, 404):
                continue
            raise
        with open(os.path.join(scratch, shot["id"] + ".png"), "wb") as f:
            f.write(png)
        # Decoded image, downscaled so the longest side is MAX_SIDE.
        img = Image.open(io.BytesIO(png)).convert("RGB")
        img.thumbnail((MAX_SIDE, MAX_SIDE))
        img.save(os.path.join(folder, shot["id"] + ".jpg"), "JPEG", quality=JPEG_QUALITY)
        print(f"{shot['id']}: ok with {model}")
        return model
    raise SystemExit(f"{shot['id']}: all models failed: {err}")


def main():
    # Command-line arguments.
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("ids", nargs="*")
    ap.add_argument("--scratch", default=None)
    args = ap.parse_args()
    # Folder for PNG originals, outside the repository.
    scratch = args.scratch or tempfile.mkdtemp(prefix="concept_png_")
    os.makedirs(scratch, exist_ok=True)
    # Parsed shot list.
    data = load_shots(os.path.join(args.folder, "shots.json"))
    for shot in data["shots"]:
        if not args.ids or shot["id"] in args.ids:
            generate(shot, data["style"], args.folder, scratch)


if __name__ == "__main__":
    main()
