# rts/tools/Models3D/tripo.py: image -> GLB through the Tripo3D v3 API (upload, image_to_model task, poll, download). Presentation-side tooling, not part of the simulation.
# Usage: python3 rts/tools/Models3D/tripo.py <input image> <output folder> [--face-limit N] [--views left=.. back=..] [--task ID] [--retexture ID]
# --retexture keeps the geometry of an earlier task and only regenerates its texture from the given image(s) (POST /models/texture).
import argparse, json, mimetypes, os, sys, time, urllib.request, uuid

# Base URL of the API. Unit: URL. Range: https URL, no trailing slash. Override with env TRIPO_BASE.
BASE = os.environ.get("TRIPO_BASE", "https://openapi.tripo3d.ai/v3")
# Bearer token sent on every call. The network proxy swaps in the real credential; the script never holds a key. Unit: text.
AUTH = {"Authorization": "Bearer " + os.environ.get("TRIPO_KEY", "dummy")}
# Model version sent with the task. Unit: Tripo model id. P series is tuned for low-poly output with clean topology.
MODEL = "P2-20260801"
# Seconds between status polls. Unit: seconds. Range: 1..60.
POLL_S = 5
# Give-up time for one task. Unit: seconds. Range: 60..3600.
TIMEOUT_S = 15 * 60


def call(method, url, body=None, headers=None):
    """Send one request. body: bytes or None. Returns parsed JSON for API calls, raw bytes for downloads (url outside BASE)."""
    req = urllib.request.Request(url, data=body, method=method, headers={**(AUTH if url.startswith(BASE) else {}), **(headers or {})})  # no credential to download hosts
    try:
        raw = urllib.request.urlopen(req, timeout=120).read()  # response bytes; TLS verification stays on
    except urllib.error.HTTPError as e:
        sys.exit("HTTP %d from %s %s: %s" % (e.code, method, url, e.read()[:500].decode("utf-8", "replace")))
    return json.loads(raw) if url.startswith(BASE) else raw


def balance():
    """Credits left on the account. Unit: credits. Range: >= 0."""
    return call("GET", BASE + "/account/balance")["data"]["balance"]


def upload(path):
    """Upload an image; returns the file token (text) used by the task."""
    boundary = uuid.uuid4().hex  # multipart separator, random text
    mime = mimetypes.guess_type(path)[0] or "image/png"  # content type of the image
    with open(path, "rb") as f:
        data = f.read()  # image bytes
    head = ('--%s\r\nContent-Disposition: form-data; name="file"; filename="%s"\r\nContent-Type: %s\r\n\r\n'
            % (boundary, os.path.basename(path), mime)).encode()  # part header bytes
    body = head + data + ("\r\n--%s--\r\n" % boundary).encode()  # full multipart body bytes
    r = call("POST", BASE + "/files", body, {"Content-Type": "multipart/form-data; boundary=" + boundary})
    return r["data"]["file_token"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("image")
    ap.add_argument("outdir")
    ap.add_argument("--face-limit", type=int, default=5000)  # triangle cap asked of the service. Unit: faces. Range: 500..20000. Source: P-001 budget.
    ap.add_argument("--task", default="")  # id of an existing task to download instead of generating (costs no credits). Unit: UUID text. Range: a task id on this account, or empty.
    ap.add_argument("--views", nargs="*", default=[])  # extra views for multi-view generation, each "left=path", "back=path" or "right=path"; the positional image is the front view. Unit: key=path text. Range: 0..3 entries.
    ap.add_argument("--retexture", default="")  # id of an earlier generation task whose model keeps its shape and gets a new texture from the image(s). Unit: UUID text. Range: a task id on this account, or empty.
    a = ap.parse_args()
    t0 = time.time()  # wall-clock start. Unit: seconds. (Tooling only; never in Sim.)
    name = os.path.splitext(os.path.basename(a.image))[0]  # output file stem, e.g. skai_scout
    os.makedirs(a.outdir, exist_ok=True)
    b0 = balance()  # credits before the task. Unit: credits
    print("balance_before", b0)
    tid = a.task  # task id, text; empty means generate a new model
    if a.retexture:
        token = upload(a.image)  # reference image token (the front view, or the only image)
        if a.views:
            refs = [{"front": {"file_token": token}}]  # view-key list: front plus each extra view
            for v in a.views:
                key, path = v.split("=", 1)  # view name and its image path
                refs.append({key: {"file_token": upload(path)}})
            prompt = {"images": refs}
        else:
            prompt = {"image": {"file_token": token}}  # one reference image for the whole texture
        body = {"input": a.retexture, "texture_prompt": prompt, "pbr": False}  # texture task: same geometry, new texture
        tid = call("POST", BASE + "/models/texture", json.dumps(body).encode(), {"Content-Type": "application/json"})["data"]["task_id"]
    if not tid:
        token = upload(a.image)  # image token returned by the upload (the front view)
        task = {"model": MODEL, "texture": True, "pbr": False, "face_limit": a.face_limit}  # task settings shared by both endpoints
        route = "/generation/image-to-model"  # endpoint path for a single image
        if a.views:
            inputs = [{"front": {"file_token": token}}]  # one object per view, keyed by view name (Tripo "view-key" format)
            for v in a.views:
                key, path = v.split("=", 1)  # view name (left, back, right) and its image path
                inputs.append({key: {"file_token": upload(path)}})
            task["inputs"] = inputs
            route = "/generation/multiview-to-model"  # endpoint path for 2 to 4 views
        else:
            task["file"] = {"file_token": token}
        tid = call("POST", BASE + route, json.dumps(task).encode(), {"Content-Type": "application/json"})["data"]["task_id"]
    print("task", tid)
    while True:
        d = call("GET", BASE + "/tasks/" + tid)["data"]  # task record: status, output, progress
        if d["status"] == "success":
            break
        if d["status"] in ("failed", "cancelled", "banned", "expired", "unknown"):
            sys.exit("task ended: " + json.dumps(d)[:500])
        if time.time() - t0 > TIMEOUT_S:
            sys.exit("timeout after %d s, task %s still %s" % (TIMEOUT_S, tid, d["status"]))
        time.sleep(POLL_S)
    out = d["output"]  # output URLs; key names vary by version
    model = out["model_url"]  # GLB URL (signed, on openapi-dl.data.tripo3d.com)
    prev = out.get("rendered_image_url")  # preview image URL (webp), may be absent
    with open(os.path.join(a.outdir, name + ".glb"), "wb") as f:
        f.write(call("GET", model))
    if prev:
        pext = os.path.splitext(prev.split("?")[0])[1] or ".webp"  # preview file extension
        with open(os.path.join(a.outdir, name + "_preview" + pext), "wb") as f:
            f.write(call("GET", prev))
    print("credits_consumed", d.get("credits_consumed", "not reported"))
    print("balance_after", balance())
    print("elapsed_s", int(time.time() - t0))


main()
