using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace KeyLearner.Studio;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            if(args.Length>=4 && args[0]=="--restore-accessibility"){AccessibilitySession.Watch(args);return;}
            if(args.Contains("--probe-accessibility")){Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"KeyLearner-accessibility-probe"));using var session=new AccessibilitySession(Path.Combine(Path.GetTempPath(),"KeyLearner-accessibility-probe"));Thread.Sleep(args.Contains("--wait")?30000:500);return;}
            using var single=new System.Threading.Mutex(false,"Local\\KeyLearner.ProtectedSession");
            if(!args.Contains("--preview") && !args.Contains("--probe-guard")){try{if(!single.WaitOne(0))return;}catch(System.Threading.AbandonedMutexException){}}
            if(OperatingSystem.IsWindows())SetCurrentProcessExplicitAppUserModelID("KeyLearner.Desktop");
            if(args.Contains("--probe-guard"))
            {
                using(var probe=new KeyboardGuard(suppress:false)){Thread.Sleep(100);if(!probe.TryReadDesktopInput(out _))throw new InvalidOperationException("Native desktop input-state query failed.");if(probe.Snapshot().Count!=0)throw new InvalidOperationException("Disarmed native probe retained input.");}
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"guard-probe.txt"),"PASS: native keyboard/focus hooks installed and released in pass-through mode; desktop input-state query succeeded; disarmed input stayed empty.");
                return;
            }
            using var app=new StudioGame(args); app.Run();
        }
        catch(Exception e)
        {
            var path=Path.Combine(AppContext.BaseDirectory,"startup-error.log");
            File.WriteAllText(path,e.ToString());
            Environment.ExitCode=1;
            if(OperatingSystem.IsWindows() && !args.Contains("--preview")) MessageBox(0,"KeyLearner could not start. Keyboard protection has been released.\n"+e.Message,"KeyLearner",0x10);
        }
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int MessageBox(nint window,string text,string caption,uint type);
}

public sealed partial class StudioGame : Game
{
    private const int W=1440,H=900;
    private readonly GraphicsDeviceManager graphics;
    private readonly bool preview,demo,startStudio;
    private readonly string? screenshot;
    private readonly string scenario;
    private readonly double replaySeconds;
    private double replayClickAt;
    private long benchmarkFrameStart;
    private readonly Queue<KeyEvent> replay=new();
    private bool captured;
    private double renderSeconds; private int renderFrames;
    private int recognizedCount,maximumHeld;
    private double lastLetterInput,lastRecognitionLag;
    private readonly Store store;
    private List<EffectRecipe> recipes=[];
    private readonly GestureAnalyzer analyzer=new();
    private readonly ParentHold parentHold=new();
    private int visibilityReset,observedFocusLosses;
    private uint previousWindowFlags;
    private bool wasVisible=true;
    private int resumeClears;
    private bool lifecycleVerified;
    private int lifecycleStage;
    private readonly EscapeExit escapeExit=new();
    private readonly TapSequence optionsTaps=new(79);

    private readonly CountingRecognizer counting=new();
    private readonly FlightModel flight=new();
    private readonly GuidedSpelling guided=new();
    private bool IsExplorer=>S.Mode is PlayMode.BirdFlight or PlayMode.Racing or PlayMode.Dolphin;
    private FlightRenderer flightRenderer=null!;
    private double flightCelebration;
    private readonly HashSet<int> held=new();
    private readonly List<(Rectangle Rect,Action Click)> buttons=new();
    private readonly Dictionary<int,double> keyGlow=new();
    private WordRecognizer recognizer;
    private KeyboardGuard? guard;
    private AccessibilitySession? accessibility;
    private Voice? voice;
    private Canvas canvas=null!;
    private SpriteBatch batch=null!;
    private SpriteFont ui=null!,title=null!,iconFont=null!;
    private IconLibrary icons=null!;
    private bool playStarted,awaitingBalloons;
    private int selectedIconKey=112,iconPage;
    private string iconQuery="";
    private Texture2D pixel=null!;
    private RenderTarget2D surface=null!,composite=null!;
    private GlassRenderer glassRenderer=null!;
    private (int Width,int Height) renderSize;
    private bool renderSmooth;
    private KeyboardState previous;
    private MouseState previousMouse;
    private bool parent,allowExit,cleaned;
    private int tab,row,wordIndex;
    private double now,hintUntil,celebrateUntil,lastWordAt,calibrateUntil;
    private string hero="",target="cat",query="",notice="";
    private int calibration=-1;
    private string? editValue;
    private Action<string>? editCommit;
    private string editLabel="";
    private readonly Random random=new();
    private Texture2D? wordImage;
    private string[] Tabs=>["Experience","Voice","Learning","Dictionary","Developer","Key icons","Balloons","Play & safety","Graphics"];
    private Settings S=>store.Settings;
    private List<WordEntry> Filtered=>store.Words.Where(w=>w.Word.Contains(query,StringComparison.OrdinalIgnoreCase)).OrderBy(w=>w.Word).ToList();

