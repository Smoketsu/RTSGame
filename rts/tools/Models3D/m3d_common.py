# rts/tools/Models3D/m3d_common.py
# Shared Blender helpers for the model pipeline: GLB import, footprint/ground normalisation, FBX export and the headless Cycles renders used to check the results.
# Imported by convert_static.py and rig.py (both run inside `blender -b`); it needs bpy and cannot run in a plain Python.
import math
import os
import re
import sys
import tempfile

import bpy
import mathutils

# Repository root, four levels up from this file (Models3D, tools, rts, root). Unit: path.
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
# Render samples per pixel for Cycles on CPU. Unit: samples. Range: 4 to 64 (16 is enough for a check image).
SAMPLES = 16
# Camera directions: view name -> (azimuth in degrees from the front camera going counter-clockwise seen from above, elevation in degrees above the horizon).
# "front" looks at the model from -Y (the agreed forward direction faces that camera).
VIEWS = {
    "front": (0, 0), "right": (90, 0), "back": (180, 0), "left": (270, 0),
    "top": (0, 90), "three_quarter": (-35, 28),
}


def script_args():
    """Return the argument list after the first `--` on the Blender command line (list of str, may be empty)."""
    # Full command line of the Blender process.
    argv = sys.argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def reset_scene():
    """Empty the Blender scene completely (no default cube, camera or light)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_glb_as_one_mesh(path):
    """Import a GLB and return its meshes joined into one object with all transforms applied and no parent."""
    bpy.ops.import_scene.gltf(filepath=path)
    # All mesh objects the importer created. Unit: objects. Range: at least 1.
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    # Drop the parent (the importer adds an empty root) but keep the world position.
    for o in meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    # The glTF root empties are deleted.
    for o in [o for o in bpy.context.scene.objects if o.type != 'MESH']:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    # The single joined mesh object.
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def world_bounds(obj):
    """Return (min corner, max corner) of the object's vertices in world space as two Vectors (Blender units)."""
    # Every vertex position in world space.
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return (mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))


