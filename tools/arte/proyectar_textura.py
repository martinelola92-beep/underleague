"""Textura de un modelo sin pintar (Hunyuan3D-2 da sólo la forma) proyectando las vistas de su hoja de modelo (9 oct 2026).

  out/blender/blender-4.2.3-linux-x64/blender -b --python tools/arte/proyectar_textura.py -- <entrada.glb> <salida.glb> <raza> [caras]

Monta un atlas con frente, perfil y espalda (`Game/Art/Figures/<raza>_<vista>.png`, con los pies en el borde inferior y la
silueta a sangre) y da a cada cara la vista hacia la que mira su normal: frente o espalda si mira hacia delante o atrás, y
el perfil (reflejado para el otro lado) si mira a un costado. Cada vista se encaja en la caja de la malla vista desde ese
lado. Antes reduce la malla a `caras` (20 000 por defecto) y quita la peana plana que el generador deja bajo los pies.
La malla de entrada mira a -Y en Blender, como la de `rig_blender.py`.
"""
import os
import sys

import bpy
import bmesh
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, race = argv[0], argv[1], argv[2]
faces_target = int(argv[3]) if len(argv) > 3 else 20000
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
mesh = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

dec = mesh.modifiers.new("dec", "DECIMATE")
dec.ratio = min(1.0, faces_target / max(1, len(mesh.data.polygons)))
bpy.ops.object.modifier_apply(modifier="dec")

# Peana: caras casi horizontales pegadas al suelo que sobresalen de las botas.
bm = bmesh.new()
bm.from_mesh(mesh.data)
zmin = min(v.co.z for v in bm.verts)
zmax = max(v.co.z for v in bm.verts)
floor = zmin + 0.012 * (zmax - zmin)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(v.co.z < floor for v in f.verts)], context="FACES")
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context="VERTS")

lo = Vector([min(v.co[i] for v in bm.verts) for i in range(3)])
hi = Vector([max(v.co[i] for v in bm.verts) for i in range(3)])
size = hi - lo

# Atlas: las tres vistas en fila, a la misma altura.
images = []
for view in ("front", "side", "back"):
    path = os.path.join(ROOT, "Game", "Art", "Figures", f"{race}_{view}.png")
    images.append(bpy.data.images.load(path))
H = max(im.size[1] for im in images)
widths = [int(im.size[0] * H / im.size[1]) for im in images]
W = sum(widths)
atlas = bpy.data.images.new(f"{race}_atlas", W, H, alpha=True)
pixels = [0.0] * (W * H * 4)
x0s = []
x0 = 0
for im, w in zip(images, widths):
    x0s.append(x0)
    src_px = list(im.pixels)
    sw, sh = im.size
    for y in range(H):
        sy = min(sh - 1, int(y * sh / H))
        for x in range(w):
            sx = min(sw - 1, int(x * sw / w))
            si = (sy * sw + sx) * 4
            di = (y * W + x0 + x) * 4
            pixels[di:di + 4] = src_px[si:si + 4]
    x0 += w
atlas.pixels = pixels


def uv_for(view, co, mirror=False):
    """Coordenada en el atlas de un punto proyectado por esa vista (v=0 abajo, como Blender)."""
    i = ("front", "side", "back").index(view)
    v = (co.z - lo.z) / size.z
    if view == "front":
        u = (co.x - lo.x) / size.x
    elif view == "back":
        u = (hi.x - co.x) / size.x
    else:
        # Perfil mirando a la derecha de la imagen = el personaje mira a -Y; visto desde +X, -Y queda a la derecha.
        u = (hi.y - co.y) / size.y
        if mirror:
            u = 1.0 - u
    u = min(max(u, 0.002), 0.998)
    return Vector(((x0s[i] + u * widths[i]) / W, min(max(v, 0.002), 0.998)))


uv_layer = bm.loops.layers.uv.new("UVMap")
for f in bm.faces:
    n = f.normal
    if abs(n.y) >= abs(n.x) * 0.45:  # el perfil casa peor con la malla: sólo para lo que mira claramente de lado
        view, mirror = ("front" if n.y < 0 else "back"), False
    else:
        view, mirror = "side", n.x < 0
    for loop in f.loops:
        loop[uv_layer].uv = uv_for(view, loop.vert.co, mirror)
bm.to_mesh(mesh.data)
bm.free()

atlas.filepath_raw = os.path.splitext(dst)[0] + "_atlas.png"
atlas.file_format = "PNG"
atlas.save()
mat = bpy.data.materials.new(f"{race}_mat")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = atlas
mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 1.0
mesh.data.materials.clear()
mesh.data.materials.append(mat)

bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", use_selection=False, export_yup=True)
print(f"[proyectar] {len(mesh.data.polygons)} caras, atlas {W}x{H}, escrito {dst}")
