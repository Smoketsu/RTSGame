# rts/tools/Models3D/fbx_pose.py
# Reads the default (un-animated) pose of the bones stored in a binary FBX, the pose Unity shows before any clip plays, without Blender.
# Used to check that a rigged FBX stores the rest pose: every bone's local rotation 0 and scale 1 (translation is the bone's rest offset).
# Usage (plain Python 3): python3 rts/tools/Models3D/fbx_pose.py <file.fbx> [<bone name>]   prints bones whose default rotation/scale is not rest.
import struct
import sys


def read_nodes(d, pos, end, wide):
    """Parse FBX node records from byte offset pos up to end. wide: True for the 64-bit record header (FBX 7.5+).
    Returns a list of (name, props, children); props only keeps strings, numbers (int/float) and skips raw arrays."""
    nodes = []
    # Size of the three header fields: end offset, property count, property list length.
    hs = 8 if wide else 4
    fmt = "<QQQ" if wide else "<III"
    while pos < end:
        eo, npr, plen = struct.unpack_from(fmt, d, pos)
        nl = d[pos + 3 * hs]
        if eo == 0:
            break
        name = d[pos + 3 * hs + 1:pos + 3 * hs + 1 + nl].decode()
        p = pos + 3 * hs + 1 + nl
        props = []
        for _ in range(npr):
            t = chr(d[p])
            p += 1
            if t == "S" or t == "R":
                ln = struct.unpack_from("<I", d, p)[0]
                props.append(d[p + 4:p + 4 + ln].decode(errors="replace") if t == "S" else b"")
                p += 4 + ln
            elif t in "YCIFDL":
                sz = {"Y": 2, "C": 1, "I": 4, "F": 4, "D": 8, "L": 8}[t]
                props.append(struct.unpack_from("<" + {"Y": "h", "C": "b", "I": "i", "F": "f", "D": "d", "L": "q"}[t], d, p)[0])
                p += sz
            else:
                # Array types: count, encoding, compressed length, then the data.
                _, _, cl = struct.unpack_from("<III", d, p)
                p += 12 + cl
        children = read_nodes(d, p, eo, wide) if p < eo else []
        nodes.append((name, props, children))
        pos = eo
    return nodes


def bone_defaults(path):
    """Return {bone name: {"Lcl Rotation": (x, y, z), "Lcl Scaling": (x, y, z), "Lcl Translation": (x, y, z)}} for every LimbNode in the file."""
    with open(path, "rb") as f:
        d = f.read()
    # FBX version number from the header decides the record header width.
    ver = struct.unpack_from("<I", d, 23)[0]
    top = read_nodes(d, 27, len(d), ver >= 7500)
    out = {}
    for (n, pr, ch) in top:
        if n != "Objects":
            continue
        for (m, mp, mc) in ch:
            if m == "Model" and len(mp) >= 3 and mp[2] == "LimbNode":
                bone = mp[1].split("\x00")[0]
                vals = {}
                for (c, cp, cc) in mc:
                    if c == "Properties70":
                        for (q, qp, _) in cc:
                            if qp and qp[0] in ("Lcl Rotation", "Lcl Scaling", "Lcl Translation"):
                                vals[qp[0]] = tuple(qp[4:7])
                out[bone] = vals
    return out


if __name__ == "__main__":
    # Bones whose stored default rotation is not 0 or scale is not 1 (degrees; scale factor).
    res = bone_defaults(sys.argv[1])
    print("%d bones" % len(res))
    for b, v in res.items():
        if len(sys.argv) > 2 and b != sys.argv[2]:
            continue
        rot, scl = v.get("Lcl Rotation", (0, 0, 0)), v.get("Lcl Scaling", (1, 1, 1))
        if max(abs(x) for x in rot) > 0.01 or max(abs(x - 1) for x in scl) > 0.001 or len(sys.argv) > 2:
            print(b, "rot", tuple(round(x, 3) for x in rot), "scale", tuple(round(x, 4) for x in scl), "trans", tuple(round(x, 4) for x in v.get("Lcl Translation", ())))
