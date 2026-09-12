namespace KeyLearner.Studio;
/// <summary>Ten complete Escape taps; repeats do not count and any other press resets it.</summary>
public sealed class EscapeExit
{
    private bool down;
    public int Count {get;private set;}
    public bool Feed(KeyEvent e)
    {
        if(e.Key!=27){if(e.Down){Count=0;down=false;}return false;}
        if(e.Down){down=true;return false;}
        if(!down)return false;
        down=false;Count++;return Count>=10;
    }
    public void Reset(){Count=0;down=false;}
}
/// <summary>Deduplicates key repeats and rebuilds state instead of dropping releases on overflow.</summary>
public sealed class KeyTransitionBuffer
{
    private readonly object gate=new();
    private readonly bool[] held=new bool[256];
    private readonly Queue<KeyEvent> queue=new();
    private bool rebuild;
    public int Recoveries {get;private set;}
    public int Pending {get{lock(gate)return queue.Count;}}
    public void Push(KeyEvent e)
    {
        if(e.Key<0 || e.Key>=256)return;
        lock(gate)
        {
            if(held[e.Key]==e.Down)return;
            held[e.Key]=e.Down;
            if(queue.Count>=512){queue.Clear();rebuild=true;Recoveries++;}
            if(!rebuild)queue.Enqueue(e);
        }
    }
    public bool TryRead(out KeyEvent e)
    {
        lock(gate)
        {
            if(rebuild)
            {
                rebuild=false;queue.Clear();
                for(int i=0;i<held.Length;i++)if(held[i])queue.Enqueue(new(i,true,0));
                e=new(-1,false,0);return true;
            }
            return queue.TryDequeue(out e);
        }
    }
}
