namespace KeyLearner.Studio;

public readonly record struct KeyEvent(int Key, bool Down, double Time);
public enum ParentAction { None, Exit, Options }

/// <summary>A chord must start clean, contain exactly three physical keys, be held,
/// and be released completely. Any additional key poisons the entire attempt.</summary>
public sealed class ParentChord
{
    private readonly HashSet<int> held = new();
    private bool poisoned;
    private int target;
    private double exactSince;
    private bool armed;
    public const double HoldSeconds = .7;
    public static bool IsControl(int key) => key is 162 or 163;
    public static bool IsAlt(int key) => key is 164 or 165;
    public static bool IsModifier(int key) => key is >= 160 and <= 165 or 91 or 92;
    public ParentAction Feed(KeyEvent e)
    {
        if (e.Down)
        {
            if (held.Contains(e.Key)) return ParentAction.None;
            if (target != 0 && held.Count < 3) poisoned=true;
            held.Add(e.Key);
            if (held.Count == 1) { poisoned=false; target=0; armed=false; }
            if (!(IsControl(e.Key) || IsAlt(e.Key) || e.Key is 27 or 79)) poisoned=true;
            if (held.Count(IsControl) > 1 || held.Count(IsAlt) > 1 || (held.Contains(27) && held.Contains(79))) poisoned=true;
            if (!poisoned && held.Count == 3 && held.Any(IsControl) && held.Any(IsAlt))
            { target=held.Contains(27) ? 27 : 79; exactSince=e.Time; }
        }
        else
        {
            if (!held.Contains(e.Key)) return ParentAction.None;
            if (held.Count == 3 && target != 0 && !poisoned && e.Time-exactSince >= HoldSeconds) armed=true;
            held.Remove(e.Key);
            if (held.Count == 0)
            {
                var action = armed && !poisoned ? (target == 27 ? ParentAction.Exit : ParentAction.Options) : ParentAction.None;
                target=0; armed=false; poisoned=false;
                return action;
            }
        }
        return ParentAction.None;
    }
    public void Reset() { held.Clear(); poisoned=false; armed=false; target=0; }
}

public enum Gesture { Deliberate, Rapid, Cluster, BroadMash, Sweep }
public readonly record struct InputContext(Gesture Gesture, double Confidence, double X, double Y, double Dx, double Dy, double Energy, int Held, double[] Features)
{
    public bool Intentional => Gesture == Gesture.Deliberate && Confidence >= .55;
}
public static class KeyboardMap
{
    private static readonly string[] Rows = ["1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM"];
    public static (double X,double Y) Position(int key)
    {
        var c = (char)key;
        for (var r=0;r<Rows.Length;r++) { var col=Rows[r].IndexOf(c); if (col>=0) return ((col+r*.35)/10.5,r/3.0); }
        if (key is >= 96 and <= 105) return (.85,(105-key)/12.0);
        return key switch { 32 => (.5,1), 37 => (0,.7),38 => (.5,0),39 => (1,.7),40 => (.5,1), _ => (.5,.5) };
    }
    public static char? Character(int key) => key is >=65 and <=90 ? char.ToLowerInvariant((char)key) : key is >=48 and <=57 ? (char)key : key is >=96 and <=105 ? (char)('0'+key-96) : null;
}
public sealed class GestureAnalyzer
{
    private readonly Queue<(int Key,double Time,int Held)> history = new();
    public InputContext Current { get; private set; } = new(Gesture.Deliberate,.6,.5,.5,0,0,0,0,new double[6]);
    public void Reset() { history.Clear(); Current = new(Gesture.Deliberate,.6,.5,.5,0,0,0,0,new double[6]); }
    public InputContext Add(int key,double time,int held,Profile profile,bool adaptive)
    {
        while (history.Count>0 && time-history.Peek().Time>1.4) history.Dequeue();
        history.Enqueue((key,time,held));
        while (history.Count>40) history.Dequeue();
        var samples=history.ToArray();
        var positions=samples.Select(s=>KeyboardMap.Position(s.Key)).ToArray();
        var p=KeyboardMap.Position(key);
        var elapsed=samples.Length>1 ? time-samples[0].Time : 1;
        var rate=(samples.Length-1)/Math.Max(.1,elapsed);
        var spread=positions.Max(p=>p.X)-positions.Min(p=>p.X);
        var dx=p.X-positions[0].X; var dy=p.Y-positions[0].Y;
        double path=0;
        for(var i=1;i<positions.Length;i++) path+=Math.Sqrt(Math.Pow(positions[i].X-positions[i-1].X,2)+Math.Pow(positions[i].Y-positions[i-1].Y,2));
        var straight=path>.01 ? Math.Sqrt(dx*dx+dy*dy)/path : 0;
        var peak=samples.Max(s=>s.Held);
        var features=new[]{Math.Min(rate/16,1),Math.Min(peak/8.0,1),spread,straight,Math.Abs(dx),Math.Abs(dy)};
        var gesture=peak>=5 && spread>.45 ? Gesture.BroadMash :
            peak>=3 ? Gesture.Cluster :
            samples.Length>=4 && straight>.82 && path>.25 && elapsed<1 ? Gesture.Sweep :
            rate>6 ? Gesture.Rapid : Gesture.Deliberate;
        var confidence=samples.Length<3 ? .6 : .8;
        if(adaptive && profile.Network.Samples>=40)
        {
            var prediction=profile.Network.Predict(features);
            // Physical overlap always overrides the learned deliberate label.
            if(prediction.Confidence>.7 && !(prediction.Label==0 && (peak>2 || rate>6)))
            { gesture=(Gesture)prediction.Label; confidence=prediction.Confidence; }
        }
        Current=new(gesture,confidence,p.X,p.Y,dx,dy,Math.Clamp(rate/12+peak*.08,.15,1),held,features);
        return Current;
    }
}

