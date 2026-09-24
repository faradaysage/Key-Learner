using System.Numerics;
namespace KeyLearner.Studio;
public enum WildlifeState { Rest, Wander, Watch, Flee }
/// <summary>Small autonomous ground actor: bounded habitat, asynchronous decisions and momentum.
/// Presentation supplies terrain legality and animation; this class knows no engine APIs.</summary>
public sealed class WildlifeMotion
{
    readonly Random random;
    Vector3 home;
    readonly float radius,cruise,alertRadius;
    Vector3 target;
    float decision,remaining,alarm;
    public Vector3 Position {get;private set;}
    public Vector3 Velocity {get;private set;}
    public Vector3 Forward {get;private set;}=Vector3.UnitZ;
    public WildlifeState State {get;private set;}
    public float Speed=>Velocity.Length();
    public int Reactions {get;private set;}
    public WildlifeMotion(Vector3 origin,int seed,float habitatRadius=28,float speed=2,float alertDistance=18)
    {
        home=Position=target=origin;random=new Random(seed);radius=Math.Max(4,habitatRadius);cruise=Math.Max(.1f,speed);alertRadius=Math.Max(1,alertDistance);
        remaining=.4f+(float)random.NextDouble()*3;decision=(float)random.NextDouble()*.3f;
        Forward=new Vector3(MathF.Sin(seed),0,MathF.Cos(seed));
    }
    static Vector3 Flat(Vector3 v)=>new(v.X,0,v.Z);
    static Vector3 Unit(Vector3 v,Vector3 fallback)=>v.LengthSquared()>.0001f?Vector3.Normalize(v):fallback;
    public void SetGroundHeight(float height)=>Position=new Vector3(Position.X,height,Position.Z);
    public void Follow(Vector3 anchor,float leash=12)
    {
        home=anchor;
        if(State!=WildlifeState.Flee && Flat(Position-anchor).LengthSquared()>leash*leash)
        {
            target=anchor+Unit(Flat(Position-anchor),Forward)*3;
            State=WildlifeState.Wander;remaining=1;
        }
    }
    public void Startle(float seconds=3)=>alarm=Math.Max(alarm,Math.Clamp(seconds,0,6));
    public void Step(float dt,Vector3 player,Func<Vector3,bool>? habitat=null,Vector3? predator=null)
    {
        dt=Math.Clamp(dt,0,.1f);decision-=dt;remaining-=dt;alarm=Math.Max(0,alarm-dt);
        var threat=predator??player;var away=Flat(Position-threat);float distance=away.Length();
        // Sense the actual 3D distance: a bird high overhead is not a ground collision.
        bool near=Vector3.Distance(Position,threat)<alertRadius || (alarm>0 && Vector3.Distance(Position,threat)<alertRadius*4);
        if(near && State!=WildlifeState.Flee){State=WildlifeState.Flee;remaining=2.3f+(float)random.NextDouble()*2;decision=0;Reactions++;}
        if(decision<=0)
        {
            decision=.2f+(float)random.NextDouble()*.17f;
            if(State==WildlifeState.Flee)
            {
                target=Position+Unit(away,Forward)*(radius*.75f);
                if(!near && remaining<=0){State=WildlifeState.Watch;remaining=1.2f+(float)random.NextDouble()*2;}
            }
            else if(remaining<=0 || (State==WildlifeState.Wander && Flat(target-Position).LengthSquared()<2))
            {
                if(random.NextDouble()<.3){State=WildlifeState.Rest;remaining=2+(float)random.NextDouble()*4;}
                else{State=WildlifeState.Wander;remaining=5+(float)random.NextDouble()*8;float angle=(float)random.NextDouble()*MathF.PI*2,r=radius*(.25f+(float)random.NextDouble()*.65f);target=home+new Vector3(MathF.Sin(angle)*r,0,MathF.Cos(angle)*r);}
            }
            else if(State==WildlifeState.Rest && distance<alertRadius*1.8f){State=WildlifeState.Watch;remaining=1.5f;}
        }
        var direction=State==WildlifeState.Watch?Flat(threat-Position):Flat(target-Position);
        var desired=Unit(direction,Forward);
        // Limit angular velocity, including the exact opposite-heading case.
        float current=MathF.Atan2(Forward.X,Forward.Z),goal=MathF.Atan2(desired.X,desired.Z);
        float error=MathF.Atan2(MathF.Sin(goal-current),MathF.Cos(goal-current));
        float angleNow=current+Math.Clamp(error,-dt*2.4f,dt*2.4f);Forward=new(MathF.Sin(angleNow),0,MathF.Cos(angleNow));
        float speed=State==WildlifeState.Flee?cruise*2.6f:State==WildlifeState.Wander?cruise:0;
        speed*=Math.Clamp(1-MathF.Abs(error)/MathF.PI,.2f,1);
        Velocity+=(Forward*speed-Velocity)*(1-MathF.Exp(-dt*4));
        var next=Position+Velocity*dt;
        // Avoid roads, water and steep slopes through the world's inexpensive height query.
        if(habitat==null || habitat(next))Position=next;
        else{Velocity*=MathF.Exp(-dt*15);target=home;decision=0;State=WildlifeState.Wander;remaining=3;}
        if(Flat(Position-home).LengthSquared()>radius*radius*9){target=home;State=WildlifeState.Wander;remaining=5;}
    }
}
