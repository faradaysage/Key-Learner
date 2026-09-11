using System.Numerics;
namespace KeyLearner.Studio;

/// <summary>Bounded 2D droplets: conserve area and momentum on fusion; high-speed
/// impacts can divide a larger drop. The renderer supplies the continuous surface.</summary>
public sealed class BlobWorld
{
    public sealed class Drop
    {
        public Vector2 Position,Velocity;
        public Vector3 Tint;
        public float Radius,Age,Life=5;
        public bool Dead;
        public float Mass=>Radius*Radius;
    }
    public List<Drop> Drops {get;}=new();
    public int Fusions {get;private set;}
    public int Splits {get;private set;}
    public void Add(Vector2 p,Vector2 v,float radius,Vector3 tint,float life,int limit)
    {if(Drops.Count<limit)Drops.Add(new(){Position=p,Velocity=v,Radius=radius,Tint=tint,Life=life});}
    public void Clear()=>Drops.Clear();
    public void Step(float dt,int width,int height,float gravity,float bounce,int limit)
    {
        dt=Math.Clamp(dt,0,1f/30);
        var cells=new Dictionary<(int,int),List<int>>();
        for(var i=0;i<Drops.Count;i++)
        {
            var d=Drops[i];d.Age+=dt;d.Velocity.Y+=gravity*dt;
            d.Velocity*=MathF.Exp(-.16f*dt);d.Position+=d.Velocity*dt;
            if(d.Position.X<d.Radius || d.Position.X>width-d.Radius){d.Position.X=Math.Clamp(d.Position.X,d.Radius,width-d.Radius);d.Velocity.X*=-bounce;}
            if(d.Position.Y>height-d.Radius){d.Position.Y=height-d.Radius;d.Velocity.Y=-Math.Abs(d.Velocity.Y)*bounce;}
            if(d.Position.Y<d.Radius){d.Position.Y=d.Radius;d.Velocity.Y=Math.Abs(d.Velocity.Y);}
            var key=((int)(d.Position.X/80),(int)(d.Position.Y/80));
            if(!cells.TryGetValue(key,out var bucket))cells[key]=bucket=new();
            bucket.Add(i);
        }
        var additions=new List<Drop>();
        for(var i=0;i<Drops.Count;i++)
        {
            var a=Drops[i];if(a.Dead)continue;
            var cell=((int)(a.Position.X/80),(int)(a.Position.Y/80));
            for(var x=-1;x<=1 && !a.Dead;x++)for(var y=-1;y<=1 && !a.Dead;y++)
            {
                if(!cells.TryGetValue((cell.Item1+x,cell.Item2+y),out var neighbors))continue;
                foreach(var j in neighbors)
                {
                    if(j<=i || a.Dead)continue;
                    var b=Drops[j];if(b.Dead)continue;
                    var delta=b.Position-a.Position;var distance=delta.Length();var reach=a.Radius+b.Radius;
                    if(distance>reach*1.5f)continue;
                    var normal=distance>.001f?delta/distance:Vector2.UnitX;
                    var relative=(b.Velocity-a.Velocity).Length();
                    // Surface tension first pulls touching halos toward each other.
                    var attraction=normal*14*dt;
                    a.Velocity+=attraction;b.Velocity-=attraction;
                    if(distance<reach*.72f && relative<150 && a.Mass+b.Mass<=38*38)
                    {
                        var total=a.Mass+b.Mass;var fraction=b.Mass/total;
                        a.Position=Vector2.Lerp(a.Position,b.Position,fraction);
                        a.Velocity=Vector2.Lerp(a.Velocity,b.Velocity,fraction);
                        a.Tint=Vector3.Lerp(a.Tint,b.Tint,fraction);
                        var remaining=(a.Life-a.Age)*(1-fraction)+(b.Life-b.Age)*fraction;
                        a.Radius=MathF.Sqrt(total);a.Life=remaining;a.Age=0;b.Dead=true;Fusions++;
                    }
                    else if(distance<reach)
                    {
                        var impact=Vector2.Dot(b.Velocity-a.Velocity,normal);
                        if(impact<0)
                        {
                            var impulse=-(1+bounce)*impact/(1/a.Mass+1/b.Mass);
                            a.Velocity-=normal*(impulse/a.Mass);b.Velocity+=normal*(impulse/b.Mass);
                            var big=a.Radius>=b.Radius?a:b;
                            if(relative>260 && big.Radius>13 && Drops.Count+additions.Count<limit)
                            {
                                var tangent=new Vector2(-normal.Y,normal.X);
                                big.Radius/=MathF.Sqrt(2);
                                var velocity=big.Velocity;
                                additions.Add(new(){Position=big.Position-tangent*big.Radius*.7f,Velocity=velocity-tangent*90,Radius=big.Radius,Tint=big.Tint,Life=big.Life,Age=big.Age});
                                big.Position+=tangent*big.Radius*.7f;big.Velocity=velocity+tangent*90;Splits++;
                            }
                        }
                        var overlap=Math.Max(0,reach-distance)*.25f;
                        a.Position-=normal*overlap;b.Position+=normal*overlap;
                    }
                }
            }
        }
        Drops.RemoveAll(d=>d.Dead || d.Age>=d.Life);
        Drops.AddRange(additions);
        if(Drops.Count>limit)Drops.RemoveRange(0,Drops.Count-limit);
    }
}