def _apply(obj, loc=False, rot=False, scl=False):
    """Apply (bake into the vertices) the chosen transform parts of one object."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=loc, rotation=rot, scale=scl)


def normalise(obj, yaw_deg, footprint=1.0):
    """Yaw the mesh by yaw_deg about +Z (degrees, counter-clockwise seen from above), put the footprint centre on the origin,
    the lowest point on z=0 and scale so the longest footprint side equals `footprint` (Blender units).
    Returns (width_x, depth_y, height_z, scale_factor) of the result."""
    # The glTF importer leaves objects in quaternion mode, which would ignore rotation_euler.
    obj.rotation_mode = 'XYZ'
    obj.rotation_euler = (0, 0, math.radians(yaw_deg))
    _apply(obj, rot=True)
    # Bounds before scaling.
    lo, hi = world_bounds(obj)
    # Uniform scale that makes the longer of the X and Y extents equal `footprint`. Unit: factor. Range: > 0.
    k = footprint / max(hi.x - lo.x, hi.y - lo.y)
    obj.scale = (k, k, k)
    _apply(obj, scl=True)
    lo, hi = world_bounds(obj)
    # Move the bounding-box footprint centre to x=y=0 and the lowest vertex to z=0.
    obj.location = (-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)
    _apply(obj, loc=True)
    lo, hi = world_bounds(obj)
    return (hi.x - lo.x, hi.y - lo.y, hi.z - lo.z, k)


# Tolerance of the rest-pose rule (longest footprint side 1.0, lowest point 0). Unit: footprint units. Range: 0.001 to 0.01.
REST_TOL = 0.005


def name_texture(data_id):
    """Give every colour image in the scene a real file name before FBX export: <data_id>.jpg or .png (by the image's actual bytes),
    written to a temp folder and set as the image's file path, so the FBX embeds it as <data_id>.fbm/<data_id>.<ext> and Unity sees a real extension.
    Returns the list of file names used."""
    # Temp folder for the renamed copies (outside the repository).
    tmp = os.path.join(tempfile.gettempdir(), "m3d_textures")
    os.makedirs(tmp, exist_ok=True)
    # File names written.
    names = []
    for img in [i for i in bpy.data.images if i.type == 'IMAGE' and (i.packed_file or i.filepath)]:
        # Raw bytes of the image as the generator delivered it.
        data = img.packed_file.data if img.packed_file else open(bpy.path.abspath(img.filepath), "rb").read()
        ext = ".jpg" if data[:2] == b"\xff\xd8" else ".png" if data[:4] == b"\x89PNG" else None
        if ext is None:
            raise RuntimeError("unknown image format for %s" % data_id)
        name = data_id + ext if not names else "%s_%d%s" % (data_id, len(names), ext)
        path = os.path.join(tmp, name)
        with open(path, "wb") as f:
            f.write(data)
        img.name = name
        img.filepath = path
        img.filepath_raw = path
        names.append(name)
    return names


def fbx_embedded_textures(path):
    """Read a binary FBX and return [(file name, extension matches the embedded bytes)] for each embedded image."""
    with open(path, "rb") as f:
        d = f.read()
    # Result list.
    out = []
    for m in re.finditer(rb"RelativeFilenameS", d):
        i = m.end()
        ln = int.from_bytes(d[i:i + 4], "little")
        name = d[i + 4:i + 4 + ln].decode(errors="replace")
        if not re.search(r"\.(jpg|png)$", name):
            out.append((name, False))
            continue
        # The embedded Content record follows the file names: "Content" R <len> <bytes>.
        c = d.find(b"\x07ContentR", i)
        cl = int.from_bytes(d[c + 9:c + 13], "little")
        magic = d[c + 13:c + 17]
        ok = (name.endswith(".jpg") and magic[:2] == b"\xff\xd8") or (name.endswith(".png") and magic == b"\x89PNG")
        out.append((name, ok and cl > 1000))
    return out


def measure_rest(fbx):
    """Import an FBX into a fresh scene and measure its rest pose: pose bones cleared, no action, armature in REST position.
    Returns (longest footprint side, width x, depth y, height z, lowest z) in footprint units (the model is normalised to longest side 1.0, lowest 0)."""
    reset_scene()
    bpy.ops.import_scene.fbx(filepath=fbx)
    for a in [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']:
        a.data.pose_position = 'REST'
        if a.animation_data:
            a.animation_data.action = None
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    # Deformed mesh vertices in world space.
    dg = bpy.context.evaluated_depsgraph_get()
    ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    ev = ms[0].evaluated_get(dg)
    me = ev.to_mesh()
    pts = [ev.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    xs, ys, zs = [p.x for p in pts], [p.y for p in pts], [p.z for p in pts]
    w, d, h = max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)
    return (max(w, d), w, d, h, min(zs))


def rest_ok(m):
    """True when a measure_rest result satisfies the rule: longest footprint side 1.0 and lowest point 0, both within REST_TOL."""
    return abs(m[0] - 1.0) <= REST_TOL and abs(m[4]) <= REST_TOL


def export_fbx(path, objects):
    """Export `objects` (mesh and optional armature) to an FBX at `path` for Unity: texture embedded,
    forward -Z / up Y, scale and space baked in, every NLA strip baked as one clip, no leaf bones (static meshes have no strips)."""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'ARMATURE'},
        path_mode='COPY', embed_textures=True,
        axis_forward='-Z', axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
        add_leaf_bones=False, bake_anim=True,
        # Clips come from NLA strips (one track per action, named like the action): in this mode the FBX take is named exactly
        # "idle"; with bake_anim_use_all_actions Blender 4.0 names it "Armature|idle" (checked by reading the FBX strings).
        bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True, bake_anim_use_all_bones=True,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        mesh_smooth_type='FACE')


def _setup_world_and_light():
    """Give the scene a neutral grey world and one sun lamp (only once per scene)."""
    # Scene being rendered.
    sc = bpy.context.scene
    if sc.world is None:
        sc.world = bpy.data.worlds.new("w")
    sc.world.use_nodes = True
    # World background node: colour (linear RGBA) and strength (unit: multiplier, range 0 to 10).
    bg = sc.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.55, 0.57, 0.6, 1)
    bg.inputs[1].default_value = 1.0
    if "sun" not in bpy.data.objects:
        # Sun lamp: strength 3 (W/m2), pointing down and slightly from the front-left.
        sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
        sun.data.energy = 3.0
        sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-30))
        sc.collection.objects.link(sun)


def add_ground(radius=3.0):
    """Add a grey disc at z=0 so feet and shadows can be judged (radius in Blender units, range 0.5 to 20)."""
    if "ground" in bpy.data.objects:
        return
    bpy.ops.mesh.primitive_circle_add(vertices=48, radius=radius, fill_type='NGON', location=(0, 0, -0.002))
    # The disc object.
    g = bpy.context.active_object
    g.name = "ground"
    # Matte grey material for the disc (linear RGBA).
    m = bpy.data.materials.new("ground")
    m.diffuse_color = (0.35, 0.36, 0.38, 1)
    g.data.materials.append(m)


def render_view(out_path, view, scale, centre_z, res=256):
    """Render the scene with an orthographic Cycles camera on CPU (16 samples, no denoiser).
    view: key of VIEWS. scale: orthographic width in Blender units. centre_z: height the camera looks at. res: pixels, square."""
    _setup_world_and_light()
    # Scene being rendered.
    sc = bpy.context.scene
    # Azimuth and elevation of the camera for this view (degrees).
    az, el = VIEWS[view]
    # Camera distance from the target (Blender units); orthographic, so it only has to clear the model.
    dist = 10.0
    # Unit vector from the target towards the camera.
    d = mathutils.Vector((math.sin(math.radians(az)) * math.cos(math.radians(el)),
                          -math.cos(math.radians(az)) * math.cos(math.radians(el)),
                          math.sin(math.radians(el))))
    # Point the camera looks at.
    target = mathutils.Vector((0, 0, centre_z))
    if "cam" not in bpy.data.objects:
        sc.collection.objects.link(bpy.data.objects.new("cam", bpy.data.cameras.new("cam")))
    # The camera object.
    cam = bpy.data.objects["cam"]
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = scale
    cam.data.clip_end = 100
    cam.location = target + d * dist
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    if el == 90:
        # Straight down: camera up is +Y so the model's front (-Y) is at the bottom of the image.
        cam.rotation_euler = (0, 0, 0)
    sc.camera = cam
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = False
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.view_settings.view_transform = 'Standard'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.filepath = out_path
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
