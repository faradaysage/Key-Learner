namespace KeyLearner.Studio;
public sealed record GameDefinition(PlayMode Mode,string Name,string Description,int MinimumAge,string Type,string[] Topics,ExplorerKind? Explorer=null);
public static class GameCatalog
{
    public static readonly GameDefinition[] All=[
        new(PlayMode.SmashGarden,"Smash Garden","Make a little keyboard magic",1,"Create",["Letters","Sensory"]),
        new(PlayMode.WordAdventure,"Word Adventure","Find the keys. Pop the balloons!",3,"Learn",["Letters","Spelling"]),
        new(PlayMode.Counting,"Counting Stars","Count your way to a fireworks show",2,"Learn",["Numbers"]),
        new(PlayMode.BirdFlight,"Sky Speller","Soar through a world of words",3,"Explore",["Letters","Spelling"],ExplorerKind.Bird),
        new(PlayMode.Racing,"Letter Racer","Boost, steer and collect letters",3,"Explore",["Letters","Spelling"],ExplorerKind.Racer),
        new(PlayMode.Dolphin,"Ocean Speller","Dive into an underwater adventure",3,"Explore",["Letters","Spelling"],ExplorerKind.Dolphin),
        new(PlayMode.Subitizing,"Dot Pop","See the dots. Tap how many.",2,"Learn",["Numbers","Subitizing"])
    ];
    public static GameDefinition For(PlayMode mode)=>All.FirstOrDefault(g=>g.Mode==mode)??All[0];
}
/// <summary>Two distinct G taps within 1.2 seconds. Repeats and modified keys never open the picker.</summary>
public sealed class GameShortcut
{
    bool down;double first=-10;int taps;
    public void Reset(){down=false;taps=0;first=-10;}
    public bool Feed(KeyEvent e,bool modified=false){
        if(modified || e.Key!=71){if(e.Down)Reset();return false;}
        if(e.Down){if(!down){if(e.Time-first>1.2)taps=0;if(taps==0)first=e.Time;down=true;}return false;}
        if(!down)return false;down=false;
        if(e.Time-first>1.2){Reset();return false;}
        if(++taps<2)return false;Reset();return true;
    }
}
