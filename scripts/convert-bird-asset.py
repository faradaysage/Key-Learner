"""Convert Panther-One's CC-BY 3.0 bird to FBX with its original flight animation."""
from pathlib import Path
import bpy,shutil
root=Path(__file__).resolve().parents[1]
stage=root/'_asset_downloads'
dest=root/'UnityPort/Assets/ThirdParty/PantherOneBird'
(dest/'Models').mkdir(parents=True,exist_ok=True)
(dest/'Textures').mkdir(exist_ok=True)
shutil.copyfile(stage/'BirdPantherOne.png',dest/'Textures/Bird.png')
bpy.ops.wm.open_mainfile(filepath=str(stage/'BirdPantherOne.blend'),load_ui=False,use_scripts=False)
for mat in bpy.data.materials:
    mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value=.65
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(str(dest/'Textures/Bird.png'))
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
for action in bpy.data.actions:action.name='Flight'
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=30;bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    if obj.type in {'MESH','ARMATURE'}:obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(dest/'Models/Bird.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,path_mode='RELATIVE',use_mesh_modifiers=False)
(dest/'LICENSE.txt').write_text('Bird - Animated by Panther-One (PantherOne)\nSource: https://opengameart.org/content/bird-animated\nLicensed under Creative Commons Attribution 3.0 Unported.\nhttps://creativecommons.org/licenses/by/3.0/\n\nDownloaded 2026-09-13. Original mesh, UVs, diffuse texture and 30-frame flight rig preserved.\nChanges: converted from Blender to FBX using Blender 4.3.2, restored legacy diffuse texture node, renamed flight clip Flight, excluded scene lights/cameras. Unity materials and scale/pivot adaptation by KeyLearner.\nCredit: Bird - Animated by Panther-One, CC BY 3.0, via OpenGameArt.org.\n',encoding='utf-8')
