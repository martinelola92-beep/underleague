"""Rig automático de un personaje generado (TRELLIS) en Blender, sin interfaz (9 oct 2026).

  out/blender/blender-4.2.3-linux-x64/blender -b --python tools/arte/rig_blender.py -- <entrada.glb> <salida.glb> [proporciones]

Coloca un esqueleto humanoide con los nombres del perfil de Godot (Hips, Spine, Chest, UpperChest, Neck, Head,
Left/RightShoulder, UpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot, Toes) midiendo la malla: alturas por fracción
del alto (las de la hoja de modelo de la raza) y posiciones laterales por el centroide de los vértices de cada brazo y
pierna a esa altura. Después «Con pesos automáticos» (difusión de calor) y, si falla, por envolvente. El personaje
queda con los pies en z=0, mirando a -Y (el frente de Blender) y de 1,8 m de alto.
"""
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
HEIGHT = 1.8

# Fracciones del alto por defecto (orco de la hoja de modelo: piernas cortas, brazos largos). Se pasan como
# "clave=valor,..." en el tercer argumento para otras razas.
F = dict(head_top=1.0, neck=0.80, shoulder=0.76, chest=0.66, spine=0.56, hips=0.46, elbow=0.56, wrist=0.40,
         fingertip=0.33, knee=0.25, ankle=0.06)
if len(argv) > 2:
    for pair in argv[2].split(","):
        k, v = pair.split("=")
        F[k] = float(v)

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
# Fuera los padres del import (vacíos con rotación) conservando la transformación, y aplicarla.
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

verts = [v.co.copy() for v in mesh.data.vertices]
zmin = min(v.z for v in verts)
zmax = max(v.z for v in verts)
scale = HEIGHT / (zmax - zmin)
cx = sum(v.x for v in verts) / len(verts)
cy = sum(v.y for v in verts) / len(verts)
for v in mesh.data.vertices:
    v.co = Vector(((v.co.x - cx) * scale, (v.co.y - cy) * scale, (v.co.z - zmin) * scale))
verts = [v.co.copy() for v in mesh.data.vertices]


def z(frac):
    return frac * HEIGHT


def band(zc, half=0.03):
    return [v for v in verts if abs(v.z - zc) < half * HEIGHT]


def side_centroid(zc, sign, outer=False):
    """Centroide de los vértices de un lado a esa altura; `outer` se queda con el tercio más exterior (el brazo)."""
    pts = [v for v in band(zc) if v.x * sign > 0]
    if not pts:
        return Vector((0.15 * sign, 0, zc))
    if outer:
        reach = max(abs(p.x) for p in pts)
        pts = [p for p in pts if abs(p.x) > reach * 0.72] or pts
    n = len(pts)
    return Vector((sum(p.x for p in pts) / n, sum(p.y for p in pts) / n, zc))


def center(zc):
    pts = band(zc)
    n = max(len(pts), 1)
    return Vector((0.0, sum(p.y for p in pts) / n if pts else 0.0, zc))


joints = {
    "Hips": center(z(F["hips"])),
    "Spine": center(z(F["spine"])),
    "Chest": center(z(F["chest"])),
    "UpperChest": center(z((F["chest"] + F["shoulder"]) / 2)),
    "Neck": center(z(F["neck"])),
    "Head": center(z(F["neck"] + 0.04)),
    "HeadTop": center(z(F["head_top"] - 0.005)),
}
for side, sign in (("Left", 1), ("Right", -1)):
    # Ojo: en Blender, con el personaje mirando a -Y, su izquierda está en +X.
    sh = side_centroid(z(F["shoulder"]), sign, outer=True)
    sh_inner = Vector((sh.x * 0.35, joints["UpperChest"].y, z(F["shoulder"]) - 0.01 * HEIGHT))
    joints[f"{side}Shoulder"] = sh_inner
    joints[f"{side}UpperArm"] = Vector((sh.x * 0.82, sh.y, z(F["shoulder"]) - 0.02 * HEIGHT))
    joints[f"{side}LowerArm"] = side_centroid(z(F["elbow"]), sign, outer=True)
    joints[f"{side}Hand"] = side_centroid(z(F["wrist"]), sign, outer=True)
    joints[f"{side}HandTip"] = side_centroid(z(F["fingertip"]), sign, outer=True)
    hip = side_centroid(z(F["hips"] - 0.04), sign)
    joints[f"{side}UpperLeg"] = Vector((hip.x * 0.8, joints["Hips"].y, z(F["hips"] - 0.03)))
    joints[f"{side}LowerLeg"] = side_centroid(z(F["knee"]), sign)
    ankle = side_centroid(z(F["ankle"]), sign)
    joints[f"{side}Foot"] = ankle
    toe_pts = [v for v in verts if v.z < 0.04 * HEIGHT and v.x * sign > 0]
    front = min(toe_pts, key=lambda p: p.y) if toe_pts else ankle + Vector((0, -0.12, 0))
    joints[f"{side}Toes"] = Vector((ankle.x, (ankle.y + front.y) / 2, 0.02 * HEIGHT))
    joints[f"{side}ToeTip"] = Vector((ankle.x, front.y, 0.02 * HEIGHT))

