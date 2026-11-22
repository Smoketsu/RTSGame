# rts/tools/Models3D/rig.py
# Rigs one approved unit model: builds an armature from the joint table in rig_data.json, binds the mesh (own soft nearest-bone weights,
# or Blender automatic weights with --weights auto), keyframes the actions idle / move / attack / death, exports the FBX with the armature and
# all actions baked, re-imports it to check, and renders 4 frames per action into one contact sheet.
# Run from the repository root:
#   blender -b -noaudio --python rts/tools/Models3D/rig.py -- --unit skai.fledgling [--weights nearest|auto] [--renders <dir>] [--sheet <jpg>] [--no-export] [--debug-frames action:frame:view,...]
# Needs convert_static.py (same folder) for the source GLB and yaw, rig_data.json for joints and clips, and python3 with Pillow for the sheet.
import json
import math
import os
import re
import shutil
import subprocess
import sys

# Blender does not put the script folder on sys.path.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import mathutils  # noqa: E402
import numpy as np  # noqa: E402
import convert_static as CS  # noqa: E402
import m3d_common as C  # noqa: E402

# Joint and clip table. Unit: path.
TABLE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "rig_data.json")
# Largest allowed difference between the animated mesh before export and after the FBX round trip. Unit: footprint units. Range: 0.001 to 0.05.
ANIM_TOL = 0.02


def load_table(path):
    """Read rig_data.json, ignoring whole-line // comments. Returns the parsed dict."""
    with open(path) as f:
        # File text with comment lines removed.
        text = "".join(line for line in f if not line.strip().startswith("//"))
    return json.loads(text)


def build_armature(unit):
    """Create the armature object 'Armature' from unit['bones'] (head/tail in footprint units). Returns the object."""
    # Armature data-block and object.
    data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for b in unit["bones"]:
        # New edit bone with the table's head and tail.
        eb = data.edit_bones.new(b["name"])
        eb.head = b["head"]
        eb.tail = b["tail"]
        eb.roll = 0.0
        if b["parent"]:
            eb.parent = data.edit_bones[b["parent"]]
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def point_segment_distance(p, a, b):
    """Distance from each row of p (n x 3 array, footprint units) to the segment a-b (3-vectors). Returns an n array."""
    # Vector along the segment and its squared length.
    ab = b - a
    # Parameter of the closest point on the infinite line, clamped to the segment. Range: 0 to 1.
    t = np.clip(((p - a) @ ab) / max(float(ab @ ab), 1e-12), 0.0, 1.0)
    return np.linalg.norm(p - (a + t[:, None] * ab), axis=1)


def nearest_bone_weights(mesh_obj, unit):
    """Soft nearest-bone weights: w_i = exp(-(d_i*bias_i - min_j d_j*bias_j) / sigma), top max_influences kept, smoothed over edges, rows sum to 1.
    Bones without a parent (the root) get no weight. Returns (n x bones array, list of weighted bone names)."""
    # Vertex positions (n x 3, footprint units).
    p = np.array([v.co[:] for v in mesh_obj.data.vertices])
    # Bones that receive weight (everything below the root anchor).
    bones = [b for b in unit["bones"] if b["parent"]]
    # Biased distance of every vertex to every weighted bone (n x bones).
    # The bone's radius (how thick the part it moves is) is subtracted first, so thick parts claim their whole volume.
    d = np.stack([(point_segment_distance(p, np.array(b["head"]), np.array(b["tail"])) - b.get("radius", 0.0)) * b.get("bias", 1.0)
                  for b in bones], axis=1)
    # Softmax-like weights relative to the nearest bone.
    w = np.exp(-(d - d.min(axis=1, keepdims=True)) / unit["sigma"])
    # Keep only the strongest max_influences per vertex.
    k = unit["max_influences"]
    # Threshold = k-th largest weight in each row.
    thr = -np.sort(-w, axis=1)[:, k - 1:k]
    w = np.where(w >= thr, w, 0.0)
    w /= w.sum(axis=1, keepdims=True)
    # Edge list for smoothing (m x 2 vertex indices).
    e = np.array([ed.vertices[:] for ed in mesh_obj.data.edges])
    for _ in range(unit["smooth_passes"]):
        # Sum of neighbour weights and neighbour count per vertex.
        acc = np.zeros_like(w)
        cnt = np.zeros((len(w), 1))
        np.add.at(acc, e[:, 0], w[e[:, 1]])
        np.add.at(acc, e[:, 1], w[e[:, 0]])
        np.add.at(cnt, e[:, 0], 1)
        np.add.at(cnt, e[:, 1], 1)
        w = 0.5 * w + 0.5 * acc / np.maximum(cnt, 1)
        # Re-apply the influence limit after smoothing.
        thr = -np.sort(-w, axis=1)[:, k - 1:k]
        w = np.where(w >= thr, w, 0.0)
        w /= w.sum(axis=1, keepdims=True)
    return w, [b["name"] for b in bones]


