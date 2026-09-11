using System.Text.Json;
namespace KeyLearner.Studio;

/// <summary>Data-only effect plugins. No executable scripts, shader code, or network access.</summary>
public sealed class EffectRecipe
{
    public string Name { get; set; } = "";
    public Celebration Shape { get; set; }
    public int Count { get; set; } = 150;
    public double Speed { get; set; } = 1;
    public double Lifetime { get; set; } = 4;
    public double Curl { get; set; } = 18;
    public double Gravity { get; set; } = 1;
    public double Trail { get; set; } = .25;
    public void Normalize()
    {
        Count=Math.Clamp(Count,1,400);
        Speed=Clamp(Speed,0,3,1);Lifetime=Clamp(Lifetime,.5,8,4);Curl=Clamp(Curl,-200,200,18);
        Gravity=Clamp(Gravity,-2,2,1);Trail=Clamp(Trail,0,1,.25);
        if(!Enum.IsDefined(Shape)) Shape=Celebration.Confetti;
    }
    private static double Clamp(double x,double min,double max,double fallback)=>double.IsFinite(x)?Math.Clamp(x,min,max):fallback;
    public static List<EffectRecipe> Load(string root)
    {
        var directory=Path.Combine(root,"effects");Directory.CreateDirectory(directory);
        var recipes=new List<EffectRecipe>();
        foreach(var file in Directory.GetFiles(directory,"*.json").Take(50))
        {
            try
            {
                if(new FileInfo(file).Length>32000) continue;
                var recipe=JsonSerializer.Deserialize<EffectRecipe>(File.ReadAllText(file));
                if(recipe is null || string.IsNullOrWhiteSpace(recipe.Name) || recipe.Name.Length>40) continue;
                recipe.Normalize();
                if(recipes.All(r=>r.Name!=recipe.Name)) recipes.Add(recipe);
            }
            catch(Exception e) when(e is JsonException or IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine(e.Message); }
        }
        return recipes;
    }
}
