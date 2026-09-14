from pathlib import Path
import json,hashlib,shutil
root=Path(__file__).resolve().parents[1]
stage=root/'_asset_downloads'
base=root/'UnityPort/Assets/ThirdParty'
manifest={'verified_date':'2026-09-14','source_archives':json.loads((stage/'import-manifest.json').read_text()),'public_creator_files':json.loads((stage/'public-file-manifest.json').read_text()),'manual_archives':json.loads((stage/'inbox-import-manifest.json').read_text()),'source_files':[]}
if (stage/'immersion-model-imports.json').exists(): manifest['immersion_files']=json.loads((stage/'immersion-model-imports.json').read_text())
for path in sorted(base.rglob('*')):
 if path.is_file() and path.suffix.lower() in {'.fbx','.png','.txt'}:
  manifest['source_files'].append({'path':str(path.relative_to(root)).replace('\\','/'),'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
(base/'ASSET_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
notices=root/'UnityPort/Assets/StreamingAssets/Notices'
notices.mkdir(parents=True,exist_ok=True)
shutil.copyfile(root/'THIRD_PARTY_ASSETS.md',notices/'THIRD_PARTY_ASSETS.md')
for name in ['NONCOMMERCIAL_ASSETS.md','NONCOMMERCIAL_ASSETS.json']:
 if (root/name).exists():shutil.copyfile(root/name,notices/name)
for path in base.glob('*/License*.txt'):
 shutil.copyfile(path,notices/(path.parent.name+'-'+path.name))
for path in base.glob('*/LICENSE.txt'):
 shutil.copyfile(path,notices/(path.parent.name+'-'+path.name))
credits='''KeyLearner game art credits

NONCOMMERCIAL BUILD CONTENT: WildMesh 3D wolf, CC BY-NC 4.0.
https://skfb.ly/pMR9W
https://creativecommons.org/licenses/by-nc/4.0/
See NONCOMMERCIAL_ASSETS.md before commercial distribution.

Polar Bear by kenchoo, CC BY 4.0, via Sketchfab.
https://skfb.ly/oRMzK
https://creativecommons.org/licenses/by/4.0/
Converted to FBX; original rig, animation and textures retained; Unity materials/pivot/scale adapted.

Bird - Animated by Panther-One, CC BY 3.0, via OpenGameArt.org.
https://opengameart.org/content/bird-animated
https://creativecommons.org/licenses/by/3.0/
Converted to FBX; original model, texture, rig and flight retained; Unity materials/pivot/scale adapted.

Coral Reef Kit by MiniPoly, CC BY 3.0, via Poly Pizza.
https://poly.pizza/bundle/Coral-Reef-Kit-ghN8EmbYa6
https://poly.pizza/u/MiniPoly
https://creativecommons.org/licenses/by/3.0/
Original models and embedded atlases retained; Unity materials/pivot/scale/composition adapted.

Pteranodon (Animated) by Chistodrako._., CC BY 4.0.
https://skfb.ly/o6KXA
https://creativecommons.org/licenses/by/4.0/
Original rig/actions/maps retained; FBX, material and scale adaptation; helper omitted.

Animated Sharks Circling Fishing Boat Loop by LasquetiSpice, CC BY 4.0.
https://skfb.ly/o9nSO
https://creativecommons.org/licenses/by/4.0/
Separated boat, waving crew and swimming shark; original rigs/maps retained; helper and cigar omitted.

Free palm treeZ v3 by Yughues / Nobiax, CC0 1.0.
https://opengameart.org/content/free-palm-treez-v3
Fern 02 and Coastal Cliff 01 by Rico Cilliers / Rob Tuytel; Mountainside by Dario Barresi / Rico Cilliers, Poly Haven CC0.
https://polyhaven.com/
Forest Ground 01 by Rob Tuytel, Poly Haven CC0.
https://polyhaven.com/a/forrest_ground_01

Nature, Farm Buildings, Animated Dinosaurs, Animated Animals, Animated Fish and Animated Cute Fish: Quaternius, CC0 1.0.
https://quaternius.com/
Cars, Pirate Kit, City Suburban, City Commercial and City Roads: Kenney, CC0 1.0.
https://kenney.nl/
Seaweed: Mohabins, CC0 1.0, via Poly Pizza.
https://poly.pizza/m/oYxUdpyc4u
https://creativecommons.org/publicdomain/zero/1.0/

All original notices and the complete source ledger ship in the game's StreamingAssets/Notices folder.
'''
(root/'UnityPort/Assets/KeyLearner/Resources/AssetCredits.txt').write_text(credits,encoding='utf-8')
(notices/'AssetCredits.txt').write_text(credits,encoding='utf-8')
print(len(manifest['source_files']),'source file hashes recorded; notices copied')
