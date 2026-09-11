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
            if(OperatingSystem.IsWindows())SetCurrentProcessExplicitAppUserModelID("KeyLearner.Desktop");
            if(args.Contains("--probe-guard"))
            {
                using(var probe=new KeyboardGuard(suppress:false)) Thread.Sleep(100);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"guard-probe.txt"),"PASS: native keyboard hook installed and released in pass-through mode.");
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

public sealed class StudioGame : Game
{
    private const int W=1440,H=900;
    private readonly GraphicsDeviceManager graphics;
    private readonly bool preview,demo,startStudio;
    private readonly string? screenshot;
    private readonly string scenario;
    private readonly Queue<KeyEvent> replay=new();
    private bool captured;
    private double renderSeconds; private int renderFrames;
    private int recognizedCount;
    private double lastLetterInput,lastRecognitionLag;
    private readonly Store store;
    private List<EffectRecipe> recipes=[];
    private readonly GestureAnalyzer analyzer=new();
    private readonly ParentChord chord=new();
    private readonly CountingRecognizer counting=new();
    private readonly HashSet<int> held=new();
    private readonly List<(Rectangle Rect,Action Click)> buttons=new();
    private readonly Dictionary<int,double> keyGlow=new();
    private WordRecognizer recognizer;
    private KeyboardGuard? guard;
    private Voice? voice;
    private Canvas canvas=null!;
    private SpriteBatch batch=null!;
    private SpriteFont ui=null!,title=null!,iconFont=null!;
    private IconLibrary icons=null!;
    private bool playStarted;
    private int selectedIconKey=112,iconPage;
    private string iconQuery="";
    private Texture2D pixel=null!;
    private RenderTarget2D surface=null!;
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
    private string[] Tabs=>["Experience","Voice","Learning","Dictionary","Developer","Key icons"];
    private Settings S=>store.Settings;
    private List<WordEntry> Filtered=>store.Words.Where(w=>w.Word.Contains(query,StringComparison.OrdinalIgnoreCase)).OrderBy(w=>w.Word).ToList();

