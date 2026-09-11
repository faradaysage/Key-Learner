using System.Text.Json;
namespace KeyLearner.Studio;
public sealed class IconLibrary
{
    public Dictionary<string,string> Catalog {get;}
    public string[] Names {get;}
    private static readonly string[] Friendly=["face-smile","star","heart","sun","moon","cloud","rainbow","snowflake","cat","dog","fish","frog","hippo","otter","dove","dragon","paw","apple-whole","carrot","lemon","ice-cream","cookie","cake-candles","car","bicycle","rocket","plane","sailboat","train","house","tree","leaf","seedling","flower","music","bell","gift","balloon","futbol","basketball","volleyball","baseball","umbrella","crown","gem","puzzle-piece","robot","shapes"];
    public static int[] Keys=>Enumerable.Range(8,247).Where(k=>KeyboardMap.Character(k)==null && k!=32 && Enum.IsDefined(typeof(Microsoft.Xna.Framework.Input.Keys),k)).ToArray();
    public IconLibrary()
    {
        Catalog=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Content","Icons","catalog.json")))!;
        Names=Friendly.Where(Catalog.ContainsKey).Concat(Catalog.Keys.Except(Friendly).Order()).Distinct().ToArray();
    }
    public string NameFor(int key,Settings settings)
    {
        if(settings.KeyIcons.TryGetValue(key,out var name) && Catalog.ContainsKey(name))return name;
        if(key==112)return "face-smile";
        var choices=Friendly.Where(Catalog.ContainsKey).ToArray();
        return choices[Math.Abs(key-112)%choices.Length];
    }
    public string Glyph(string name)=>char.ConvertFromUtf32(Convert.ToInt32(Catalog.GetValueOrDefault(name,Catalog["star"]),16));
    public static string KeyName(int key)=>((Microsoft.Xna.Framework.Input.Keys)key).ToString();
}
