using System.Text.Json;
namespace KeyLearner.Studio;
/// <summary>Prepared WAV lookup shared by both engines; no generator dependency.</summary>
public sealed class PreparedSpeechCatalog
{
    readonly Dictionary<string,string> paths = new(StringComparer.Ordinal);
    public int Count => paths.Count;
    public static string Normalize(string text) => string.Join(" ", (text ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.', '!', '?');
    public PreparedSpeechCatalog(string directory)
    {
        var index=Path.Combine(directory,"catalog.json");
        if(!File.Exists(index))return;
        try
        {
            using var json=JsonDocument.Parse(File.ReadAllText(index));
            foreach(var property in json.RootElement.GetProperty("clips").EnumerateObject())
            {
                var entry=property.Value;
                string? file=entry.GetProperty("file").GetString();
                if(string.IsNullOrEmpty(file) || Path.GetFileName(file)!=file || !file.StartsWith("speech-",StringComparison.Ordinal) || !file.EndsWith(".wav",StringComparison.Ordinal))continue;
                string path=Path.Combine(directory,file);
                foreach(var alias in entry.GetProperty("aliases").EnumerateArray())
                {
                    var text=alias.GetString();
                    if(!string.IsNullOrEmpty(text))paths[Normalize(text)]=path;
                }
            }
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {paths.Clear();}
    }
    public string? Find(string text) => paths.TryGetValue(Normalize(text),out var path) && File.Exists(path) ? path : null;
}
