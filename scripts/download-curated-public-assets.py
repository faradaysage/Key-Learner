"""Download a bounded selection from creator-published, public CC0 folders."""
from pathlib import Path
import hashlib,json,shutil,time,urllib.request
root=Path(__file__).resolve().parents[1]
stage=root/'_asset_downloads'
assets={
'QuaterniusCuteFish':{
'BlueTang.fbx':'10o_C5IoboQdAY_atzJDwgiSwQZeCyD0a',
'ButterflyFish.fbx':'1d3VM3jIiLL04slsQn06MiGm8nogW-tZn',
'Clownfish.fbx':'1ogBE40FyvQjbsrLRRcIPc2ldvqQVV8G1',
'CoralGrouper.fbx':'1yNwcJefaEwXTtpEWtx6a89HokyUxPrvx',
'Goldfish.fbx':'1qTRidk3QjQBO7GrwQuz8_QhGPyvsQ-jy',
'Koi.fbx':'1eVVtIyZCrNyJYLoKyVMy8ms57EemAT5h',
'MandarinFish.fbx':'15-6jLQYSmaw-JyAPo5E2D01REfX74Gdp',
'MoorishIdol.fbx':'1E7tyai4SqOu2VEv64c6Sd-Kg0W1_A2kn',
'ParrotFish.fbx':'1Cmhze83EmhFkOJr8A9WONbQcMU2pVrdF',
'Puffer.fbx':'1jvukg0wJP4y1bTmlelWKVzYAetgFxulE',
'RoyalGramma.fbx':'10RbIT1E3VquhQ_vYE6bN3tOmhW-s0ULl',
'Tetra.fbx':'1NjKZsSKF7L_vLEb4K088qoaa36oJF3IT'},
'QuaterniusFarm':{
'Barn.fbx':'1V0eVUo_eAKIAtrWCf2ZxiJEX6d-Knhry',
'BigBarn.fbx':'1UEFF-6zGNiYfKeLj8srV_jJvisD9RgPr',
'ChickenCoop.fbx':'1wUEjZYRU9lHiiYIkDw66aHIQLcNumeKQ',
'Fence.fbx':'1c0SNguly8AG2zcIWIvyi300m4It4V8Dl',
'Silo.fbx':'1vDCNee7hjCCTOzvdgs8l5IdcVQmSErQi',
'TowerWindmill.fbx':'17JL1rj_E1mPuM3SC13e62HPFZSAd_w-Z',
'WaterTower.fbx':'1I_BbSwEvA0uZKoJFmCXqoQV8gwCz7755',
'Well.fbx':'16bw85R_OC-fyNQRsNj58TjWW69vjjZQi',
'Windmill.fbx':'1IrJBhR4fSyTUN3Xkw5lOgFgKGsotMUB8'}}
manifest={}
for pack,entries in assets.items():
    dest=root/'UnityPort/Assets/ThirdParty'/pack
    (dest/'Models').mkdir(parents=True,exist_ok=True)
    pack_stage=stage/pack
    pack_stage.mkdir(exist_ok=True)
    manifest[pack]=[]
    license_name='cute-fish-license.txt' if pack=='QuaterniusCuteFish' else 'farm-buildings-license.txt'
    shutil.copyfile(stage/license_name,dest/'License.txt')
    for name,identifier in entries.items():
        url='https://drive.google.com/uc?export=download&id='+identifier
        path=pack_stage/name
        if not path.exists():
            data=urllib.request.urlopen(url,timeout=45).read()
            if not data.startswith(b'Kaydara FBX Binary'):
                raise ValueError('Not an FBX download: '+name)
            path.write_bytes(data)
            time.sleep(.4)
        shutil.copyfile(path,dest/'Models'/name)
        manifest[pack].append({'file':name,'download_url':url,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size})
        print(pack,name,path.stat().st_size,flush=True)
(stage/'public-file-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
