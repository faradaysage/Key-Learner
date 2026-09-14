"""Blender adaptation of Yughues/Nobiax CC0 palm silhouettes and UVs."""
import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[1];stage=root/'_asset_downloads/YughuesPalms';dest=root/'_asset_downloads/YughuesPalmsAdapted'
(dest/'Models').mkdir(parents=True,exist_ok=True);(dest/'Textures').mkdir(exist_ok=True)
for path in stage.glob('*.obj'):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.wm.obj_import(filepath=str(path))
 bpy.ops.export_scene.fbx(filepath=str(dest/'Models'/(path.stem+'.fbx')),object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
for source,out in [('diffuse.tga','Palm_Diffuse.png'),('normal.tga','Palm_Normal.png')]:
 image=bpy.data.images.load(str(stage/source),check_existing=False);_ = image.pixels[0];image.filepath_raw=str(dest/'Textures'/out);image.file_format='PNG';image.save()
(dest/'License.txt').write_text('Free palm treeZ v3 by Yughues / Nobiax\nhttps://opengameart.org/content/free-palm-treez-v3\nCC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/\nAdaptations: original five authored OBJ variants and UVs converted to FBX; source diffuse/normal TGA converted losslessly to PNG; Unity material/pivot/scale adaptation.\n\n'+(stage/'readme.txt').read_text(),encoding='utf-8')
print('PALMS_CONVERTED')
