namespace KeyLearner.Studio;

/// <summary>One accepted touch edge per gesture, with mouse-emulation exclusion.</summary>
public sealed class PointerGestureSafety
{
    bool multipleTouches;
    double lastTouch=-10;
    public bool ObserveTouches(double now,int activeCount,bool hasTouches,bool began)
    {
        if(hasTouches)lastTouch=now;
        if(activeCount>1)multipleTouches=true;
        if(activeCount==0)multipleTouches=false;
        return !multipleTouches&&activeCount==1&&began;
    }
    public bool AllowsMouse(double now)=>now-lastTouch>.25;
    public void Reset(double now){multipleTouches=true;lastTouch=now;}
}
