using System.Numerics;
namespace KeyLearner.Studio;

public enum DotPhase { Ready, Set, Go, Reveal, Answer, Reward }
public readonly record struct DotDifficulty(int Minimum,int Maximum,bool Structured,double IrregularChance,double DisplaySeconds)
{
    public static DotDifficulty At(int mastery)=>mastery switch {
        <12=>new(0,3,true,0,double.PositiveInfinity),
        <28=>new(0,4,false,1,double.PositiveInfinity),
        <44=>new(5,6,true,0,double.PositiveInfinity),
        <60=>new(7,9,true,0,double.PositiveInfinity),
        <80=>new(5,9,false,.15+(mastery-60)*.85/19,double.PositiveInfinity),
        _=>new(5,9,false,1,Math.Max(.4,4-(mastery-80)*.045))
    };
}

/// <summary>All 512 occupancy masks; quantity balance, unused masks, then new symmetry families.</summary>
public sealed class DotPatterns(int seed)
{
    readonly Random random=new(seed);
    readonly int[] visits=new int[512],familyVisits=new int[512],quantityVisits=new int[10];
    static readonly HashSet<int> structured=BuildStructured();
    public static int Count(int mask)=>BitOperations.PopCount((uint)(mask&511));
    public static IEnumerable<int> Cells(int mask)=>Enumerable.Range(0,9).Where(i=>(mask&(1<<i))!=0);
    public static int Transform(int mask,int rotation,bool mirror){
        int result=0;foreach(int cell in Cells(mask)){int x=cell%3,y=cell/3;if(mirror)x=2-x;
            for(int i=0;i<rotation;i++)(x,y)=(2-y,x);result|=1<<(y*3+x);}
        return result;
    }
    public static int Family(int mask)=>Enumerable.Range(0,8).Min(t=>Transform(mask,t%4,t>=4));
    static HashSet<int> BuildStructured(){
        int[] seeds=[0,16,1,2,3,5,257,7,273,11,27,325,341,186,455,219,254,381,495,510,511];
        return seeds.SelectMany(m=>Enumerable.Range(0,8).Select(t=>Transform(m,t%4,t>=4))).ToHashSet();
    }
    public static bool IsStructured(int mask)=>structured.Contains(mask);
    public int Next(DotDifficulty level,int previous){
        bool grouped=level.Structured || random.NextDouble()>level.IrregularChance;
        var legal=Enumerable.Range(0,512).Where(m=>m!=previous && Count(m)>=level.Minimum && Count(m)<=level.Maximum && (!grouped || IsStructured(m))).ToArray();
        if(legal.Length==0)throw new InvalidOperationException("No non-repeating dot pattern in this tier.");
        var counts=legal.Select(Count).Distinct().ToArray();int fewest=counts.Min(n=>quantityVisits[n]);
        var balanced=counts.Where(n=>quantityVisits[n]==fewest).ToArray();int quantity=balanced[random.Next(balanced.Length)];
        var options=legal.Where(m=>Count(m)==quantity).ToArray();int seen=options.Min(m=>visits[m]);options=options.Where(m=>visits[m]==seen).ToArray();
        int families=options.Min(m=>familyVisits[Family(m)]);options=options.Where(m=>familyVisits[Family(m)]==families).ToArray();
        int difference=options.Max(m=>Count(m^Math.Max(0,previous)));options=options.Where(m=>Count(m^Math.Max(0,previous))==difference).ToArray();
        int chosen=options[random.Next(options.Length)];visits[chosen]++;familyVisits[Family(chosen)]++;quantityVisits[quantity]++;return chosen;
    }
}

