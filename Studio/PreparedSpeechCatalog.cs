using System.Text.Json;
namespace KeyLearner.Studio;

/// <summary>Semantic IDs and text aliases shared by both engines; no generator dependency.</summary>
public sealed class PreparedSpeechCatalog
{
    public sealed class Clip
    {
        public string Id {get;}
        public string Text {get;}
        public string Path {get;}
        public string Sha256 {get;}
        public IReadOnlyList<string> Aliases {get;}
        internal Clip(string id,string text,string path,string hash,string[] aliases)
        {Id=id;Text=text;Path=path;Sha256=hash;Aliases=Array.AsReadOnly(aliases);}
    }
    readonly Dictionary<string,string> aliases=new(StringComparer.Ordinal);
    readonly Dictionary<string,Clip> clips=new(StringComparer.Ordinal);
    public int Count => aliases.Count;
    public IReadOnlyDictionary<string,Clip> Clips => clips;
    public bool IsValid {get;private set;}
    public static string Normalize(string text) => string.Join(" ", (text ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.', '!', '?');
    public PreparedSpeechCatalog(string directory)
    {
        var index=System.IO.Path.Combine(directory,"catalog.json");
        if(!File.Exists(index))return;
        try
        {
            using var json=JsonDocument.Parse(File.ReadAllText(index));
            foreach(var property in json.RootElement.GetProperty("clips").EnumerateObject())
            {
                var entry=property.Value;
                var file=entry.GetProperty("file").GetString();
                if(file!="speech-"+property.Name+".wav" || string.IsNullOrEmpty(file) || file.IndexOfAny(new[]{'/', '\\', ':'})>=0 || !file.StartsWith("speech-",StringComparison.Ordinal) || !file.EndsWith(".wav",StringComparison.Ordinal))throw new InvalidDataException("Invalid speech filename");
                var text=entry.TryGetProperty("text",out var spoken)?spoken.GetString()??"":"";
                var hash=entry.TryGetProperty("sha256",out var sha)?sha.GetString()??"":"";
                var names=entry.GetProperty("aliases").EnumerateArray().Select(a=>a.GetString()??"").Where(a=>a.Length>0).ToArray();
                var clip=new Clip(property.Name,text,System.IO.Path.Combine(directory,file),hash,names);
                clips.Add(property.Name,clip);
                foreach(var name in names)
                {
                    var normalized=Normalize(name);
                    if(aliases.TryGetValue(normalized,out var other) && other!=property.Name)throw new InvalidDataException("Ambiguous speech alias");
                    aliases[normalized]=property.Name;
                }
            }
            IsValid=clips.Count>0;
        }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {clips.Clear();aliases.Clear();}
    }
    public string? KeyForText(string text) => aliases.TryGetValue(Normalize(text),out var key)?key:null;
    public string? TextForKey(string key) => clips.TryGetValue(key,out var clip)?clip.Text:null;
    public string? FindKey(string key) => clips.TryGetValue(key,out var clip) && File.Exists(clip.Path)?clip.Path:null;
    public string? Find(string text) => KeyForText(text) is {} key?FindKey(key):null;
}
