# rts/tools/Models3D/convert_static.py
# Converts the 25 approved Tripo GLB models (14 units, 11 buildings) to Unity-ready static FBX files and checks each result.
# Run from the repository root:
#   blender -b -noaudio --python rts/tools/Models3D/convert_static.py -- [--only <data id>] [--probe <dir>] [--renders <dir>]
# Models that rig.py has rigged (see rig_data.json) are converted for checking only; their rigged FBX in the output folder is left alone.
# Default: converts every model into rts/src/Game/Assets/Resources/Models/<data id>.fbx, re-imports each FBX into a fresh scene to
# verify bounds and facing, writes rts/docs/art/models/unity/sizes.csv and renders top/front/three_quarter checks into --renders.
# --probe <dir> only renders the four side views of each model at yaw 0 (used to decide the YAW values below) and exports nothing.
#
# Normalisation rule (what Unity can rely on): after the yaw the model is scaled uniformly so its LONGEST FOOTPRINT SIDE
# (bounding-box X or Z extent) is exactly 1.0 unit, its footprint centre is at the origin, its lowest point is at y = 0 and
# it faces +Z in Unity (Blender -Y). Unity scales the instance by (2 x radius_tiles x tile size) for units, or by the building's
# footprint width in tiles for buildings; height follows from the table in rts/docs/art/models/unity/README.md.
import csv
import json
import os
import sys

# Make m3d_common importable: Blender does not put the script folder on sys.path.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import m3d_common as C  # noqa: E402

# Model table. Each row: (data ID = FBX file name, faction, GLB path relative to the repository root, yaw in degrees).
# YAW: rotation about the vertical axis, counter-clockwise seen from above, applied so the model's front faces Blender -Y
# (Unity +Z). Unit: degrees. Range: 0, 90, 180 or 270. Each value was chosen by LOOKING at the probe renders
# of the model (run with --probe), not guessed; the last element says why. Every approved model came out of the generator
# facing +X (270 turns it to -Y); near-symmetric ones follow that rule; buildings face their door where they have one.
MODELS = [
    ("skai.fledgling", "skai", "rts/docs/art/models/batch1/skai_worker.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.windrunner", "skai", "rts/docs/art/models/trial2/skai_scout.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.talon_archer", "skai", "rts/docs/art/models/batch1/skai_marksman_v3.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.stormcaller", "skai", "rts/docs/art/models/batch1/skai_stormcaller_v4.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.roc_rider", "skai", "rts/docs/art/models/batch1/skai_roc_rider_v2.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.thunderhead", "skai", "rts/docs/art/models/batch1/skai_thunderhead_v6.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.tender", "water", "rts/docs/art/models/batch1/water_worker_v2.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.shellback", "water", "rts/docs/art/models/trial2/water_clam.glb", 270, "no distinguishable front (near-symmetric); same +X as the rest of the generator output"),
    ("water.grasper", "water", "rts/docs/art/models/batch1/water_pummeler_v2.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.spoutling", "water", "rts/docs/art/models/batch1/water_spoutling.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.ink_lobber", "water", "rts/docs/art/models/trial2/water_snail.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.tidecaller", "water", "rts/docs/art/models/batch1/water_tidecaller.glb", 270, "no distinguishable front (near-symmetric); same +X as the rest of the generator output"),
    ("water.leviathan", "water", "rts/docs/art/models/batch1/water_leviathan.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.reef_runner", "water", "rts/docs/art/models/batch1/water_reef_runner.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.roost", "skai", "rts/docs/art/models/buildings1/skai_roost.glb", 270, "no distinguishable front (near-symmetric); same +X as the rest of the generator output"),
    ("skai.windspire", "skai", "rts/docs/art/models/buildings1/skai_windspire.glb", 270, "no distinguishable front (near-symmetric); same +X as the rest of the generator output"),
    ("skai.perch_tower", "skai", "rts/docs/art/models/buildings1/skai_perch_tower.glb", 270, "no distinguishable front (near-symmetric); same +X as the rest of the generator output"),
    ("skai.perch_depot", "skai", "rts/docs/art/models/buildings1/skai_perch_depot.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.great_aerie", "skai", "rts/docs/art/models/buildings1/skai_great_aerie_rooted.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("skai.great_aerie.uprooted", "skai", "rts/docs/art/models/buildings1/skai_great_aerie_uprooted.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.grand_shell", "water", "rts/docs/art/models/buildings1/water_grand_shell.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.tidepool", "water", "rts/docs/art/models/buildings1/water_tidepool.glb", 270, "no distinguishable front (near-symmetric); same +X as the rest of the generator output"),
    ("water.shell_forge", "water", "rts/docs/art/models/buildings1/water_shell_forge.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.coral_turret", "water", "rts/docs/art/models/buildings1/water_coral_turret.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
    ("water.pearl_depot", "water", "rts/docs/art/models/buildings1/water_pearl_depot.glb", 270, "front (face, door or barrel) points +X in the source; seen in the probe renders"),
]
# Output folder for the FBX files (Unity Resources path). Unit: path.
OUT_DIR = os.path.join(C.REPO, "rts/src/Game/Assets/Resources/Models")
# Folder for the size table. Unit: path.
DOC_DIR = os.path.join(C.REPO, "rts/docs/art/models/unity")
# Data IDs that rig.py has rigged. Their FBX in OUT_DIR holds the armature and clips, so this script must not overwrite it: it exports their
# static version to the --renders folder instead (for the checks and the sheets only). Read from rig_data.json; no rigs means an empty set.
RIGGED = set()
try:
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "rig_data.json")) as _f:
        RIGGED = set(json.loads("".join(l for l in _f if not l.strip().startswith("//")))["units"])
