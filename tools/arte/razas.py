#!/usr/bin/env python3
"""Prepara los modelos de las cinco razas del partido 3D a partir de los packs CC0 de Quaternius.

Fuentes (tiers *Standard* gratuitos, CC0, descargados a `out/arte/q/`, no versionados):
  - Universal Base Characters: cabeza del cuerpo base (`Superhero_Male_FullBody`), pelo y barba.
  - Modular Character Outfits - Fantasy: `Male_Peasant` (campesino) y `Male_Ranger` (explorador con capucha).
  - Bestiary - Dungeon Monsters Kit: `Imp` (con su textura verde, `T_Imp_BaseColor_2`).

Todos van sobre el esqueleto UAL de Quaternius; el retargeting a los clips de Mixamo lo hace el importador de Godot
(`BoneMap` + `SkeletonProfileHumanoid`, ver `Game/models/races/*.import` y `Game/Ui/PlayerModel.cs`).

Lo que hace, por fichero:
  - Copia el glTF quitando lo que no se usa en el partido (la maza del Imp) y los mapas de normales, ORM y
    rugosidad: a la distancia de la retransmisión no se ven y multiplicaban el tamaño por cuatro.
  - Reduce las texturas de color a <= 1024 px (pelo y ojos, menos).
  - Del cuerpo base sólo deja la CABEZA y el cuello (triángulos cuyo peso dominante es `Head`/`neck_01`): el
    traje cubre el resto, y el cuerpo entero asomaba por dentro de la ropa.
  - Pinta en el ALFA de la textura de la ropa la máscara de lo que se tiñe del color del equipo: la región UV de la
    camisa (`*_Body`) del traje y los calzones, el collar y las cadenas del Imp. El alfa no es transparencia: el material
    del partido (`Game/models/races/team_tint.gdshader`) lo lee como máscara.

Determinista; reescribe `Game/models/races/` (menos `.import`, `LICENSE` y el shader).

Uso:  out/arte-venv/bin/python tools/arte/razas.py
"""
from __future__ import annotations

import copy
import io
import json
import os
import struct
import sys
import zipfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
# Los zips se descargaron una vez en el checkout principal; un worktree los busca allí.
SRC_CANDIDATES = [os.path.join(ROOT, "out", "arte", "q"), "/home/martinelola92/underleague/out/arte/q"]
OUT = os.path.join(ROOT, "Game", "models", "races")

ZIP_BASE = "universal-base-characters/Universal Base Characters[Standard].zip"
ZIP_OUTFITS = "modular-character-outfits-fantasy/Modular Character Outfits - Fantasy[Standard].zip"
ZIP_BESTIARY = "bestiary-dungeon-monsters-kit/Bestiary - Dungeon Monsters Kit[Standard].zip"

BASE_DIR = "Universal Base Characters[Standard]/Base Characters/Godot - UE/"
HAIR_DIR = "Universal Base Characters[Standard]/Hairstyles/Rigged to Head Bone/glTF (Godot -Unreal)/"
OUTFIT_DIR = "Modular Character Outfits - Fantasy[Standard]/Exports/glTF (Godot-Unreal)/Outfits/"
BESTIARY_DIR = "Bestiary - Dungeon Monsters Kit[Standard]/"

MAX_TEXTURE = 1024
SMALL_TEXTURE = 512

COMPONENT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class Source:
    def __init__(self, zip_path: str):
        self.zip = zipfile.ZipFile(zip_path)

    def read(self, name: str) -> bytes:
        return self.zip.read(name)


def find_src() -> str:
    for candidate in SRC_CANDIDATES:
        if os.path.exists(os.path.join(candidate, ZIP_BASE)):
            return candidate
    sys.exit("no están los zips de Quaternius en out/arte/q/ (ver docs/ui/arte.md)")


