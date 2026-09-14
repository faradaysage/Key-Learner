"""Blender-only explicit asset adaptation; never run by an ordinary game build."""
import bpy,json,shutil
from pathlib import Path
root=Path(__file__).resolve().parents[1];stage=root/'_asset_downloads/PolyHavenScenery';target=root/'UnityPort/Assets/ThirdParty/PolyHavenScenery'
(target/'Models').mkdir(parents=True,exist_ok=True);(target/'Textures').mkdir(exist_ok=True)
for name in ['fern_02','coastal_cliff_01','mountainside']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(stage/(name+'.fbx')))
 meshes=[o for o in bpy.data.objects if o.type=='MESH']
 groups=[[o] for o in meshes] if name=='fern_02' else [[o for o in meshes if o.name.endswith(('_LOD1','_LOD2','_LOD3'))]]
 for group in groups:
  bpy.ops.object.select_all(action='DESELECT')
  for o in group:o.select_set(True)
  bpy.context.view_layer.objects.active=group[0]
  out=group[0].name if name=='fern_02' else name
  bpy.ops.export_scene.fbx(filepath=str(target/'Models'/(out+'.fbx')),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
for p in stage.glob('*.png'):shutil.copyfile(p,target/'Textures'/p.name)
# Preserve the authored leaf opacity in the diffuse alpha, so rerunning this
# converter cannot turn the transparent fern cards into solid rectangles.
color=bpy.data.images.load(str(stage/'fern_02_Diffuse.png'),check_existing=False)
alpha=bpy.data.images.load(str(stage/'fern_02_Alpha.png'),check_existing=False)
pixels=list(color.pixels[:]);opacity=alpha.pixels[:]
for i in range(3,len(pixels),4):pixels[i]=opacity[i-3]
color.pixels[:]=pixels;color.filepath_raw=str(target/'Textures/fern_02_Diffuse.png');color.file_format='PNG';color.save()

(target/'License.txt').write_text("Poly Haven scenery, CC0 1.0\nhttps://polyhaven.com/license\nFern 02: Rico Cilliers (modeling), Rob Tuytel (scanning)\nhttps://polyhaven.com/a/fern_02\nCoastal Cliff 01: Rico Cilliers, Rob Tuytel\nhttps://polyhaven.com/a/coastal_cliff_01\nMountainside: Dario Barresi (photography), Rico Cilliers (processing)\nhttps://polyhaven.com/a/mountainside\nAdaptations: separate fern clumps, select authored LOD1-3 for cliffs/mountains, preserve UVs and 2K textures; Unity materials/pivots/scales and LOD distances.\n")
print('Scenery source adaptations exported')