# (hueso, cabeza, cola, padre)
B = [
    ("Hips", "Hips", "Spine", None),
    ("Spine", "Spine", "Chest", "Hips"),
    ("Chest", "Chest", "UpperChest", "Spine"),
    ("UpperChest", "UpperChest", "Neck", "Chest"),
    ("Neck", "Neck", "Head", "UpperChest"),
    ("Head", "Head", "HeadTop", "Neck"),
]
for s in ("Left", "Right"):
    B += [
        (f"{s}Shoulder", f"{s}Shoulder", f"{s}UpperArm", "UpperChest"),
        (f"{s}UpperArm", f"{s}UpperArm", f"{s}LowerArm", f"{s}Shoulder"),
        (f"{s}LowerArm", f"{s}LowerArm", f"{s}Hand", f"{s}UpperArm"),
        (f"{s}Hand", f"{s}Hand", f"{s}HandTip", f"{s}LowerArm"),
        (f"{s}UpperLeg", f"{s}UpperLeg", f"{s}LowerLeg", "Hips"),
        (f"{s}LowerLeg", f"{s}LowerLeg", f"{s}Foot", f"{s}UpperLeg"),
        (f"{s}Foot", f"{s}Foot", f"{s}Toes", f"{s}LowerLeg"),
        (f"{s}Toes", f"{s}Toes", f"{s}ToeTip", f"{s}Foot"),
    ]

arm_data = bpy.data.armatures.new("Armature")
arm = bpy.data.objects.new("Armature", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
bones = {}
for name, head, tail, parent in B:
    b = arm_data.edit_bones.new(name)
    b.head = joints[head]
    b.tail = joints[tail]
    if (b.tail - b.head).length < 0.01:
        b.tail = b.head + Vector((0, 0, 0.05))
    if parent:
        b.parent = bones[parent]
        b.use_connect = False
    bones[name] = b
bpy.ops.object.mode_set(mode="OBJECT")

# Pesos: la malla generada no es cerrada y la difusión de calor falla sobre ella. Se calculan sobre una copia rehecha
# en vóxeles (cerrada) y se transfieren a la original por el vértice más cercano.
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
bpy.context.view_layer.objects.active = mesh
bpy.ops.object.duplicate()
proxy = bpy.context.view_layer.objects.active
remesh = proxy.modifiers.new("remesh", "REMESH")
remesh.mode = "VOXEL"
remesh.voxel_size = 0.018
bpy.ops.object.modifier_apply(modifier="remesh")

bpy.ops.object.select_all(action="DESELECT")
proxy.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
weighted = sum(1 for v in proxy.data.vertices if len(v.groups) > 0)
print(f"[rig] intermedia: vértices con peso {weighted}/{len(proxy.data.vertices)}")

for bone in arm_data.bones:
    if bone.name not in mesh.vertex_groups:
        mesh.vertex_groups.new(name=bone.name)
transfer = mesh.modifiers.new("transfer", "DATA_TRANSFER")
transfer.object = proxy
transfer.use_vert_data = True
transfer.data_types_verts = {"VGROUP_WEIGHTS"}
transfer.vert_mapping = "POLYINTERP_NEAREST"
transfer.layers_vgroup_select_src = "ALL"
transfer.layers_vgroup_select_dst = "NAME"
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
bpy.context.view_layer.objects.active = mesh
bpy.ops.object.modifier_apply(modifier="transfer")
bpy.data.objects.remove(proxy, do_unlink=True)

mod = mesh.modifiers.new("Armature", "ARMATURE")
mod.object = arm
mesh.parent = arm
weighted = sum(1 for v in mesh.data.vertices if len(v.groups) > 0)
print(f"[rig] vértices con peso: {weighted}/{len(mesh.data.vertices)}")

bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", use_selection=False, export_skins=True,
                          export_animations=False, export_yup=True)
for k, v in joints.items():
    print(f"[rig] {k}: {tuple(round(c, 3) for c in v)}")
print("[rig] escrito", dst)
