using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using KeyLearner.Studio;

// Deliberately .NET-only: ordinary builds never install or invoke speech generation.
try
{
    if(args.Length<2 || args[0] is not ("verify" or "import" or "self-test"))
        throw new ArgumentException("Usage: verify <repository> [--require-packs] | import <repository> <completed-output> --default <stable-id>");
    var repository=Path.GetFullPath(args[1]);
    var runtime=Path.Combine(repository,"Content","Voice");
    if(args[0]=="self-test")SelfTest(repository);
    else if(args[0]=="verify")Verify(runtime,Path.Combine(repository,"tools","speech","manifest.json"),args.Contains("--require-packs"));
    else
    {
        if(args.Length!=5 || args[3]!="--default")throw new ArgumentException("Import requires a completed output directory and explicit --default ID.");
        Import(repository,Path.GetFullPath(args[2]),args[4]);
    }
    return 0;
}
catch(Exception e) when(e is IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or UnauthorizedAccessException or FormatException)
{Console.Error.WriteLine("VOICE_PACK_VALIDATION_FAILED: "+e.Message);return 1;}

static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
static JsonElement Read(string path){using var json=JsonDocument.Parse(File.ReadAllText(path));return json.RootElement.Clone();}
static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
static string Text(JsonElement item,string name)=>item.GetProperty(name).GetString()??throw new InvalidDataException("Missing "+name);
static bool Same(JsonElement a,JsonElement b)=>Canonical(a).SequenceEqual(Canonical(b));
static byte[] Canonical(JsonElement element)
{
    using var stream=new MemoryStream();
    using(var writer=new Utf8JsonWriter(stream,new JsonWriterOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}))Write(writer,element);
    return stream.ToArray();
}
static void Write(Utf8JsonWriter writer,JsonElement value)
{
    if(value.ValueKind==JsonValueKind.Object){writer.WriteStartObject();foreach(var p in value.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal)){writer.WritePropertyName(p.Name);Write(writer,p.Value);}writer.WriteEndObject();}
    else if(value.ValueKind==JsonValueKind.Array){writer.WriteStartArray();foreach(var item in value.EnumerateArray())Write(writer,item);writer.WriteEndArray();}
    else value.WriteTo(writer);
}
static string Contained(string root,string relative)
{
    Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains(':') && !relative.Split('/','\\').Contains(".."),"Unsafe relative pack path");
    var path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar).Replace('\\',Path.DirectorySeparatorChar)));
    Require(path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Pack path leaves its root");
    // Never copy/move through a junction or symlink, including an ancestor of the supplied root.
    for(var current=path;!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
        if(File.Exists(current)||Directory.Exists(current))Require((File.GetAttributes(current)&FileAttributes.ReparsePoint)==0,"Reparse points are not supported: "+current);
    return path;
}
static Dictionary<string,JsonElement> Corpus(string file)
{
    var entries=Read(file).GetProperty("entries").EnumerateArray().ToDictionary(e=>Text(e,"id"),e=>e,StringComparer.Ordinal);
    Require(entries.Count>0,"Empty corpus");return entries;
}
static void Catalog(string directory,Dictionary<string,JsonElement> corpus,JsonElement? configuration=null,bool allowAuxiliary=false)
{
    var clips=Read(Path.Combine(directory,"catalog.json")).GetProperty("clips");
    Require(clips.EnumerateObject().Select(p=>p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(corpus.Keys),"Catalog does not expose the exact required speech IDs: "+directory);
    var reader=new PreparedSpeechCatalog(directory);Require(reader.IsValid && reader.Clips.Count==corpus.Count,"Invalid/ambiguous speech catalog: "+directory);
    var expected=new HashSet<string>(StringComparer.Ordinal){"catalog.json"};
    foreach(var (id,entry) in corpus)
    {
        var clip=clips.GetProperty(id);var name="speech-"+id+".wav";expected.Add(name);
        Require(Text(clip,"file")==name && Text(clip,"text")==Text(entry,"text") && Same(clip.GetProperty("aliases"),entry.GetProperty("aliases")),"Speech contract mismatch: "+id);
        var path=Contained(directory,name);
        Require(Hash(path).Equals(Text(clip,"sha256"),StringComparison.OrdinalIgnoreCase),"WAV checksum mismatch: "+path);
        using(var stream=File.OpenRead(path))Require(VoicePackRegistry.IsRuntimeWave(stream),"Expected mono 24 kHz PCM16 WAV, minimum 0.12 seconds: "+path);
        if(configuration is JsonElement config)
        {
            var recipe=new Dictionary<string,object?>{["text"]=entry.GetProperty("text"),["generationText"]=entry.TryGetProperty("generationText",out var generation)?generation:entry.GetProperty("text"),["trimPrefix"]=entry.TryGetProperty("trimPrefix",out var trim)?trim:null,["voice"]=config,["generator"]=2};
            long offset=entry.TryGetProperty("seedOffset",out var seedOffset)?seedOffset.GetInt64():0;
            if(offset!=0)recipe["seedOffset"]=seedOffset;
            if(entry.TryGetProperty("trimAligner",out var aligner))recipe["trimAligner"]=aligner;
            var recipeHash=Convert.ToHexString(SHA256.HashData(Canonical(JsonSerializer.SerializeToElement(recipe))));
            Require(recipeHash.Equals(Text(clip,"recipe"),StringComparison.OrdinalIgnoreCase),"Stale recipe: "+id);
            long seed=(config.GetProperty("seed").GetInt64()+offset+Convert.ToUInt32(Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)))[..8],16))&0xffffffffL;
            Require(clip.GetProperty("seed").GetInt64()==seed && Same(clip.GetProperty("modelFiles"),config.GetProperty("modelFiles")),"Seed/model mismatch: "+id);
        }
    }
    if(allowAuxiliary)return;
    Require(Directory.GetFiles(directory).Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal).SetEquals(expected),"Unexpected files in runtime Voice directory: "+directory);
    Require(!Directory.EnumerateDirectories(directory).Any(),"Unexpected nested runtime audio directory");
}
static void Verify(string runtime,string corpusFile,bool requirePacks,bool verifyOriginalAudio=true)
{
    var corpus=Corpus(corpusFile);
    // The original root also contains legacy sound effects/brand files; its prepared clips are validated by SpeechVerify.
    var contract=new PreparedSpeechCatalog(runtime);
    Require(contract.IsValid && contract.Clips.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(corpus.Keys),"Bundled semantic contract differs from the production corpus");
    if(verifyOriginalAudio)Catalog(runtime,corpus,allowAuxiliary:true);
    var manifestPath=Path.Combine(runtime,"voice-packs.json");
    if(!File.Exists(manifestPath)){Require(!requirePacks,"Finalist packs have not been integrated");Console.WriteLine($"PASS: original narrator contract ({corpus.Count} IDs); finalist integration is pending.");return;}
    var manifest=Read(manifestPath);Require(manifest.GetProperty("schema").GetInt32()==1 && Text(manifest,"status")=="complete","Runtime manifest is not finalized");
    var registry=new VoicePackRegistry(runtime);var ids=new HashSet<string>(StringComparer.Ordinal);
    foreach(var voice in manifest.GetProperty("voices").EnumerateArray())
    {
        var id=Text(voice,"voiceId");Require(id!=VoicePackRegistry.LegacyId && ids.Add(id),"Invalid/duplicate voice ID");
        Require(voice.GetProperty("readyForIntegration").GetBoolean() && Text(voice,"status")=="complete","Unready production voice: "+id);
        var pack=registry.Packs.SingleOrDefault(p=>p.Id==id);Require(pack!=null,"Registry rejected voice: "+id);
        Catalog(pack!.AssetRoot,corpus,voice.GetProperty("generationConfiguration"));
        foreach(var key in corpus.Keys)Require(registry.Resolve(key,id)?.VoiceId==id,"Runtime fell back instead of resolving requested pack: "+id+" / "+key);
    }
    Require(ids.Count>0 && ids.Contains(Text(manifest,"defaultVoiceId")) && registry.DefaultVoiceId==Text(manifest,"defaultVoiceId"),"Unavailable explicit default voice");
    Console.WriteLine($"PASS: {ids.Count} complete packs x {corpus.Count} identical IDs, recipes, hashes, PCM and runtime resolution; default {registry.DefaultVoiceId}.");
}
static void Import(string repository,string source,string defaultId)
{
    var sourceManifest=Contained(source,"voice-packs.json");var manifestBytes=File.ReadAllBytes(sourceManifest);var manifest=Read(sourceManifest);
    Require(Text(manifest,"status")=="complete","Generation is active or incomplete. No output was copied or changed.");
    var worker=Read(Contained(source,"worker-status.json"));Require(Text(worker,"state")=="complete","Generation worker has not completed. No output was copied or changed.");
    var sourceCorpus=Contained(source,Text(manifest,"corpusManifest"));var corpus=Corpus(sourceCorpus);var corpusHash=Hash(sourceCorpus);
    var currentCorpus=Corpus(Path.Combine(repository,"tools","speech","manifest.json"));
    Require(currentCorpus.Count==corpus.Count && corpus.All(p=>currentCorpus.TryGetValue(p.Key,out var current)&&Same(p.Value,current)),"Generated corpus differs from current game corpus");
    var voices=manifest.GetProperty("voices").EnumerateArray().ToArray();var ids=new HashSet<string>(StringComparer.Ordinal);
    var sources=new List<(string Id,string Root,JsonElement Voice)>();
    foreach(var voice in voices)
    {
        var id=Text(voice,"voiceId");Require(id.Length is >0 and <=96 && id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_') && id!="builtin" && ids.Add(id),"Invalid/duplicate voice ID");
        Require(voice.GetProperty("readyForIntegration").GetBoolean() && Text(voice,"status")=="complete" && voice.GetProperty("requiredSpeechCount").GetInt32()==corpus.Count && Text(voice,"corpusSha256")==corpusHash,"Unready or stale generated pack: "+id);
        var root=Contained(source,Text(voice,"packDirectory"));var state=Read(Contained(root,"validation.json"));
        Require(state.GetProperty("complete").GetBoolean() && state.GetProperty("validCount").GetInt32()==corpus.Count && state.GetProperty("requiredCount").GetInt32()==corpus.Count,"Full validation did not pass: "+id);
        foreach(var field in new[]{"missingOrInvalid","extraCatalogIds","extraFiles"})Require(state.GetProperty(field).GetArrayLength()==0,"Outstanding validation entries: "+id);
        var local=Read(Contained(root,"voice.json"));Require(Text(local,"voiceId")==id && local.GetProperty("readyForIntegration").GetBoolean() && Text(local,"status")=="complete","Pack metadata not finalized: "+id);
        var config=Read(Contained(root,"generation-config.json"));Require(Same(config,voice.GetProperty("generationConfiguration")),"Generation metadata mismatch: "+id);
        var catalog=Contained(root,Text(voice,"catalog"));Require(Path.GetFileName(catalog)=="catalog.json","Invalid catalog filename");
        Catalog(Path.GetDirectoryName(catalog)!,corpus,config);
        sources.Add((id,root,voice));
    }
    Require(ids.Contains(defaultId),"Choose an explicit finalized default voice ID");
    // No writes above this point, and never any writes under the generator output.
    var transaction=Contained(repository,"artifacts/voice-pack-import/"+Guid.NewGuid().ToString("N"));
    Require(!transaction.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Import staging cannot be inside generator output");
    var staging=Contained(transaction,"new");Directory.CreateDirectory(staging);
    var runtime=Contained(repository,"Content/Voice");File.Copy(Contained(runtime,"catalog.json"),Contained(staging,"catalog.json"));
    var projected=JsonNode.Parse(manifestBytes)!.AsObject();projected["defaultVoiceId"]=defaultId;projected["corpusCatalog"]="catalog.json";projected.Remove("detail");projected.Remove("corpusManifest");
    var projectedVoices=new JsonArray();
    foreach(var (id,root,voice) in sources)
    {
        var destination=Contained(staging,"Packs/"+id);Directory.CreateDirectory(Contained(destination,"Voice"));
        var catalog=Contained(root,Text(voice,"catalog"));var audioRoot=Path.GetDirectoryName(catalog)!;
        foreach(var file in corpus.Keys.Select(id=>"speech-"+id+".wav").Append("catalog.json"))File.Copy(Contained(audioRoot,file),Contained(destination,"Voice/"+file));
        foreach(var file in new[]{"voice.json","CHATTERBOX_LICENSE.txt","reference/SOURCE_PROVENANCE.txt"})
        {var from=Contained(root,file);Require(File.Exists(from),"Missing required credit: "+file);var to=Contained(destination,file);Directory.CreateDirectory(Path.GetDirectoryName(to)!);File.Copy(from,to);}
        var node=JsonNode.Parse(voice.GetRawText())!.AsObject();node["packDirectory"]="Packs/"+id;node["catalog"]="Voice/catalog.json";node["manifestFile"]="Packs/"+id+"/voice.json";projectedVoices.Add(node);
    }
    projected["voices"]=projectedVoices;var stagedManifest=Contained(staging,"voice-packs.json");
    File.WriteAllText(stagedManifest,projected.ToJsonString(new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    Verify(staging,sourceCorpus,true,verifyOriginalAudio:false);
    Require(File.ReadAllBytes(sourceManifest).SequenceEqual(manifestBytes) && Text(Read(Contained(source,"worker-status.json")),"state")=="complete","Source status changed during validation; staged copy retained for inspection, production unchanged");
    var packs=Contained(runtime,"Packs");var liveManifest=Contained(runtime,"voice-packs.json");var backup=Contained(transaction,"previous-packs");var backupManifest=Contained(transaction,"previous-manifest.json");
    bool movedOld=false,movedNew=false;
    if(File.Exists(liveManifest))File.Copy(liveManifest,backupManifest);
    try
    {
        if(Directory.Exists(packs)){Directory.Move(packs,backup);movedOld=true;}
        Directory.Move(Contained(staging,"Packs"),packs);movedNew=true;
        File.Copy(stagedManifest,liveManifest,true);
    }
    catch
    {
        if(movedNew)Directory.Move(packs,Contained(transaction,"failed-packs"));
        if(movedOld)Directory.Move(backup,packs);
        if(File.Exists(backupManifest))File.Copy(backupManifest,liveManifest,true);else if(File.Exists(liveManifest))File.Delete(liveManifest);
        throw;
    }
    Console.WriteLine("IMPORTED complete runtime WAVs and metadata. Previous production content retained at "+transaction+". Review and explicitly stage Content/Voice/Packs plus voice-packs.json; generation output was not modified.");
}

// No TTS, Python or production writes: exercise the transaction with two tiny complete PCM fixtures.
static void SelfTest(string repository)
{
    var workspace=Contained(repository,"artifacts/voice-pack-tool-tests/"+Guid.NewGuid().ToString("N"));
    var repo=Contained(workspace,"repository");var source=Contained(workspace,"generated");
    var voiceRoot=Contained(repo,"Content/Voice");Directory.CreateDirectory(voiceRoot);Directory.CreateDirectory(Contained(repo,"tools/speech"));Directory.CreateDirectory(source);
    void Save(string path,object value)=>File.WriteAllText(path,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
    var entries=new[]{new{id="number-three",text="Three",aliases=new[]{"Three"}},new{id=VoicePackRegistry.PreviewKey,text="Hello explorer",aliases=new[]{"Hello explorer"}}};
    Save(Contained(source,"corpus-manifest.json"),new{entries});File.Copy(Contained(source,"corpus-manifest.json"),Contained(repo,"tools/speech/manifest.json"));
    var configs=new Dictionary<string,JsonElement>();var voices=new List<object>();
    foreach(var id in new[]{"first","second"})
    {
        var root=Contained(source,id);Directory.CreateDirectory(Contained(root,"Voice"));Directory.CreateDirectory(Contained(root,"reference"));
        var config=JsonSerializer.SerializeToElement(new{seed=42,modelFiles=new Dictionary<string,string>{{"fixture","fixture-only-no-model"}}});configs[id]=config;
        var clips=new Dictionary<string,object>();
        foreach(var entry in entries)
        {
            var filename="speech-"+entry.id+".wav";var path=Contained(root,"Voice/"+filename);
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(9636);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                writer.Write((short)1);writer.Write((short)1);writer.Write(24000);writer.Write(48000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(9600);
                for(int i=0;i<4800;i++)writer.Write((short)(id=="first"?1000:2000));
            }
            var recipe=new{text=entry.text,generationText=entry.text,trimPrefix=(string?)null,voice=config,generator=2};
            var seed=(42L+Convert.ToUInt32(Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(entry.id)))[..8],16))&0xffffffffL;
            clips[entry.id]=new{text=entry.text,file=filename,entry.aliases,sha256=Hash(path),seed,modelFiles=config.GetProperty("modelFiles"),recipe=Convert.ToHexString(SHA256.HashData(Canonical(JsonSerializer.SerializeToElement(recipe)))).ToLowerInvariant()};
        }
        Save(Contained(root,"Voice/catalog.json"),new{clips});Save(Contained(root,"generation-config.json"),config);
        var metadata=new{voiceId=id,displayName=id,packDirectory=id,catalog="Voice/catalog.json",status="complete",readyForIntegration=true,requiredSpeechCount=entries.Length,corpusSha256=Hash(Contained(source,"corpus-manifest.json")),generationConfiguration=config};
        voices.Add(metadata);Save(Contained(root,"voice.json"),metadata);
        Save(Contained(root,"validation.json"),new{complete=true,validCount=2,requiredCount=2,missingOrInvalid=Array.Empty<object>(),extraCatalogIds=Array.Empty<string>(),extraFiles=Array.Empty<string>()});
        File.WriteAllText(Contained(root,"CHATTERBOX_LICENSE.txt"),"TEST FIXTURE ONLY");File.WriteAllText(Contained(root,"reference/SOURCE_PROVENANCE.txt"),"TEST PCM; no voice reference or speech model used");
    }
    foreach(var file in Directory.GetFiles(Contained(source,"first/Voice")))File.Copy(file,Contained(voiceRoot,Path.GetFileName(file)));
    void Manifest(string status)=>Save(Contained(source,"voice-packs.json"),new{schema=1,status,corpusManifest="corpus-manifest.json",voices});
    void Refuses(Action operation,string label){try{operation();}catch(Exception e) when(e is InvalidDataException or IOException){Console.WriteLine("PASS: "+label);return;}throw new InvalidDataException("Test failed: "+label);}
    Save(Contained(source,"worker-status.json"),new{state="running"});Manifest("running");
    Refuses(()=>Import(repo,source,"first"),"active generation refused before staging");Require(!Directory.Exists(Contained(repo,"artifacts")),"Active import wrote staging output");
    Manifest("complete");Refuses(()=>Import(repo,source,"first"),"incomplete worker refused");Save(Contained(source,"worker-status.json"),new{state="complete"});
    Import(repo,source,"first");Verify(voiceRoot,Contained(source,"corpus-manifest.json"),true);
    var manifestBefore=File.ReadAllBytes(Contained(voiceRoot,"voice-packs.json"));
    var damaged=Contained(source,"second/Voice/speech-number-three.wav");var bytes=File.ReadAllBytes(damaged);File.WriteAllText(damaged,"bad");
    Refuses(()=>Import(repo,source,"second"),"corrupt finalist refused without replacing production");Require(File.ReadAllBytes(Contained(voiceRoot,"voice-packs.json")).SequenceEqual(manifestBefore),"Failed import altered production manifest");
    File.WriteAllBytes(damaged,bytes);Import(repo,source,"second");Require(new VoicePackRegistry(voiceRoot).DefaultVoiceId=="second","Explicit default update failed");
    Verify(voiceRoot,Contained(source,"corpus-manifest.json"),true);
    File.Delete(Contained(voiceRoot,"Packs/first/Voice/speech-number-three.wav"));Refuses(()=>Verify(voiceRoot,Contained(source,"corpus-manifest.json"),true),"strict verification never hides an incomplete pack behind runtime fallback");
    Console.WriteLine("PASS: isolated import/replace/validation fixtures retained at "+workspace);
}