public sealed class SubitizingGame
{
    readonly DotPatterns patterns;
    int previous=-1;bool retried;
    double elapsed,shownFor,retryLock;
    public int Stage {get;private set;}=1;
    public int Mastery {get;private set;}
    public int Mask {get;private set;}
    public int Quantity=>DotPatterns.Count(Mask);
    public int WrongAttempts {get;private set;}
    public DotDifficulty Difficulty {get;private set;}
    public DotPhase Phase {get;private set;}=DotPhase.Ready;
    public double PhaseTime=>elapsed;
    public double WrongShake=>retryLock/.24;
    public bool CanAnswer=>Phase==DotPhase.Answer && retryLock<=0;
    public bool DotsVisible=>Phase==DotPhase.Reveal || Phase==DotPhase.Answer && (retried || shownFor<Difficulty.DisplaySeconds);
    public const double RewardSeconds=1.12;
    public static double LaunchAt(int index)=>.04+index*.065;
    public static double ImpactAt(int index)=>LaunchAt(index)+.24;
    public int Popped=>Phase==DotPhase.Reward?Math.Min(Quantity,Enumerable.Range(0,Quantity).Count(i=>elapsed>=ImpactAt(i))):0;
    public SubitizingGame(int seed=1,int mastery=0){patterns=new(seed);Mastery=Math.Max(0,mastery);NextPattern();}
    void NextPattern(){Difficulty=DotDifficulty.At(Mastery);Mask=patterns.Next(Difficulty,previous);previous=Mask;retried=false;WrongAttempts=0;RestartRound();}
    // Focus/visibility changes replay this pattern from READY; no hidden timer or old volley survives.
    public void RestartRound(){if(Phase==DotPhase.Reward){Phase=DotPhase.Ready;Stage++;NextPattern();return;}Phase=DotPhase.Ready;elapsed=shownFor=retryLock=0;}
    public bool Answer(int number){
        if(!CanAnswer || number<0 || number>9)return false;
        if(number!=Quantity){WrongAttempts++;retried=true;retryLock=.24;return false;}
        if(!retried)Mastery++;Phase=DotPhase.Reward;elapsed=0;return true;
    }
    public void Step(double dt){
        if(!double.IsFinite(dt)||dt<=0)return;dt=Math.Min(dt,.25);retryLock=Math.Max(0,retryLock-dt);
        if(Phase is DotPhase.Reveal or DotPhase.Answer)shownFor+=dt;
        elapsed+=dt;
        double duration=Phase switch {DotPhase.Ready or DotPhase.Set=>.42,DotPhase.Go=>.24,DotPhase.Reveal=>.18,DotPhase.Reward=>RewardSeconds,_=>double.PositiveInfinity};
        if(elapsed<duration)return;
        elapsed=0;
        switch(Phase){
            case DotPhase.Ready:Phase=DotPhase.Set;break;
            case DotPhase.Set:Phase=DotPhase.Go;break;
            case DotPhase.Go:Phase=DotPhase.Reveal;shownFor=0;break;
            case DotPhase.Reveal:Phase=DotPhase.Answer;break;
            case DotPhase.Reward:Phase=DotPhase.Ready;Stage++;NextPattern();break;
        }
    }
}

public readonly record struct DotRect(float X,float Y,float Width,float Height)
{
    public bool Contains(Vector2 p)=>p.X>=X&&p.X<X+Width&&p.Y>=Y&&p.Y<Y+Height;
    public Vector2 Center=>new(X+Width/2,Y+Height/2);
}
public static class DotLayout
{
    public const float Width=720,Height=1080,Radius=55;
    public static Vector2 Cell(int i)=>new(184+(i%3)*176,320+(i/3)*176);
    public static DotRect Number(int n)=>new(48+n%5*128,816+n/5*120,112,104);
    public static DotRect Mute=>new(36,32,112,62);
    public static (float Scale,Vector2 Offset) Fit(float width,float height){float s=Math.Min(width/Width,height/Height);return(s,new((width-Width*s)/2,(height-Height*s)/2));}
    public static (Vector2 Scale,Vector2 Offset) RenderFit(float width,float height,float renderWidth,float renderHeight){var(s,o)=Fit(width,height);var ratio=new Vector2(renderWidth/width,renderHeight/height);return(new Vector2(s)*ratio,o*ratio);}
    public static Vector2 Unproject(Vector2 point,float width,float height){var (s,o)=Fit(width,height);return(point-o)/s;}
    public static int HitNumber(Vector2 point)=>Enumerable.Range(0,10).FirstOrDefault(n=>Number(n).Contains(point),-1);
}
