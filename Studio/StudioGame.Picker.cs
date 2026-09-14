using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;
public sealed partial class StudioGame
{
    bool picker;
    readonly GameShortcut gameShortcut=new();
    int gameSelection,gameFilter;
    readonly string[] gameFilters=["All games","Explore","Letters","Numbers"];
    readonly Dictionary<PlayMode,RenderTarget2D> gamePreviews=new();
    GameDefinition[] PickerGames=>GameCatalog.Legacy.Where(g=>gameFilter==0 || g.Type==gameFilters[gameFilter] || g.Topics.Contains(gameFilters[gameFilter])).ToArray();
    void OpenPicker(){
        if(benchmarking)FinishBenchmark();
        parent=false;editValue=null;editCommit=null;calibration=-1;picker=true;gameFilter=0;
        gameSelection=Math.Max(0,Array.FindIndex(GameCatalog.Legacy,g=>g.Mode==S.Mode));ResetVisibleSession();
    }
    void PickGame(GameDefinition game){
        if(game.Mode==PlayMode.CannonHop&&!store.MathLearning.HopUnlocked){voice?.Stop();voice?.Say("First, play How Many Now. Practice joining and taking away.",S);return;}
        math=null;
        S.Mode=game.Mode;picker=false;ResetVisibleSession();ChooseTarget();store.Save();
    }
    void PickerHover(Point point){int first=gameSelection/6*6;for(int i=first;i<Math.Min(first+6,PickerGames.Length);i++)if(new Rectangle(64+(i%6)%3*446,250+(i%6)/3*281,420,261).Contains(point))gameSelection=i;}
    void PickerKey(int key){
        int count=PickerGames.Length;
        if(key==9){gameFilter=(gameFilter+1)%gameFilters.Length;gameSelection=0;}
        if(key is 37 or 39)gameSelection=(gameSelection+(key==37?count-1:1))%count;
        if(key is 38 or 40)gameSelection=Math.Clamp(gameSelection+(key==38?-3:3),0,count-1);
        if(key==13)PickGame(PickerGames[gameSelection]);
    }
    void PrepareGamePreviews(){
        if(!picker)return;
        foreach(var game in GameCatalog.Legacy.Where(g=>g.Explorer!=null && !gamePreviews.ContainsKey(g.Mode))){
            var texture=new RenderTarget2D(GraphicsDevice,808,302,false,SurfaceFormat.Color,DepthFormat.Depth24);
            GraphicsDevice.SetRenderTarget(texture);
            var model=new FlightModel();model.Configure(game.Explorer!.Value);
            if(game.Explorer==ExplorerKind.Bird)model.Position=new(ExplorerWorld.Valley(-2350),67,-2350);
            model.SetWord(game.Explorer==ExplorerKind.Racer?"race":game.Explorer==ExplorerKind.Dolphin?"dive":"bird");
            flightRenderer.Draw(model,S,canvas.Palette);gamePreviews.Add(game.Mode,texture);
        }
        GraphicsDevice.SetRenderTarget(null);
    }
    void DrawPicker(){
        Fill(new(0,0,W,H),new(10,16,35));
        for(int i=0;i<50;i++){int x=(i*313+37)%W,y=(i*173+53)%H;Fill(new(x,y,2,2),Color.White*.18f);}
        Text("A LITTLE WORLD OF DISCOVERY",64,30,canvas.Palette[1],.43f);
        Text("Where shall we play?",60,66,Color.White,1.12f,title);
        Text("Choose an adventure. Make it yours.",64,131,Color.White*.65f,.5f);
        for(int i=0;i<gameFilters.Length;i++){int filter=i;Button(new(64+i*200,179,185,44),gameFilters[i],()=>{gameFilter=filter;gameSelection=0;},i==gameFilter);}
        var games=PickerGames;
        int page=gameSelection/6,pages=(games.Length+5)/6;
        if(pages>1){Button(new(1036,179,96,44),"<",()=>gameSelection=((page+pages-1)%pages)*6);Text((page+1)+" / "+pages,1150,190,Color.White,.42f);Button(new(1244,179,96,44),">",()=>gameSelection=((page+1)%pages)*6);}
        for(int i=page*6;i<Math.Min(page*6+6,games.Length);i++){
            var game=games[i];int x=64+(i%6)%3*446,y=250+(i%6)/3*281;bool selected=i==gameSelection;
            var rect=new Rectangle(x,y,420,261);var tint=canvas.Palette[i%4];
            Fill(new(x-3,y-3,426,267),selected?tint:new Color(30,41,64));Fill(rect,new(20,29,49));
            var art=new Rectangle(x+8,y+8,404,151);
            if(gamePreviews.TryGetValue(game.Mode,out var previewImage))batch.Draw(previewImage,art,Color.White);
            else DrawGameIllustration(game,art,tint);
            Fill(new(x+17,y+19,80,25),new Color(10,16,35)*.9f);Text("AGE "+game.MinimumAge+"+",x+25,y+21,Color.White,.33f);
            Text(game.Name,x+18,y+171,Color.White,.68f,title,maxWidth:385);
            Text(game.Mode==PlayMode.CannonHop&&!store.MathLearning.HopUnlocked?"Play How Many Now? to unlock":game.Description,x+18,y+209,Color.White*.7f,.4f,maxWidth:385);
            Text(string.Join(" / ",game.Topics),x+18,y+237,tint,.3f,maxWidth:300);
            if(selected)Text("PLAY >",x+326,y+237,tint,.3f);
            buttons.Add((rect,()=>PickGame(game)));
        }
        Text("Arrow keys to explore   /   Enter to play   /   Tab to filter   /   Or click a card",64,839,Color.White*.8f,.48f);
        Text("During play: tap G, then G to come back here",64,876,canvas.Palette[1],.38f);
    }
    void DrawGameIllustration(GameDefinition game,Rectangle r,Color tint){
        Fill(r,game.Mode==PlayMode.Counting?new(13,19,52):new(26,43,68));
        if(MathActivityFor(game.Mode) is {} activity){
            EnsureDotDisc();
            for(int i=0;i<5;i++)DotCircle(new(r.X+105+i*48,r.Y+59),16,tint);
            DotText(activity switch{MathActivity.HowManyNow=>"+  /  -",MathActivity.Hiding=>"?",MathActivity.MakeNumber=>"1  2  3",MathActivity.Duel=>"<  =  >",_=>"0  >  10"},new(r.Center.X,r.Y+112),.72f,Color.White);
        }else if(game.Mode==PlayMode.Subitizing){
            foreach(int cell in new[]{0,2,4,6,8}){int x=r.X+155+cell%3*44,y=r.Y+26+cell/3*44;for(int line=-12;line<=12;line++){int half=(int)Math.Sqrt(Math.Max(0,144-line*line));Fill(new(x-half,y+line,half*2,1),canvas.Palette[1]);}}
        }else if(game.Mode==PlayMode.Counting){
            for(int fire=0;fire<3;fire++){float cx=r.X+80+fire*121,cy=r.Y+53+(fire%2)*30;var color=canvas.Palette[fire];
                for(int ray=0;ray<18;ray++){float a=ray*MathF.Tau/18;var start=new Vector2(cx+MathF.Cos(a)*19,cy+MathF.Sin(a)*19);batch.Draw(pixel,start,null,color,a,Vector2.Zero,new Vector2(23,2),SpriteEffects.None,0);}}
            Text("1   2   3",r.X+139,r.Y+103,Color.White,.7f,title);
        }else{
            for(int i=0;i<5;i++){float x=r.X+39+i*78,y=r.Y+25+(i%2)*29;var color=canvas.Palette[i%4];
                // Deliberately illustrated previews for the two canvas games.
                for(int line=-23;line<=23;line++){int half=(int)(Math.Sqrt(1-line*line/576d)*25);Fill(new((int)x-half,(int)y+line+24,half*2,1),color*.8f);}
                Text((game.Mode==PlayMode.WordAdventure?"WORDS":"SMASH")[i].ToString(),x-10,y+10,Color.White,.6f,title);
                if(game.Mode==PlayMode.WordAdventure)Fill(new((int)x,(int)y+48,1,35),Color.White*.45f);
            }
        }
    }
}
