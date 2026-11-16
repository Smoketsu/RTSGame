<!-- rts/docs/art/models/trial2/README.md: second 3D trial (16 Nov 2026): the same three units regenerated from three-view model sheets that apply the developer's notes on the first trial. -->
# 3D generation trial 2: multi-view (16 Nov 2026)

The developer's notes on trial 1 (uneven legs, missing tread detail, an untidy scope, a bracket not seated in the body) all came from generating a model out of one side-on drawing. Trial 2 redraws each unit as a front, side and back sheet with the fixes built in (`concept/turnarounds1/`) and uses Tripo's multi-view endpoint.

## Settings

`rts/tools/Models3D/tripo.py <front> <out> --views left=<side> back=<back>`, endpoint `POST /v3/generation/multiview-to-model`, model `P2-20260801`, `texture` on, `pbr` off, `face_limit` 5000. The side view in each sheet faces left, so it is sent as the `left` view.

## Results

| Model | Views | Task id | Seconds | Credits | File size | Triangles | Texture |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Skai scout (`skai_scout.glb`) | `skai_scout_front.png`, `_left.png`, `_back.png` | `7ab8da81-0d8d-419d-8528-28dd70f26222` | 75 | 110 | 1.22 MB | 4,740 | 2048 x 2048 |
| Tracked clam (`water_clam.glb`) | `water_clam_front.png`, `_left.png`, `_back.png` | `11aa49a0-ee6d-4234-b69a-cd1caf112ff3` | 101 | 110 | 0.91 MB | 4,735 | 2048 x 2048 |
| Sea snail (`water_snail.glb`) | `water_snail_front.png`, `_left.png`, `_back.png` | `9c122bf8-72db-4c69-b022-ae60a614d343` | 75 | 110 | 1.20 MB | 5,025 | 2048 x 2048 |

Multi-view costs the same 110 credits as single-view. Balance after trial 2: 3,800 credits.

## Against the developer's notes (judged from the service's previews only)

| Note | Result |
| --- | --- |
| Scout: legs symmetrical, yellow piece on both | Met: both legs carry the same knee guard, ankle cuff and blade talons. |
| Scout: back feathers like a short cape | Drawn in the sheet; the back is not visible in the preview, so unconfirmed. |
| Clam: treads missing texture | Improved: separate tread plates are visible on both tracks. |
| Snail: targeting gear haphazard | Improved: one scope with a crosshair on one stalk. |
| Snail: brackets not flush with the back | The arms now rise from a collar on the body; how flush it sits needs a look in the viewer. |

The standing, symmetric scout is also a better starting point for automatic rigging than the running pose of trial 1. Rigging, animation and the Unity import are still untested.

## Developer review (16 Nov 2026)

The Skai scout and the tracked clam are marked Keep. The sea snail was left unmarked, and the developer closed the trial with "I think we've done it here". The route used here is now the standard one for units: draw a front, side and back sheet from the approved concept, then generate with the multi-view endpoint.
