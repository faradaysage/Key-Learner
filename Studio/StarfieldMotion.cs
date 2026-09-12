using System.Numerics;
namespace KeyLearner.Studio;
/// <summary>Bounded, frame-rate independent stars for forward flight and a rotating volume.</summary>
public sealed class StarfieldMotion
{
    private readonly Vector3[] flight=new Vector3[2400],cloud=new Vector3[2400];
    private readonly Random random=new(811);
    public float Time {get;private set;}
    public StarfieldMotion(){for(int i=0;i<flight.Length;i++){flight[i]=Spawn(.15f+(float)random.NextDouble()*4);cloud[i]=new(Range(3),Range(3),Range(3));}}
    private float Range(float range)=>((float)random.NextDouble()*2-1)*range;
    private Vector3 Spawn(float z)=>new(Range(3),Range(2),z);
    public void Step(float dt,float speed,bool gentle)
    {
        dt=Math.Clamp(dt,0,.1f);var motion=dt*speed*(gentle?.25f:1);Time+=motion;
        for(int i=0;i<flight.Length;i++)
        {
            var p=flight[i];p.Z-=motion*.7f;
            if(p.Z<.12f || Math.Abs(p.X/p.Z)>2 || Math.Abs(p.Y/p.Z)>1.5f)p=Spawn(4);
            flight[i]=p;
        }
    }
    public Vector3 Project(int index,bool rotating)
    {
        if(!rotating){var p=flight[index];return new(p.X/p.Z,p.Y/p.Z,p.Z);}
        var point=Vector3.Transform(cloud[index],Matrix4x4.CreateRotationX(Time*.045f)*Matrix4x4.CreateRotationY(Time*.075f));
        var depth=6.5f-point.Z;
        return new(point.X/depth*2,point.Y/depth*2,depth);
    }
}
