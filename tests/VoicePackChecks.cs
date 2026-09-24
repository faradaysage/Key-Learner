using System.Security.Cryptography;
using System.Text.Json;
using KeyLearner.Studio;

static class VoicePackChecks
{
    public static void Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"KeyLearner-voicepacks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Catalog(root,100);
            Catalog(Path.Combine(root,"Packs","alpha","Voice"),200);
            Catalog(Path.Combine(root,"Packs","beta","Voice"),300);
            Manifest(root);
            var messages=new List<string>();var registry=new VoicePackRegistry(root,messages.Add);
            check(registry.DefaultVoiceId=="alpha" && registry.Packs[0].Id=="beta","voice default is an explicit ID, not pack order");
            check(registry.Packs.Count==3 && registry.Packs.All(p=>p.AvailableSpeechKeys.Count==2),"voice packs expose identical semantic IDs");
            check(registry.KeyForText("  CAT!  ")=="word-cat","legacy text requests map centrally to semantic IDs");
            var virtualRoot=Path.Combine(root,"imported-only");
            var metadata=Directory.GetFiles(root,"*.json",SearchOption.AllDirectories).ToDictionary(path=>Path.GetRelativePath(root,path),File.ReadAllText);
            int importedLookups=0;
            var imported=new VoicePackRegistry(virtualRoot,
                readMetadata:path=>metadata.GetValueOrDefault(Path.GetRelativePath(virtualRoot,path)),
                importedClipAvailable:clip=>{importedLookups++;return true;},includeLegacy:false);
            check(importedLookups==0 && imported.Packs.Count==2,"imported voice startup reads metadata without resolving audio or exposing excluded legacy pack");
            var importedClip=imported.Resolve("word-cat","beta")!;
            check(importedLookups==1 && importedClip.VoiceId=="beta" && !File.Exists(importedClip.Path),"imported voice resolution does not require raw WAVs on the filesystem");
            imported.Reject(importedClip);
            check(imported.Resolve("word-cat","beta")?.VoiceId=="alpha","imported decoder failure retains same-key fallback semantics");
            var beta=registry.Resolve("word-cat","beta")!;var alpha=registry.Resolve("word-cat","alpha")!;
            check(beta.VoiceId=="beta" && alpha.VoiceId=="alpha" && beta.Path!=alpha.Path,"same key selects a different voice data root immediately");
            check(registry.Resolve(VoicePackRegistry.PreviewKey,"beta")?.VoiceId=="beta","preview is a regular speech key in the candidate pack");
            check(registry.Resolve("absent","beta")==null,"unknown speech key never substitutes unrelated speech");
            var settings=new Settings{VoicePackId="beta"};var before=settings.VoicePackId;
            registry.Resolve(VoicePackRegistry.PreviewKey,"alpha");
            check(settings.VoicePackId==before,"candidate resolution does not mutate selected settings");
            var profile=Path.Combine(root,"profile");var store=new Store(profile,root,root);store.Settings.VoicePackId="beta";check(store.Save(),"voice selection uses existing settings save");
            Manifest(root,reverse:true);
            check(new Store(profile,root,root).Settings.VoicePackId=="beta","voice ID persists across restart and manifest reordering");
            File.WriteAllText(Path.Combine(profile,"settings.json"),"{\"VoicePackId\":\"removed-voice\"}");
            check(new Store(profile,root,root).Settings.VoicePackId=="alpha","missing stored pack safely restores explicit default");
            check(registry.NormalizeChoice("../../bad")=="alpha","invalid stored voice is not interpreted as a filesystem path");
            var original=File.ReadAllBytes(beta.Path);File.WriteAllBytes(beta.Path,new byte[]{1,2,3});
            check(registry.Resolve("word-cat","beta")?.VoiceId=="alpha","corrupt selected WAV falls back to the same key in default");
            File.WriteAllBytes(beta.Path,original);
            check(registry.Resolve("word-cat","beta")?.VoiceId=="beta","repaired WAV is revalidated after file changes");
            File.Delete(beta.Path);
            check(registry.Resolve("word-cat","beta")?.VoiceId=="alpha","missing selected WAV falls back to default");
            File.Delete(alpha.Path);
            check(registry.Resolve("word-cat","beta")==null,"missing selected and default WAV fails gracefully");
            for(int i=0;i<1000;i++)registry.Resolve("word-cat","beta");
            check(messages.Count<10,"repeated missing speech diagnostics are deduplicated");
            Catalog(Path.Combine(root,"Packs","alpha","Voice"),200);Catalog(Path.Combine(root,"Packs","beta","Voice"),300);
            registry=new VoicePackRegistry(root);beta=registry.Resolve("word-cat","beta")!;registry.Reject(beta);
            check(registry.Resolve("word-cat","beta")?.VoiceId=="alpha","decoder rejection advances to default instead of repeating bad clip");
            Catalog(Path.Combine(root,"Packs","gamma","Voice"),400);Manifest(root,extra:true);
            registry=new VoicePackRegistry(root);
            check(registry.Packs.Count==4 && registry.Resolve("word-cat","gamma")?.VoiceId=="gamma","adding manifest-backed data discovers another voice without code changes");
            Manifest(root);
            var explicitFallback=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(root,"voice-packs.json")))!;
            explicitFallback["fallbackVoiceId"]=VoicePackRegistry.LegacyId;
            File.WriteAllText(Path.Combine(root,"voice-packs.json"),explicitFallback.ToJsonString());
            registry=new VoicePackRegistry(root);
            check(registry.DefaultVoiceId=="alpha" && registry.FallbackVoiceId==VoicePackRegistry.LegacyId,"default and same-key fallback are independent manifest IDs");
            check(new Store(Path.Combine(root,"fresh-profile"),root,root).Settings.VoicePackId=="alpha","new profile selects manifest default");
            var originalStore=new Store(profile,root,root);originalStore.Settings.VoicePackId=VoicePackRegistry.LegacyId;originalStore.Save();
            check(new Store(profile,root,root).Settings.VoicePackId==VoicePackRegistry.LegacyId,"explicit Original narrator choice survives a different manifest default");
            registry.Reject(registry.Resolve("word-cat","alpha")!);
            check(registry.Resolve("word-cat","alpha")?.VoiceId==VoicePackRegistry.LegacyId,"default pack decoder failure falls back to same-key Original narrator");
            check(registry.Resolve(VoicePackRegistry.PreviewKey,VoicePackRegistry.LegacyId)?.VoiceId==VoicePackRegistry.LegacyId,"Original narrator remains directly selectable and previewable");
            Manifest(root,ready:false);registry=new VoicePackRegistry(root);
            check(registry.Packs.All(p=>p.Id!="beta") && registry.NormalizeChoice("beta")=="alpha","unfinished pack is not selectable");
            Catalog(Path.Combine(root,"Packs","beta","Voice"),300,wrongKeys:true);Manifest(root);registry=new VoicePackRegistry(root);
            check(registry.Packs.All(p=>p.Id!="beta"),"same file count with different IDs is rejected");
            Catalog(Path.Combine(root,"Packs","beta","Voice"),300,badFormat:true);registry=new VoicePackRegistry(root);
            check(registry.Resolve("word-cat","beta")?.VoiceId=="alpha","invalid WAV encoding is rejected even with a matching hash");
            Manifest(root,path:"../../outside");registry=new VoicePackRegistry(root);
            check(registry.Packs.All(p=>p.Id!="beta"),"manifest path traversal is rejected");
            Manifest(root,status:"running");registry=new VoicePackRegistry(root);
            check(registry.Packs.Count==1 && registry.DefaultVoiceId==VoicePackRegistry.LegacyId,"in-flight registry does not expose unfinished packs");
            File.WriteAllText(Path.Combine(root,"voice-packs.json"),"{bad json");registry=new VoicePackRegistry(root);
            check(registry.Resolve("word-cat","beta")?.VoiceId==VoicePackRegistry.LegacyId,"corrupt registry keeps legacy narrator and semantic alias contract");
            File.Delete(Path.Combine(root,"voice-packs.json"));registry=new VoicePackRegistry(root);
            check(registry.Resolve("word-cat",null)!=null,"existing single-narrator installations remain compatible");
        }
        finally
        {
            var resolved=Path.GetFullPath(root);var temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!resolved.StartsWith(temp,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("KeyLearner-voicepacks-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe fixture cleanup");
            Directory.Delete(resolved,true);
        }
    }
    static void Manifest(string root,bool reverse=false,bool extra=false,bool ready=true,string path="Packs/beta",string status="complete")
    {
        object Pack(string id,string name,string location,bool complete=true)=>new{voiceId=id,displayName=name,packDirectory=location,catalog="Voice/catalog.json",readyForIntegration=complete,license="test fixture"};
        var a=Pack("alpha","Alpha","Packs/alpha");var b=Pack("beta","Beta",path,ready);var voices=new List<object>(reverse?new[]{a,b}:new[]{b,a});if(extra)voices.Add(Pack("gamma","Gamma","Packs/gamma"));
        File.WriteAllText(Path.Combine(root,"voice-packs.json"),JsonSerializer.Serialize(new{schema=1,status,defaultVoiceId="alpha",corpusCatalog="catalog.json",voices}));
    }
    static void Catalog(string root,short value,bool wrongKeys=false,bool badFormat=false)
    {
        Directory.CreateDirectory(root);var clips=new Dictionary<string,object>();
        foreach(var item in new[]{(Id:wrongKeys?"word-dog":"word-cat",Text:wrongKeys?"dog":"cat"),(Id:VoicePackRegistry.PreviewKey,Text:"Hello little explorer. Milk. Mommy. Let's play.")})
        {
            string file="speech-"+item.Id+".wav",path=Path.Combine(root,file);
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+9600);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(badFormat?22050:24000);writer.Write(48000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(9600);
                for(int i=0;i<4800;i++)writer.Write(value);
            }
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            clips[item.Id]=new{file,text=item.Text,aliases=new[]{item.Text},sha256=hash};
        }
        File.WriteAllText(Path.Combine(root,"catalog.json"),JsonSerializer.Serialize(new{clips}));
    }
}