/// <summary>6 inputs, 8 tanh units, 5 softmax outputs. Only parent-labeled samples train it.</summary>
public sealed class TinyNetwork
{
    public double[] Hidden { get; set; } = Seed(56,17);
    public double[] Output { get; set; } = Seed(45,31);
    public int Samples { get; set; }
    private static double[] Seed(int length,int seed) { var random=new Random(seed); return Enumerable.Range(0,length).Select(_=>(random.NextDouble()-.5)*.4).ToArray(); }
    public void Validate()
    {
        if(Hidden is not {Length:56} || Output is not {Length:45} || Hidden.Concat(Output).Any(n=>!double.IsFinite(n)))
        { Hidden=Seed(56,17); Output=Seed(45,31); Samples=0; }
    }
    private (double[] H,double[] P) Forward(double[] x)
    {
        var h=new double[8]; var p=new double[5];
        for(var j=0;j<8;j++) { double sum=Hidden[j*7+6]; for(var i=0;i<6;i++) sum+=Hidden[j*7+i]*x[i]; h[j]=Math.Tanh(sum); }
        for(var k=0;k<5;k++) { p[k]=Output[k*9+8]; for(var j=0;j<8;j++) p[k]+=Output[k*9+j]*h[j]; }
        var max=p.Max(); for(var k=0;k<5;k++) p[k]=Math.Exp(p[k]-max);
        var total=p.Sum(); for(var k=0;k<5;k++) p[k]/=total;
        return (h,p);
    }
    public (int Label,double Confidence) Predict(double[] x) { var p=Forward(x).P; var label=Array.IndexOf(p,p.Max()); return(label,p[label]); }
    public void Train(double[] x,int label)
    {
        if(x.Length!=6 || label<0 || label>=5) return;
        var (h,p)=Forward(x); var errors=p.Select((v,k)=>v-(k==label?1:0)).ToArray();
        var hiddenErrors=new double[8];
        for(var j=0;j<8;j++) { for(var k=0;k<5;k++) hiddenErrors[j]+=errors[k]*Output[k*9+j]; hiddenErrors[j]*=1-h[j]*h[j]; }
        for(var k=0;k<5;k++) { for(var j=0;j<8;j++) Output[k*9+j]-=.04*errors[k]*h[j]; Output[k*9+8]-=.04*errors[k]; }
        for(var j=0;j<8;j++) { for(var i=0;i<6;i++) Hidden[j*7+i]-=.04*hiddenErrors[j]*x[i]; Hidden[j*7+6]-=.04*hiddenErrors[j]; }
        Samples++;
    }
}
