using System.Text.Json;
namespace KeyLearner.Studio;

public enum Backdrop { Aurora, Plasma, Vortex }
public enum Mood { Aurora, Lagoon, Sunset, Candy, PrimaryColors, BlackAndWhite }
public enum PlayMode { SmashGarden, WordAdventure, Counting }
public enum Celebration { Confetti, Rain, Orbit, Bubbles, Embers }
public enum LetterFont { Fredoka, Classic, Baloo }

public sealed class Settings
{
    public int EffectsVersion { get; set; }
    public Backdrop Backdrop { get; set; } = Backdrop.Plasma;
    public Mood Theme { get; set; } = Mood.PrimaryColors;
    public PlayMode Mode { get; set; }
    public LetterFont Font { get; set; }
    public bool Sound { get; set; } = true;
    public bool SpeakLetters { get; set; } = true;
    public bool GentleMotion { get; set; }
    public bool ForgivingSpelling { get; set; } = true;
    public bool AdaptiveLearning { get; set; } = true;
    public bool ShowKeyboard { get; set; } = true;
    public Dictionary<int,string> KeyIcons { get; set; } = new();
    public bool ShowContext { get; set; }
    public int KeyVoiceChannels { get; set; } = 4;
    public int WordVoiceChannels { get; set; } = 2;
    public int Volume { get; set; } = 85;
    public int SpeechRate { get; set; } = -1;
    public int ParticleLimit { get; set; } = 650;
    public double WordPause { get; set; } = 1.1;
    public double PrefixPause { get; set; } = 1.8;
    public double LetterLifetime { get; set; } = 5;
    public double Gravity { get; set; } = 110;
    public double Bounce { get; set; } = .65;
    public double EffectStrength { get; set; } = 1;
    public double FontScale { get; set; } = 1;
    public string WindowsVoice { get; set; } = "";
    public string PiperExecutable { get; set; } = "";
    public string PiperModel { get; set; } = "";
    public void Normalize()
    {
        KeyIcons ??= new(); KeyVoiceChannels=Math.Clamp(KeyVoiceChannels,1,5); WordVoiceChannels=Math.Clamp(WordVoiceChannels,1,3);
        Volume = Math.Clamp(Volume, 0, 100); SpeechRate = Math.Clamp(SpeechRate, -10, 10);
        ParticleLimit = Math.Clamp(ParticleLimit, 50, 2000);
        WordPause = Finite(WordPause, .4, 5, 1.1); PrefixPause = Finite(PrefixPause, .6, 6, 1.8);
        LetterLifetime = Finite(LetterLifetime, 1, 12, 5); Gravity = Finite(Gravity, 0, 500, 110);
        Bounce = Finite(Bounce, 0, .95, .65); EffectStrength = Finite(EffectStrength, .1, 2, 1);
        FontScale = Finite(FontScale, .5, 1.8, 1);
        WindowsVoice ??= ""; PiperExecutable ??= ""; PiperModel ??= "";
        if (!Enum.IsDefined(Backdrop)) Backdrop=Backdrop.Plasma;
        if (!Enum.IsDefined(Theme)) Theme = Mood.PrimaryColors;
        if (!Enum.IsDefined(Mode)) Mode = PlayMode.SmashGarden;
        if (!Enum.IsDefined(Font)) Font = LetterFont.Fredoka;
    }
    private static double Finite(double n, double min, double max, double fallback) => double.IsFinite(n) ? Math.Clamp(n,min,max) : fallback;
}
public sealed class WordEntry
{
    public string Word { get; set; } = "";
    public string Spoken { get; set; } = "";
    public string Recording { get; set; } = "";
    public string Image { get; set; } = "";
    public Celebration Effect { get; set; } = Celebration.Embers;
    public string EffectPreset { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool Adventure { get; set; }
}
public sealed class Profile
{
    public Dictionary<string, int> WordCounts { get; set; } = new();
    public Dictionary<string, double> PrefixHabits { get; set; } = new();
    public double TypingInterval { get; set; } = .4;
    public TinyNetwork Network { get; set; } = new();
}
public sealed class Store
{
    public string Root { get; }
    public string Status { get; private set; } = "Saved locally. No accounts or uploads.";
    public Settings Settings { get; }
    public Profile Profile { get; private set; }
    public List<WordEntry> Words { get; }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public Store(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyLearner");
        Directory.CreateDirectory(Root);
        var savedSettings=Read<Settings>("settings.json");
        Settings = savedSettings ?? new(); Settings.Normalize();
        if(savedSettings is null) DiscoverLocalVoice();
        Profile = Read<Profile>("profile.json") ?? new();
        Profile.PrefixHabits ??= new();
        foreach(var key in Profile.PrefixHabits.Keys.ToArray()) Profile.PrefixHabits[key]=double.IsFinite(Profile.PrefixHabits[key])?Math.Clamp(Profile.PrefixHabits[key],0,8):0;
        Profile.WordCounts ??= new(); Profile.Network ??= new(); Profile.Network.Validate();
        if (!double.IsFinite(Profile.TypingInterval)) Profile.TypingInterval = .4;
        Words = Read<List<WordEntry>>("words.json") ?? ImportWords();
        Words.RemoveAll(w => w is null || !ValidWord(w.Word));
        if(Settings.EffectsVersion<1) {foreach(var w in Words.Where(w=>w.Effect==Celebration.Confetti && string.IsNullOrEmpty(w.EffectPreset)))w.Effect=Celebration.Embers;Settings.EffectsVersion=1;}
        foreach (var w in Words) { w.Word = w.Word.ToLowerInvariant(); w.Spoken ??= ""; w.Recording ??= ""; w.Image ??= ""; }
    }
        private void DiscoverLocalVoice()
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
        {
            var executable=Path.Combine(directory.FullName,".local","piper","Scripts","piper.exe");
            var model=Path.Combine(directory.FullName,".local","voices","en_US-ljspeech-high.onnx");
            if(File.Exists(executable) && File.Exists(model)) { Settings.PiperExecutable=executable;Settings.PiperModel=model;break; }
        }
    }
    public static bool ValidWord(string? word) => word is { Length: >= 2 and <= 24 } && word.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z');
    private T? Read<T>(string name)
    {
        try { return File.Exists(Path.Combine(Root,name)) ? JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Root,name)),Json) : default; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Status = "Could not read " + name + "; using defaults. Original file preserved."; return default; }
    }
    public bool Save()
    {
        Settings.Normalize();
        try
        {
            Write("settings.json",Settings); Write("words.json",Words); Write("profile.json",Profile);
            Status = "Saved locally. No accounts or uploads."; return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status = "Save failed: " + e.Message; return false; }
    }
    private void Write<T>(string name,T value)
    {
        var path = Path.Combine(Root,name);
        File.WriteAllText(path + ".tmp",JsonSerializer.Serialize(value,Json));
        if (File.Exists(path)) File.Copy(path,path + ".bak",true);
        File.Move(path + ".tmp",path,true);
    }
    public void ResetLearning() => Profile = new();
    private static List<WordEntry> ImportWords()
    {
        string[] seeds = ["milk","mom","mommy","dad","daddy","cat","dog","sun","moon","star","rain","fish","bird","bear","tree","apple","happy","love","ball","book","blue","red","green","yellow"];
        var words = seeds.ToDictionary(w => w, w => new WordEntry { Word=w, Adventure=w.Length <= 5, Effect = w == "rain" ? Celebration.Rain : w == "moon" ? Celebration.Orbit : Celebration.Embers });
        var dir = Path.Combine(AppContext.BaseDirectory,"data");
        if (Directory.Exists(dir)) foreach (var file in Directory.GetFiles(dir,"*_dictionary.csv").Order())
            foreach (var line in File.ReadLines(file).Skip(1))
            {
                var parts = line.Split(','); var word = parts[0].Trim().ToLowerInvariant();
                if (!ValidWord(word)) continue;
                if (!words.TryGetValue(word,out var entry)) words[word] = entry = new() { Word=word };
                if (parts.Length > 1 && parts[1].Trim().Length > 0) entry.Image = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,parts[1].Trim()));
                if (parts.Length > 2 && parts[2].Trim().Length > 0) entry.Recording = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,parts[2].Trim()));
            }
        return words.Values.OrderBy(w=>w.Word).ToList();
    }
}
