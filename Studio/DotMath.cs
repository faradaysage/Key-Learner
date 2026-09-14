using System.Numerics;
namespace KeyLearner.Studio;

public enum MathActivity { HowManyNow, Hiding, MakeNumber, Duel, CannonHop }
public enum MathPhase { Ready, Set, Go, Initial, Transform, Observe, Ask, AwaitAnswer, Incorrect, Count, Confirm, Reward }
public readonly record struct MathDifficulty(int Level,int Limit,int Step,int Choices,bool VisualChoices,bool Hide,bool Equation,bool AskFirst)
{
    public int Capacity=>Limit<=5?5:10;
    public static MathDifficulty At(int level)=>Math.Clamp(level,1,8) switch {
        1=>new(1,3,1,2,true,false,false,false),
        2=>new(2,5,1,3,false,false,false,false),
        3=>new(3,5,2,3,false,false,false,false),
        4=>new(4,5,5,4,false,false,false,false),
        5=>new(5,10,10,4,false,false,false,false),
        6=>new(6,10,10,4,false,true,false,false),
        7=>new(7,10,10,4,false,true,true,false),
        _=>new(8,10,10,4,false,true,true,true)
    };
}
public sealed class MathSkillProgress
{
    public int Level {get;set;}=1;
    public int Stage {get;set;}=1;
    public List<bool> Recent {get;set;}=[];
    public void Normalize(){Level=Math.Clamp(Level,1,8);Stage=Math.Max(1,Stage);Recent=(Recent??[]).TakeLast(6).ToList();}
    public void Complete(bool firstTry){
        Normalize();Stage++;Recent.Add(firstTry);if(Recent.Count>6)Recent.RemoveAt(0);
        if(Recent.Count>=4 && Recent.TakeLast(4).All(x=>x)){Level=Math.Min(8,Level+1);Recent.Clear();}
        else if(Recent.Count>=3 && Recent.TakeLast(3).Count(x=>!x)>=2){Level=Math.Max(1,Level-1);Recent.Clear();}
    }
}
public sealed class MathProgress
{
    public MathActivity LastActivity {get;set;}
    public Dictionary<MathActivity,MathSkillProgress> Skills {get;set;}=[];
    public int JoiningCompleted {get;set;}
    public int SeparatingCompleted {get;set;}
    public bool HopUnlocked=>JoiningCompleted>=3 && SeparatingCompleted>=3;
    public MathSkillProgress For(MathActivity activity){Skills??=[];if(!Skills.TryGetValue(activity,out var p)||p==null)Skills[activity]=p=new();p.Normalize();return p;}
}
public sealed record MathRound(MathActivity Activity,int A,int B,bool Subtract,int Answer,int[] Choices,bool Fewer=false)
{
    public int Final=>Subtract?A-B:A+B;
    public int Total=>Activity switch {MathActivity.Hiding=>A,MathActivity.MakeNumber=>B,MathActivity.Duel=>A+B,_=>Final};
    public string Equation=>Activity switch {
        MathActivity.Hiding=>$"{A-B} + {B} = {A}",
        MathActivity.MakeNumber=>$"{A} + {B-A} = {B}",
        MathActivity.Duel=>$"{A} {(A==B?"=":A>B?">":"<")} {B}",
        _=>$"{A} {(Subtract?"-":"+")} {B} = {Final}"
    };
}
/// <summary>Small constrained decks: deterministic with a seed, alternating operations and fresh operands/results.</summary>
public sealed class MathRounds(int seed)
{
    readonly Random random=new(seed);
    MathRound? previous;
    public MathRound Next(MathActivity activity,MathDifficulty d,int? a=null,int? b=null,bool? subtract=null){
        var candidates=new List<MathRound>();
        for(int x=0;x<=d.Limit;x++)for(int y=0;y<=d.Limit;y++){
            switch(activity){
                case MathActivity.HowManyNow:case MathActivity.CannonHop:
                    if(y<1||y>d.Step)break;
                    foreach(bool minus in new[]{false,true}){
                        int result=minus?x-y:x+y;
                        if(result<0||result>d.Limit||d.Level==1&&(x==0||result==0))continue;
                        if(d.Level==5 && !(x==5||y==5||x==y||x==4&&y==2||minus&&x>=5))continue;
                        candidates.Add(new(activity,x,y,minus,result,[]));
                    }break;
                case MathActivity.Hiding:
                    if(x<1||y>x||d.Level==1&&(y==0||y==x))break;
                    candidates.Add(new(activity,x,y,false,y,[]));break;
                case MathActivity.MakeNumber:
                    if(y<1||x>=y||d.Level<=2&&x!=0||d.Level==3&&y-x>2)break;
                    candidates.Add(new(activity,x,y,false,y,[]));break;
                case MathActivity.Duel:
                    if(d.Level==1&&(x<1||y<1||Math.Abs(x-y)<2)||d.Level==2&&x==y||d.Level==3&&Math.Abs(x-y)!=1)break;
                    candidates.Add(new(activity,x,y,false,x==y?2:x>y?0:1,[]));break;
            }
        }
        if(a.HasValue||b.HasValue||subtract.HasValue){candidates=candidates.Where(r=>(!a.HasValue||r.A==a)&&(!b.HasValue||r.B==b)&&(!subtract.HasValue||r.Subtract==subtract)).ToList();}
        else if(previous is {} last && last.Activity==activity){
            var alternated=candidates.Where(r=>activity is not (MathActivity.HowManyNow or MathActivity.CannonHop)||r.Subtract!=last.Subtract).ToList();if(alternated.Count>0)candidates=alternated;
            var fresh=candidates.Where(r=>(r.A!=last.A||r.B!=last.B)&&r.Answer!=last.Answer).ToList();if(fresh.Count>0)candidates=fresh;
            // Zero remains a regular outcome, but never dominates the deck.
            if(last.Answer==0){var nonzero=candidates.Where(r=>r.Answer!=0).ToList();if(nonzero.Count>0)candidates=nonzero;}
        }
        if(candidates.Count==0)throw new ArgumentException("Operands are not valid for this activity and difficulty.");
        var round=candidates[random.Next(candidates.Count)];
        if(activity==MathActivity.Duel){bool fewer=random.Next(2)==0;round=round with{Fewer=fewer,Answer=round.A==round.B?2:(fewer?round.A<round.B:round.A>round.B)?0:1,Choices=[0,1,2]};}
        else{
            var options=Enumerable.Range(d.Level==1?1:0,d.Limit+(d.Level==1?0:1)).Where(n=>n!=round.Answer).OrderBy(_=>random.Next()).Take(d.Choices-1).Append(round.Answer).OrderBy(n=>n).ToArray();
            round=round with{Choices=options};
        }
        previous=round;return round;
    }
}
public sealed class MathGame
{
    readonly MathRounds generator;
    readonly MathProgress progress;
    readonly MathSkillProgress skill;
    readonly int? forcedLevel,forcedA,forcedB;readonly bool? forcedSubtract;
    public MathActivity Activity {get;}
    public MathDifficulty Difficulty {get;private set;}
    public MathRound Round {get;private set;}=null!;
    public MathPhase Phase {get;private set;}
    public double Time {get;private set;}
    public int Errors {get;private set;}
    public int BuiltMask {get;private set;}
    public int BuiltCount=>BitOperations.PopCount((uint)BuiltMask);
    public int Stage=>skill.Stage;
    public int Revision {get;private set;}
    public bool Replaying {get;private set;}
    public bool CanAnswer=>Phase==MathPhase.AwaitAnswer;
    public bool AskFirst=>!Replaying && (Activity==MathActivity.HowManyNow&&Difficulty.AskFirst||Activity==MathActivity.CannonHop&&Difficulty.Level>=7);
    public bool Hidden=>Difficulty.Hide&&!Replaying&&Phase is MathPhase.Ask or MathPhase.AwaitAnswer&&Activity is MathActivity.HowManyNow;
    public bool Counting=>Phase==MathPhase.Count;
    public int CountIndex=>Math.Min(CountQuantity-1,(int)(Time/.55));
    public int CountQuantity=>Activity==MathActivity.MakeNumber?Round.B:Activity==MathActivity.Hiding?Round.B:Activity==MathActivity.Duel?Round.A+Round.B:Round.Final;
    public int RewardCount=>Activity==MathActivity.Duel?Round.A+Round.B:Activity==MathActivity.MakeNumber?Round.B:Activity==MathActivity.Hiding?Round.A:Round.Final;
    public double TransformSeconds=>Activity==MathActivity.CannonHop?Math.Max(.9,Round.B*.3)*(Replaying?1.65:1):Replaying?2.0:1.05;
    public float Motion=>Math.Clamp((float)(Time/TransformSeconds),0,1);
    public const double RewardSeconds=1.6;
    public static double ImpactAt(int index,int count)=>.48+index*.52/Math.Max(1,count-1);
    public int Popped=>Phase==MathPhase.Reward?Enumerable.Range(0,RewardCount).Count(i=>Time>=ImpactAt(i,RewardCount)):0;
    public MathGame(MathActivity activity,MathProgress progress,int seed=1,int? level=null,int? a=null,int? b=null,bool? subtract=null){
        Activity=activity;this.progress=progress;skill=progress.For(activity);generator=new(seed);forcedLevel=level;forcedA=a;forcedB=b;forcedSubtract=subtract;Next();
    }
    void Next(){Difficulty=MathDifficulty.At(forcedLevel??skill.Level);Round=generator.Next(Activity,Difficulty,forcedA,forcedB,forcedSubtract);Errors=0;Replaying=false;BuiltMask=(1<<Round.A)-1;Set(MathPhase.Ready);Revision++;}
    void Set(MathPhase phase){Phase=phase;Time=0;}
    public void Restart(){
        if(Phase==MathPhase.Reward){Next();return;}
        BuiltMask=(1<<Round.A)-1;Replaying=Errors>0;Set(MathPhase.Ready);
    }
    public bool Answer(int value){
        if(!CanAnswer||Activity==MathActivity.MakeNumber)return false;
        if(value!=Round.Answer){Errors++;Replaying=true;Set(MathPhase.Incorrect);return false;}
        if(AskFirst||Activity==MathActivity.CannonHop){Set(MathPhase.Confirm);return true;}
        Win();return true;
    }
    public bool Cell(int index){
        if(!CanAnswer||Activity!=MathActivity.MakeNumber||index<0||index>=Difficulty.Capacity)return false;
        BuiltMask^=1<<index;
        if(BuiltCount==Round.B)Win();return true;
    }
    void Win(){
        skill.Complete(Errors==0); // Stage is earned immediately; navigation cannot lose or duplicate it.
        if(Activity==MathActivity.HowManyNow){if(Round.Subtract)progress.SeparatingCompleted++;else progress.JoiningCompleted++;}
        Set(MathPhase.Reward);Revision++;
    }
    public void Step(double dt,bool narrationBusy=false){
        if(!double.IsFinite(dt)||dt<=0)return;Time+=Math.Min(dt,.1);
        double duration=Phase switch {
            MathPhase.Ready or MathPhase.Set=>.3,MathPhase.Go=>.22,
            MathPhase.Initial=>Replaying?1.3:.95,
            MathPhase.Transform or MathPhase.Confirm=>TransformSeconds,
            MathPhase.Observe=>.85,MathPhase.Ask=>.45,MathPhase.Incorrect=>.24,
            MathPhase.Count=>Math.Max(.8,CountQuantity*.55+.35),MathPhase.Reward=>RewardSeconds,
            _=>double.PositiveInfinity};
        if(Time<duration || narrationBusy&&Time<duration+3&&Phase is MathPhase.Ready or MathPhase.Set or MathPhase.Go or MathPhase.Initial or MathPhase.Transform or MathPhase.Ask)return;
        switch(Phase){
            case MathPhase.Ready:Set(MathPhase.Set);break;
            case MathPhase.Set:Set(MathPhase.Go);break;
            case MathPhase.Go:Set(AskFirst?MathPhase.Ask:MathPhase.Initial);break;
            case MathPhase.Initial:Set(Activity is MathActivity.MakeNumber or MathActivity.Duel?(Errors>=2?MathPhase.Count:MathPhase.Ask):MathPhase.Transform);break;
            case MathPhase.Transform:Set(Errors>=2?MathPhase.Count:Difficulty.Hide&&Activity==MathActivity.HowManyNow&&!Replaying?MathPhase.Observe:MathPhase.Ask);break;
            case MathPhase.Observe:Set(MathPhase.Ask);break;
            case MathPhase.Ask:Set(MathPhase.AwaitAnswer);break;
            case MathPhase.Incorrect:Set(MathPhase.Initial);break;
            case MathPhase.Count:Set(MathPhase.Ask);break;
            case MathPhase.Confirm:Win();break;
            case MathPhase.Reward:Next();break;
        }
    }
    // Preview-only caller exposes reproducible visual states; no shipping menu or progress writes.
    public void DebugState(string state){
        if(state=="correct"){Set(MathPhase.AwaitAnswer);if(Activity==MathActivity.MakeNumber){BuiltMask=(1<<Round.B)-1;Win();}else Answer(Round.Answer);}
        else if(state=="incorrect"||state=="count"){Errors=state=="count"?2:1;Replaying=true;Set(state=="count"?MathPhase.Count:MathPhase.Incorrect);}
        else if(Enum.TryParse<MathPhase>(state,true,out var phase))Set(phase);
    }
}
public static class MathLayout
{
    public static DotRect Back=>new(36,26,150,68);
    public static DotRect Mute=>new(208,26,150,68);
    public static DotRect Frame=>new(260,300,920,300);
    public static Vector2 Cell(int i,int capacity)=>new(360+i%5*180,capacity==5?452:376+i/5*150);
    public static DotRect CellButton(int i,int capacity){var p=Cell(i,capacity);return new(p.X-80,p.Y-65,160,130);}
    public static DotRect Choice(int i,int count)=>new(720-count*142+i*284+12,708,260,142);
    public static DotRect Group(int side)=>new(120+side*640,285,560,345);
    public static Vector2 GroupCell(int side,int index)=>new(180+side*640+index%5*110,383+index/5*132);
    public static DotRect Same=>new(550,713,340,130);
    public static DotRect Track(int number)=>new(60+number*120,459,112,154);
    public static Vector2 HopPoint(float number)=>new(116+number*120,424);
    public static Vector2 Unproject(Vector2 screen,float width,float height)=>screen*new Vector2(1440/width,900/height);
}
