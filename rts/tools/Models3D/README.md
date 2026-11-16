<!-- rts/tools/Models3D/README.md: how to run the Tripo3D image-to-3D trial script. Presentation-side tooling (P-001). -->
# Models3D

`python3 rts/tools/Models3D/tripo.py <image> <outdir> [--face-limit 5000]` uploads a cropped concept image, creates an `image_to_model` task (textured, no PBR, default model version), polls every 5 s (15 min limit) and saves `<id>.glb` and `<id>_preview.<ext>`. Needs Pillow only for cropping, not for the script itself.

Endpoints (v3): `GET /account/balance`, `POST /files` (multipart, returns `file_token`), `POST /generation/image-to-model` (`model` P2-20260801, `texture`, `pbr`, `face_limit` all accepted), `GET /tasks/{id}` (`output.model_url`, `output.rendered_image_url`, `credits_consumed`). Status: upload, task and polling work; the model download host `openapi-dl.data.tripo3d.com` is blocked by the network proxy, so GLBs could not be fetched yet. Set `TRIPO_BASE` to override the base URL.
