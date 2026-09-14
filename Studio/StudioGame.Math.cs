using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace KeyLearner.Studio;

public sealed partial class StudioGame
{
    MathGame? math;
    MathBallRenderer? mathBalls;
    MathPhase? mathSpokenPhase;
    int mathCountSpoken=-1,mathSavedRevision=-1,mathPreviewActions;
    int? mathDebugLevel,mathDebugA,mathDebugB;bool? mathDebugSubtract;
    string mathDebugState="";bool mathDebugApplied;
    Vector2 mathPointer;
    static readonly string[] MathNumbers=["zero","one","two","three","four","five","six","seven","eight","nine","ten"];
    bool IsMathGame=>MathActivityFor(S.Mode)!=null;
    bool IsMathPlay=>IsMathGame&&!parent&&!picker&&calibration<0;
    static MathActivity? MathActivityFor(PlayMode mode)=>mode switch {
        PlayMode.HowManyNow=>MathActivity.HowManyNow,PlayMode.WhatsHiding=>MathActivity.Hiding,
        PlayMode.MakeNumber=>MathActivity.MakeNumber,PlayMode.DotDuel=>MathActivity.Duel,PlayMode.CannonHop=>MathActivity.CannonHop,_=>null};
    static PlayMode MathMode(MathActivity activity)=>activity switch {MathActivity.HowManyNow=>PlayMode.HowManyNow,MathActivity.Hiding=>PlayMode.WhatsHiding,MathActivity.MakeNumber=>PlayMode.MakeNumber,MathActivity.Duel=>PlayMode.DotDuel,_=>PlayMode.CannonHop};
    void ConfigureMathPreview(string[] args){
        if(!preview)return;
        string requested=ReadArg(args,"--math");
        if(requested.Length==0)return;
        if(!Enum.TryParse<MathActivity>(requested,true,out var activity))throw new ArgumentException("--math: HowManyNow, Hiding, MakeNumber, Duel, or CannonHop");
        S.Mode=MathMode(activity);
        int? Number(string flag)=>int.TryParse(ReadArg(args,flag),out int n)?n:null;
        mathDebugLevel=Number("--math-level");mathDebugA=Number("--math-a");mathDebugB=Number("--math-b");
        mathDebugSubtract=ReadArg(args,"--math-operation") switch {"subtract"=>true,"add"=>false,_=>null};
        mathDebugState=ReadArg(args,"--math-state");
        if(args.Contains("--mute"))S.Sound=false;
        if(double.TryParse(ReadArg(args,"--render-scale"),out double scale))S.RenderScale=Math.Clamp(scale,.5,1.5);
    }
    void EnsureMath(){
        if(MathActivityFor(S.Mode) is not {} activity)return;
        if(math?.Activity==activity)return;
        math=new(activity,store.MathLearning,preview?701:Environment.TickCount,mathDebugLevel,mathDebugA,mathDebugB,mathDebugSubtract);
        store.MathLearning.LastActivity=activity;mathSpokenPhase=null;mathSavedRevision=-1;mathCountSpoken=-1;canvas.Clear();
    }
    void ResetMath(){math?.Restart();mathSpokenPhase=null;mathCountSpoken=-1;dotSounds?.Stop();}
    void MathKey(int key){
        if(key==27){OpenPicker();return;}
        if(math?.CanAnswer==true && math.Activity is not (MathActivity.MakeNumber or MathActivity.Duel)){
            int n=key is >=48 and <=57?key-48:key is >=96 and <=105?key-96:-1;
            if(n>=0 && (math.Activity==MathActivity.CannonHop||math.Round.Choices.Contains(n)))math.Answer(n);
        }
    }
    void MathTap(Vector2 screen){
        if(math==null)return;var p=MathLayout.Unproject(new(screen.X,screen.Y),GraphicsDevice.PresentationParameters.BackBufferWidth,GraphicsDevice.PresentationParameters.BackBufferHeight);
        if(MathLayout.Back.Contains(p)){OpenPicker();return;}
        if(MathLayout.Mute.Contains(p)){S.Sound=!S.Sound;if(!S.Sound){voice?.Stop();dotSounds?.Stop();}store.Save();return;}
        if(preview&&scenario=="math-pointer"&&screenshot!=null)File.AppendAllText(screenshot+".input.log",$"{math.Phase} ({p.X:0},{p.Y:0}) stage={math.Stage}\n");
        if(!math.CanAnswer)return;
        if(math.Activity==MathActivity.MakeNumber){
            for(int i=0;i<math.Difficulty.Capacity;i++)if(MathLayout.CellButton(i,math.Difficulty.Capacity).Contains(p)){
                if(math.Cell(i)&&math.Difficulty.Level<=2){voice?.Stop();voice?.Say(MathNumbers[math.BuiltCount],S);}break;
            }
        }else if(math.Activity==MathActivity.Duel){for(int side=0;side<2;side++)if(MathLayout.Group(side).Contains(p)){math.Answer(side);return;}if(MathLayout.Same.Contains(p))math.Answer(2);}
        else if(math.Activity==MathActivity.CannonHop){for(int n=0;n<=10;n++)if(MathLayout.Track(n).Contains(p)){math.Answer(n);return;}}
        else for(int i=0;i<math.Round.Choices.Length;i++)if(MathLayout.Choice(i,math.Round.Choices.Length).Contains(p)){math.Answer(math.Round.Choices[i]);return;}
    }
    string MathQuestion=>math!.Activity switch {
        MathActivity.Hiding=>"How many are hiding?",MathActivity.MakeNumber=>"Make "+MathNumbers[math.Round.B],
        MathActivity.Duel=>math.Round.Fewer?"Which has fewer?":"Which has more?",
        MathActivity.CannonHop=>(math.Round.Subtract?"Take away ":"")+MathNumbers[math.Round.B]+(math.Round.Subtract?"":" more")+". Where will it land?",
        _=>"How many now?"};
    void SpeakMathPhase(){
        var m=math!;if(mathSpokenPhase==m.Phase)return;mathSpokenPhase=m.Phase;mathCountSpoken=-1;
        if(preview&&scenario=="math-pointer"&&screenshot!=null)File.WriteAllText(screenshot+".state.json",System.Text.Json.JsonSerializer.Serialize(new{Phase=m.Phase.ToString(),m.Stage,m.Round.Answer,m.Round.Choices}));
        string text=m.Phase switch {
            MathPhase.Ready=>"Ready",MathPhase.Set=>"Set",MathPhase.Go=>"Go",
            MathPhase.Initial=>m.Activity==MathActivity.MakeNumber?MathQuestion:m.Activity==MathActivity.Duel?"":MathNumbers[m.Round.A],
            MathPhase.Transform=>m.Activity==MathActivity.Hiding?"":(m.Round.Subtract?"Take away ":"")+MathNumbers[m.Round.B]+(m.Round.Subtract?"":" more"),
            MathPhase.Ask=>MathQuestion,MathPhase.Incorrect=>"",MathPhase.Reward=>m.Activity==MathActivity.Duel?"":MathNumbers[m.Round.Answer],_=>""};
        if(text.Length>0){voice?.Stop();voice?.Say(text,S,brisk:m.Phase is MathPhase.Ready or MathPhase.Set or MathPhase.Go);}
        if(m.Phase==MathPhase.Incorrect){voice?.Stop();canvas.Clear();}
        if(m.Phase==MathPhase.Ready)canvas.Clear();
    }
    void UpdateMath(float dt,MouseState mouse){
        EnsureMath();var m=math!;dotSounds??=new();
        mathPointer=V(MathLayout.Unproject(new(mouse.X,mouse.Y),GraphicsDevice.PresentationParameters.BackBufferWidth,GraphicsDevice.PresentationParameters.BackBufferHeight));
        if(preview&&scenario=="math-pointer"&&screenshot!=null&&mouse.LeftButton!=previousMouse.LeftButton)File.AppendAllText(screenshot+".input.log",$"mouse {mouse.X},{mouse.Y} {mouse.LeftButton} world={mathPointer} phase={m.Phase}\n");
        int popped=m.Popped;
        m.Step(dt,voice?.Pending>0);SpeakMathPhase();
        if(m.Phase==MathPhase.Count&&(m.CountQuantity==0?mathCountSpoken!=-2:m.CountIndex!=mathCountSpoken)){
            mathCountSpoken=m.CountQuantity==0?-2:m.CountIndex;
            int n=m.CountQuantity==0?0:mathCountSpoken+1;
            if(m.Activity==MathActivity.Duel&&n>m.Round.A)n-=m.Round.A;
            voice?.Stop();voice?.Say(MathNumbers[Math.Clamp(n,0,10)],S);
        }
        if(m.Phase==MathPhase.Reward){
            if(m.Time>=.17&&m.Time-dt<.17)dotSounds.Play("cannon",S,.65f);
            var destinations=MathRewardPoints();
            for(int i=popped;i<m.Popped&&i<destinations.Count;i++){canvas.MathImpact(destinations[i]);dotSounds.Play("pop",S,.55f,.03);}
            if(m.RewardCount==0&&m.Time>=.48&&m.Time-dt<.48){canvas.MathImpact(new(720,454));dotSounds.Play("powerup",S,.45f);}
            canvas.Update(dt,W,H);
        }
        dotSounds.Update(S,0);
        if(mouse.LeftButton==ButtonState.Pressed&&previousMouse.LeftButton==ButtonState.Released)MathTap(new(mouse.X,mouse.Y));
        if(m.Revision!=mathSavedRevision){mathSavedRevision=m.Revision;if(!preview)store.Save();}
        if(preview){
            if(mathDebugState.Length>0&&!mathDebugApplied&&m.CanAnswer){m.DebugState(mathDebugState);mathDebugApplied=true;mathSpokenPhase=null;}
            if(scenario=="math-complete"&&m.CanAnswer&&mathPreviewActions==0){
                if(m.Activity==MathActivity.MakeNumber){for(int i=0;i<m.Difficulty.Capacity&&m.CanAnswer;i++)if((m.BuiltMask&(1<<i))==0)MathPreviewTap(MathLayout.CellButton(i,m.Difficulty.Capacity));}
                else MathPreviewAnswer(m.Round.Answer);
                mathPreviewActions++;
            }
        }
    }
    void MathPreviewTap(DotRect r){var p=r.Center*new System.Numerics.Vector2(GraphicsDevice.PresentationParameters.BackBufferWidth/1440f,GraphicsDevice.PresentationParameters.BackBufferHeight/900f);MathTap(V(p));}
    void MathPreviewAnswer(int n){var m=math!;MathPreviewTap(m.Activity==MathActivity.Duel?(n==2?MathLayout.Same:MathLayout.Group(n)):m.Activity==MathActivity.CannonHop?MathLayout.Track(n):MathLayout.Choice(Array.IndexOf(m.Round.Choices,n),m.Round.Choices.Length));}
    Color MathInk=>S.Theme==Mood.BlackAndWhite?Color.White:canvas.Palette[1];
    static Vector2 HiddenCell(int i)=>new(170+i%5*150,386+i/5*138);
    List<Vector2> MathRewardPoints(){
        var m=math!;var p=new List<Vector2>();
        for(int i=0;i<m.RewardCount;i++)p.Add(m.Activity switch{
            MathActivity.Hiding=>HiddenCell(i),MathActivity.Duel=>V(MathLayout.GroupCell(i<m.Round.A?0:1,i<m.Round.A?i:i-m.Round.A)),
            MathActivity.CannonHop=>new(390+i%5*165,278+i/5*98),_=>V(MathLayout.Cell(i,m.Difficulty.Capacity))});
        if(m.Activity==MathActivity.MakeNumber){p.Clear();for(int i=0;i<m.Difficulty.Capacity;i++)if((m.BuiltMask&(1<<i))!=0)p.Add(V(MathLayout.Cell(i,m.Difficulty.Capacity)));}
        return p;
    }
    List<MathToken> MathTokens(){
        var m=math!;var r=m.Round;var tokens=new List<MathToken>();
        if(m.Phase is MathPhase.Ready or MathPhase.Set or MathPhase.Go)return tokens;
        void Ball(Vector2 p,int i,float radius=48)=>tokens.Add(new(p,radius,MathInk,i));
        if(m.Phase==MathPhase.Reward){var points=MathRewardPoints();for(int i=m.Popped;i<points.Count;i++)Ball(points[i],i,m.Activity is MathActivity.Duel or MathActivity.CannonHop?35:48);return tokens;}
        if(m.Activity==MathActivity.Duel){
            for(int side=0;side<2;side++){
                bool numeral=!m.Replaying&&m.Difficulty.Level>=6&&(side==1||m.Difficulty.Level>=7);
                if(!numeral)for(int i=0;i<(side==0?r.A:r.B);i++)Ball(V(MathLayout.GroupCell(side,i)),i+(side==0?0:r.A),37);
            }return tokens;
        }
        if(m.Activity==MathActivity.MakeNumber){
            if(m.Counting){for(int i=0;i<r.B;i++)Ball(V(MathLayout.Cell(i,m.Difficulty.Capacity)),i);}
            else for(int i=0;i<m.Difficulty.Capacity;i++)if((m.BuiltMask&(1<<i))!=0)Ball(V(MathLayout.Cell(i,m.Difficulty.Capacity)),i);
            return tokens;
        }
        if(m.Activity==MathActivity.CannonHop){
            if(m.Counting){for(int i=0;i<r.Final;i++)Ball(new(390+i%5*165,278+i/5*98),i,35);return tokens;}
            float location=r.A,arc=0;
            bool transform=m.Phase is MathPhase.Transform or MathPhase.Confirm;
            if(transform){float hops=m.Motion*r.B;location=r.A+(r.Subtract?-1:1)*hops;arc=MathF.Sin((hops-MathF.Floor(hops))*MathF.PI)*90;}
            else if(!m.AskFirst&&m.Phase is MathPhase.Ask or MathPhase.AwaitAnswer or MathPhase.Count)location=r.Final;
            Ball(V(MathLayout.HopPoint(location))-new Vector2(0,arc),0,35);return tokens;
        }
        bool initial=m.Phase==MathPhase.Initial || m.AskFirst&&m.Phase is MathPhase.Ask or MathPhase.AwaitAnswer;
        bool moving=m.Phase is MathPhase.Transform or MathPhase.Confirm;
        if(m.Activity==MathActivity.Hiding){
            for(int i=0;i<r.A;i++){
                bool hiding=i>=r.A-r.B;
                if(hiding&&!initial&&!moving&&!m.Counting)continue;
                var p=HiddenCell(i);
                if(moving&&hiding)p=Vector2.Lerp(p,new(1100,440),MathHelper.SmoothStep(0,1,m.Motion));
                Ball(p,i,43);
            }return tokens;
        }
        if(m.Hidden)return tokens;
        int count=initial?r.A:moving?Math.Max(r.A,r.Final):r.Final;
        for(int i=0;i<count;i++){
            var p=V(MathLayout.Cell(i,m.Difficulty.Capacity));
            if(moving){float t=MathHelper.SmoothStep(0,1,m.Motion);
                if(!r.Subtract&&i>=r.A)p=Vector2.Lerp(new(-120-(i-r.A)*90,650),p,t);
                if(r.Subtract&&i>=r.Final)p+=new Vector2(t*330,t*t*620);
            }
            Ball(p,i);
        }return tokens;
    }
    void MathButton(DotRect r,string label,bool enabled,bool selected=false,float size=.72f){
        bool hover=enabled&&r.Contains(new(mathPointer.X,mathPointer.Y));
        DotRounded(r,selected?MathInk:Color.White*(enabled?.16f:.07f));
        if(selected)DotRounded(new(r.X+4,r.Y+4,r.Width-8,r.Height-8),new(28,37,53));
        if(hover){DotRounded(new(r.X-3,r.Y-3,r.Width+6,r.Height+6),MathInk);DotRounded(r,new(33,42,58));}
        DotText(label,V(r.Center),size,Color.White*(enabled||selected?1:.32f));
    }
    void MathFrame(){
        var m=math!;
        if(m.Activity is MathActivity.Duel or MathActivity.CannonHop or MathActivity.Hiding)return;
        for(int i=0;i<m.Difficulty.Capacity;i++){
            var r=MathLayout.CellButton(i,m.Difficulty.Capacity);bool hover=m.Activity==MathActivity.MakeNumber&&m.CanAnswer&&r.Contains(new(mathPointer.X,mathPointer.Y));
            DotRounded(r,hover?MathInk*.25f:Color.White*.07f);
            if(m.Activity==MathActivity.MakeNumber&&m.CanAnswer&&(m.BuiltMask&(1<<i))==0)DotText("+",V(r.Center),.6f,Color.White*.27f);
        }
    }
    Vector2 MathShake=>S.GentleMotion||math==null?Vector2.Zero:math.Phase switch {
        MathPhase.Incorrect=>new(MathF.Sin((float)math.Time*85)*3,0),
        MathPhase.Reward when math.Time>.45&&math.Time<.85=>new(MathF.Sin((float)math.Time*90)*4,MathF.Cos((float)math.Time*77)*2),_=>Vector2.Zero};
    void DrawMath(){
        EnsureMath();var m=math!;EnsureDotDisc();mathBalls??=new(GraphicsDevice,Content.Load<Effect>("Shaders/Toon"));
        Fill(new(0,0,W,H),S.Theme==Mood.BlackAndWhite?Color.Black:new Color(8,12,24));
        MathFrame();
        if(m.Activity==MathActivity.Duel)for(int side=0;side<2;side++){
            var r=MathLayout.Group(side);bool select=m.Phase==MathPhase.Reward&&(m.Round.Answer==side||m.Round.Answer==2);
            MathButton(r,"",m.CanAnswer,select); // Different positions and outlines work without color cues.
            if(m.Phase is not (MathPhase.Ready or MathPhase.Set or MathPhase.Go or MathPhase.Reward)&&!m.Replaying&&m.Difficulty.Level>=6&&(side==1||m.Difficulty.Level>=7))DotText((side==0?m.Round.A:m.Round.B).ToString(),V(r.Center),2.3f,Color.White);
        }
        if(m.Activity==MathActivity.CannonHop){
            for(int n=0;n<=10;n++)MathButton(MathLayout.Track(n),n.ToString(),m.CanAnswer,m.Phase==MathPhase.Reward&&n==m.Round.Final,.82f);
            DotLine(new(116,634),new(1316,634),3,Color.White*.13f);
            DotText(m.Round.Subtract?"<":" >",new(720,686),.8f,MathInk);
        }
        var tokens=MathTokens();
        foreach(var ball in tokens)DotCircle(ball.Position+new Vector2(6,ball.Radius*.2f),ball.Radius*1.03f,Color.Black*.4f);
        batch.End();mathBalls.Draw(tokens);RenderSpace.Begin(batch);
        if(m.Activity==MathActivity.Hiding && m.Phase is MathPhase.Transform or MathPhase.Ask or MathPhase.AwaitAnswer){
            var door=new DotRect(965,282,330,330);DotRounded(door,new(37,52,74));
            DotText("?",V(door.Center),2.1f,Color.White);DotCircle(new(1262,454),9,MathInk);
            DotLine(new(980,594),new(1280,594),4,Color.White*.16f);
        }
        if(m.Counting){
            var countTokens=tokens.Where(t=>m.Activity!=MathActivity.Hiding||t.Index>=m.Round.A-m.Round.B).ToArray();
            if(m.CountIndex>=0&&m.CountIndex<countTokens.Length){var t=countTokens[m.CountIndex];DotRing(t.Position,t.Radius+12,5,MathInk);DotText((m.Activity==MathActivity.Duel&&m.CountIndex>=m.Round.A?m.CountIndex-m.Round.A+1:m.CountIndex+1).ToString(),t.Position-new Vector2(0,85),.7f,Color.White);}
            if(m.CountQuantity==0)DotText("0",new(720,450),1.6f,Color.White);
        }
        if(m.Phase==MathPhase.Reward){
            var points=MathRewardPoints();float time=(float)m.Time;
            for(int i=0;i<points.Count;i++){
                float age=time-(float)MathGame.ImpactAt(i,points.Count),flight=age+.25f;
                if(flight>=0&&flight<.25f){var origin=new Vector2(720,676);var tip=Vector2.Lerp(origin,points[i],flight/.25f);DotLine(Vector2.Lerp(origin,points[i],Math.Max(0,flight/.25f-.28f)),tip,8,MathInk);DotCircle(tip,9,Color.White);}
                if(age>=0&&age<.4f)DotRing(points[i],38+age*135,5*(1-age/.4f),MathInk*(1-age/.4f));
            }
            canvas.DrawFeedbackParticles(batch);
            // Keep the equation in a quiet, opaque strip above the impacts.
            Fill(new(300,175,840,88),new(8,12,24));DotText(m.Activity==MathActivity.MakeNumber&&m.Round.A==0?m.Round.B.ToString():m.Round.Equation,new(720,217),1.12f,MathInk);
        }
        string cue=m.Phase switch{
            MathPhase.Ready=>"READY",MathPhase.Set=>"SET",MathPhase.Go=>"GO",
            MathPhase.Initial=>m.Activity==MathActivity.MakeNumber?MathQuestion.ToUpperInvariant():m.Activity==MathActivity.Duel?"":m.Round.A.ToString(),
            MathPhase.Transform=>m.Activity==MathActivity.Hiding?"":(m.Round.Subtract?"TAKE AWAY ":"")+m.Round.B+(m.Round.Subtract?"":" MORE"),
            MathPhase.Incorrect=>"",MathPhase.Count=>"LET'S COUNT",MathPhase.Reward=>"",_=>MathQuestion.ToUpperInvariant()};
        float cueSize=m.Activity==MathActivity.CannonHop?.55f:.85f;
        DotText(cue,new(720,151),cueSize,Color.White);
        if(m.Difficulty.Equation&&!m.Replaying&&m.Phase is not (MathPhase.Ready or MathPhase.Set or MathPhase.Go or MathPhase.Reward)){
            string equation=m.Activity switch {MathActivity.Hiding=>$"{m.Round.A-m.Round.B} + ? = {m.Round.A}",MathActivity.MakeNumber=>$"{m.Round.A} + ? = {m.Round.B}",MathActivity.Duel=>"",_=>$"{m.Round.A} {(m.Round.Subtract?"-":"+")} {m.Round.B} = ?"};
            DotText(equation,new(720,225),.84f,MathInk);
        }
        if(m.Activity==MathActivity.MakeNumber){
            if(m.Phase is MathPhase.Ask or MathPhase.AwaitAnswer){
            // A concrete reference as well as a numeral: early builders need no reading.
            DotText(m.Round.B.ToString(),new(720,710),1.05f,MathInk);
            for(int i=0;i<m.Round.B;i++)DotCircle(new(720-(m.Round.B-1)*21+i*42,804),13,MathInk);
            }
        }else if(m.Activity==MathActivity.Duel)MathButton(MathLayout.Same,"=   SAME",m.CanAnswer,false,.7f);
        else if(m.Activity!=MathActivity.CannonHop){
            for(int i=0;i<m.Round.Choices.Length;i++){
                int n=m.Round.Choices[i];var r=MathLayout.Choice(i,m.Round.Choices.Length);
                MathButton(r,m.Difficulty.VisualChoices?"":n.ToString(),m.CanAnswer,false,1.2f);
                if(m.Difficulty.VisualChoices)for(int k=0;k<n;k++)DotCircle(new(r.Center.X-(n-1)*29+k*58,r.Center.Y),20,MathInk*(m.CanAnswer?1:.28f));
            }
        }
        MathButton(MathLayout.Back,"<  GAMES",true,false,.48f);
        MathButton(MathLayout.Mute,S.Sound?"MUTE":"UNMUTE",true,false,.44f);
        DotText(m.Phase==MathPhase.Reward?(m.Stage-1).ToString():m.Stage.ToString(),new(1354,59),.7f,Color.White);
    }
}