class Gltf:
    """Un glTF en memoria: JSON + un único buffer binario."""

    def __init__(self, doc: dict, binary: bytearray, images: dict[int, bytes]):
        self.doc = doc
        self.bin = binary
        self.images = images  # índice de imagen -> bytes PNG originales

    @staticmethod
    def from_gltf(src: Source, folder: str, name: str) -> "Gltf":
        doc = json.loads(src.read(folder + name))
        binary = bytearray(src.read(folder + doc["buffers"][0]["uri"]))
        images = {}
        for i, image in enumerate(doc.get("images", [])):
            try:
                images[i] = src.read(folder + image["uri"])
            except KeyError:
                images[i] = b""
        return Gltf(doc, binary, images)

    @staticmethod
    def from_glb(data: bytes) -> "Gltf":
        length = struct.unpack("<I", data[12:16])[0]
        doc = json.loads(data[20:20 + length])
        offset = 20 + length
        bin_length = struct.unpack("<I", data[offset:offset + 4])[0]
        binary = bytearray(data[offset + 8:offset + 8 + bin_length])
        images = {}
        for i, image in enumerate(doc.get("images", [])):
            view = doc["bufferViews"][image["bufferView"]]
            start = view.get("byteOffset", 0)
            images[i] = bytes(binary[start:start + view["byteLength"]])
        return Gltf(doc, binary, images)

    def accessor(self, index: int) -> np.ndarray:
        acc = self.doc["accessors"][index]
        view = self.doc["bufferViews"][acc["bufferView"]]
        dtype = np.dtype(COMPONENT[acc["componentType"]])
        width = WIDTH[acc["type"]]
        start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
        stride = view.get("byteStride", 0) or dtype.itemsize * width
        count = acc["count"]
        raw = np.frombuffer(bytes(self.bin), dtype=np.uint8)
        rows = np.lib.stride_tricks.as_strided(raw[start:], shape=(count, dtype.itemsize * width), strides=(stride, 1))
        return np.ascontiguousarray(rows).view(dtype).reshape(count, width)

    def append_indices(self, indices: np.ndarray) -> int:
        while len(self.bin) % 4:
            self.bin.append(0)
        data = indices.astype(np.uint32).tobytes()
        self.doc["bufferViews"].append({"buffer": 0, "byteOffset": len(self.bin), "byteLength": len(data), "target": 34963})
        self.bin.extend(data)
        self.doc["accessors"].append({
            "bufferView": len(self.doc["bufferViews"]) - 1, "componentType": 5125, "count": int(indices.size), "type": "SCALAR",
        })
        return len(self.doc["accessors"]) - 1

    def mesh_node(self, name: str) -> dict:
        for node in self.doc["nodes"]:
            if node.get("name") == name and "mesh" in node:
                return node
        raise KeyError(name)

    def drop_nodes(self, names: set[str]) -> None:
        """Quita nodos de malla de la escena (se quedan huérfanos en el JSON, que el importador ignora)."""
        dropped = {i for i, n in enumerate(self.doc["nodes"]) if n.get("name") in names}
        for node in self.doc["nodes"]:
            if "children" in node:
                node["children"] = [c for c in node["children"] if c not in dropped]
        for i in dropped:
            node = self.doc["nodes"][i]
            node.pop("mesh", None)
            node.pop("skin", None)

    def uv_triangles(self, mesh_name: str):
        mesh = self.doc["meshes"][self.mesh_node(mesh_name)["mesh"]]
        for primitive in mesh["primitives"]:
            uv = self.accessor(primitive["attributes"]["TEXCOORD_0"])
            idx = self.accessor(primitive["indices"]).reshape(-1, 3)
            yield primitive.get("material"), uv, idx


def keep_dominant(g: Gltf, mesh_name: str, bones: set[str]) -> None:
    """Deja en la malla sólo los triángulos cuyos tres vértices pesan más de la mitad en `bones`."""
    node = g.mesh_node(mesh_name)
    joints = [g.doc["nodes"][j]["name"] for j in g.doc["skins"][node["skin"]]["joints"]]
    wanted = np.array([name in bones for name in joints])
    for primitive in g.doc["meshes"][node["mesh"]]["primitives"]:
        j = g.accessor(primitive["attributes"]["JOINTS_0"]).astype(np.int64)
        w = g.accessor(primitive["attributes"]["WEIGHTS_0"]).astype(np.float64)
        if w.max() > 1.5:  # pesos normalizados como enteros
            w = w / w.sum(axis=1, keepdims=True)
        share = (w * wanted[j]).sum(axis=1)
        tri = g.accessor(primitive["indices"]).reshape(-1, 3)
        keep = (share[tri] > 0.5).all(axis=1)
        primitive["indices"] = g.append_indices(tri[keep].reshape(-1))
        print(f"  {mesh_name}: {keep.sum()} de {len(tri)} triángulos (cabeza y cuello)")


def mask_from_uv(g: Gltf, meshes: list[str], size: int, material: int | None = None) -> Image.Image:
    """Máscara (L) de la región UV que ocupan esas mallas en su textura, un poco dilatada para no dejar costuras."""
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    for name in meshes:
        for prim_material, uv, idx in g.uv_triangles(name):
            if material is not None and prim_material != material:
                continue
            pts = uv * size
            for a, b, c in idx:
                draw.polygon([tuple(pts[a]), tuple(pts[b]), tuple(pts[c])], fill=255)
    return mask.filter(ImageFilter.MaxFilter(5))


