using System.Numerics;
namespace KeyLearner.Studio;
/// <summary>A planar partition: every new fracture is clipped to one existing convex pane.
/// The same polygon vertices become falling shards; edges never cross an earlier crack.</summary>
public sealed class GlassSheet
{
    public sealed class Pane {public Vector2[] Points=[];public Vector2 Center,Velocity;public float Age,Angle,Spin,Reveal;public float Area=>AreaOf(Points);}
    public List<Pane> Panes {get;}=new();
    public bool Falling{get;private set;}
    public float Pressure{get;private set;}
    public int Breaks{get;private set;}
    float quiet;
    readonly Random random=new(427);
    const float Width=1440,Height=900;
    public GlassSheet()=>Reset();
    public void Reset(){Panes.Clear();Panes.Add(Make([new(0,0),new(Width,0),new(Width,Height),new(0,Height)]));Pressure=0;quiet=0;Falling=false;}
    static Pane Make(Vector2[] points)=>new(){Points=points,Center=points.Aggregate(Vector2.Zero,(a,b)=>a+b)/points.Length};
    public static float AreaOf(IReadOnlyList<Vector2> p){float area=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];area+=a.X*b.Y-b.X*a.Y;}return Math.Abs(area)*.5f;}
    public bool Hit(Vector2 point,float energy)
    {
        if(Falling)return false;quiet=0;Pressure=Math.Min(1,Pressure+.07f);
        // Pressure propagates into the largest remaining load-bearing piece after a local split.
        var pane=Panes.Count%3==0?Panes.MaxBy(p=>p.Area)!:Panes.OrderBy(p=>Vector2.DistanceSquared(p.Center,point)/Math.Max(1,p.Area)).First();
        if(Panes.Count<80 && pane.Area>2000)
        {
            var center=Panes.Count==1?Vector2.Clamp(point,new(30,30),new(1410,870)):pane.Center;Panes.Remove(pane);
            if(Panes.Count==0){for(int i=0;i<pane.Points.Length;i++)Panes.Add(Make([center,pane.Points[i],pane.Points[(i+1)%pane.Points.Length]]));}
            else{
                var angle=(float)random.NextDouble()*MathF.Tau;var normal=new Vector2(MathF.Cos(angle),MathF.Sin(angle));
                var a=Clip(pane.Points,center,normal);var b=Clip(pane.Points,center,-normal);
                if(a.Length>=3 && b.Length>=3 && AreaOf(a)>20 && AreaOf(b)>20){Panes.Add(Make(a));Panes.Add(Make(b));}else Panes.Add(pane);
            }
        }
        if(Panes.Count<18 || Panes.Max(p=>p.Area)>Width*Height*.15f)return false;
        Falling=true;Breaks++;foreach(var p in Panes){p.Reveal=1;p.Velocity=new(random.Next(-120,121),random.Next(-65,60));p.Spin=(float)(random.NextDouble()-.5)*1.8f;p.Age=0;}return true;
    }
    static Vector2[] Clip(Vector2[] polygon,Vector2 origin,Vector2 normal)
    {
        var result=new List<Vector2>();for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];var da=Vector2.Dot(a-origin,normal);var db=Vector2.Dot(b-origin,normal);if(da>=0)result.Add(a);if((da>=0)!=(db>=0))result.Add(Vector2.Lerp(a,b,da/(da-db)));}return result.ToArray();
    }
    public void Step(float dt,bool gentle)
    {
        quiet+=dt;foreach(var p in Panes){p.Reveal=Math.Min(1,p.Reveal+dt*4);if(!Falling)continue;p.Age+=dt;p.Velocity.Y+=400*dt;p.Center+=p.Velocity*dt;p.Angle+=p.Spin*dt*(gentle?.2f:1);}
        if(Falling){if(Panes.All(p=>p.Age>3))Reset();}
        else if(quiet>.5f){Pressure=Math.Max(0,Pressure-dt*.22f);if(Pressure==0 && Panes.Count>1)Reset();}
    }
}