def bind(mesh_obj, arm, unit, method):
    """Bind the mesh to the armature with the chosen weighting method ('nearest' = own weights, 'auto' = Blender bone heat).
    Returns a one-line description of what happened."""
    if method == "auto":
        bpy.ops.object.select_all(action='DESELECT')
        mesh_obj.select_set(True)
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        # Vertices that ended up in no vertex group (bone heat failure sign).
        unweighted = sum(1 for v in mesh_obj.data.vertices if not any(g.weight > 0 for g in v.groups))
        return "Blender automatic weights (bone heat); %d of %d vertices unweighted" % (unweighted, len(mesh_obj.data.vertices))
    # Own weights and the vertex groups named after the bones.
    w, names = nearest_bone_weights(mesh_obj, unit)
    for j, n in enumerate(names):
        mesh_obj.vertex_groups.new(name=n)
    for i in range(len(w)):
        for j in range(len(names)):
            if w[i, j] > 0.001:
                mesh_obj.vertex_groups[names[j]].add([i], float(w[i, j]), 'REPLACE')
    mesh_obj.parent = arm
    # Armature modifier that deforms the mesh by the vertex groups.
    mod = mesh_obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    return "own soft nearest-bone weights (sigma %.3f, max %d influences, %d smoothing passes)" % (
        unit["sigma"], unit["max_influences"], unit["smooth_passes"])


def to_local(pb, euler_deg, loc):
    """Convert an armature-space rotation (degrees about X then Y then Z) and offset to the pose bone's own rest frame.
    Returns (Quaternion, Vector) for rotation_quaternion and location."""
    # Rest orientation of the bone in armature space (3x3 matrix).
    r = pb.bone.matrix_local.to_3x3()
    # Armature-space rotation matrix.
    q = mathutils.Euler([math.radians(a) for a in euler_deg], 'XYZ').to_matrix()
    return ((r.inverted() @ q @ r).to_quaternion(), r.inverted() @ mathutils.Vector(loc))


def wave_value(spec, t):
    """Value of a wave spec at clip time t (0 to 1): amp x sin(2 pi (cycles t + phase)) + bias, for rotation (degrees) and location (footprint units); bias_rot and bias_loc add a constant. Returns (rot, loc)."""
    # Phase angle in radians.
    a = 2 * math.pi * (spec.get("cycles", 1) * t + spec.get("phase", 0.0))
    s = math.sin(a)
    rot = [x * s + b for x, b in zip(spec.get("rot", [0, 0, 0]), spec.get("bias_rot", [0, 0, 0]))]
    loc = [x * s + b for x, b in zip(spec.get("loc", [0, 0, 0]), spec.get("bias_loc", [0, 0, 0]))]
    return rot, loc


def make_actions(arm, unit, fps):
    """Create one Blender action per entry of unit['actions'] (named exactly idle, move, attack, death) and key every bone in each.
    Returns {action name: frame count}."""
    bpy.context.scene.render.fps = fps
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    arm.animation_data_create()
    # Frame counts per action.
    lengths = {}
    for name, act in unit["actions"].items():
        # Frames in the clip; keys go on frames 1 to n.
        n = act["frames"]
        lengths[name] = n
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        arm.animation_data.action = action
        for pb in arm.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
        for pb in arm.pose.bones:
            # Channel spec for this bone, or none (the bone is keyed at rest on the first and last frame).
            ch = act["channels"].get(pb.name)
            if ch is None:
                frames = [(1, [0, 0, 0], [0, 0, 0]), (n, [0, 0, 0], [0, 0, 0])]
            elif "wave" in ch:
                frames = [(f, *wave_value(ch["wave"], (f - 1) / (n - 1))) for f in range(1, n + 1)]
            else:
                frames = [(k[0], k[1], k[2] if len(k) > 2 else [0, 0, 0]) for k in ch["keys"]]
            for (f, rot, loc) in frames:
                pb.rotation_quaternion, pb.location = to_local(pb, rot, loc)
                pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
                pb.keyframe_insert("location", frame=f, group=pb.name)
        action.use_frame_range = True
        action.frame_start, action.frame_end = 1, n
        if act["loop"]:
            action.use_cyclic = True
        # One NLA track per clip, named like the action, so the FBX take gets exactly that name. Muted until export so it does not mix into the preview.
        track = arm.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 1, action)
        track.mute = True
        for pb in arm.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
    arm.animation_data.action = None
    bpy.ops.object.mode_set(mode='OBJECT')
    return lengths


