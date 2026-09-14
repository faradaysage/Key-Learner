using System.Text.Json;
using System.Security.Cryptography;
using KeyLearner.Studio;
var root=Path.GetFullPath(args.FirstOrDefault() ?? ".");
bool partial=args.Contains("--available");
var voice=Path.Combine(root,"Content","Voice");
using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools","speech","manifest.json")));
using var index=JsonDocument.Parse(File.ReadAllText(Path.Combine(voice,"catalog.json")));
using var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools","speech","voice.json")));
var clips=index.RootElement.GetProperty("clips");
var resolver=new PreparedSpeechCatalog(voice);
var ids=new HashSet<string>();var aliases=new HashSet<string>();var errors=new List<string>();int checkedFiles=0;
foreach(var entry in manifest.RootElement.GetProperty("entries").EnumerateArray())
{
    var id=entry.GetProperty("id").GetString()!;
    if(!ids.Add(id))errors.Add("Duplicate ID: "+id);
    if(!clips.TryGetProperty(id,out var clip)){if(!partial)errors.Add("Missing clip: "+id);continue;}
    var recipe = new Dictionary<string,object?> { ["text"]=entry.GetProperty("text"), ["generationText"]=entry.TryGetProperty("generationText",out var generation)?generation:entry.GetProperty("text"), ["trimPrefix"]=entry.TryGetProperty("trimPrefix",out var trim)?trim:null, ["voice"]=config.RootElement, ["generator"]=2 };
    if(entry.TryGetProperty("seedOffset",out var offset) && offset.GetInt32()!=0) recipe["seedOffset"]=offset;
    if(entry.TryGetProperty("trimAligner",out var trimAligner)) recipe["trimAligner"]=trimAligner;
    using(var buffer=new MemoryStream())
    {
        using(var writer=new Utf8JsonWriter(buffer,new JsonWriterOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping})) WriteSorted(writer,JsonSerializer.SerializeToElement(recipe));
        if(!Convert.ToHexString(SHA256.HashData(buffer.ToArray())).Equals(clip.GetProperty("recipe").GetString(),StringComparison.OrdinalIgnoreCase)) errors.Add("Stale generation recipe: "+id);
    }
    var file="speech-"+id+".wav";var path=Path.Combine(voice,file);
    if(clip.GetProperty("file").GetString()!=file || !File.Exists(path)){errors.Add("Missing WAV: "+id);continue;}
    if(entry.GetProperty("text").GetString()!=clip.GetProperty("text").GetString())errors.Add("Stale spoken text: "+id);
    foreach(var alias in entry.GetProperty("aliases").EnumerateArray())
    {
        var text=alias.GetString()!;
        if(!aliases.Add(PreparedSpeechCatalog.Normalize(text)))errors.Add("Duplicate alias: "+text);
        if(resolver.Find(text)!=path || resolver.Find("  "+text.ToUpperInvariant()+"!  ")!=path)errors.Add("Unresolved alias: "+text);
    }
    using(var stream=File.OpenRead(path))
        if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(clip.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))errors.Add("Changed WAV: "+id);
    using(var reader=new BinaryReader(File.OpenRead(path)))
    {
        bool format=false,data=false;
        if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException(path);
        reader.ReadInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException(path);
        while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
        {
            string name=new(reader.ReadChars(4));int size=reader.ReadInt32();long end=reader.BaseStream.Position+size+(size&1);
            if(size<0 || end>reader.BaseStream.Length)throw new InvalidDataException(path);
            if(name=="fmt "){if(size<16)throw new InvalidDataException(path);format=reader.ReadUInt16()==1 && reader.ReadUInt16()==1 && reader.ReadInt32()==24000;reader.ReadInt32();reader.ReadUInt16();format=reader.ReadUInt16()==16 && format;}
            if(name=="data")data=size>0;
            reader.BaseStream.Position=end;
        }
        if(!format || !data)errors.Add("Unsupported runtime WAV: "+id);
    }
    checkedFiles++;
}
// These are the shipped CSV sources only, never a parent's private dictionary/profile.
if(!partial)foreach(var name in new[]{"app_dictionary.csv","CPB_dictionary.csv","custom_dictionary.csv"})
    foreach(var line in File.ReadLines(Path.Combine(root,"data",name)).Skip(1))
    {
        var text=line.Split(',')[0].Trim();
        if(text.Length>0 && resolver.Find(text)==null)errors.Add("Unprepared public dictionary word: "+text);
    }
foreach(var clip in clips.EnumerateObject())if(!ids.Contains(clip.Name))errors.Add("Obsolete catalog entry: "+clip.Name);
if(errors.Count>0){foreach(var error in errors.Take(30))Console.Error.WriteLine(error);throw new InvalidDataException($"{errors.Count} speech catalog errors.");}
Console.WriteLine(partial ? $"PARTIAL DEVELOPMENT CHECK: {checkedFiles} available clips and their aliases/hashes verified; catalog generation remains incomplete." : $"PASS: {checkedFiles} prepared speech WAVs, all manifest aliases and public dictionary words resolve in the shared runtime. No Python/model dependency.");

static void WriteSorted(Utf8JsonWriter writer,JsonElement value)
{
    switch(value.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();foreach(var p in value.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal)){writer.WritePropertyName(p.Name);WriteSorted(writer,p.Value);}writer.WriteEndObject();break;
        case JsonValueKind.Array:
            writer.WriteStartArray();foreach(var item in value.EnumerateArray())WriteSorted(writer,item);writer.WriteEndArray();break;
        default:value.WriteTo(writer);break;
    }
}
