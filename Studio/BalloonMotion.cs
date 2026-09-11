namespace KeyLearner.Studio;
/// <summary>A short squeeze precedes each pressure impulse; a damped spring settles the balloon.</summary>
public sealed class BalloonMotion
{
    public float Size {get;private set;}=1;
    public float Target {get;private set;}=1;
    public float Squeeze {get;private set;}
    private float velocity,impulseIn=-1;
    public void Inflate(){Target=Math.Min(3.8f,Target+.28f);impulseIn=.045f;velocity-=1.5f;}
    public void Step(float dt)
    {
        if(impulseIn>=0){impulseIn-=dt;if(impulseIn<0)velocity+=4.5f;}
        var destination=impulseIn>=0?Math.Max(.85f,Size-.08f):Target;
        velocity+=((destination-Size)*95-velocity*11)*dt;
        Size=Math.Clamp(Size+velocity*dt,.75f,4.2f);
        Squeeze=Math.Clamp(velocity*.035f,-.12f,.16f);
    }
}