def evaluated_bounds(obj):
    """Min and max corner (two numpy 3-arrays, footprint units) of the object's mesh as deformed at the current frame."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    # Vertex positions in object space (n x 3) and the object-to-world matrix (4 x 4).
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    m = np.array(ev.matrix_world)
    p = co.reshape(-1, 3) @ m[:3, :3].T + m[:3, 3]
    ev.to_mesh_clear()
    return p.min(axis=0), p.max(axis=0)


def pose_signature(arm, mesh_obj, action, frame):
    """Set the action and frame on the armature and return the deformed mesh bounds as a flat 6-array (min xyz, max xyz)."""
    arm.animation_data.action = action
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    lo, hi = evaluated_bounds(mesh_obj)
    return np.concatenate([lo, hi])


def lowest_point(arm, mesh_obj, action, first, last):
    """Lowest z of the deformed mesh over frames first..last of the action (footprint units; negative = below the ground, 0 = on it)."""
    return min(pose_signature(arm, mesh_obj, action, f)[2] for f in range(first, last + 1))


def fbx_take_names(path):
    """Names of the animation takes (clips) stored in a binary FBX, read from the raw bytes. Returns a sorted list of str."""
    with open(path, "rb") as f:
        data = f.read()
    # FBX stores a take as "<name>\x00\x01AnimStack".
    return sorted(set(m.decode() for m in re.findall(rb"([A-Za-z0-9_|. ]+)\x00\x01AnimStack", data)))


def set_nla_mute(arm, mute):
    """Mute or unmute every NLA track of the armature (muted tracks do not mix into previews)."""
    for t in arm.animation_data.nla_tracks:
        t.mute = mute


def render_sheet_frames(arm, w, d, h, unit_id, lengths, renders):
    """Render 4 evenly spaced three-quarter frames of every action. Returns the list of (caption, png path) in action order."""
    C.add_ground()
    # Orthographic width and look-at height that frame the model with margin.
    scale = CS.fit_scale(w, d, h) * 1.15
    # (caption, path) list.
    out = []
    for name, n in lengths.items():
        action = bpy.data.actions[name]
        arm.animation_data.action = action
        # One-shot clips include the last frame in the 4 samples; looping clips do not (last equals first).
        loop = n and action.use_cyclic
        for i in range(4):
            f = round(1 + i * (n - 1) / (4 if loop else 3))
            bpy.context.scene.frame_set(f)
            path = os.path.join(renders, "%s_%s_%d.png" % (unit_id, name, i))
            C.render_view(path, "three_quarter", scale, h / 2, 300)
            out.append(("%s %s f%d" % (unit_id, name, f), path))
    arm.animation_data.action = None
    return out


def run(unit_id, method, renders, sheet, export, dbg=None):
    """Rig one unit end to end. Returns 0 when the FBX round trip verified, else 1."""
    table = load_table(TABLE)
    unit = table["units"][unit_id]
    # Source row of the static converter for this unit: (id, faction, glb, yaw, why).
    row = next(r for r in CS.MODELS if r[0] == unit_id)
    C.reset_scene()
    mesh = C.import_glb_as_one_mesh(os.path.join(C.REPO, row[2]))
    mesh.name = unit_id
    mesh.data.name = unit_id
    w, d, h, k = C.normalise(mesh, row[3])
    arm = build_armature(unit)
    note = bind(mesh, arm, unit, method)
    print("WEIGHTS:", note)
    lengths = make_actions(arm, unit, table["fps"])
    print("BONES (%d): %s" % (len(unit["bones"]), ", ".join(b["name"] for b in unit["bones"])))
    print("CLIPS:", lengths)
    # Deformed mesh bounds at four frames of every action, to compare after the FBX round trip.
    expected = {n: {f: pose_signature(arm, mesh, bpy.data.actions[n], f)
                    for f in set([1, 1 + (lengths[n] - 1) // 3, 1 + 2 * (lengths[n] - 1) // 3, lengths[n]])} for n in lengths}
    # Ground clearance of every clip over all its frames (negative = the mesh dips below the ground plane).
    for n in lengths:
        print("GROUND %-6s lowest z %.3f" % (n, lowest_point(arm, mesh, bpy.data.actions[n], 1, lengths[n])))
    arm.animation_data.action = None
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    # Rest-pose bounds before export (REST position, because the pose bones still hold the last previewed action).
    arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    rest = evaluated_bounds(mesh)
    arm.data.pose_position = 'POSE'
    ok = True
    if export:
        fbx = os.path.join(CS.OUT_DIR, unit_id + ".fbx")
        set_nla_mute(arm, False)
        C.export_fbx(fbx, [arm, mesh])
        set_nla_mute(arm, True)
        print("FBX takes:", fbx_take_names(fbx))
        print("EXPORTED", fbx, os.path.getsize(fbx) // 1024, "KB")
    # Optional large single-frame renders for inspection: "action:frame:view,..." (view is a key of m3d_common.VIEWS).
    for spec in [s for s in (dbg or "").split(",") if s]:
        a, f, v = spec.split(":")
        arm.animation_data.action = bpy.data.actions[a]
        bpy.context.scene.frame_set(int(f))
        C.add_ground()
        C.render_view(os.path.join(renders, "dbg_%s_%s_%s.png" % (a, f, v)), v, CS.fit_scale(w, d, h) * 0.9, h / 2, 700)
    frames = render_sheet_frames(arm, w, d, h, unit_id, lengths, renders)
    if export:
        # Round trip into a fresh scene.
        C.reset_scene()
        bpy.ops.import_scene.fbx(filepath=fbx)
        arms = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        print("REIMPORT armatures=%d meshes=%d bones=%d actions=%s" % (
            len(arms), len(meshes), len(arms[0].data.bones) if arms else 0, [a.name for a in bpy.data.actions]))
        ok = len(arms) == 1 and len(meshes) == 1 and len(arms[0].data.bones) == len(unit["bones"])
        # Blender's FBX importer joins a bone to its parent when it is the parent's only child, and joined bones ignore translation
        # curves in Blender (not in Unity). Un-join them so the check sees the translation keys that are in the file.
        bpy.context.view_layer.objects.active = arms[0]
        bpy.ops.object.mode_set(mode='EDIT')
        for eb in arms[0].data.edit_bones:
            eb.use_connect = False
        bpy.ops.object.mode_set(mode='OBJECT')
        arms[0].animation_data.action = None
        for pb in arms[0].pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()
        # Rest pose = the armature's REST position (the pose bones still hold the last imported action otherwise).
        arms[0].data.pose_position = 'REST'
        bpy.context.view_layer.update()
        lo, hi = evaluated_bounds(meshes[0])
        arms[0].data.pose_position = 'POSE'
        berr = float(np.abs(np.concatenate([lo, hi]) - np.concatenate(rest)).max())
        print("REIMPORT rest bounds error %.4f" % berr)
        ok = ok and berr < ANIM_TOL
        ok = ok and fbx_take_names(fbx) == sorted(lengths)
        for n in lengths:
            # Imported action whose name ends with the clip name (Blender prefixes the armature name).
            act = next((a for a in bpy.data.actions if a.name == n or a.name.endswith("|" + n)), None)
            if act is None:
                print("REIMPORT MISSING ACTION", n)
                ok = False
                continue
            # Largest bounds difference over 4 frames of the clip, authoring scene vs re-imported FBX.
            err = 0.0
            for f in sorted(set([1, 1 + (lengths[n] - 1) // 3, 1 + 2 * (lengths[n] - 1) // 3, lengths[n]])):
                sig = pose_signature(arms[0], meshes[0], act, int(act.frame_range[0]) + f - 1)
                err = max(err, float(np.abs(sig - expected[n][f]).max()))
            print("REIMPORT action %-6s frames %d-%d bounds error %.4f" % (n, act.frame_range[0], act.frame_range[1], err))
            ok = ok and err < ANIM_TOL
    py = shutil.which("python3")
    if py and sheet:
        os.makedirs(os.path.dirname(sheet), exist_ok=True)
        subprocess.run([py, os.path.join(os.path.dirname(os.path.abspath(__file__)), "contact_sheet.py"), sheet, "4", "300"] +
                       ["%s=%s" % (c, p) for c, p in frames], check=True)
    print("RESULT", unit_id, "ok" if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    # Command line after `--`.
    args = C.script_args()

    def opt(name, default=None):
        """Value following a flag in args, or default."""
        return args[args.index(name) + 1] if name in args else default
    # Unit data ID to rig. Required.
    uid = opt("--unit")
    sys.exit(run(uid, opt("--weights", "nearest"), opt("--renders", "/tmp/models3d_rig_renders"),
                 opt("--sheet", os.path.join(C.REPO, "rts/docs/art/models/unity/pilot", uid + ".jpg")), "--no-export" not in args, opt("--debug-frames")))
