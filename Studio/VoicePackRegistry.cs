using System.Security.Cryptography;
using System.Text.Json;
namespace KeyLearner.Studio;

/// <summary>One metadata-driven catalog for both engines. No voice-specific gameplay or model runtime.</summary>
public sealed class VoicePackRegistry
{
    public const string LegacyId="builtin";
    public const string PreviewKey="cue-voice-preview";
    public sealed class VoicePack
    {
        public string Id {get;}
        public string DisplayName {get;}
        public string AssetRoot {get;}
        public JsonElement Metadata {get;}
        public IReadOnlyCollection<string> AvailableSpeechKeys => Catalog.Clips.Keys.ToArray();
        internal PreparedSpeechCatalog Catalog {get;}
        internal VoicePack(string id,string name,string root,PreparedSpeechCatalog catalog,JsonElement metadata)
        {Id=id;DisplayName=name;AssetRoot=root;Catalog=catalog;Metadata=metadata;}
    }
    public sealed class SpeechAsset
    {
        public string Key {get;}
        public string VoiceId {get;}
        public string Path {get;}
        internal SpeechAsset(string key,string voice,string path){Key=key;VoiceId=voice;Path=path;}
    }
    readonly Dictionary<string,VoicePack> byId=new(StringComparer.Ordinal);
    readonly List<VoicePack> packs=new();
    readonly Dictionary<string,(long Length,long Stamp,bool Valid)> checkedFiles=new(StringComparer.Ordinal);
    readonly HashSet<string> rejected=new(StringComparer.Ordinal),reported=new(StringComparer.Ordinal);
    readonly Action<string>? diagnostic;
    readonly PreparedSpeechCatalog contract;
    public IReadOnlyList<VoicePack> Packs => packs.AsReadOnly();
    public string DefaultVoiceId {get;private set;}=LegacyId;
    public string FallbackVoiceId {get;private set;}=LegacyId;
    public int RequiredCount => contract.Clips.Count;
    public VoicePackRegistry(string root,Action<string>? diagnostic=null)
    {
        this.diagnostic=diagnostic;
        root=System.IO.Path.GetFullPath(root);
        contract=new PreparedSpeechCatalog(root);
        var legacy=new VoicePack(LegacyId,"Original narrator",root,contract,default);
        var manifest=System.IO.Path.Combine(root,"voice-packs.json");
        if(File.Exists(manifest))
        {
            try
            {
                using var document=JsonDocument.Parse(File.ReadAllText(manifest));
                var json=document.RootElement;
                if(json.GetProperty("schema").GetInt32()!=1 || json.GetProperty("status").GetString()!="complete")throw new InvalidDataException("Packs are not finalized");
                // A bundled corpus catalog is the stable key/alias contract, independent of pack ordering.
                if(json.TryGetProperty("corpusCatalog",out var corpus))
                {
                    var corpusFile=Contained(root,corpus.GetString()??"");
                    if(System.IO.Path.GetFileName(corpusFile)!="catalog.json")throw new InvalidDataException("Invalid corpus catalog");
                    contract=new PreparedSpeechCatalog(System.IO.Path.GetDirectoryName(corpusFile)!);
                }
                if(!contract.IsValid)throw new InvalidDataException("Missing speech contract");
                foreach(var item in json.GetProperty("voices").EnumerateArray())
                {
                    try
                    {
                        string id=item.GetProperty("voiceId").GetString()??"",name=item.GetProperty("displayName").GetString()??"";
                        if(!ValidId(id) || id==LegacyId || name.Trim().Length==0 || !item.GetProperty("readyForIntegration").GetBoolean())throw new InvalidDataException("Invalid or unfinished pack");
                        var packRoot=Contained(root,item.GetProperty("packDirectory").GetString()??"");
                        var catalogPath=Contained(packRoot,item.GetProperty("catalog").GetString()??"");
                        if(System.IO.Path.GetFileName(catalogPath)!="catalog.json")throw new InvalidDataException("Unsupported catalog path");
                        var folder=System.IO.Path.GetDirectoryName(catalogPath)!;
                        var catalog=new PreparedSpeechCatalog(folder);
                        if(!MatchesContract(catalog))throw new InvalidDataException("Speech contract mismatch");
                        Add(new VoicePack(id,name,folder,catalog,item.Clone()));
                    }
                    catch(Exception e) when(IsContentError(e)){Report("pack-invalid","An invalid or incomplete voice pack was ignored.");}
                }
                var requested=json.GetProperty("defaultVoiceId").GetString()??"";
                if(byId.ContainsKey(requested))DefaultVoiceId=requested;
                else Report("default-missing","The declared default voice is unavailable; using the original narrator.");
                var fallback=json.TryGetProperty("fallbackVoiceId",out var fallbackValue)?fallbackValue.GetString():DefaultVoiceId;
                if(fallback==LegacyId || (fallback!=null && byId.ContainsKey(fallback)))FallbackVoiceId=fallback;
                else Report("fallback-missing","The declared fallback voice is unavailable; using the original narrator.");
            }
            catch(Exception e) when(IsContentError(e))
            {packs.Clear();byId.Clear();contract=legacy.Catalog;DefaultVoiceId=LegacyId;FallbackVoiceId=LegacyId;Report("manifest-invalid","Voice-pack metadata is unavailable; using the original narrator.");}
        }
        // Compatibility data also makes malformed/missing registry metadata nonfatal.
        Add(legacy);
    }
    static bool IsContentError(Exception e)=>e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or NotSupportedException or FormatException;
    static bool ValidId(string id)=>id.Length>0 && id.Length<=96 && id.All(c=>c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
    static string Contained(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative) || System.IO.Path.IsPathRooted(relative) || relative.IndexOf(':')>=0 || relative.Split('/','\\').Any(p=>p==".."))throw new InvalidDataException("Unsafe voice path");
        var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative.Replace('/',System.IO.Path.DirectorySeparatorChar).Replace('\\',System.IO.Path.DirectorySeparatorChar)));
        var prefix=root.TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
        if(path!=root && !path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unsafe voice path");
        return path;
    }
    void Add(VoicePack pack){if(byId.ContainsKey(pack.Id))throw new InvalidDataException("Duplicate voice ID");byId.Add(pack.Id,pack);packs.Add(pack);}
    bool MatchesContract(PreparedSpeechCatalog candidate)=>candidate.IsValid && candidate.Clips.Count==contract.Clips.Count && contract.Clips.All(p=>candidate.Clips.TryGetValue(p.Key,out var other) && p.Value.Text==other.Text && new HashSet<string>(p.Value.Aliases,StringComparer.Ordinal).SetEquals(other.Aliases));
    public string NormalizeChoice(string? id)
    {
        if(id!=null && byId.ContainsKey(id))return id;
        if(!string.IsNullOrEmpty(id))Report("voice-missing:"+id,"Selected voice is unavailable; using the default voice.");
        return DefaultVoiceId;
    }
    public string DisplayName(string? id)=>byId[NormalizeChoice(id)].DisplayName;
    public string? KeyForText(string text)=>contract.KeyForText(text);
    public string? TextForKey(string key)=>contract.TextForKey(key);
    public SpeechAsset? Resolve(string key,string? voiceId)
    {
        var selected=NormalizeChoice(voiceId);
        var result=FromPack(key,selected);
        return result??(selected!=FallbackVoiceId?FromPack(key,FallbackVoiceId):null);
    }
    SpeechAsset? FromPack(string key,string voiceId)
    {
        if(!byId.TryGetValue(voiceId,out var pack) || !pack.Catalog.Clips.TryGetValue(key,out var clip))
        {Report("key-missing:"+voiceId+":"+key,"Speech key unavailable in voice pack: "+voiceId+" / "+key);return null;}
        if(rejected.Contains(clip.Path) || !ValidFile(clip))
        {Report("clip-invalid:"+voiceId+":"+key,"Speech asset unavailable in voice pack: "+voiceId+" / "+key);return null;}
        return new SpeechAsset(key,voiceId,clip.Path);
    }
    public void Reject(SpeechAsset asset)
    {rejected.Add(asset.Path);Report("decode:"+asset.VoiceId+":"+asset.Key,"Speech decoder could not play: "+asset.VoiceId+" / "+asset.Key);}
    bool ValidFile(PreparedSpeechCatalog.Clip clip)
    {
        try
        {
            var file=new FileInfo(clip.Path);if(!file.Exists)return false;
            long length=file.Length,stamp=file.LastWriteTimeUtc.Ticks;
            if(checkedFiles.TryGetValue(clip.Path,out var cached) && cached.Length==length && cached.Stamp==stamp)return cached.Valid;
            bool valid=false;
            using(var stream=File.OpenRead(clip.Path))
            {
                using var sha=SHA256.Create();
                var hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");stream.Position=0;
                valid=clip.Sha256.Length==64 && hash.Equals(clip.Sha256,StringComparison.OrdinalIgnoreCase) && IsRuntimeWave(stream);
            }
            checkedFiles[clip.Path]=(length,stamp,valid);return valid;
        }
        catch(Exception e) when(IsContentError(e)){return false;}
    }
    public static bool IsRuntimeWave(Stream stream)
    {
        try
        {
            using var reader=new BinaryReader(stream,System.Text.Encoding.ASCII,true);
            if(new string(reader.ReadChars(4))!="RIFF")return false;
            uint riff=reader.ReadUInt32();if(riff+8L!=stream.Length || new string(reader.ReadChars(4))!="WAVE")return false;
            bool format=false,data=false;
            while(stream.Position+8<=stream.Length)
            {
                var chunk=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long end=stream.Position+size+(size&1);
                if(end>stream.Length)return false;
                if(chunk=="fmt " && size>=16)
                {
                    var encoding=reader.ReadUInt16();var channels=reader.ReadUInt16();var rate=reader.ReadUInt32();var bytes=reader.ReadUInt32();var align=reader.ReadUInt16();var bits=reader.ReadUInt16();
                    format=encoding==1 && channels==1 && rate==24000 && bytes==48000 && align==2 && bits==16;
                }
                if(chunk=="data")data=size>=5760 && size%2==0;
                stream.Position=end;
            }
            return format && data;
        }
        catch(IOException){return false;}
    }
    void Report(string key,string message){if(reported.Count<128 && reported.Add(key))diagnostic?.Invoke(message);}
}
