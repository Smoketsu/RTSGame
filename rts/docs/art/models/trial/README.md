<!-- rts/docs/art/models/trial/README.md: results of the three-model Tripo3D trial from proposal P-001, run on 16 Nov 2026 from approved low-poly concept art. -->
# 3D generation trial (16 Nov 2026)

Three approved concepts went through Tripo3D image-to-model. All three came back as usable textured models at the triangle budget, for 110 credits and about one minute each. Rigging, animation and the Unity import are not tested yet.

## Settings

`rts/tools/Models3D/tripo.py`, API `https://openapi.tripo3d.ai/v3`, model `P2-20260801` (the low-poly series), `texture` on, `pbr` off, `face_limit` 5000. Each input is one unit cropped from its approved sheet.

## Results

| Model | Input | Task id | Seconds | Credits | File size | Triangles | Texture | Materials |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Skai scout (`skai_scout.glb`) | `skai_scout.png`, from `concept/round9/skai_ground_lineup.jpg` | `14a14b56-5b0b-4c9a-8862-e7d70e593b1d` | about 65 | 110 | 1.18 MB | 5,404 | 2048 x 2048 | 1 |
| Tracked clam (`water_clam.glb`) | `water_clam.png`, from `concept/round8/water_lineup_style_c_lowpoly.jpg` | `ea268d99-8823-43f5-8964-aa1cce509561` | 59 | 110 | 0.74 MB | 4,976 | 2048 x 2048 | 1 |
| Sea snail (`water_snail.glb`) | `water_snail.png`, from `concept/round12/water_snail_poses.jpg` | `c0d061c7-33b1-4aee-9cc9-490a68ac1cae` | 58 | 110 | 1.12 MB | 5,193 | 2048 x 2048 | 1 |

`<name>_preview.jpg` is the service's own render of each model.

## Trial checks (from P-001)

| Check | Result |
| --- | --- |
| Style match | Pass, judged from the previews. Silhouettes and colours follow the concepts. The scout keeps its crest, foot blades and leg armour; the clam keeps its treads, bands and core; the snail keeps its cannon shell, bands and panels. Surfaces are smoother than the faceted concept, and the snail's scope lens and outside brace are simplified. |
| Budget | Pass. 4,976 to 5,404 triangles against a 5,000 limit (the scout is 8% over). |
| Cost | Measured: 110 credits and about one minute per model. A 30-model roster is about 3,300 credits before retries. |
| Readability at game zoom | Not tested. Needs the Unity greybox. |
| Animation | Not tested. The models have no skeleton. |

## Notes

- The balance was 4,570 credits before the trial and 4,130 after: four tasks at 110 credits. One of the four was an accidental billed request for the scout made while probing parameters (task `cb71096c-6f56-4497-a601-11b77c488b24`, PBR on, not downloaded). The API accepts wrong parameter values and bills the task rather than rejecting it, so parameters are checked against the documentation, not by trial requests.
- Model files are served from `openapi-dl.data.tripo3d.com`; that host and `developers.tripo3d.com` (documentation) were added to the environment's allowed hosts for this trial.
- Unity does not import GLB without a package (glTFast) or a conversion to FBX. That choice is part of the next step.