    public StudioGame(string[] args)
    {
        scenario=ReadArg(args,"--scenario");
        replaySeconds=double.TryParse(ReadArg(args,"--seconds"),out var seconds)?Math.Clamp(seconds,3,30):3;
        preview=args.Contains("--preview"); demo=args.Contains("--demo"); startStudio=args.Contains("--studio");
        var capture=Array.IndexOf(args,"--screenshot"); if(capture>=0 && capture+1<args.Length) screenshot=Path.GetFullPath(args[capture+1]);
        if((demo || startStudio || screenshot!=null || scenario.Length>0) && !preview) throw new ArgumentException("Demo, studio inspection and screenshots require --preview.");
        var data=Array.IndexOf(args,"--data"); var root=data>=0 && data+1<args.Length?args[data+1]:null;
        if(preview&&string.IsNullOrWhiteSpace(root))root=Path.Combine(Path.GetTempPath(),"KeyLearner-preview-"+Guid.NewGuid().ToString("N"));
        store=new Store(root); recognizer=new(store);
        if(preview) {if(Enum.TryParse<Backdrop>(ReadArg(args,"--backdrop"),true,out var previewBackdrop))S.Backdrop=previewBackdrop;if(Enum.TryParse<Mood>(ReadArg(args,"--theme"),true,out var previewTheme))S.Theme=previewTheme;tab=ReadArg(args,"--page") switch {"voice"=>1,"learning"=>2,"dictionary"=>3,"developer"=>4,"icons"=>5,"balloons"=>6,"graphics"=>8,_=>0};if(ReadArg(args,"--mode")=="adventure")S.Mode=PlayMode.WordAdventure;if(ReadArg(args,"--mode")=="flight")S.Mode=PlayMode.BirdFlight;if(ReadArg(args,"--mode")=="racing")S.Mode=PlayMode.Racing;if(ReadArg(args,"--mode")=="dolphin")S.Mode=PlayMode.Dolphin;if(ReadArg(args,"--mode")=="dots" || scenario.StartsWith("dots-"))S.Mode=PlayMode.Subitizing;if(ReadArg(args,"--mode")=="counting")S.Mode=PlayMode.Counting;}
        ConfigureMathPreview(args);
        PrepareReplay();
        var sortedReplay=replay.OrderBy(e=>e.Time).ToArray();replay.Clear();foreach(var e in sortedReplay)replay.Enqueue(e);
        graphics=new(this) {PreferredBackBufferWidth=preview?(args.Contains("--portrait")?720:1152):GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
            PreferredBackBufferHeight=preview?(args.Contains("--portrait")?1080:720):GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height,
            IsFullScreen=!preview,HardwareModeSwitch=false,SynchronizeWithVerticalRetrace=S.VSync};
        if(preview){
            if(int.TryParse(ReadArg(args,"--width"),out int pw))graphics.PreferredBackBufferWidth=Math.Clamp(pw,640,2560);
            if(int.TryParse(ReadArg(args,"--height"),out int ph))graphics.PreferredBackBufferHeight=Math.Clamp(ph,360,1600);
        }
        Content.RootDirectory="Content"; Window.Title="KeyLearner | a little world of discovery";
        Window.AllowUserResizing=false; Window.AllowAltF4=preview;
        Activated+=(_,_)=>Interlocked.Exchange(ref visibilityReset,1);
        Deactivated+=(_,_)=>{guard?.SetGameActive(false);voice?.Stop();canvas?.Clear();dotSounds?.Stop();Interlocked.Exchange(ref visibilityReset,1);};
        IsMouseVisible=true; IsFixedTimeStep=true; TargetElapsedTime=TimeSpan.FromSeconds(1.0/60);
    }
    protected override void LoadContent()
    {
        SetWindowIcon();
        batch=new(GraphicsDevice); pixel=new(GraphicsDevice,1,1); pixel.SetData(new[]{Color.White});
        ResizeSurface();
        glassRenderer=new(GraphicsDevice,Content.Load<Effect>("Shaders/Glass"));
        ui=Content.Load<SpriteFont>("Fonts/StudioUI"); title=Content.Load<SpriteFont>("Fonts/StudioTitle");
        var fonts=new[]{Content.Load<SpriteFont>("Fonts/StudioRounded"),Content.Load<SpriteFont>("Fonts/StudioClassic"),Content.Load<SpriteFont>("Fonts/StudioBold")};
        iconFont=Content.Load<SpriteFont>("Fonts/StudioIcons");icons=new();
        flightRenderer=new(GraphicsDevice,fonts[0]);
        canvas=new(GraphicsDevice,fonts,S);canvas.PaintShader=Content.Load<Effect>("Shaders/Paint");canvas.ToonShader=Content.Load<Effect>("Shaders/Toon");flightRenderer.ToonShader=Content.Load<Effect>("Shaders/Toon");canvas.Fields.Configure(Content.Load<Effect>("Shaders/LiquidDensity"),Content.Load<Effect>("Shaders/LiquidSurface")); voice=new(store.Root);
        parent=startStudio;picker=!preview || scenario is "picker" or "picker-select"; recipes=EffectRecipe.Load(store.Root); ChooseTarget();
        if(scenario is "word-balloons" or "word-pop"){S.Mode=PlayMode.WordAdventure;target="cat";Celebrate(store.Words.First(w=>w.Word=="cat"));}
        if(scenario is "racing" or "dolphin"){S.Mode=scenario=="racing"?PlayMode.Racing:PlayMode.Dolphin;ChooseTarget();}
        if(scenario.StartsWith("region-") && Enum.TryParse<Region>(scenario[7..],true,out var region)){S.Mode=PlayMode.BirdFlight;flight.Position=new(0,region==Region.City||region==Region.Mountains?145:65,-((int)region*950+400));flight.SetWord("cat");}
        if(scenario=="flight-loop"){S.Mode=PlayMode.BirdFlight;S.FlightAssist=false;}
        if(scenario=="patient-red"){S.Mode=PlayMode.WordAdventure;target="red";guided.Start(target);}
        if(scenario=="spelling-timeout"){S.Mode=PlayMode.WordAdventure;for(int i=0;i<8;i++){guided.Start("red");foreach(char c in "red")guided.Add(c,0);}target="red";guided.Start(target);guided.Add('r',KeyboardGuard.Now-42.5);guided.Add('e',KeyboardGuard.Now-42.5);}
        if(scenario=="cannon-miss")canvas.Pointer(new(720,500),true,false,true,false,W,H);
        if(scenario=="flight"){S.Mode=PlayMode.BirdFlight;flight.SetWord("cat");}
        if(scenario=="fireworks")canvas.CountFireworks(10);
        if(scenario=="guided-mommy"){S.Mode=PlayMode.WordAdventure;target="mommy";guided.Start(target);}
        if(!preview)
        {
            accessibility=new(store.Root);
            guard=new KeyboardGuard();
            SDL_SetWindowGrab(Window.Handle,1);
            SDL_SetWindowAlwaysOnTop(Window.Handle,1);
        }
        if(scenario=="window-resume"){canvas.Emit("A",new(Gesture.Deliberate,1,.4,.5,0,0,.5,1,[]),W,H);for(int i=0;i<20;i++)voice.Say("mommy",S);held.Add(20);hero="old word";}
        if(scenario=="benchmark")StartBenchmark();
        if(scenario is "fracture" or "twenty-keys")S.ShowContext=true;
        if(!preview){ResetVisibleSession();previousWindowFlags=SDL_GetWindowFlags(Window.Handle);visibilityReset=0;}
        notice=store.Status;
        base.LoadContent();
    }
    protected override void Update(GameTime gameTime)
    {
        if(benchmarking)benchmarkFrameStart=System.Diagnostics.Stopwatch.GetTimestamp();
        now=KeyboardGuard.Now;
        if(scenario=="window-resume"){
            double t=gameTime.TotalGameTime.TotalSeconds;
            if(lifecycleStage==0 && t>.5){SDL_HideWindow(Window.Handle);lifecycleStage=1;}
            if(lifecycleStage==1 && t>1){SDL_ShowWindow(Window.Handle);lifecycleStage=2;}
        }
        uint windowFlags=SDL_GetWindowFlags(Window.Handle);
        bool visible=(preview && scenario!="window-resume") || (preview || IsActive && guard?.OwnsForeground==true) && (windowFlags&4)!=0 && (windowFlags&(8|64))==0;
        if((!preview || scenario=="window-resume") && (Interlocked.Exchange(ref visibilityReset,0)!=0 || visible!=wasVisible || guard!=null && observedFocusLosses!=guard.FocusLosses || ((windowFlags^previousWindowFlags)&(4|8|64|128))!=0))ResetVisibleSession();
        previousWindowFlags=windowFlags;wasVisible=visible;guard?.SetGameActive(visible);observedFocusLosses=guard?.FocusLosses??0;
        if(!preview && !visible && accessibility!=null){accessibility.Dispose();accessibility=null;}
        if(!preview && visible && accessibility==null)accessibility=new(store.Root);
        if(!visible){base.Update(gameTime);return;}
        UpdateBenchmark();
        while(replay.Count>0 && gameTime.TotalGameTime.TotalSeconds>=replay.Peek().Time) {var item=replay.Dequeue();if(scenario=="picker-select"&&item.Down&&item.Key==39)gameSelection=0;Handle(item with {Time=now});}
        if(guard!=null)
        {

            while(guard.TryRead(out var e)) {
                if(e.Key==-1){analyzer.Reset();recognizer.Reset();parentHold.Reset();continue;}
                if(e.Time<=0 || now-e.Time>.25)continue; // Never replay a backlog after a stalled frame.
                if(!guard.OwnsForeground){guard.SetGameActive(false);ResetVisibleSession();base.Update(gameTime);return;}
                Handle(e);
            }
            // The event queue is only history. This snapshot always replaces game held state.
            held.Clear();held.UnionWith(guard.Snapshot().Keys);
        }
        else
        {
            var current=Keyboard.GetState();
            foreach(var k in previous.GetPressedKeys().Where(k=>current.IsKeyUp(k))) Handle(new((int)k,false,now));
            foreach(var k in current.GetPressedKeys().Where(k=>previous.IsKeyUp(k))) Handle(new((int)k,true,now));
            previous=current;
        }
        var liveAction=parentHold.Update(KeySnapshot.From(held),now);
        if(liveAction==ParentAction.Exit){allowExit=true;Exit();return;}
        if(liveAction==ParentAction.Options){ToggleParent();guard?.DiscardEvents();}
        var mouse=Mouse.GetState();
        if(picker && (mouse.X!=previousMouse.X || mouse.Y!=previousMouse.Y))PickerHover(new(mouse.X*W/GraphicsDevice.Viewport.Width,mouse.Y*H/GraphicsDevice.Viewport.Height));
        if((parent || picker) && mouse.LeftButton==ButtonState.Pressed && previousMouse.LeftButton==ButtonState.Released)
        {
            var p=new Point(mouse.X*W/GraphicsDevice.Viewport.Width,mouse.Y*H/GraphicsDevice.Viewport.Height);
            var button=buttons.LastOrDefault(b=>b.Rect.Contains(p)); button.Click?.Invoke();
        }
        if(!parent && !picker && !IsExplorer && !IsDotGame && !IsMathGame)canvas.Pointer(new Vector2(mouse.X*W/(float)GraphicsDevice.Viewport.Width,mouse.Y*H/(float)GraphicsDevice.Viewport.Height),IsActive,mouse.X!=previousMouse.X || mouse.Y!=previousMouse.Y,mouse.LeftButton==ButtonState.Pressed && previousMouse.LeftButton==ButtonState.Released,mouse.RightButton==ButtonState.Pressed && previousMouse.RightButton==ButtonState.Released,W,H,mouse.LeftButton==ButtonState.Pressed);
        if(scenario=="word-pop" && gameTime.TotalGameTime.TotalSeconds>replayClickAt+.85 && canvas.FirstRewardPosition is {} balloonPoint){canvas.Pointer(balloonPoint,true,false,true,false,W,H);replayClickAt=gameTime.TotalGameTime.TotalSeconds;}
        if(scenario=="fracture" && gameTime.TotalGameTime.TotalSeconds>1.5 && canvas.Sheet.Panes.Count<12){canvas.Sheet.Hit(new(320,350),1);}
        if(IsMathPlay)UpdateMath((float)gameTime.ElapsedGameTime.TotalSeconds,mouse);
        if(IsDotPlay)UpdateDots((float)gameTime.ElapsedGameTime.TotalSeconds,mouse);
        previousMouse=mouse;
        if(calibration>=0 && now>calibrateUntil) { calibration=-1; parent=true; store.Save(); notice="Calibration saved. Add samples for each pattern for a balanced model."; held.Clear(); parentHold.Reset(); }
        if(!parent && !picker && (!IsDotGame && !IsMathGame || calibration>=0))
        {
            if(IsExplorer && calibration<0)UpdateFlight((float)gameTime.ElapsedGameTime.TotalSeconds);
            if(!IsExplorer && calibration<0 && !awaitingBalloons) { if(S.Mode==PlayMode.WordAdventure){if(guided.Update(now))canvas.SpellingTimeout();}else{var word=recognizer.Update(now);if(word!=null)Celebrate(word);} }

            if(awaitingBalloons && canvas.RewardRemaining==0){awaitingBalloons=false;recognizer.Reset();ChooseTarget();}
            counting.Update(now);
            canvas.Fields.SpaceHeld=!IsExplorer && held.Contains(32);
            canvas.Update((float)gameTime.ElapsedGameTime.TotalSeconds,W,H);
        }
        if(demo && gameTime.TotalGameTime.TotalSeconds<2.5 && gameTime.TotalGameTime.Milliseconds%120<18)
        {
            var c="WONDER"[(int)(gameTime.TotalGameTime.TotalSeconds*4)%6];
            canvas.Emit(c.ToString(),new(Gesture.Sweep,.9,random.NextDouble(),random.NextDouble(),.4,-.1,.8,1,[]),W,H);
            hero="wonder"; celebrateUntil=now+5;
        }
        if(screenshot!=null && gameTime.TotalGameTime.TotalSeconds>replaySeconds) { VerifyReplay(); allowExit=true; Exit(); }
        voice?.Update();
        base.Update(gameTime);
    }
    private void Handle(KeyEvent e)
    {
        if(e.Key==-1){held.Clear();parentHold.Reset();escapeExit.Reset();optionsTaps.Reset();gameShortcut.Reset();analyzer.Reset();return;}
        if(escapeExit.Feed(e)){allowExit=true;Exit();return;}
        bool openOptions=optionsTaps.Feed(e);
        if(e.Down) { if(!held.Add(e.Key)) return; } else held.Remove(e.Key);
        if(openOptions){optionsTaps.Reset();calibration=-1;guard?.ResetInput();held.Clear();parentHold.Reset();if(!parent)ToggleParent();return;}
        parentHold.Observe(KeySnapshot.From(held));
        maximumHeld=Math.Max(maximumHeld,held.Count);
        if(!parent && calibration<0 && gameShortcut.Feed(e,held.Any(ParentChord.IsModifier))){OpenPicker();return;}
        if(!e.Down) return;
        if(picker && !parent){if(!held.Any(ParentChord.IsModifier))PickerKey(e.Key);return;}
        if(e.Key==27) hintUntil=now+7;
                if(parent)
        {
            if(captureIconKey) {captureIconKey=false;if(KeyboardMap.Character(e.Key)==null && e.Key!=32){selectedIconKey=e.Key;notice="Choose an icon for "+IconLibrary.KeyName(e.Key);}return;}
            if(ParentChord.IsModifier(e.Key) || held.Any(ParentChord.IsControl) || held.Any(ParentChord.IsAlt))return;
            ParentKey(e.Key);return;
        }
        if(e.Key is 27 or 79 && held.Any(ParentChord.IsControl) && held.Any(k=>ParentChord.IsAlt(k)||k is 160 or 161))return;
        if(IsMathGame && calibration<0){if(!held.Any(ParentChord.IsModifier))MathKey(e.Key);return;}
        if(IsDotGame && calibration<0)return;
        if(benchmarking)return;
        if(IsExplorer && calibration<0){
            if(e.Key is 37 or 39)flight.TapTurn(e.Key==37?-1:1);
            if(ParentChord.IsControl(e.Key)&&flight.Signal())canvas.ExplorerSignal(flight.Kind);return;
        }
        playStarted=true;canvas.BeginKey(e.Key);
        var context=analyzer.Add(e.Key,e.Time,held.Count(k=>!ParentChord.IsModifier(k)),store.Gestures.Network,S.UseGestureCalibration);
        keyGlow[e.Key]=now;
        if(calibration>=0) { store.Gestures.Network.Train(context.Features,calibration); canvas.Emit(KeyboardMap.Character(e.Key)?.ToString()??"",context,W,H); return; }
        var character=KeyboardMap.Character(e.Key);
        if(S.Mode==PlayMode.WordAdventure){
            if(awaitingBalloons)return;
            if(e.Key==8){guided.Backspace();return;}
            if(character is {} typed && char.IsAsciiLetter(typed)){
                if(guided.Update(now))canvas.SpellingTimeout();
                canvas.Emit(typed.ToString(),context with{Gesture=Gesture.Deliberate},W,H);lastLetterInput=now;
                if(guided.Add(typed,now)){
                    var match=store.Words.FirstOrDefault(w=>w.Word==target);
                    if(match!=null){if(S.AdaptiveLearning)store.Profile.WordCounts[target]=Math.Min(100000,store.Profile.WordCounts.GetValueOrDefault(target)+1);Celebrate(match);}
                }else if(S.SpeakLetters)voice?.Say(typed.ToString(),S,key:true);
            }
            return;
        }
        var gestured=canvas.GestureEffect(context,W,H);
                if(e.Key==32) canvas.Fields.Ignite();
        else if(!gestured && character!=null) canvas.Emit(character.ToString()!,context,W,H);
        else if(!gestured) canvas.Emit(icons.Glyph(icons.NameFor(e.Key,S)),context,W,H,iconFont);
        if(ParentChord.IsModifier(e.Key))return;
        if(e.Key==8) { recognizer.Backspace(); return; }
        if(e.Key is 32 or 13) { var done=recognizer.Flush(S.Mode==PlayMode.WordAdventure?target:null); if(done!=null) Celebrate(done); return; }
        if(character==null) { if(S.SpeakLetters && !gestured)voice?.Say(icons.NameFor(e.Key,S).Replace("face-", "").Replace("-", " "),S,key:true); return; }
        if(char.IsAsciiDigit(character.Value))
        {
            recognizer.Reset();
            var number=counting.Add(character.Value,e.Time);
            if(number!=null) { hero=number.Value.ToString(); celebrateUntil=now+2.5; lastWordAt=now;  canvas.CountFireworks(number.Value); voice?.Say(hero,S); }
            if(number==null && counting.Pending.Length==0 && S.SpeakLetters)voice?.Say(character.ToString()!,S,key:true);
            return; // Keep the first digit of a pending multi-digit count quiet.
        }
        if(S.Mode==PlayMode.Counting || awaitingBalloons) return;
        if(S.Mode==PlayMode.WordAdventure)celebrateUntil=0;
        lastLetterInput=now;
        var word=recognizer.Add(character.Value,e.Time,context,S.Mode==PlayMode.WordAdventure?target:null); if(word!=null) Celebrate(word);
        if(word==null && S.SpeakLetters && !gestured) voice?.Say(character.ToString()!,S,key:true);
    }
    private void Celebrate(WordEntry word)
    {
        recognizedCount++;lastRecognitionLag=now-lastLetterInput;
        hero=word.Word; celebrateUntil=now+(S.Mode==PlayMode.WordAdventure?.65:3.8); lastWordAt=now;
        canvas.Celebrate(word.Effect,W,H,recipes.FirstOrDefault(r=>r.Name==word.EffectPreset));
        voice?.Say(word.Spoken.Length>0?word.Spoken:word.Word,S,word.Recording);
        wordImage?.Dispose(); wordImage=null;
        if(word.Image.Length>0)
        {
            try { using var stream=File.OpenRead(word.Image); if(stream.Length>8_000_000) throw new IOException("Image must be smaller than 8 MB."); wordImage=Texture2D.FromStream(GraphicsDevice,stream); }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { notice="Image unavailable: "+e.Message; }
        }
        if(S.Mode==PlayMode.WordAdventure && word.Word==target){canvas.ReleaseWordBalloons(word.Word,W,H);awaitingBalloons=true;recognizer.Reset();}
    }
    private void UpdateFlight(float dt)
    {
        if(benchmarking){benchmarkFlight.Step(dt,0,0,false,true);return;}

        flight.Response=(float)S.FlightResponse;flight.TopSpeed=(float)S.FlightTopSpeed;
        if(flight.Collected>=flight.Word.Length && flight.RewardRemaining<=0){ChooseTarget();}
        var previous=flight.Collected;
        if(flight.Step(dt,(held.Contains(39)?1:0)-(held.Contains(37)?1:0),(held.Contains(40)?1:0)-(held.Contains(38)?1:0),held.Contains(32),S.FlightAssist))
        {
            if(flight.Collected>=flight.Word.Length){voice?.Say(flight.Word,S);hero=flight.Word;flightCelebration=now+2;canvas.ExplorerComplete();}
            else voice?.Say(flight.Word[previous].ToString(),S,key:true);
        }
    }
    private void DrawFlight()
    {
        string label=S.Mode==PlayMode.Racing?"LETTER RACER":S.Mode==PlayMode.Dolphin?"OCEAN SPELLER":"SKY SPELLER";
        Text(label,45,30,Color.White,.65f,title);DrawScore(flight.Score);
        Center(now<flightCelebration?hero.ToUpperInvariant():new string(flight.Word.Take(flight.Collected).Select(char.ToUpperInvariant).ToArray())+new string('_',flight.Word.Length-flight.Collected),85,Color.White,.9f,title);
        string controls=S.Mode==PlayMode.Racing?"Left / Right steer   Up accelerates   Down brakes   Space boosts   Ctrl: letter magnet":S.Mode==PlayMode.Dolphin?"Up dives / Down rises   Space: swim faster   Double-tap Left / Right: roll   Ctrl: sonar":"Up dives / Down climbs into loops   Space: boost   Double-tap Left / Right: roll   Ctrl: call letters";
        Text(controls,45,825,Color.White,.43f);
        Center(flight.RewardRemaining>0?"WORD COMPLETE!   +"+(flight.Word.Length*10)+" BONUS":(flight.FirstLetter && flight.LastLetter?"START + FINISH":flight.FirstLetter?"START":flight.LastLetter?"FINISH":"KEEP GOING")+"   /   "+(flight.Collected+1)+" of "+flight.Word.Length,145,flight.LastLetter?Color.Gold:Color.White,.52f);
        Text("G G  /  Choose a game",45,785,Color.White*.8f,.43f);
        Text("Speed "+flight.Speed.ToString("0")+"     "+(S.Mode==PlayMode.Dolphin?"Coral gardens / kelp forest":ExplorerWorld.Area(flight.Position.Z).ToString())+(flight.Pulse>0?"     Letter magnet!":""),45,860,Color.White*.8f,.43f);
        if(now<hintUntil)Text("Hold Ctrl + Alt + O for 2s: parents. Ten O taps: parents. Ten Escape taps: quit.",45,120,Color.White,.45f);
    }
    private void DrawScore(int score){string text="SCORE  "+score;Text(text,W-45-ui.MeasureString(text).X*.58f,35,Color.White,.58f);}
    private void ChooseTarget()
    {
        var candidates=store.Words.Where(w=>w.Enabled && w.Adventure).Select(w=>w.Word).ToArray();
        if(S.Mode==PlayMode.WordAdventure && candidates.Length>0){var shortWords=candidates.Where(w=>w.Length<=guided.MaxWordLength).ToArray();candidates=shortWords.Length>0?shortWords:candidates.Where(w=>w.Length==candidates.Min(v=>v.Length)).ToArray();}
        target=candidates.Length>0?candidates[random.Next(candidates.Length)]:"";
        if(S.Mode==PlayMode.WordAdventure){guided.Timed=S.TimedSpelling;guided.Start(target);}
        if(IsExplorer){flight.Configure(GameCatalog.For(S.Mode).Explorer??ExplorerKind.Bird);flight.SetWord(target.Length>0?target:"cat");}
    }
    private void ToggleParent()
    {
        optionsTaps.Reset();picker=false;gameShortcut.Reset();dotSounds?.Stop();ResetMath();
        if(benchmarking){FinishBenchmark();return;}
        if(parent && !store.Save()) { notice=store.Status; return; }
        awaitingBalloons=false;parent=!parent; editValue=null; editCommit=null; calibration=-1;
        recognizer.Reset(); recognizer.RefreshDictionary(); analyzer.Reset();  voice?.Stop(); canvas.Clear();playStarted=false;
        if(!parent) { ChooseTarget(); counting.Reset(); }
        notice=store.Status; recipes=EffectRecipe.Load(store.Root);
    }
    protected override void Draw(GameTime gameTime)
    {
        var renderStart=System.Diagnostics.Stopwatch.GetTimestamp();
        ResizeSurface();
        PrepareGamePreviews();
        if(!IsDotPlay && !IsMathPlay)canvas.Prepare();
        GraphicsDevice.SetRenderTarget(surface); GraphicsDevice.Clear(canvas.Background);
        RenderSpace.Begin(batch);
        buttons.Clear();
        if(IsMathPlay)DrawMath();else if(IsDotPlay)DrawDots();else if(!parent && !picker && IsExplorer && calibration<0){batch.End();flightRenderer.Draw(benchmarking?benchmarkFlight:flight,S,canvas.Palette);RenderSpace.Begin(batch);}else canvas.Draw(batch,(float)gameTime.TotalGameTime.TotalSeconds,W,H);
        if(parent) DrawParent(); else if(picker)DrawPicker();else if(!benchmarking && !IsDotPlay && !IsMathPlay) DrawPlay();
        if(benchmarking)Text("Benchmark / "+Math.Max(0,6-(now-benchmarkStart)).ToString("0")+" seconds",40,160,Color.White,.6f);
        if(editValue!=null) DrawEdit();
        batch.End();
        GraphicsDevice.SetRenderTarget(composite);GraphicsDevice.Clear(canvas.Background);
        batch.Begin();batch.Draw(surface,composite.Bounds,Color.White);batch.End();
        if(!parent && !picker && !IsDotGame && !IsMathGame)glassRenderer.Draw(canvas.Sheet,surface,batch,pixel,S.GlassShader,S.GentleMotion);
        GraphicsDevice.SetRenderTarget(null); GraphicsDevice.Clear(canvas.Background);
        batch.Begin(samplerState:SamplerState.LinearClamp); var destination=GraphicsDevice.Viewport.Bounds;if(!parent && !IsDotGame && !IsMathGame){destination.X+=(int)canvas.Shake.X;destination.Y+=(int)canvas.Shake.Y;}if(IsMathPlay){destination.X+=(int)MathShake.X;destination.Y+=(int)MathShake.Y;}batch.Draw(composite,destination,Color.White); batch.End();
        if(benchmarking){var sample=new Color[1];composite.GetData(0,new Rectangle(composite.Width/2,composite.Height/2,1,1),sample,0,1);benchmarkFrames.Add(System.Diagnostics.Stopwatch.GetElapsedTime(benchmarkFrameStart>0?benchmarkFrameStart:renderStart).TotalMilliseconds);}
        renderSeconds+=System.Diagnostics.Stopwatch.GetElapsedTime(renderStart).TotalSeconds;renderFrames++;
        if(!captured && screenshot!=null && gameTime.TotalGameTime.TotalSeconds>replaySeconds-.1)
        { using var file=File.Create(screenshot); composite.SaveAsPng(file,composite.Width,composite.Height); captured=true; }
        base.Draw(gameTime);
    }
        private static string ReadArg(string[] args,string name) {var i=Array.IndexOf(args,name);return i>=0 && i+1<args.Length?args[i+1]:"";}
    private void PrepareReplay()
    {
        if(scenario=="game-picker"){replay.Enqueue(new(71,true,.2));replay.Enqueue(new(71,false,.3));replay.Enqueue(new(71,true,.45));replay.Enqueue(new(71,false,.55));}
        if(scenario=="picker-select"){picker=true;replay.Enqueue(new(39,true,.4));replay.Enqueue(new(39,false,.5));replay.Enqueue(new(13,true,.7));replay.Enqueue(new(13,false,.8));}
        if(scenario=="flight-loop"){replay.Enqueue(new(40,true,.1));replay.Enqueue(new(32,true,.1));replay.Enqueue(new(40,false,3.1));}
        if(scenario is "racing" or "dolphin"){replay.Enqueue(new(32,true,.1));}
        if(scenario=="patient-red"){double t=.2;foreach(char c in "red"){replay.Enqueue(new(char.ToUpperInvariant(c),true,t));replay.Enqueue(new(char.ToUpperInvariant(c),false,t+.05));t+=4;}}
        if(scenario=="shift-options"){foreach(int k in new[]{162,160,79})replay.Enqueue(new(k,true,.2));}
        if(scenario=="ten-o"){
            foreach(int k in new[]{65,83,68,162,164})replay.Enqueue(new(k,true,.1));
            for(int i=0;i<10;i++){replay.Enqueue(new(79,true,.3+i*.15));replay.Enqueue(new(79,false,.35+i*.15));}
        }
        if(scenario=="native-recovery"){
            var buffer=new KeyTransitionBuffer();var physical=new PhysicalKeyboard(buffer);
            physical.Feed(96,82,false,true,false,.1);physical.Feed(45,82,false,false,false,.12);
            physical.Feed(19,69,false,true,false,.15);physical.Feed(255,255,false,true,false,.17);
            double t=.3;foreach(var k in new[]{(77,50),(73,23),(76,38),(75,37)}){physical.Feed(k.Item1,k.Item2,false,true,false,t);physical.Feed(k.Item1,k.Item2,false,false,false,t+.04);t+=.09;}
            foreach(var k in new[]{(17,29),(18,56),(79,24)}){physical.Feed(k.Item1,k.Item2,false,true,false,t);t+=.03;}
            t+=2.15;
            foreach(var k in new[]{(79,24),(18,56),(17,29)}){physical.Feed(k.Item1,k.Item2,false,false,false,t);t+=.03;}
            while(buffer.TryRead(out var e))replay.Enqueue(e);
        }
        if(scenario=="twenty-keys")for(int k=65;k<85;k++)replay.Enqueue(new(k,true,.2));
        if(scenario=="quick-options"){double t=.1;foreach(int k in new[]{162,164,79}){replay.Enqueue(new(k,true,t));t+=.01;}foreach(int k in new[]{79,164,162}){replay.Enqueue(new(k,false,t));t+=.01;}}
        if(scenario is "cluster" or "glass" or "swipe")
        {
            var keys=scenario=="swipe"?"QWERTYUIOP":scenario=="glass"?"QAZPLMWSXOKNEDC":"ASDF";
            double t=.2;for(int round=0;round<(scenario=="glass"?4:1);round++){foreach(var c in keys){replay.Enqueue(new(c,true,t));if(scenario=="swipe")replay.Enqueue(new(c,false,t+.03));t+=scenario=="glass"?.02:.055;}if(scenario!="swipe")foreach(var c in keys){replay.Enqueue(new(c,false,t));t+=.001;}t+=.04;}
        }
        if(scenario is "milk" or "mommy" or "rapid-milk" or "guided-mommy")
        {
            var word=scenario=="milk"?"miolk":scenario=="rapid-milk"?"milk":"mommy";double time=.15;
            var interval=scenario=="rapid-milk"?.09:.25;
            foreach(var c in word) {replay.Enqueue(new(char.ToUpperInvariant(c),true,time));replay.Enqueue(new(char.ToUpperInvariant(c),false,time+.05));time+=interval;}
        }
                if(scenario is "fire" or "fire-tap")
        {
            replay.Enqueue(new(32,true,.1));replay.Enqueue(new(32,false,scenario=="fire"?3.1:.2));
        }
        if(scenario=="balloon")
        {
            for(int i=0;i<10;i++){replay.Enqueue(new(65,true,.1+i*.28));replay.Enqueue(new(65,false,.15+i*.28));}
        }
        if(scenario=="balloon-sequence"){double t=.1;foreach(var c in "qwqq"){replay.Enqueue(new(char.ToUpperInvariant(c),true,t));replay.Enqueue(new(char.ToUpperInvariant(c),false,t+.08));t+=.3;}}
        if(scenario=="icons")
        {
            replay.Enqueue(new(112,true,.1));replay.Enqueue(new(112,false,.2));
            replay.Enqueue(new(113,true,.7));replay.Enqueue(new(113,false,.8));
            replay.Enqueue(new(112,true,1.2));replay.Enqueue(new(112,false,1.3));
        }
        if(scenario is "options" or "extra-key")
        {
            replay.Enqueue(new(162,true,.1));replay.Enqueue(new(164,true,.2));replay.Enqueue(new(79,true,.3));
            if(scenario=="extra-key") {replay.Enqueue(new(160,true,1.5));replay.Enqueue(new(160,false,2.8));}
            replay.Enqueue(new(79,false,2.65));replay.Enqueue(new(164,false,2.7));replay.Enqueue(new(162,false,2.75));
        }
        if(scenario=="counting")
        {
            double time=.1;
            foreach(var c in "12345678910") {replay.Enqueue(new(c,true,time));replay.Enqueue(new(c,false,time+.05));time+=.15;}
        }
    }
    private void VerifyReplay()
    {
        var success=scenario switch {"math-pointer"=>math?.Stage==2&&picker&&!parent,"math-complete"=>math!=null&&math.Stage>=2&&mathPreviewActions==1,"math-round"=>math!=null,"dots-correct"=>dots.Stage>=2,"dots-retry"=>dots.Stage>=2 && dots.Mastery==0,"dots-visible"=>dots.DotsVisible && dots.Stage==1,"picker" or "game-picker"=>picker && !parent,"picker-select"=>!picker && S.Mode==PlayMode.WordAdventure,"cannon-miss"=>canvas.CannonShots==0 && canvas.Blasts==0,"patient-red"=>hero=="red" && recognizedCount==1,"spelling-timeout"=>guided.Progress==1,"racing"=>flight.Kind==ExplorerKind.Racer && flight.Position.Z< -100,"dolphin"=>flight.Kind==ExplorerKind.Dolphin && flight.Position.Y<0 && flight.Position.Z< -100,"flight-loop"=>float.IsFinite(flight.Position.Y),"shift-options"=>parent,"window-resume"=>lifecycleStage==2 && resumeClears>=2 && lifecycleVerified && voice?.Pending==0 && held.Count==0 && hero=="" && canvas.ParticleCount==0 && canvas.GlyphCount==0,"ten-o"=>parent,"native-recovery"=>parent && hero=="milk" && canvas.PaintCount==0 && canvas.ShatterCount==0,"twenty-keys"=>held.Count==20 && maximumHeld==20,"quick-options"=>!parent,"benchmark"=>!benchmarking && benchmarkFrames.Count>30 && recommendation.Length>0,"fracture"=>true,"flight"=>flight.Position.Z< -40,"cluster"=>canvas.PaintCount>0,"glass"=>canvas.ShatterCount>0,"swipe"=>canvas.Fields.Blobs.Drops.Any(d=>d.Wobble>0),"fireworks"=>canvas.RocketsLaunched==(replaySeconds>=5?10:8),"word-balloons"=>canvas.RewardRemaining==3,"word-pop"=>canvas.Score==45 && canvas.RewardRemaining==0,"balloon"=>canvas.PoppedCount>=1,"balloon-sequence"=>canvas.GlyphCount==3 && canvas.CountGlyph("Q")==2,"milk"=>hero=="milk","rapid-milk"=>hero=="milk" && lastRecognitionLag<.001,"guided-mommy"=>hero=="mommy" && recognizedCount==1 && lastRecognitionLag<.001,"fire"=>canvas.Fields.FireLevel>.15f && playStarted,"fire-tap"=>!canvas.Fields.SpaceHeld && canvas.Fields.FireLevel<.01f,"icons"=>icons.NameFor(112,new Settings())=="face-smile" && playStarted,"mommy"=>hero=="mommy","options"=>parent,"extra-key"=>!parent,"counting"=>hero=="10" && counting.Expected==11,_=>true};
        if(scenario is "rapid-milk" or "mommy" or "balloon" && S.Sound)success=success && voice!=null && voice.Started==voice.Requested && voice.Completed==voice.Requested && voice.Overflow==0;
        if(!success) throw new InvalidOperationException("Preview scenario failed: "+scenario+"; hero="+hero+"; parent="+parent);
        if(screenshot!=null && voice!=null) File.WriteAllLines(screenshot+".audio.txt",new[]{ $"requested={voice.Requested}; started={voice.Started}; completed={voice.Completed}; overflow={voice.Overflow}; peak={voice.PeakOverlap}; max delay={voice.MaximumStartDelay:0.000}s"}.Concat(voice.Trace));
        if(scenario.Length>0 && screenshot!=null) File.WriteAllText(screenshot+".verified.txt","PASS "+scenario+"; voice="+voice?.Status+"; mean draw="+(renderSeconds/Math.Max(1,renderFrames)*1000).ToString("0.0")+"ms");
    }
    private void DrawPlay()
    {
        if(IsExplorer && calibration<0){DrawFlight();return;}
        var quiet=S.Mode==PlayMode.SmashGarden && playStarted && calibration<0;
        if(!quiet) {
        Text("keylearner",50,35,Color.White*.8f,.85f);
        Text(S.Mode==PlayMode.WordAdventure?"WORD ADVENTURE":S.Mode==PlayMode.Counting?"COUNTING STARS":"SMASH GARDEN",50,77,canvas.Palette[0],.44f);
        Text(preview?"PREVIEW / keyboard not protected":"SESSION GUARD / OS shortcuts have limits",W-490,43,Color.White*.38f,.42f);
        }
        if(calibration>=0)
        {
            Center("Show me: "+((Gesture)calibration),150,Color.White,.75f);
            Center("Play for "+Math.Max(0,(int)(calibrateUntil-now))+" more seconds. Ctrl + Alt + O returns to the studio.",205,canvas.Palette[0],.48f);
        }
        else if(now<celebrateUntil)
        {
            Center(hero,275,Color.White,Math.Min(2.3f,1000/Math.Max(1,title.MeasureString(hero).X)),title);
            if(!quiet) Center(S.Mode==PlayMode.WordAdventure?"Wonderful. Let's discover another word.":"Look what you discovered!",420,canvas.Palette[0],.58f);
            if(wordImage!=null) {
                var scale=Math.Min(250f/wordImage.Width,180f/wordImage.Height);
                batch.Draw(wordImage,new Rectangle((int)(W/2-wordImage.Width*scale/2),470,(int)(wordImage.Width*scale),(int)(wordImage.Height*scale)),Color.White);
            }
        }
        else if(awaitingBalloons)
        {
            Center("Pop the balloons!",170,Color.White,.9f,title);
            Center("Click to fire. Move the mouse to bounce them.",235,canvas.Palette[0],.55f);
        }
        else if(S.Mode==PlayMode.WordAdventure)
        {
            Center(target.Length>0?"Can you find these letters?":"Choose adventure words in the parent dictionary.",220,Color.White*.7f,.62f);
            DrawGuidedLetters();
        }
        else if(S.Mode==PlayMode.Counting)
        {
            Center("Let's count the stars",215,Color.White*.75f,.7f);
            Center(counting.Expected.ToString(),290,canvas.Palette[2],2.3f,title);
            Center(counting.Pending.Length>0?counting.Pending+" _":"Find the number on your keyboard",450,Color.White*.6f,.62f);
        }
        else if(!quiet && recognizer.Buffer.Length>0) Center(recognizer.Buffer,150,Color.White*.6f,.7f);
        else if(!quiet && canvas.ParticleCount==0) { Center("A little touch. A little wonder.",305,Color.White*.9f,1.04f,title); Center("Press a key and see what grows.",390,canvas.Palette[0],.7f); }
        if(S.Mode==PlayMode.WordAdventure)DrawScore(guided.Score+canvas.Score);
        if(S.ShowKeyboard && (!quiet || S.ShowContext)) DrawKeyboard();
        if(S.ShowContext && !quiet) Text($"{analyzer.Current.Gesture} / confidence {analyzer.Current.Confidence:P0} / {canvas.ParticleCount} particles",45,680,Color.White*.55f,.46f);
        if(now<hintUntil) Text("Hold exact Ctrl + Alt + Esc for 2s to close. Hold Ctrl + Alt + O (or Ctrl + Shift + O) for 2s: parents. Ten taps: Esc / O.",28,8,Color.White*.8f,.42f);
        if(!quiet) Text("Every little discovery counts.",50,850,Color.White*.3f,.42f);
    }
    private void DrawKeyboard()
    {
        Text($"Held: {held.Count}  /  maximum: {maximumHeld}",490,675,Color.White*.7f,.38f);
        string[] rows=["1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM"];
        for(var r=0;r<rows.Length;r++) for(var i=0;i<rows[r].Length;i++)
        {
            var key=rows[r][i]; var lit=held.Contains(key)?1f:keyGlow.TryGetValue(key,out var time)?(float)Math.Clamp(1-(now-time)/1.2,0,1):0;
            var x=490+i*43+r*13; var y=705+r*31;
            if(S.Mode==PlayMode.WordAdventure && !awaitingBalloons && guided.Progress<target.Length && key==char.ToUpperInvariant(target[guided.Progress]))Fill(new(x-2,y-2,41,29),canvas.Palette[1]);
            Fill(new(x,y,37,25),Color.Lerp(new Color(33,41,61),canvas.Palette[r],lit*.75f));
            Text(key.ToString(),x+12,y+3,Color.White*(.27f+lit*.7f),.33f);
        }
    }
    private void DrawParent()
    {
        Fill(new(0,0,W,H),new Color(12,17,31)*.96f);
        Text("THE PARENT STUDIO",55,32,canvas.Palette[0],.45f);
        Text("Make room for wonder.",55,74,Color.White,.96f,title);
        Text("A little world, tuned to your child.",56,137,Color.White*.5f,.55f);
        for(var i=0;i<Tabs.Length;i++) { var index=i; Button(new(55+i*148,193,140,53),Tabs[i],()=>{tab=index;row=0;},tab==i); }
        if(tab==3) DrawDictionary(); else if(tab==5) DrawIcons(); else DrawSettings();
        Fill(new(0,795,W,105),new Color(15,22,38));
        Text($"v{typeof(Program).Assembly.GetName().Version} / {guard?.Diagnostics??"preview"}",55,879,Color.White*.45f,.3f);
        Text(notice,55,810,Color.White*.6f,.43f,maxWidth:1020);
        Text("Tab: next section    Arrows: select / adjust    Enter: edit    Ctrl + Alt + Esc: close",55,853,Color.White*.4f,.4f);
        Button(new(1135,816,250,53),"Save & return to play",()=>ToggleParent(),true);
    }
    private string[] PropertiesForTab() => tab switch {
        0=>["Theme","Font","Sound","GentleMotion","ShowKeyboard","FontScale","Backdrop"],
        1=>["WindowsVoice","SpeechRate","Volume","SpeakLetters","PiperExecutable","PiperModel","KeyVoiceChannels","WordVoiceChannels"],
        2=>["ForgivingSpelling","AdaptiveLearning","WordPause","PrefixPause","UseGestureCalibration"],
        8=>["RenderScale","LiquidScale","TerrainDetail","ExtrudedAssets","GlassShader","SmoothEdges","VSync"],
        7=>["GestureEffects","MousePlay","GrowingFire","EffectsSound","EffectsVolume","MouseTrailSize"],
        6=>["BalloonPopSize","BalloonDeflateSeconds"],
        _=>["ParticleLimit","Gravity","Bounce","EffectStrength","LetterLifetime","ShowContext","StarCount","StarSpeed"] };
    private void DrawSettings()
    {
        var names=PropertiesForTab();
        Text(tab switch {0=>"Small changes. A whole new mood.",1=>"A familiar voice makes a difference.",2=>"Follow their intent, at their pace.",6=>"A little puff. A big celebration.",8=>"A sharper world. Tuned to this laptop.",_=>"Fine-tune the feel."},55,275,Color.White,.78f,title);
        var descriptions=tab switch {
            0=>"Smash for exploration. Word Adventure for early spelling. Counting for number sequences.",
            1=>"Recordings first, optional offline Piper second, installed Windows speech as fallback.",
            8=>"Native output and GPU shading. Render scale 1 = native display resolution.",
            2=>"Mistakes are forgiven only when typing looks deliberate and a correction is unambiguous.",
            6=>"Pop size is a multiple of normal size. Deflate seconds is the time to lose one puff.",
            _=>"Bounded particles and fixed physics substeps keep keyboard storms responsive." };
        Text(descriptions,56,327,Color.White*.5f,.45f);
        for(var i=0;i<names.Length;i++)
        {
            var index=i; var property=typeof(Settings).GetProperty(names[i])!;
            var y=374+i*49;
            Button(new(55,y,1050,42),Nice(property.Name)+"   /   "+Display(property),()=>{row=index;ChangeProperty(property,1);},row==i);
        }
        if(tab==8){DrawBenchmarkControls();Button(new(55,737,490,42),"Toon asset shading / "+(S.ToonAssets?"On":"Off"),()=>S.ToonAssets=!S.ToonAssets);Button(new(565,737,490,42),"Flight assistance / "+(S.FlightAssist?"On":"Off"),()=>S.FlightAssist=!S.FlightAssist);}
        if(tab==7){Button(new(1130,620,245,48),"Touchpad setup & quit",()=>{allowExit=true;ReleaseSession();System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:devices-touchpad"){UseShellExecute=true});Exit();});Text("Clusters paint / mashing cracks glass / swipes make liquid trails.",55,674,Color.White*.6f,.43f);Text("Emergency: ten Escape taps quit; ten O taps open options. No other presses.",55,705,canvas.Palette[0],.43f);Text("Windows touchpad: set three/four-finger actions to Nothing before play.",55,737,Color.White*.6f,.43f);Text("Guard cannot block secure desktop or guarantee isolation. Use a dedicated child account.",55,768,Color.White*.5f,.4f);}
        if(tab==6){Text("Switching keys retires the old balloon. Return to that key to start a fresh one.",55,500,Color.White*.6f,.45f);Text("Inflated balloons float upward; a steady rhythm earns a sparkling pop.",55,535,Color.White*.6f,.45f);}
        if(tab==1)
        {
            Button(new(1140,374,240,48),"Try this voice",()=>voice?.Say("Hello little explorer. Milk. Mommy. Let's play.",S));
            Text("All voices play locally.",1140,443,Color.White*.5f,.42f);
            Text("No subscription needed.",1140,470,Color.White*.5f,.42f);
            Text(voice?.Status??"",55,779,canvas.Palette[0],.43f,maxWidth:1280);
        }
        if(tab==2)
        {
            Button(new(1130,374,245,42),"Timed spelling / "+(S.TimedSpelling?"On":"Off"),()=>{S.TimedSpelling=!S.TimedSpelling;guided.Timed=S.TimedSpelling;});
            Button(new(1130,429,245,42),"Response / "+S.FlightResponse.ToString("0.00"),()=>S.FlightResponse=S.FlightResponse>=2?.5:Math.Round(S.FlightResponse+.25,2));
            Button(new(1130,484,245,42),"Boost limit / "+S.FlightTopSpeed,()=>S.FlightTopSpeed=S.FlightTopSpeed>=240?80:S.FlightTopSpeed+20);
            Button(new(1130,539,245,42),"Restart spelling",()=>{guided.Restart();ChooseTarget();});
            Text("Optional calibration / 12 seconds per pattern",55,632,canvas.Palette[0],.48f);
            for(var i=0;i<5;i++) {var index=i; Button(new(55+i*264,667,248,45),((Gesture)i).ToString(),()=>{S.UseGestureCalibration=true;calibration=index;calibrateUntil=now+12;parent=false;analyzer.Reset();recognizer.Reset();voice?.Stop();});}
            Text("Calibration only trains the network. Word timing learns during play. Samples: "+store.Gestures.Network.Samples,55,722,Color.White*.5f,.44f);
            Button(new(55,750,350,38),"Reset gesture training",()=>{store.ResetGestureTraining();analyzer.Reset();notice="Gesture calibration reset; word learning kept. Save to keep this change.";});
            Button(new(425,750,350,38),"Reset word learning",()=>{recognizer.Reset();store.ResetWordLearning();notice="Word habits reset; gesture calibration kept. Save to keep this change.";});
        }
    }
        private void DrawIcons()
    {
        Text("Same key. Same little discovery.",55,275,Color.White,.78f,title);
        Text("Select a key, then choose its icon. Space always lights the fireplace. Parent chords stay reserved.",55,329,Color.White*.5f,.45f);
        Button(new(55,376,330,44),"Key / "+IconLibrary.KeyName(selectedIconKey),()=>{captureIconKey=true;notice="Press the key you want to remap.";});
        // Key capture is handled separately from text editing.
        Button(new(55,430,330,44),"Choose key from keyboard",()=>{captureIconKey=true;notice="Press a non-letter, non-number key to remap it. Space is reserved for fire.";});
        var assigned=icons.NameFor(selectedIconKey,S);
        batch.DrawString(iconFont,icons.Glyph(assigned),new Vector2(155,510),canvas.Palette[0],0,Vector2.Zero,2.3f,SpriteEffects.None,0);
        Text(assigned,55,655,Color.White,.48f,maxWidth:320);
        Button(new(55,711,330,44),"Restore this key's default",()=>S.KeyIcons.Remove(selectedIconKey));
        Button(new(420,376,730,44),iconQuery.Length>0?iconQuery:"Search "+icons.Names.Length+" icons...",()=>Edit("Find an icon",iconQuery,v=>{iconQuery=v;iconPage=0;}));
        var choices=icons.Names.Where(n=>n.Contains(iconQuery,StringComparison.OrdinalIgnoreCase)).ToArray();
        iconPage=Math.Clamp(iconPage,0,Math.Max(0,(choices.Length-1)/24));
        for(var i=iconPage*24;i<Math.Min(choices.Length,(iconPage+1)*24);i++)
        {
            var name=choices[i];var local=i%24;var rect=new Rectangle(420+(local%6)*159,435+(local/6)*74,148,65);
            Button(rect,"",()=>{S.KeyIcons[selectedIconKey]=name;notice=IconLibrary.KeyName(selectedIconKey)+" now always shows "+name+". Save to keep it.";},name==assigned);
            batch.DrawString(iconFont,icons.Glyph(name),new Vector2(rect.X+55,rect.Y+3),canvas.Palette[0],0,Vector2.Zero,.7f,SpriteEffects.None,0);
            Text(name,rect.X+6,rect.Y+45,Color.White*.7f,.29f,maxWidth:136);
        }
        Button(new(420,745,200,38),"Previous icons",()=>iconPage--);
        Button(new(635,745,200,38),"Next icons",()=>iconPage++);
        Text((iconPage+1)+" / "+Math.Max(1,(int)Math.Ceiling(choices.Length/24.0)),855,753,Color.White*.5f,.4f);
    }
    private bool captureIconKey;
    private void DrawDictionary()
    {
        Text("Words with a little magic.",55,272,Color.White,.78f,title);
        Button(new(55,329,470,44),query.Length==0?"Search words...":query,()=>Edit("Search dictionary",query,v=>{query=v;wordIndex=0;}));
        Button(new(545,329,190,44),"+ Add word",()=>Edit("New word (2-24 letters)","",v=>{
            v=v.Trim().ToLowerInvariant();
            if(!Store.ValidWord(v)) {notice="Use 2-24 letters A-Z.";return;}
            if(!store.Words.Any(w=>w.Word==v)) store.Words.Add(new(){Word=v});
            query=v;wordIndex=0;
        }));
        var words=Filtered; wordIndex=Math.Clamp(wordIndex,0,Math.Max(0,words.Count-1));
        var start=wordIndex/7*7;
        for(var i=start;i<Math.Min(start+7,words.Count);i++) {var index=i;var w=words[i];Button(new(55,392+(i-start)*48,470,42),(w.Enabled?"":"[off] ")+w.Word,()=>wordIndex=index,i==wordIndex);}
        Button(new(55,739,140,38),"Previous",()=>wordIndex=Math.Max(0,wordIndex-7));
        Button(new(205,739,140,38),"Next",()=>wordIndex=Math.Min(words.Count-1,wordIndex+7));
        Text(words.Count+" words",365,746,Color.White*.4f,.4f);
        if(words.Count==0) return;
        var entry=words[wordIndex];
        Text(entry.Word,575,391,Color.White,.85f,title);
        Button(new(575,459,800,40),"Spoken phrase / "+(entry.Spoken.Length>0?entry.Spoken:entry.Word),()=>Edit("What should the voice say?",entry.Spoken,v=>entry.Spoken=v));
        Button(new(575,507,800,40),"Celebration / "+(string.IsNullOrEmpty(entry.EffectPreset)?entry.Effect.ToString():entry.EffectPreset),()=>CycleEffect(entry));
        Button(new(575,555,800,40),"WAV recording / "+(entry.Recording.Length>0?entry.Recording:"Use speech"),()=>Edit("Absolute path to a WAV recording",entry.Recording,v=>entry.Recording=v.Trim().Trim('"')));
        Button(new(575,603,800,40),"Picture / "+(entry.Image.Length>0?entry.Image:"None"),()=>Edit("Absolute path to a PNG or JPG",entry.Image,v=>entry.Image=v.Trim().Trim('"')));
        Button(new(575,651,390,40),"Enabled / "+entry.Enabled,()=>entry.Enabled=!entry.Enabled);
        Button(new(980,651,395,40),"Adventure word / "+entry.Adventure,()=>entry.Adventure=!entry.Adventure);
        Button(new(575,715,390,44),"Preview word & celebration",()=>{Celebrate(entry);parent=false;});
        Button(new(980,715,395,44),"Rename word",()=>Edit("Rename word",entry.Word,v=>{
            v=v.ToLowerInvariant().Trim();
            if(Store.ValidWord(v) && !store.Words.Any(w=>w!=entry && w.Word==v)) entry.Word=v; else notice="Invalid or duplicate word.";
        }));
    }
    private void CycleEffect(WordEntry entry)
    {
        var names=Enum.GetNames<Celebration>().Concat(recipes.Select(r=>r.Name)).ToArray();
        var current=string.IsNullOrEmpty(entry.EffectPreset)?entry.Effect.ToString():entry.EffectPreset;
        var next=names[(Array.IndexOf(names,current)+1)%names.Length];
        if(Enum.TryParse<Celebration>(next,out var builtin)) {entry.Effect=builtin;entry.EffectPreset="";} else entry.EffectPreset=next;
    }
    private static string Nice(string name)=> name=="AdaptiveLearning"?"Learn word habits":System.Text.RegularExpressions.Regex.Replace(name,"([a-z])([A-Z])","$1 $2");
    private string Display(PropertyInfo p)
    {
        var value=p.GetValue(S);
        return value switch {bool b=>b?"On":"Off",double d=>d.ToString("0.##",CultureInfo.InvariantCulture),string s=>s.Length>0?s:"Automatic / not configured",_=>Nice(value?.ToString()??"")};
    }
    private void ChangeProperty(PropertyInfo p,int direction)
    {
        var type=p.PropertyType;
        if(type==typeof(bool)) p.SetValue(S,!(bool)p.GetValue(S)!);
        else if(type.IsEnum) {var values=Enum.GetValues(type);var index=Array.IndexOf(values.Cast<object>().ToArray(),p.GetValue(S));p.SetValue(S,values.GetValue((index+direction+values.Length)%values.Length));}
        else if(p.Name=="WindowsVoice")
        {
            var values=new[]{""}.Concat(voice?.Voices??[]).ToArray(); var index=Array.IndexOf(values,S.WindowsVoice);
            S.WindowsVoice=values[(Math.Max(0,index)+direction+values.Length)%values.Length];
        }
        else if(type==typeof(string)) Edit(Nice(p.Name),(string)p.GetValue(S)!,v=>p.SetValue(S,v.Trim().Trim('"')));
        else if(type==typeof(int)) {var step=p.Name is "ParticleLimit" or "StarCount"?50:p.Name=="Volume"?5:1;p.SetValue(S,(int)p.GetValue(S)!+step*direction);}
        else if(type==typeof(double)) {var step=p.Name=="Gravity"?10:.1;p.SetValue(S,(double)p.GetValue(S)!+step*direction);}
        S.Normalize(); if(p.Name=="VSync"){graphics.SynchronizeWithVerticalRetrace=S.VSync;graphics.ApplyChanges();}notice="Unsaved changes. Save & return to keep them.";
    }
    private void ParentKey(int key)
    {
        if(editValue!=null)
        {
            if(key==27) {editValue=null;editCommit=null;return;}
            if(key==13) {var v=editValue;var commit=editCommit;editValue=null;editCommit=null;commit?.Invoke(v);return;}
            if(key==8) {if(editValue.Length>0) editValue=editValue[..^1];return;}
            var c=TypedCharacter(key,held.Contains(160)||held.Contains(161));
            if(c!=null && editValue.Length<500) editValue+=c;
            return;
        }
        if(key==9) {tab=(tab+1)%Tabs.Length;row=0;return;}
        if(tab==5) {if(key==191)Edit("Find an icon",iconQuery,v=>{iconQuery=v;iconPage=0;});return;}
        if(tab==3)
        {
            if(key==38) wordIndex=Math.Max(0,wordIndex-1);
            if(key==40) wordIndex=Math.Min(Filtered.Count-1,wordIndex+1);
            if(key==191) Edit("Search dictionary",query,v=>{query=v;wordIndex=0;});
            return;
        }
        var props=PropertiesForTab();
        if(key==38) row=(row-1+props.Length)%props.Length;
        if(key==40) row=(row+1)%props.Length;
        if(key is 37 or 39 or 13) ChangeProperty(typeof(Settings).GetProperty(props[row])!,key==37?-1:1);
    }
    private static char? TypedCharacter(int key,bool shift)
    {
        if(key is >=65 and <=90) return shift?(char)key:char.ToLowerInvariant((char)key);
        if(key is >=48 and <=57) return shift?")!@#$%^&*("[key-48]:(char)key;
        return key switch {32=>' ',186=>shift?':':';',187=>shift?'+':'=',188=>shift?'<':',',189=>shift?'_':'-',190=>shift?'>':'.',191=>shift?'?':'/',192=>shift?'~':'`',219=>shift?'{':'[',220=>shift?'|':'\\',221=>shift?'}':']',222=>shift?'"':'\'',_=>null};
    }
    private void Edit(string label,string value,Action<string> commit) { editLabel=label;editValue=value;editCommit=commit; }
    private void DrawEdit()
    {
        buttons.Clear(); Fill(new(0,0,W,H),Color.Black*.8f); Fill(new(180,275,1080,320),new Color(28,37,59));
        Text(editLabel,215,311,canvas.Palette[0],.7f);
        Text(editValue+"_",215,380,Color.White,.58f,maxWidth:990);
        Text("Type to edit. Backspace removes a character. Enter saves. Escape cancels.",215,444,Color.White*.5f,.43f);
        Button(new(215,510,230,45),"Apply",()=>{var v=editValue;var c=editCommit;editValue=null;editCommit=null;c?.Invoke(v!);},true);
        Button(new(465,510,230,45),"Cancel",()=>{editValue=null;editCommit=null;});
        Button(new(715,510,230,45),"Clear field",()=>editValue="");
    }
    private void Fill(Rectangle rect,Color color)=>batch.Draw(pixel,rect,color);
    private void Text(string text,float x,float y,Color color,float scale=1,SpriteFont? font=null,float maxWidth=1300)
    {
        font??=ui;
        text=new string(text.Select(c=>font.Characters.Contains(c)?c:'?').ToArray());
        while(text.Length>3 && font.MeasureString(text).X*scale>maxWidth) text=text[..^4]+"...";
        batch.DrawString(font,text,new(x,y),color,0,Vector2.Zero,scale,SpriteEffects.None,0);
    }
    private void Center(string text,float y,Color color,float scale,SpriteFont? font=null)
    {font??=ui;Text(text,(W-font.MeasureString(text).X*scale)/2,y,color,scale,font);}
    private void Button(Rectangle rect,string label,Action click,bool selected=false)
    {
        Fill(rect,selected?new Color(54,75,84):new Color(28,37,56));
        if(selected) Fill(new(rect.X,rect.Y,3,rect.Height),canvas.Palette[0]);
        Text(label,rect.X+15,rect.Y+(rect.Height-23)/2,selected?canvas.Palette[0]:Color.White*.78f,.47f,maxWidth:rect.Width-30);
        buttons.Add((rect,click));
    }
    protected override void OnExiting(object sender,ExitingEventArgs args)
    {
        if(!allowExit && !preview) {args.Cancel=true;return;}
        ReleaseSession(); base.OnExiting(sender,args);
    }
    private void ReleaseSession()
    {
        if(cleaned)return;cleaned=true;
        if(benchmarking){benchmarking=false;RestoreBenchmarkSettings();}
        guard?.Dispose(); guard=null;accessibility?.Dispose();accessibility=null;
        if(!preview) {SDL_SetWindowGrab(Window.Handle,0);SDL_SetWindowAlwaysOnTop(Window.Handle,0);}
        voice?.Dispose();store.Save();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) {ReleaseSession();mathBalls?.Dispose();dotDisc?.Dispose();dotSounds?.Dispose();foreach(var texture in gamePreviews.Values)texture.Dispose();wordImage?.Dispose();flightRenderer?.Dispose();canvas?.Dispose();surface?.Dispose();composite?.Dispose();pixel?.Dispose();batch?.Dispose();}
        base.Dispose(disposing);
    }
    private void ResizeSurface()
    {
        var size=(Math.Clamp((int)(GraphicsDevice.PresentationParameters.BackBufferWidth*S.RenderScale),576,4096),Math.Clamp((int)(GraphicsDevice.PresentationParameters.BackBufferHeight*S.RenderScale),360,4096));
        if(renderSize==size && renderSmooth==S.SmoothEdges)return;renderSmooth=S.SmoothEdges;surface?.Dispose();composite?.Dispose();renderSize=size;
        surface=new(GraphicsDevice,size.Item1,size.Item2,false,SurfaceFormat.Color,DepthFormat.Depth24,S.SmoothEdges?4:0,RenderTargetUsage.DiscardContents);composite=new(GraphicsDevice,size.Item1,size.Item2);
    }
    private void SetWindowIcon()
    {
        using var stream=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Content","Branding","window-icon.png"));
        using var texture=Texture2D.FromStream(GraphicsDevice,stream);
        var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
        var handle=GCHandle.Alloc(pixels,GCHandleType.Pinned);
        try
        {
            var image=SDL_CreateRGBSurfaceFrom(handle.AddrOfPinnedObject(),texture.Width,texture.Height,32,texture.Width*4,0x000000ff,0x0000ff00,0x00ff0000,0xff000000);
            if(image!=0){SDL_SetWindowIcon(Window.Handle,image);SDL_FreeSurface(image);}
        }
        finally{handle.Free();}
    }
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern nint SDL_CreateRGBSurfaceFrom(nint pixels,int width,int height,int depth,int pitch,uint r,uint g,uint b,uint a);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_SetWindowIcon(nint window,nint surface);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_FreeSurface(nint surface);    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_SetWindowGrab(nint window,int grabbed);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_SetWindowAlwaysOnTop(nint window,int onTop);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern uint SDL_GetWindowFlags(nint window);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_HideWindow(nint window);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_ShowWindow(nint window);
}
