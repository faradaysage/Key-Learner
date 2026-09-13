using System.Numerics;
namespace KeyLearner.Studio;
public interface IExplorerMovement
{
    Vector3 Spawn {get;}
    float Cruise {get;}
    Vector3 NextGate(FlightModel f);
    void Step(FlightModel f,float dt,float turn,float pitch,bool boost,bool assist);
}
public abstract class SwimmingFlyingMovement : IExplorerMovement
{
    public abstract Vector3 Spawn {get;}
    public abstract float Cruise {get;}
    protected abstract float Floor(float x,float z);
    protected abstract float CourseHeight(float x,float z);
    protected virtual float SpeedFactor=>1;
    protected virtual float PitchRate=>2.2f;
    protected virtual float Ceiling(float x,float z)=>Floor(x,z)+650;
    public virtual Vector3 NextGate(FlightModel f){
        var p=f.Position+new Vector3(MathF.Sin(f.Yaw),0,-MathF.Cos(f.Yaw))*100;
        return new(p.X,CourseHeight(p.X,p.Z),p.Z);
    }
    static float Angle(float a)=>MathF.Atan2(MathF.Sin(a),MathF.Cos(a));
    public void Step(FlightModel f,float h,float turn,float pitch,bool boost,bool assist){
        float steering=turn,climb=pitch;
        if(assist && turn==0 && pitch==0){
            var d=f.Gate-f.Position;
            float yaw=f.Collected<f.Word.Length?MathF.Atan2(d.X,-d.Z):f.Yaw;
            steering=Math.Clamp(Angle(yaw-f.Yaw)*1.8f,-1,1);
            float height=CourseHeight(f.Position.X+MathF.Sin(f.Yaw)*50,f.Position.Z-MathF.Cos(f.Yaw)*50);
            float targetPitch=Math.Clamp((height-f.Position.Y)/80,-.7f,.7f);
            // Correct the actual orientation, not asin(sin(pitch)), which loses inverted state.
            climb=Math.Clamp(Angle(targetPitch-f.Pitch)*1.7f,-1,1);
        }
        f.Yaw=Angle(f.Yaw+steering*1.65f*f.Response*h);f.Pitch=Angle(f.Pitch+climb*PitchRate*f.Response*h);f.Roll=turn*-.6f;
        f.Speed=Math.Clamp(f.Speed+((boost?80:0)+(Cruise-f.Speed)*.4f-MathF.Sin(f.Pitch)*15)*h,18,f.TopSpeed*SpeedFactor);
        f.Position+=(f.Forward*f.Speed+new Vector3(MathF.Sin(f.Time*.3f)*1.2f,0,MathF.Cos(f.Time*.17f)*.5f))*h;
        if(assist && turn==0 && pitch==0){
            float error=CourseHeight(f.Position.X,f.Position.Z)-f.Position.Y;
            f.Position+=Vector3.UnitY*Math.Clamp(error*.7f,-65,45)*h;
        }
        float ground=Floor(f.Position.X,f.Position.Z)+7,ceiling=Ceiling(f.Position.X,f.Position.Z);
        if(f.Position.Y<ground){f.Position=new(f.Position.X,ground,f.Position.Z);if(f.Forward.Y<0)f.Pitch=.3f;}
        if(f.Position.Y>ceiling){f.Position=new(f.Position.X,ceiling,f.Position.Z);if(f.Forward.Y>0)f.Pitch=-.2f;}
    }
}
public sealed class BirdMovement : SwimmingFlyingMovement
{
    public override Vector3 Spawn=>new(0,65,0);
    public override float Cruise=>42;
    protected override float Floor(float x,float z)=>ExplorerWorld.Height(x,z);
    protected override float CourseHeight(float x,float z)=>Floor(x,z)+45;
    public override Vector3 NextGate(FlightModel f){var p=base.NextGate(f);if(ExplorerWorld.Area(p.Z)==Region.Mountains)p.X+=(ExplorerWorld.Valley(p.Z)-p.X)*.45f;p.Y=CourseHeight(p.X,p.Z);return p;}
}
public sealed class DolphinMovement : SwimmingFlyingMovement
{
    public override Vector3 Spawn=>new(0,-28,0);
    public override float Cruise=>30;
    protected override float SpeedFactor=>.7f;
    protected override float PitchRate=>1.85f;
    protected override float Floor(float x,float z)=>ExplorerWorld.Bed(x,z);
    protected override float CourseHeight(float x,float z)=>Math.Clamp(Floor(x,z)+48,Floor(x,z)+18,-12);
    protected override float Ceiling(float x,float z)=>-4;
}
public sealed class CarMovement : IExplorerMovement
{
    public Vector3 Spawn=>new(0,7,0);
    public float Cruise=>70;
    public Vector3 NextGate(FlightModel f){float z=f.Position.Z-150;return new(ExplorerWorld.Road(z)+(f.Collected%3-1)*9,10,z);}
    public void Step(FlightModel f,float h,float turn,float pitch,bool boost,bool assist){
        float off=f.OffRoad,targetSpeed=(pitch>0?22:boost?f.TopSpeed:pitch<0?110:Cruise)/(1+off*.045f);
        f.Speed+=(targetSpeed-f.Speed)*(1-MathF.Exp(-h*1.5f));
        float z=f.Position.Z-f.Speed*h,center=ExplorerWorld.Road(z);
        float x=f.Position.X+turn*45*h/(1+off*.08f);
        // Soft shoulder: sustained steering can leave the road, but it pushes back.
        x+=(center-x)*Math.Min(1,off*.04f)*h;
        if(assist && turn==0)x+=((f.Collected<f.Word.Length?f.Gate.X:center)-x)*(1-MathF.Exp(-h*.85f));
        var candidate=new Vector3(x,0,z);
        if(!ExplorerWorld.Driveable(x,z)){
            // Sweep to the shoreline. Neither speed nor steering can tunnel into water.
            float low=0,high=1;for(int i=0;i<14;i++){float t=(low+high)*.5f;var p=Vector3.Lerp(f.Position,candidate,t);if(ExplorerWorld.Driveable(p.X,p.Z))low=t;else high=t;}
            candidate=Vector3.Lerp(f.Position,candidate,low);f.Speed*=MathF.Exp(-h*7);
            if(turn==0){float inward=candidate.X+(ExplorerWorld.Road(candidate.Z)-candidate.X)*(1-MathF.Exp(-h*2));if(ExplorerWorld.Driveable(inward,candidate.Z))candidate.X=inward;}
        }
        candidate.Y=Math.Max(5.6f,ExplorerWorld.Land(candidate.X,candidate.Z,true))+1.4f;f.Position=candidate;
        f.Yaw+=(turn*.25f-f.Yaw)*(1-MathF.Exp(-h*6));f.Roll=-turn*.06f;
    }
}
