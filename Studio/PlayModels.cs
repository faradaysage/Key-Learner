namespace KeyLearner.Studio;
/// <summary>Each count has its own five-second show, independent of frame rate.</summary>
public sealed class FireworkSchedule
{
    readonly List<double> launches=new();
    public int Pending=>launches.Count;
    public void Add(int number,double now){for(int i=0;i<Math.Clamp(number,1,100);i++)launches.Add(now+(number<=1?0:4.0*i/(number-1)));}
    public int Due(double now){var count=launches.RemoveAll(t=>t<=now);return count;}
    public void Clear()=>launches.Clear();
}
public sealed class BalloonReward
{
    public int Remaining{get;private set;}
    public int Score{get;private set;}
    int total;
    public void Start(int difficulty){total=Remaining=Math.Clamp(difficulty,2,16);}
    public bool Pop(){if(Remaining==0)return false;Score+=10;Remaining--;if(Remaining!=0)return false;Score+=total*5;return true;}
    public void Clear()=>Remaining=0;
}
public sealed class GlassDamage
{
    public float Amount{get;private set;}
    float quiet,cooldown;
    public bool Hit(float energy){quiet=0;if(cooldown>0)return false;Amount+=.045f+.045f*Math.Clamp(energy,0,1);if(Amount<1)return false;Amount=0;cooldown=1.2f;return true;}
    public void Step(float dt){quiet+=dt;cooldown=Math.Max(0,cooldown-dt);if(quiet>.35f)Amount=Math.Max(0,Amount-dt*.3f);}
    public void Clear(){Amount=0;quiet=cooldown=0;}
}
