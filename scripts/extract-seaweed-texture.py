from pathlib import Path
import zipfile,struct,json
root=Path(__file__).resolve().parents[1]
with zipfile.ZipFile(root/'_asset_inbox/Seaweed by Mohabins - oYxUdpyc4u.zip') as archive:
 data=archive.read('Seaweed_Mohabins.glb')
json_length,json_type=struct.unpack_from('<II',data,12)
model=json.loads(data[20:20+json_length])
offset=20+json_length
binary_length,binary_type=struct.unpack_from('<II',data,offset)
binary=data[offset+8:offset+8+binary_length]
for image in model.get('images',[]):
 print(image)
 if 'bufferView' not in image:continue
 view=model['bufferViews'][image['bufferView']]
 raw=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
 path=root/'UnityPort/Assets/ThirdParty/MohabinsSeaweed/Textures/Water Gradients.png'
 path.parent.mkdir(parents=True,exist_ok=True)
 path.write_bytes(raw)
 print('EXTRACTED',len(raw))
