from pathlib import Path
import zipfile,hashlib,json
root=Path(__file__).resolve().parents[1]
dest=root/'UnityPort/Assets/ThirdParty'
manifest=[]
for name,pack in [('Coral Reef Kit.undefined-zip.zip','MiniPolyCoral'),('Seaweed by Mohabins - oYxUdpyc4u.zip','MohabinsSeaweed')]:
    archive_path=root/'_asset_inbox'/name
    with zipfile.ZipFile(archive_path) as archive:
        for member in archive.infolist():
            if not member.filename.lower().endswith('.fbx'):continue
            out=dest/pack/'Models'/Path(member.filename).name
            out.parent.mkdir(parents=True,exist_ok=True)
            out.write_bytes(archive.read(member))
    manifest.append({'archive':name,'sha256':hashlib.sha256(archive_path.read_bytes()).hexdigest(),'pack':pack})
(dest/'MiniPolyCoral/LICENSE.txt').write_text('Coral Reef Kit by MiniPoly\nCreative Commons Attribution 3.0 Unported\nhttps://creativecommons.org/licenses/by/3.0/\nhttps://poly.pizza/bundle/Coral-Reef-Kit-ghN8EmbYa6\nCreator: https://poly.pizza/u/MiniPoly\n\nSix FBX models (CoralReefSet1 through CoralReefSet6) manually supplied by the user. Bundle attribution and exact license supplied directly by the user on 2026-09-13. No automated access to Poly Pizza was used.\nChanges: Unity URP material adaptation, scale/pivot normalization and scene composition. Source meshes preserved.\nCredit: Coral Reef Kit by MiniPoly, CC BY 3.0, via Poly Pizza.\n',encoding='utf-8')
(dest/'MohabinsSeaweed/LICENSE.txt').write_text('Seaweed by Mohabins\nCC0 1.0 Universal Public Domain Dedication\nhttps://creativecommons.org/publicdomain/zero/1.0/\nSource: https://poly.pizza/m/oYxUdpyc4u\n\nFBX manually supplied by the user. License recorded in the user approved migration source list. No automated access to Poly Pizza was used. Source geometry preserved; Unity URP material and pivot adaptation.\n',encoding='utf-8')
(root/'_asset_downloads/inbox-import-manifest.json').write_text(json.dumps(manifest,indent=2))
print(manifest)