except OSError:
    pass
# Largest allowed distance between the model before export and after the FBX round trip (centroid and bounds). Unit: footprint units. Range: 0.0001 to 0.01.
TOL = 0.002


def fit_scale(w, d, h):
    """Orthographic camera width that frames a model of the given extents with a margin. Unit: footprint units. Range: > 0."""
    return max(w, d, h) * 1.25


def centroid(obj):
    """Mean of all vertex positions in world space as (x, y, z) in footprint units."""
    # Vertex positions in world space.
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    # Number of vertices. Range: >= 1.
    n = len(pts)
    return (sum(p.x for p in pts) / n, sum(p.y for p in pts) / n, sum(p.z for p in pts) / n)


def probe(only, outdir):
    """Render the four side views and the top view of each model at yaw 0 into outdir/<id>_<view>.png."""
    for (mid, fac, src, yaw, why) in MODELS:
        if only and mid != only:
            continue
        C.reset_scene()
        obj = C.import_glb_as_one_mesh(os.path.join(C.REPO, src))
        w, d, h, k = C.normalise(obj, 0)
        C.add_ground()
        for v in ("front", "right", "back", "left", "top"):
            C.render_view(os.path.join(outdir, "%s_%s.png" % (mid, v)), v, fit_scale(w, d, h), h / 2, 256)


def convert(only, renders):
    """Convert, export, re-import and verify every model; render check images; write sizes.csv. Returns the process exit code."""
    # Rows for sizes.csv.
    rows = []
    # Count of models whose round trip failed verification.
    failures = 0
    for (mid, fac, src, yaw, why) in MODELS:
        if only and mid != only:
            continue
        C.reset_scene()
        glb = os.path.join(C.REPO, src)
        obj = C.import_glb_as_one_mesh(glb)
        obj.name = mid
        obj.data.name = mid
        # Normalised extents (width X, depth Y, height Z) in footprint units, plus the scale factor that was applied.
        w, d, h, k = C.normalise(obj, yaw)
        # Centroid before export, for the round-trip facing check.
        c0 = centroid(obj)
        # Path of the FBX being written.
        fbx = os.path.join(renders if mid in RIGGED else OUT_DIR, mid + ".fbx")
        C.export_fbx(fbx, [obj])
        # Renders of the normalised model (top, front, three-quarter), later assembled into contact sheets.
        C.add_ground()
        for v in ("top", "front", "three_quarter"):
            C.render_view(os.path.join(renders, "%s_%s.png" % (mid, v)), v, fit_scale(w, d, h), h / 2, 256)
        # Round trip: a fresh scene, the FBX imported with Blender's default axes (-Z forward, Y up), then compared.
        C.reset_scene()
        bpy.ops.import_scene.fbx(filepath=fbx)
        # Meshes in the re-imported file.
        ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        lo, hi = C.world_bounds(ms[0])
        c1 = centroid(ms[0])
        # Largest deviation of bounds extents, floor height and centroid from the pre-export model.
        err = max(abs(hi.x - lo.x - w), abs(hi.y - lo.y - d), abs(hi.z - lo.z - h), abs(lo.z),
                  abs(c1[0] - c0[0]), abs(c1[1] - c0[1]), abs(c1[2] - c0[2]))
        # Image data-blocks in the re-imported file (texture survived if >= 1).
        imgs = [i for i in bpy.data.images if i.size[0] > 0]
        ok = err < TOL and len(imgs) >= 1
        failures += 0 if ok else 1
        # Sizes in kilobytes of the source GLB and the produced FBX.
        glb_kb = os.path.getsize(glb) // 1024
        fbx_kb = os.path.getsize(fbx) // 1024
        print("VERIFY %s ok=%s err=%.5f bounds=(%.3f,%.3f,%.3f) yaw=%d glb=%dKB fbx=%dKB textures=%s" % (
            mid, ok, err, w, d, h, yaw, glb_kb, fbx_kb, [(i.name, i.size[0], i.size[1]) for i in imgs]))
        rows.append([mid, src, yaw, "%.3f" % w, "%.3f" % d, "%.3f" % h, "%.4f" % k, glb_kb, fbx_kb, "ok" if ok else "FAIL"])
    if not only:
        os.makedirs(DOC_DIR, exist_ok=True)
        with open(os.path.join(DOC_DIR, "sizes.csv"), "w", newline="") as f:
            f.write("# rts/docs/art/models/unity/sizes.csv: normalised size, yaw and file sizes of the static conversion of each approved model (written by convert_static.py; fbx_kb is the static FBX, rigged units keep a larger rigged FBX).\n")
            wr = csv.writer(f)
            wr.writerow(["data_id", "source", "yaw_deg", "width_x", "depth_z_unity", "height_y_unity", "scale_applied", "glb_kb", "fbx_kb", "roundtrip"])
            wr.writerows(rows)
    return 1 if failures else 0


if __name__ == "__main__":
    # Parsed command line after `--`.
    args = C.script_args()
    # Value following a flag, or None.
    def opt(name):
        return args[args.index(name) + 1] if name in args else None
    # Scratch folder for render images; never inside the repository.
    renders_dir = opt("--renders") or "/tmp/models3d_renders"
    if "--probe" in args:
        probe(opt("--only"), opt("--probe"))
    else:
        sys.exit(convert(opt("--only"), renders_dir))
