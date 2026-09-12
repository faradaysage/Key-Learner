namespace KeyLearner.Studio;
/// <summary>Pressure builds on repeated taps, leaks slowly, and ends in a pop reward.</summary>
public sealed class BalloonMotion
{
    public const float InflationStep=.28f;
    public float Size {get;private set;}=1;
    public float Target {get;private set;}=1;
    public float Squeeze {get;private set;}
    public bool Popped {get;private set;}
    public bool Retired {get;private set;}
    private float velocity,impulseIn=-1,releasePressure;
    public void Inflate()
    {
        if(Popped || Retired)return;
        Target=Math.Min(6.3f,Target+InflationStep);impulseIn=.045f;velocity-=1.5f;
    }
    public void Release(){if(Retired)return;Retired=true;releasePressure=Math.Max(InflationStep,Target-1);impulseIn=-1;}
    public void Step(float dt,float deflateSeconds=3,float popSize=3)
    {
        if(Popped)return;
        Target=Math.Max(1,Target-(Retired?releasePressure:InflationStep)*dt/Math.Max(.3f,deflateSeconds));
        if(impulseIn>=0){impulseIn-=dt;if(impulseIn<0)velocity+=4.5f;}
        var destination=impulseIn>=0?Math.Max(.85f,Size-.08f):Target;
        velocity+=((destination-Size)*95-velocity*11)*dt;
        Size=Math.Clamp(Size+velocity*dt,.75f,6.5f);
        Squeeze=Math.Clamp(velocity*.035f,-.12f,.16f);
        if(!Retired && Size>=popSize)Popped=true;
    }
}