    public StudioGame(string[] args)
    {
        scenario=ReadArg(args,"--scenario");
        preview=args.Contains("--preview"); demo=args.Contains("--demo"); startStudio=args.Contains("--studio");
        var capture=Array.IndexOf(args,"--screenshot"); if(capture>=0 && capture+1<args.Length) screenshot=Path.GetFullPath(args[capture+1]);
        if((demo || startStudio || screenshot!=null || scenario.Length>0) && !preview) throw new ArgumentException("Demo, studio inspection and screenshots require --preview.");
        var data=Array.IndexOf(args,"--data"); var root=data>=0 && data+1<args.Length?args[data+1]:null;
        store=new Store(root); recognizer=new(store);
        if(preview) {if(Enum.TryParse<Mood>(ReadArg(args,"--theme"),true,out var previewTheme))S.Theme=previewTheme;tab=ReadArg(args,"--page") switch {"voice"=>1,"learning"=>2,"dictionary"=>3,"developer"=>4,"icons"=>5,_=>0};if(ReadArg(args,"--mode")=="adventure")S.Mode=PlayMode.WordAdventure;if(ReadArg(args,"--mode")=="counting")S.Mode=PlayMode.Counting;}
        PrepareReplay();
        graphics=new(this) {PreferredBackBufferWidth=preview?1152:GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
            PreferredBackBufferHeight=preview?720:GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height,
            IsFullScreen=!preview,HardwareModeSwitch=false,SynchronizeWithVerticalRetrace=true};
        Content.RootDirectory="Content"; Window.Title="KeyLearner | a little world of discovery";
        Window.AllowUserResizing=false; Window.AllowAltF4=preview;
        IsMouseVisible=true; IsFixedTimeStep=true; TargetElapsedTime=TimeSpan.FromSeconds(1.0/60);
    }
    protected override void LoadContent()
    {
        SetWindowIcon();
        batch=new(GraphicsDevice); pixel=new(GraphicsDevice,1,1); pixel.SetData(new[]{Color.White});
        surface=new(GraphicsDevice,W,H);
        ui=Content.Load<SpriteFont>("Fonts/StudioUI"); title=Content.Load<SpriteFont>("Fonts/StudioTitle");
        var fonts=new[]{Content.Load<SpriteFont>("Fonts/StudioRounded"),Content.Load<SpriteFont>("Fonts/StudioClassic"),Content.Load<SpriteFont>("Fonts/StudioBold")};
        iconFont=Content.Load<SpriteFont>("Fonts/StudioIcons");icons=new();
        canvas=new(GraphicsDevice,fonts,S); voice=new(store.Root);
        parent=startStudio; recipes=EffectRecipe.Load(store.Root); ChooseTarget();
        if(scenario=="guided-mommy"){S.Mode=PlayMode.WordAdventure;target="mommy";}
        if(!preview)
        {
            guard=new KeyboardGuard();
            SDL_SetWindowGrab(Window.Handle,1);
            SDL_SetWindowAlwaysOnTop(Window.Handle,1);
        }
        notice=store.Status;
        base.LoadContent();
    }
    protected override void Update(GameTime gameTime)
    {
        now=KeyboardGuard.Now;
        while(replay.Count>0 && gameTime.TotalGameTime.TotalSeconds>=replay.Peek().Time) {var item=replay.Dequeue();Handle(item with {Time=now});}
        if(guard!=null)
        {
            if(guard.Overflow) throw new InvalidOperationException("Input queue overflow. Session ended safely.");
            while(guard.TryRead(out var e)) Handle(e);
            if(!IsActive) { SDL_RestoreWindow(Window.Handle); SDL_RaiseWindow(Window.Handle); }
        }
        else
        {
            var current=Keyboard.GetState();
            foreach(var k in previous.GetPressedKeys().Where(k=>current.IsKeyUp(k))) Handle(new((int)k,false,now));
            foreach(var k in current.GetPressedKeys().Where(k=>previous.IsKeyUp(k))) Handle(new((int)k,true,now));
            previous=current;
        }
        var mouse=Mouse.GetState();
        if(parent && mouse.LeftButton==ButtonState.Pressed && previousMouse.LeftButton==ButtonState.Released)
        {
            var p=new Point(mouse.X*W/GraphicsDevice.Viewport.Width,mouse.Y*H/GraphicsDevice.Viewport.Height);
            var button=buttons.LastOrDefault(b=>b.Rect.Contains(p)); button.Click?.Invoke();
        }
        previousMouse=mouse;
        if(calibration>=0 && now>calibrateUntil) { calibration=-1; parent=true; store.Save(); notice="Calibration saved. Add samples for each pattern for a balanced model."; held.Clear(); chord.Reset(); }
        if(!parent)
        {
            if(calibration<0) { var word=recognizer.Update(now,S.Mode==PlayMode.WordAdventure?target:null); if(word!=null) Celebrate(word); }

            counting.Update(now);
            canvas.Fields.SpaceHeld=held.Contains(32);
            canvas.Update((float)gameTime.ElapsedGameTime.TotalSeconds,W,H);
        }
        if(demo && gameTime.TotalGameTime.TotalSeconds<2.5 && gameTime.TotalGameTime.Milliseconds%120<18)
        {
            var c="WONDER"[(int)(gameTime.TotalGameTime.TotalSeconds*4)%6];
            canvas.Emit(c.ToString(),new(Gesture.Sweep,.9,random.NextDouble(),random.NextDouble(),.4,-.1,.8,1,[]),W,H);
            hero="wonder"; celebrateUntil=now+5;
        }
        if(screenshot!=null && gameTime.TotalGameTime.TotalSeconds>3) { VerifyReplay(); allowExit=true; Exit(); }
        voice?.Update();
        base.Update(gameTime);
    }
    private void Handle(KeyEvent e)
    {
        if(e.Down) { if(!held.Add(e.Key)) return; } else held.Remove(e.Key);
        var action=chord.Feed(e);
        if(action==ParentAction.Exit) { allowExit=true; Exit(); return; }
        if(action==ParentAction.Options) { ToggleParent(); return; }
        if(!e.Down) return;
        if(e.Key==27) hintUntil=now+7;
                if(parent)
        {
            if(captureIconKey) {captureIconKey=false;if(KeyboardMap.Character(e.Key)==null && e.Key!=32){selectedIconKey=e.Key;notice="Choose an icon for "+IconLibrary.KeyName(e.Key);}return;}
            if(ParentChord.IsModifier(e.Key) || held.Any(ParentChord.IsControl) || held.Any(ParentChord.IsAlt))return;
            ParentKey(e.Key);return;
        }
        if(e.Key is 27 or 79 && held.Any(ParentChord.IsControl) && held.Any(ParentChord.IsAlt))return;
        playStarted=true;
        var context=analyzer.Add(e.Key,e.Time,held.Count,store.Profile,S.AdaptiveLearning);
        keyGlow[e.Key]=now;
        if(calibration>=0) { store.Profile.Network.Train(context.Features,calibration); canvas.Emit(KeyboardMap.Character(e.Key)?.ToString()??"",context,W,H); return; }
        var character=KeyboardMap.Character(e.Key);
                if(e.Key==32) canvas.Fields.Ignite();
        else if(character!=null) canvas.Emit(character.ToString()!,context,W,H);
        else canvas.Emit(icons.Glyph(icons.NameFor(e.Key,S)),context,W,H,iconFont);
        if(ParentChord.IsModifier(e.Key))return;
        if(e.Key==8) { recognizer.Backspace(); return; }
        if(e.Key is 32 or 13) { var done=recognizer.Flush(S.Mode==PlayMode.WordAdventure?target:null); if(done!=null) Celebrate(done); return; }
        if(character==null) { if(S.SpeakLetters)voice?.Say(icons.NameFor(e.Key,S).Replace("face-", "").Replace("-", " "),S,key:true); return; }
        if(char.IsAsciiDigit(character.Value))
        {
            recognizer.Reset();
            var number=counting.Add(character.Value,e.Time);
            if(number!=null) { hero=number.Value.ToString(); celebrateUntil=now+2.5; lastWordAt=now;  canvas.Celebrate(Celebration.Orbit,W,H); voice?.Say(hero,S); }
            if(number==null && counting.Pending.Length==0 && S.SpeakLetters)voice?.Say(character.ToString()!,S,key:true);
            return; // Keep the first digit of a pending multi-digit count quiet.
        }
        if(S.Mode==PlayMode.Counting) return;
        if(S.Mode==PlayMode.WordAdventure)celebrateUntil=0;
        lastLetterInput=now;
        var word=recognizer.Add(character.Value,e.Time,context,S.Mode==PlayMode.WordAdventure?target:null); if(word!=null) Celebrate(word);
        if(word==null && S.SpeakLetters) voice?.Say(character.ToString()!,S,key:true);
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
        if(S.Mode==PlayMode.WordAdventure && word.Word==target) ChooseTarget();
    }
    private void ChooseTarget()
    {
        var candidates=store.Words.Where(w=>w.Enabled && w.Adventure).Select(w=>w.Word).ToArray();
        target=candidates.Length>0?candidates[random.Next(candidates.Length)]:"";
    }
    private void ToggleParent()
    {
        if(parent && !store.Save()) { notice=store.Status; return; }
        parent=!parent; editValue=null; editCommit=null; calibration=-1;
        recognizer.Reset(); recognizer.RefreshDictionary(); analyzer.Reset();  voice?.Stop(); canvas.Clear();playStarted=false;
        if(!parent) { ChooseTarget(); counting.Reset(); }
        notice=store.Status; recipes=EffectRecipe.Load(store.Root);
    }
    protected override void Draw(GameTime gameTime)
    {
        var renderStart=System.Diagnostics.Stopwatch.GetTimestamp();
        canvas.Prepare();
        GraphicsDevice.SetRenderTarget(surface); GraphicsDevice.Clear(canvas.Background);
        batch.Begin(samplerState:SamplerState.LinearClamp,blendState:BlendState.AlphaBlend);
        buttons.Clear();
        canvas.Draw(batch,(float)gameTime.TotalGameTime.TotalSeconds,W,H);
        if(parent) DrawParent(); else DrawPlay();
        if(editValue!=null) DrawEdit();
        batch.End();
        GraphicsDevice.SetRenderTarget(null); GraphicsDevice.Clear(canvas.Background);
        batch.Begin(samplerState:SamplerState.LinearClamp); batch.Draw(surface,GraphicsDevice.Viewport.Bounds,Color.White); batch.End();
        renderSeconds+=System.Diagnostics.Stopwatch.GetElapsedTime(renderStart).TotalSeconds;renderFrames++;
        if(!captured && screenshot!=null && gameTime.TotalGameTime.TotalSeconds>2.9)
        { using var file=File.Create(screenshot); surface.SaveAsPng(file,W,H); captured=true; }
        base.Draw(gameTime);
    }
        private static string ReadArg(string[] args,string name) {var i=Array.IndexOf(args,name);return i>=0 && i+1<args.Length?args[i+1]:"";}
    private void PrepareReplay()
    {
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
            for(int i=0;i<10;i++){replay.Enqueue(new(65,true,.1+i*.14));replay.Enqueue(new(65,false,.15+i*.14));}
        }
        if(scenario=="icons")
        {
            replay.Enqueue(new(112,true,.1));replay.Enqueue(new(112,false,.2));
            replay.Enqueue(new(113,true,.7));replay.Enqueue(new(113,false,.8));
            replay.Enqueue(new(112,true,1.2));replay.Enqueue(new(112,false,1.3));
        }
        if(scenario is "options" or "extra-key")
        {
            replay.Enqueue(new(162,true,.1));replay.Enqueue(new(164,true,.2));replay.Enqueue(new(79,true,.3));
            if(scenario=="extra-key") {replay.Enqueue(new(160,true,.4));replay.Enqueue(new(160,false,.5));}
            replay.Enqueue(new(79,false,1.2));replay.Enqueue(new(164,false,1.3));replay.Enqueue(new(162,false,1.4));
        }
        if(scenario=="counting")
        {
            double time=.1;
            foreach(var c in "12345678910") {replay.Enqueue(new(c,true,time));replay.Enqueue(new(c,false,time+.05));time+=.15;}
        }
    }
    private void VerifyReplay()
    {
        var success=scenario switch {"balloon"=>canvas.GlyphCount==1 && canvas.LargestGlyph>3,"milk"=>hero=="milk","rapid-milk"=>hero=="milk" && lastRecognitionLag<.001,"guided-mommy"=>hero=="mommy" && recognizedCount==1 && lastRecognitionLag<.001,"fire"=>canvas.Fields.FireLevel>.15f && playStarted,"fire-tap"=>!canvas.Fields.SpaceHeld && canvas.Fields.FireLevel<.01f,"icons"=>icons.NameFor(112,new Settings())=="face-smile" && playStarted,"mommy"=>hero=="mommy","options"=>parent,"extra-key"=>!parent,"counting"=>hero=="10" && counting.Expected==11,_=>true};
        if(scenario is "rapid-milk" or "mommy" or "balloon" && S.Sound)success=success && voice!=null && voice.Started==voice.Requested && voice.Completed==voice.Requested && voice.Overflow==0;
        if(!success) throw new InvalidOperationException("Preview scenario failed: "+scenario+"; hero="+hero+"; parent="+parent);
        if(screenshot!=null && voice!=null) File.WriteAllLines(screenshot+".audio.txt",new[]{ $"requested={voice.Requested}; started={voice.Started}; completed={voice.Completed}; overflow={voice.Overflow}; peak={voice.PeakOverlap}; max delay={voice.MaximumStartDelay:0.000}s"}.Concat(voice.Trace));
        if(scenario.Length>0 && screenshot!=null) File.WriteAllText(screenshot+".verified.txt","PASS "+scenario+"; voice="+voice?.Status+"; mean draw="+(renderSeconds/Math.Max(1,renderFrames)*1000).ToString("0.0")+"ms");
    }
    private void DrawPlay()
    {
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
        else if(S.Mode==PlayMode.WordAdventure)
        {
            Center(target.Length>0?"Can you find these letters?":"Choose adventure words in the parent dictionary.",220,Color.White*.7f,.62f);
            Center(target,290,Color.White,1.7f,title);
            Center(recognizer.Buffer.Length>0?recognizer.Buffer:"One little letter at a time.",420,canvas.Palette[0],.75f);
        }
        else if(S.Mode==PlayMode.Counting)
        {
            Center("Let's count the stars",215,Color.White*.75f,.7f);
            Center(counting.Expected.ToString(),290,canvas.Palette[2],2.3f,title);
            Center(counting.Pending.Length>0?counting.Pending+" _":"Find the number on your keyboard",450,Color.White*.6f,.62f);
        }
        else if(!quiet && recognizer.Buffer.Length>0) Center(recognizer.Buffer,150,Color.White*.6f,.7f);
        else if(!quiet && canvas.ParticleCount==0) { Center("A little touch. A little wonder.",305,Color.White*.9f,1.04f,title); Center("Press a key and see what grows.",390,canvas.Palette[0],.7f); }
        if(S.ShowKeyboard && !quiet) DrawKeyboard();
        if(S.ShowContext && !quiet) Text($"{analyzer.Current.Gesture} / confidence {analyzer.Current.Confidence:P0} / {canvas.ParticleCount} particles",45,680,Color.White*.55f,.46f);
        if(now<hintUntil) Text("Hold exactly Ctrl + Alt + Esc for 0.7s, then release to close. Ctrl + Alt + O: parents.",28,8,Color.White*.8f,.42f);
        if(!quiet) Text("Every little discovery counts.",50,850,Color.White*.3f,.42f);
    }
    private void DrawKeyboard()
    {
        string[] rows=["1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM"];
        for(var r=0;r<rows.Length;r++) for(var i=0;i<rows[r].Length;i++)
        {
            var key=rows[r][i]; var lit=keyGlow.TryGetValue(key,out var time)?(float)Math.Clamp(1-(now-time)/1.2,0,1):0;
            var x=490+i*43+r*13; var y=705+r*31;
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
        for(var i=0;i<Tabs.Length;i++) { var index=i; Button(new(55+i*220,193,207,53),Tabs[i],()=>{tab=index;row=0;},tab==i); }
        if(tab==3) DrawDictionary(); else if(tab==5) DrawIcons(); else DrawSettings();
        Fill(new(0,795,W,105),new Color(15,22,38));
        Text(notice,55,810,Color.White*.6f,.43f,maxWidth:1020);
        Text("Tab: next section    Arrows: select / adjust    Enter: edit    Ctrl + Alt + Esc: close",55,853,Color.White*.4f,.4f);
        Button(new(1135,816,250,53),"Save & return to play",()=>ToggleParent(),true);
    }
    private string[] PropertiesForTab() => tab switch {
        0=>["Mode","Theme","Font","Sound","GentleMotion","ShowKeyboard","FontScale","Backdrop"],
        1=>["WindowsVoice","SpeechRate","Volume","SpeakLetters","PiperExecutable","PiperModel","KeyVoiceChannels","WordVoiceChannels"],
        2=>["ForgivingSpelling","AdaptiveLearning","WordPause","PrefixPause"],
        _=>["ParticleLimit","Gravity","Bounce","EffectStrength","LetterLifetime","ShowContext"] };
    private void DrawSettings()
    {
        var names=PropertiesForTab();
        Text(tab switch {0=>"Small changes. A whole new mood.",1=>"A familiar voice makes a difference.",2=>"Follow their intent, at their pace.",_=>"Fine-tune the feel."},55,275,Color.White,.78f,title);
        var descriptions=tab switch {
            0=>"Smash for exploration. Word Adventure for early spelling. Counting for number sequences.",
            1=>"Recordings first, optional offline Piper second, installed Windows speech as fallback.",
            2=>"Mistakes are forgiven only when typing looks deliberate and a correction is unambiguous.",
            _=>"Bounded particles and fixed physics substeps keep keyboard storms responsive." };
        Text(descriptions,56,327,Color.White*.5f,.45f);
        for(var i=0;i<names.Length;i++)
        {
            var index=i; var property=typeof(Settings).GetProperty(names[i])!;
            var y=374+i*49;
            Button(new(55,y,1050,42),Nice(property.Name)+"   /   "+Display(property),()=>{row=index;ChangeProperty(property,1);},row==i);
        }
        if(tab==1)
        {
            Button(new(1140,374,240,48),"Try this voice",()=>voice?.Say("Hello little explorer. Milk. Mommy. Let's play.",S));
            Text("All voices play locally.",1140,443,Color.White*.5f,.42f);
            Text("No subscription needed.",1140,470,Color.White*.5f,.42f);
            Text(voice?.Status??"",55,779,canvas.Palette[0],.43f,maxWidth:1280);
        }
        if(tab==2)
        {
            Text("Optional calibration / 12 seconds per pattern",55,589,canvas.Palette[0],.48f);
            for(var i=0;i<5;i++) {var index=i; Button(new(55+i*264,628,248,45),((Gesture)i).ToString(),()=>{calibration=index;calibrateUntil=now+12;parent=false;analyzer.Reset();recognizer.Reset();voice?.Stop();});}
            Text("Patterns are estimates, not proof of one hand or two. Samples: "+store.Profile.Network.Samples,55,693,Color.White*.5f,.44f);
            Button(new(1110,718,270,44),"Reset learned preferences",()=>{store.ResetLearning();notice="Learning reset. Save to keep this change.";});
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
    private static string Nice(string name)=>System.Text.RegularExpressions.Regex.Replace(name,"([a-z])([A-Z])","$1 $2");
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
        else if(type==typeof(int)) {var step=p.Name=="ParticleLimit"?50:p.Name=="Volume"?5:1;p.SetValue(S,(int)p.GetValue(S)!+step*direction);}
        else if(type==typeof(double)) {var step=p.Name=="Gravity"?10:.1;p.SetValue(S,(double)p.GetValue(S)!+step*direction);}
        S.Normalize(); notice="Unsaved changes. Save & return to keep them.";
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
        guard?.Dispose(); guard=null;
        if(!preview) {SDL_SetWindowGrab(Window.Handle,0);SDL_SetWindowAlwaysOnTop(Window.Handle,0);}
        voice?.Dispose();store.Save();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) {ReleaseSession();wordImage?.Dispose();canvas?.Dispose();surface?.Dispose();pixel?.Dispose();batch?.Dispose();}
        base.Dispose(disposing);
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
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_RestoreWindow(nint window);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)] private static extern void SDL_RaiseWindow(nint window);
}