def write(g: Gltf, name: str, textures: dict[int, tuple[str, Image.Image]], keep_emissive: bool = False) -> None:
    """Escribe `name.gltf` + `name.bin` con sólo las texturas de color (las de `textures`, ya procesadas)."""
    doc = copy.deepcopy(g.doc)
    used_textures = []

    def remap(info: dict | None) -> dict | None:
        if info is None:
            return None
        texture = doc["textures"][info["index"]]
        source = texture["source"]
        if source not in textures:
            return None
        if source not in used_textures:
            used_textures.append(source)
        return {"index": used_textures.index(source), **({"texCoord": info["texCoord"]} if "texCoord" in info else {})}

    for material in doc.get("materials", []):
        pbr = material.setdefault("pbrMetallicRoughness", {})
        pbr.pop("metallicRoughnessTexture", None)
        pbr["metallicFactor"] = 0.0
        pbr["roughnessFactor"] = 0.9
        material.pop("normalTexture", None)
        material.pop("occlusionTexture", None)
        base = remap(pbr.get("baseColorTexture"))
        if base:
            pbr["baseColorTexture"] = base
        else:
            pbr.pop("baseColorTexture", None)
        emissive = remap(material.get("emissiveTexture")) if keep_emissive else None
        if emissive:
            material["emissiveTexture"] = emissive
        else:
            material.pop("emissiveTexture", None)
            material.pop("emissiveFactor", None)
        material.pop("extensions", None)

    doc["images"] = []
    doc["textures"] = []
    for source in used_textures:
        file_name, image = textures[source]
        image.save(os.path.join(OUT, file_name), optimize=True)
        doc["images"].append({"uri": file_name, "name": file_name[:-4], "mimeType": "image/png"})
        doc["textures"].append({"source": len(doc["images"]) - 1, "sampler": 0})
    doc["samplers"] = [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]
    if not doc["textures"]:
        doc.pop("textures")
        doc.pop("images")
        doc.pop("samplers")
    # Sólo los tramos del buffer que leen los accesores: fuera las imágenes incrustadas del GLB.
    used_views = sorted({acc["bufferView"] for acc in doc["accessors"] if "bufferView" in acc})
    packed = bytearray()
    new_index = {}
    views = []
    for old in used_views:
        view = dict(doc["bufferViews"][old])
        while len(packed) % 4:
            packed.append(0)
        start = view.get("byteOffset", 0)
        chunk = g.bin[start:start + view["byteLength"]]
        view["buffer"] = 0
        view["byteOffset"] = len(packed)
        packed.extend(chunk)
        new_index[old] = len(views)
        views.append(view)
    for acc in doc["accessors"]:
        if "bufferView" in acc:
            acc["bufferView"] = new_index[acc["bufferView"]]
    doc["bufferViews"] = views
    doc["buffers"] = [{"uri": name + ".bin", "byteLength": len(packed)}]
    doc.pop("extensionsUsed", None)
    doc.pop("extensionsRequired", None)
    with open(os.path.join(OUT, name + ".bin"), "wb") as f:
        f.write(packed)
    with open(os.path.join(OUT, name + ".gltf"), "w") as f:
        json.dump(doc, f, separators=(",", ":"))


def shrink(data: bytes, size: int) -> Image.Image:
    image = Image.open(io.BytesIO(data))
    image = image.convert("RGBA" if image.mode in ("RGBA", "LA", "P") else "RGB")
    if image.width > size:
        image = image.resize((size, size), Image.LANCZOS)
    return image


