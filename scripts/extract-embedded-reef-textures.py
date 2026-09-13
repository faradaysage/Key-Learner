from pathlib import Path
import bpy,json
root=Path(__file__).resolve().parents[1]
for pack in ['MiniPolyCoral','MohabinsSeaweed']:
 for path in sorted((root/'UnityPort/Assets/ThirdParty'/pack/'Models').glob('*.fbx')):
  bpy.ops.wm.read_factory_settings(use_empty=True)
  bpy.ops.import_scene.fbx(filepath=str(path))
  print(path.name,[(im.name,bool(im.packed_file),im.filepath,list(im.size)) for im in bpy.data.images])
  dest=path.parent.parent/'Textures'/path.stem
  for im in bpy.data.images:
   if im.packed_file:
    dest.mkdir(parents=True,exist_ok=True)
    output=dest/Path(im.filepath).name
    output.write_bytes(im.packed_file.data)
    print('EXTRACTED',output.name,len(im.packed_file.data))
