"""Import only the pack's verified animated wolf, under the approved CC BY-NC exception."""
import bpy,zipfile,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1];stage=root/"_asset_downloads/WildMesh"
dest=root/"UnityPort/Assets/ThirdParty/WildMeshAnimals";(dest/"Models").mkdir(parents=True,exist_ok=True);(dest/"Textures").mkdir(exist_ok=True)
archive=root/"_asset_inbox/ultimate-animal-pack-100-animals-50-off.zip"
with zipfile.ZipFile(archive) as z:(dest/"Textures/Wolf_Diffuse.png").write_bytes(z.read("textures/T_Wolf.png"))
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(stage/"Realistic Animated Pack.fbx"))
rig=bpy.data.objects["Wolf"];mesh=bpy.data.objects["Wolf.001"]
rig.animation_data.action=next(a for a in bpy.data.actions if "WalkFast" in a.name)
rig.animation_data.action.name="Walk"
scene=bpy.context.scene;scene.frame_start=1;scene.frame_end=24;scene.render.fps=24;scene.frame_set(1)
for m in mesh.data.materials:
 for node in m.node_tree.nodes:
  if node.type=="TEX_IMAGE" and node.image:node.image.filepath=str(dest/"Textures/Wolf_Diffuse.png");node.image.reload()
bpy.ops.object.select_all(action="DESELECT");rig.select_set(True);mesh.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(dest/"Models/Wolf.fbx"),use_selection=True,object_types={"MESH","ARMATURE"},axis_forward="-Z",axis_up="Y",add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,path_mode="RELATIVE",use_mesh_modifiers=False)
(dest/"License.txt").write_text('"ULTIMATE ANIMAL PACK |100 ANIMALS | 50% OFF" (https://skfb.ly/pMR9W) by WildMesh 3D is licensed under Creative Commons Attribution-NonCommercial 4.0 International.\nhttp://creativecommons.org/licenses/by-nc/4.0/\nSource: https://sketchfab.com/3d-models/ultimate-animal-pack-100-animals-50-off-165f755e44fc477fbc4ee41085606dd1\n\nNONCOMMERCIAL USE ONLY unless a separate applicable commercial license is obtained. See NONCOMMERCIAL_ASSETS.md in the repository and distributed notices. User approved this exception on 2026-09-14.\nImported subset: Wolf, original fast walk loop and T_Wolf diffuse texture. The supplied FBX has a combined static display and one separately rigged wolf, not 100 individually animated animals. Changes: selected only wolf; omitted sales-display geometry; converted with Blender 4.3; source walk retained; Unity material/pivot/scale adaptation.\n',encoding="utf-8")
records=root/"_asset_downloads/immersion-model-imports.json";data=json.loads(records.read_text())
for p in sorted(dest.rglob("*")):
 if not p.is_file() or p.suffix==".meta":continue
 relative=p.relative_to(root).as_posix();data=[r for r in data if r["file"]!=relative]
 data.append(dict(file=relative,archive=archive.name,archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),license="CC-BY-NC-4.0",conversion="scripts/convert-wildmesh-wolf.py",url="https://sketchfab.com/3d-models/ultimate-animal-pack-100-animals-50-off-165f755e44fc477fbc4ee41085606dd1"))
records.write_text(json.dumps(data,indent=2)+"\n",encoding="utf-8")
print("WOLF_CONVERTED",len(mesh.data.vertices),"source walk retained")
