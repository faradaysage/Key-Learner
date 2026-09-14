"""Split the supplied LasquetiSpice CC BY scene into source-animated reusable actors."""
import bpy,json,re,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1];source=root/'_asset_downloads/SharksBoat.glb';dest=root/'_asset_downloads/LasquetiBoatAdapted'
(dest/'Models').mkdir(parents=True,exist_ok=True);(dest/'Textures').mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source))
scene=bpy.context.scene;scene.frame_start=0;scene.frame_end=767;scene.render.fps=24;scene.frame_set(0)
for m in bpy.data.materials:
 if not m.node_tree:continue
 stem=re.sub(r'[^a-zA-Z0-9_-]','_',m.name)
 for link in m.node_tree.links:
  if link.from_node.type!='TEX_IMAGE':continue
  suffix='Diffuse' if link.to_socket.name=='Base Color' else 'Normal' if link.to_node.type=='NORMAL_MAP' else None
  if suffix:
   image=link.from_node.image;image.filepath_raw=str(dest/'Textures'/(stem+'_'+suffix+'.png'));image.file_format='PNG';image.save()
def members(name):
 o=bpy.data.objects[name];return [o]+list(o.children_recursive)
for name,roots in [('FishingBoat',['Fishing Boat','Waving','Waving (1)']),('SpectatorA',['Waving']),('SpectatorB',['Waving (1)']),('SwimmingShark',['Swimming shark'])]:
 if '--only-shark' in sys.argv and name!='SwimmingShark':continue
 if name=='SwimmingShark':
  # The source root bone circles the original boat. Freeze only that travel
  # transform; retain head, tail, fins and all other deformation channels.
  arm=next(o for o in members('Swimming shark') if o.type=='ARMATURE')
  root_bones={b.name for b in arm.data.bones if not b.parent}
  for curve in arm.animation_data.action.fcurves:
   if any(curve.data_path.startswith('pose.bones["'+bone+'"].') for bone in root_bones):
    value=curve.evaluate(0)
    for point in curve.keyframe_points:point.co.y=value;point.handle_left.y=value;point.handle_right.y=value
  scene.frame_set(0)
 bpy.ops.object.select_all(action='DESELECT');selected=[]
 for group in roots:
  for o in members(group):
   # The original cigar is omitted from this children's adaptation.
   if 'cigar' in o.name.lower():continue
   if o.type in {'MESH','ARMATURE','EMPTY'}:o.select_set(True);selected.append(o)
 bpy.ops.export_scene.fbx(filepath=str(dest/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,path_mode='STRIP',use_mesh_modifiers=False)
 print('BOAT_PART',name,len(selected),flush=True)
(dest/'License.txt').write_text('"Animated Sharks Circling Fishing Boat Loop" (https://skfb.ly/o9nSO) by LasquetiSpice is licensed under Creative Commons Attribution 4.0 International.\nhttp://creativecommons.org/licenses/by/4.0/\nhttps://sketchfab.com/3d-models/animated-sharks-circling-fishing-boat-loop-2a9677a044fd4190bb703c318dcdb668\nAdaptations: selected boat/crew/shark parts exported to FBX; original meshes, UV textures and skeletal animation retained; helper geometry and cigar prop omitted; Unity materials/pivots/scales and contextual movement; shark root travel frozen to remove its original boat-circling path.\n',encoding='utf-8')
