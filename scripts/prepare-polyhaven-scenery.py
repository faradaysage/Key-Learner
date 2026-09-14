"""Explicit development-only download of the selected CC0 scenery sources.
The game never calls the Poly Haven API. Build uses the committed FBX/PNG adaptations.
"""
from pathlib import Path
import hashlib,json,requests
ROOT=Path(__file__).resolve().parents[1]
stage=ROOT/'_asset_downloads/PolyHavenScenery';stage.mkdir(parents=True,exist_ok=True)
s=requests.Session();s.headers['User-Agent']='KeyLearner-AssetPreparation/1.0 (offline game development)'
ledger=[]
for name in ['fern_02','coastal_cliff_01','mountainside']:
    cache=stage/(name+'-files.json')
    if not cache.exists():
        r=s.get('https://api.polyhaven.com/files/'+name,timeout=60);r.raise_for_status();cache.write_text(json.dumps(r.json(),indent=2))
    data=json.loads(cache.read_text())
    requests_to_make=[(name+'.fbx',data['fbx']['2k']['fbx'])]
    for kind,suffix in [('Diffuse','Diffuse'),('nor_gl','Normal'),('Rough','Roughness'),('Alpha','Alpha')]:
        if kind in data:requests_to_make.append((name+'_'+suffix+'.png',data[kind]['2k']['png']))
    for filename,entry in requests_to_make:
        target=stage/filename
        if not target.exists() or hashlib.md5(target.read_bytes()).hexdigest()!=entry['md5']:
            r=s.get(entry['url'],timeout=180);r.raise_for_status();target.write_bytes(r.content)
        assert hashlib.md5(target.read_bytes()).hexdigest()==entry['md5'],filename
        ledger.append({'asset':name,'source':'https://polyhaven.com/a/'+name,'license':'CC0-1.0','url':entry['url'],'file':filename,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
        print(filename,target.stat().st_size,flush=True)
(stage/'download-manifest.json').write_text(json.dumps(ledger,indent=2))

# After Blender conversion, pack the source alpha into Unity's base-color texture.
if (ROOT/'UnityPort/Assets/ThirdParty/PolyHavenScenery/Textures').exists():
    from PIL import Image
    image=Image.open(stage/'fern_02_Diffuse.png').convert('RGBA')
    image.putalpha(Image.open(stage/'fern_02_Alpha.png').convert('L'))
    image.save(ROOT/'UnityPort/Assets/ThirdParty/PolyHavenScenery/Textures/fern_02_Diffuse.png')
