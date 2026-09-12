namespace KeyLearner.Studio;
/// <summary>Matches releases to the physical key that went down, before virtual-key translation.
/// Windows can translate one scan code to different VK labels as Num Lock/Shift changes.</summary>
public sealed class PhysicalKeyboard
{
    readonly Dictionary<int,(int Key,bool Seeded)> pressed=new();
    readonly KeyTransitionBuffer output;
    public int Held=>pressed.Count;
    public int RemappedReleases{get;private set;}
    public int RepairedReleases{get;private set;}
    public int IgnoredPackets{get;private set;}
    public PhysicalKeyboard(KeyTransitionBuffer output)=>this.output=output;
    static int Id(int key,int scan,bool extended)=>scan==0?0x10000+key:(scan&0xff)|(extended?0x100:0);
    static int Normalize(int key,int scan,bool extended)=>key switch {16=> (scan&0xff)==0x36?161:160,17=>extended?163:162,18=>extended?165:164,_=>key};
    public void Clear(){pressed.Clear();output.Clear();}
    public void Seed(int key,int scan,bool extended)
    {
        if(key is 3 or 19 || key>=255)return;
        key=Normalize(key,scan,extended);pressed[Id(key,scan,extended)]=(key,true);output.Push(new(key,true,0));
    }
    public void Feed(int key,int scan,bool extended,bool down,bool injected,double time)
    {
        // Keyboard overrun, unmapped packets, and synthetic extended Shift are not held keys.
        if(key>=255 || scan==0xff || key<=0 || (key is 16 or 160 or 161 && extended)){IgnoredPackets++;return;}
        key=Normalize(key,scan,extended);int id=Id(key,scan,extended);
        if(down)
        {
            if(injected)return;
            // Pause/Break may have no break packet. Treat them as completed taps.
            if(key is 3 or 19){output.Push(new(key,true,time));output.Push(new(key,false,time));return;}
            if(pressed.ContainsKey(id))return;
            bool already=pressed.Values.Any(p=>p.Key==key);pressed[id]=(key,false);if(!already)output.Push(new(key,true,time));
        }
        else
        {
            if(!pressed.TryGetValue(id,out var prior))
            {
                var seeded=pressed.FirstOrDefault(p=>p.Value.Seeded&&p.Value.Key==key);
                if(seeded.Value.Seeded){id=seeded.Key;prior=seeded.Value;}else return;
            }
            pressed.Remove(id);if(prior.Key!=key)RemappedReleases++;
            if(!pressed.Values.Any(p=>p.Key==prior.Key))output.Push(new(prior.Key,false,time));
            if(injected){RepairedReleases++;output.Rebuild();} // A repair must never complete a parent chord.
        }
    }
}
