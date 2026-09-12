namespace KeyLearner.Studio;
/// <summary>One immutable 256-key snapshot. Down bits only, never lock/toggle bits.</summary>
public readonly record struct KeySnapshot(ulong A,ulong B,ulong C,ulong D)
{
    public bool IsDown(int key)=>key is >=0 and <256 && (((key/64) switch{0=>A,1=>B,2=>C,_=>D})&(1UL<<(key%64)))!=0;
    public int Count=>System.Numerics.BitOperations.PopCount(A)+System.Numerics.BitOperations.PopCount(B)+System.Numerics.BitOperations.PopCount(C)+System.Numerics.BitOperations.PopCount(D);
    public IEnumerable<int> Keys{get{for(int i=0;i<256;i++)if(IsDown(i))yield return i;}}
    public static KeySnapshot From(IEnumerable<int> keys){ulong a=0,b=0,c=0,d=0;foreach(int k in keys){if(k<0||k>=256)continue;ulong bit=1UL<<(k%64);switch(k/64){case 0:a|=bit;break;case 1:b|=bit;break;case 2:c|=bit;break;case 3:d|=bit;break;}}return new(a,b,c,d);}
    public static bool WindowsDown(short state)=>(state&0x8000)!=0;
}
/// <summary>Evaluated every frame, before gameplay. No gesture history or release-order dependency.</summary>
public sealed class ParentHold
{
    public const double Seconds=2;
    ParentAction candidate;double since,lastSample;bool latched;
    public ParentAction Update(KeySnapshot keys,double now)
    {
        if(keys.Count==0){Reset();return ParentAction.None;}
        if(latched)return ParentAction.None;
        var action=Match(keys);
        // A stalled/background loop never counts as two seconds of observed hold.
        if(action!=candidate || now-lastSample>.25){candidate=action;since=now;}
        lastSample=now;
        if(action==ParentAction.None || now-since<Seconds)return ParentAction.None;
        latched=true;return action;
    }
    static ParentAction Match(KeySnapshot keys){
        bool ctrl=keys.IsDown(162)^keys.IsDown(163),alt=keys.IsDown(164)^keys.IsDown(165),shift=keys.IsDown(160)^keys.IsDown(161);
        return keys.Count!=3||!ctrl?ParentAction.None:keys.IsDown(79)&&(alt^shift)?ParentAction.Options:keys.IsDown(27)&&alt?ParentAction.Exit:ParentAction.None;
    }
    // Extra presses between two rendered frames also restart the continuous-hold timer.
    public void Observe(KeySnapshot keys){if(keys.Count==0){Reset();return;}if(!latched && Match(keys)!=candidate){candidate=ParentAction.None;since=0;}}
    public void Reset(){candidate=ParentAction.None;since=lastSample=0;latched=false;}
}
