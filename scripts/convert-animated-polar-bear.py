"""Convert kenchoo's supplied CC BY 4.0 GLB to Unity FBX, retaining its animation."""
import bpy,zipfile,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1]
archive=root/"_asset_inbox/polar-bear.zip";source=root/"_asset_downloads/AnimatedPolarBear.glb"
with zipfile.ZipFile(archive) as z:source.write_bytes(z.read("source/PolarBear.glb"))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
dest=root/"UnityPort/Assets/ThirdParty/KenchooPolarBear";(dest/"Models").mkdir(parents=True,exist_ok=True);(dest/"Textures").mkdir(exist_ok=True)
for original,name in [("material_0_diffuse","PolarBear_Diffuse.png"),("material_0_normal","PolarBear_Normal.png"),("roughness","PolarBear_Roughness.png")]:
 image=bpy.data.images[original];image.filepath_raw=str(dest/"Textures"/name);image.file_format="PNG";image.save()
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=141;scene.render.fps=30;scene.frame_set(1)
bpy.ops.object.select_all(action="DESELECT")
for o in scene.objects:
 if o.type=="ARMATURE" or (o.type=="MESH" and any(m.type=="ARMATURE" for m in o.modifiers)):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(dest/"Models/PolarBear.fbx"),use_selection=True,object_types={"MESH","ARMATURE"},axis_forward="-Z",axis_up="Y",add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,path_mode="RELATIVE",use_mesh_modifiers=False)
(dest/"License.txt").write_text('"Polar Bear" (https://skfb.ly/oRMzK) by kenchoo is licensed under Creative Commons Attribution 4.0 International.\nhttps://creativecommons.org/licenses/by/4.0/\nSource: https://sketchfab.com/3d-models/polar-bear-deeb7f4add9f4c36abaacdb73ce3e553\n\nUser supplied original GLB and confirmed attribution on 2026-09-14.\nChanges by KeyLearner: GLB-to-FBX conversion with Blender 4.3; original mesh, rig, animation and diffuse/normal/roughness textures retained; omitted Blender bone-display helper geometry; Unity materials/pivot/scale adaptation; smaller instance represents the cub.\n',encoding="utf-8")
records=root/"_asset_downloads/immersion-model-imports.json";data=json.loads(records.read_text());data=[r for r in data if "/GouwPolarBear/" not in r["file"]]
for p in sorted(dest.rglob("*")):
 if not p.is_file() or p.suffix==".meta":continue
 relative=p.relative_to(root).as_posix();data=[r for r in data if r["file"]!=relative]
 data.append(dict(file=relative,archive=archive.name,archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),member="source/PolarBear.glb",sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),conversion="scripts/convert-animated-polar-bear.py",url="https://sketchfab.com/3d-models/polar-bear-deeb7f4add9f4c36abaacdb73ce3e553"))
records.write_text(json.dumps(data,indent=2)+"\n",encoding="utf-8")
print("ANIMATED_BEAR_CONVERTED",[(o.name,len(o.data.vertices)) for o in scene.objects if o.type=="MESH" and o.select_get()])