def green_mask(color: Image.Image) -> Image.Image:
    """Máscara de los píxeles de tono verde (paño del explorador)."""
    rgb = np.asarray(color.convert("RGB")).astype(np.float32)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    green = (g > r * 1.12) & (g > b * 1.12) & (g > 25)
    return Image.fromarray((green * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(3))


def with_mask(color: Image.Image, mask: Image.Image) -> Image.Image:
    rgba = color.convert("RGB").copy()
    rgba.putalpha(mask.resize(rgba.size, Image.LANCZOS))
    return rgba


def image_index(g: Gltf, needle: str) -> int:
    for i, image in enumerate(g.doc["images"]):
        if needle in (image.get("uri") or image.get("name") or ""):
            return i
    raise KeyError(needle)


def material_index(g: Gltf, name: str) -> int:
    return [m.get("name") for m in g.doc["materials"]].index(name)


def main() -> None:
    src_root = find_src()
    os.makedirs(OUT, exist_ok=True)
    for stale in os.listdir(OUT):
        if stale.endswith((".gltf", ".bin", ".png")):
            os.remove(os.path.join(OUT, stale))

    base = Source(os.path.join(src_root, ZIP_BASE))
    outfits = Source(os.path.join(src_root, ZIP_OUTFITS))
    bestiary = Source(os.path.join(src_root, ZIP_BESTIARY))

    # Cabeza: el cuerpo base sin lo que tapa la ropa.
    print("head_male")
    head = Gltf.from_gltf(base, BASE_DIR, "Superhero_Male_FullBody.gltf")
    keep_dominant(head, "SuperHero_Male", {"Head", "neck_01"})
    write(head, "head_male", {
        image_index(head, "T_Superhero_Male_Dark"): ("T_Head_Male.png", shrink(head.images[image_index(head, "T_Superhero_Male_Dark")], MAX_TEXTURE)),
        image_index(head, "T_Eye_Brown"): ("T_Eye.png", shrink(head.images[image_index(head, "T_Eye_Brown")], 256)),
        image_index(head, "T_Hair_1_BaseColor"): ("T_Hair_1.png", shrink(head.images[image_index(head, "T_Hair_1_BaseColor")], SMALL_TEXTURE)),
    })

    # Trajes: la camisa (*_Body) se tiñe del color del equipo; la máscara va en el alfa de la textura del traje.
    for outfit, body, cloth, texture in (
        ("Male_Peasant", ["Male_Peasant_Body"], "MI_Peasant", "T_Peasant_BaseColor"),
        ("Male_Ranger", ["Male_Ranger_Body"], "MI_Ranger", "T_Ranger_BaseColor"),
    ):
        name = outfit.lower().replace("male_", "") + "_male"
        print(name)
        g = Gltf.from_gltf(outfits, OUTFIT_DIR, outfit + ".gltf")
        cloth_image = image_index(g, texture)
        color = shrink(g.images[cloth_image], MAX_TEXTURE)
        if outfit == "Male_Ranger":
            # El explorador: el paño verde (capucha, capa y túnica) es lo que se ve desde la grada, no el chaleco.
            mask = green_mask(color)
        else:
            mask = mask_from_uv(g, body, MAX_TEXTURE, material_index(g, cloth))
        skin = image_index(g, "T_Regular_Male_Dark_BaseColor")
        write(g, name, {
            cloth_image: (f"T_{outfit.split('_')[1]}.png", with_mask(color, mask)),
            skin: ("T_Skin_Male.png", shrink(g.images[skin], MAX_TEXTURE)),
        })

    # Pelo y barba, cosidos al hueso de la cabeza.
    for hair in ("Hair_Buzzed", "Hair_SimpleParted", "Hair_Long", "Hair_Beard"):
        name = hair.lower()
        print(name)
        g = Gltf.from_gltf(base, HAIR_DIR, hair + ".gltf")
        index = image_index(g, "BaseColor")
        file_name = "T_Hair_1.png" if "Hair_1" in g.doc["images"][index]["uri"] else "T_Hair_2.png"
        write(g, name, {index: (file_name, shrink(g.images[index], SMALL_TEXTURE))})

    # Orco: el Imp, verde, sin la maza; los pantalones se tiñen del color del equipo.
    print("imp")
    imp = Gltf.from_glb(bestiary.read(BESTIARY_DIR + "Exports/GLB (Godot-Unreal)/Imp.glb"))
    imp.drop_nodes({"Imp_Mace"})
    green = shrink(bestiary.read(BESTIARY_DIR + "Textures/T_Imp_BaseColor_2.png"), MAX_TEXTURE)
    # Calzones, collar de pinchos y cadenas: con sólo los calzones, un partido de orcos contra orcos no se leía (9 oct).
    shorts = mask_from_uv(imp, ["Imp_Shorts", "Imp_SpikedCollar", "Imp_Chains"], MAX_TEXTURE)
    write(imp, "imp", {
        image_index(imp, "T_Imp_BaseColor"): ("T_Imp.png", with_mask(green, shorts)),
        image_index(imp, "T_Imp_Emissive"): ("T_Imp_Emissive.png", shrink(imp.images[image_index(imp, "T_Imp_Emissive")], 256)),
    }, keep_emissive=True)

    total = sum(os.path.getsize(os.path.join(OUT, f)) for f in os.listdir(OUT))
    print(f"Game/models/races: {total / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
