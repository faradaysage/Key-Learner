"""Convert the supplied CC BY 4.0 Pteranodon; retain its three source actions."""
import bpy,zipfile,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1];archive=root/'_asset_inbox/pteranodon-animated.zip';source=root/'_asset_downloads/Pteranodon.glb'
with zipfile.ZipFile(archive) as z:
 member=next(n for n in z.namelist() if n.startswith('source/') and n.endswith('.glb'));source.write_bytes(z.read(member))
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source))
# Stage outside Assets until the current Editor build has finished.
dest=root/'_asset_downloads/PteranodonAdapted';(dest/'Models').mkdir(parents=True,exist_ok=True);(dest/'Textures').mkdir(exist_ok=True)
for original,name in [('Image_0','Pteranodon_Diffuse.png'),('Image_2','Pteranodon_Normal.png')]:
 image=bpy.data.images[original];image.filepath_raw=str(dest/'Textures'/name);image.file_format='PNG';image.save()
for action in bpy.data.actions:
 action.name='Flight' if action.name.startswith('flying') else 'Walk' if action.name.startswith('walking') else 'Idle'
armature=next(o for o in bpy.data.objects if o.type=='ARMATURE');armature.animation_data.action=bpy.data.actions['Flight']
scene=bpy.context.scene;scene.frame_start=0;scene.frame_end=257;scene.render.fps=24;scene.frame_set(0)
bpy.ops.object.select_all(action='DESELECT')
for o in scene.objects:
 if o.type=='ARMATURE' or o.type=='EMPTY' or (o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(dest/'Models/Pteranodon.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,path_mode='STRIP',use_mesh_modifiers=False)
(dest/'License.txt').write_text('"Pteranodon (Animated)" (https://skfb.ly/o6KXA) by Chistodrako._. is licensed under Creative Commons Attribution 4.0 International.\nhttp://creativecommons.org/licenses/by/4.0/\nSource: https://sketchfab.com/3d-models/pteranodon-animated-7d7683df41d1405283f160e81a5dff1b\nAdaptations: GLB-to-FBX conversion; original mesh, rig, flight/walk/standing actions and diffuse/normal maps retained; omitted bone-display helper; Unity materials/pivot/scale.\n',encoding='utf-8')
print('PTERANODON_CONVERTED',[(a.name,list(a.frame_range)) for a in bpy.data.actions])
