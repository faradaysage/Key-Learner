using System.Numerics;
namespace KeyLearner.Studio;
public sealed class FlightModel
{
    public Vector3 Position=new(0,48,0);
    public float Yaw,Pitch,Roll,Speed=23,Time;
    public Vector3 Forward=>new(MathF.Sin(Yaw)*MathF.Cos(Pitch),MathF.Sin(Pitch),-MathF.Cos(Yaw)*MathF.Cos(Pitch));
    public Vector3 Gate {get;private set;}
    public string Word{get;private set;}="cat";
    public int Collected{get;private set;}
    public int Score{get;private set;}
    public int Completed{get;private set;}
    public char Letter=>Word[Math.Min(Collected,Word.Length-1)];
    public FlightModel()=>PlaceGate();
    public void SetWord(string word){Word=string.IsNullOrWhiteSpace(word)?"cat":word.ToLowerInvariant();Collected=0;PlaceGate();}
    public static float Terrain(float x,float z)=>7+MathF.Sin(x*.012f)*9+MathF.Cos(z*.017f)*7+MathF.Sin((x+z)*.028f)*3;
    void PlaceGate(){Gate=Position+Forward*100+new Vector3(MathF.Sin(Collected*1.4f)*12,MathF.Cos(Collected*1.1f)*6,0);Gate=new(Gate.X,Math.Max(Terrain(Gate.X,Gate.Z)+16,Gate.Y),Gate.Z);}
    public bool Step(float dt,float turn,float pitch,bool accelerate,bool assist=false)
    {
        dt=Math.Clamp(dt,0,.1f);int steps=Math.Max(1,(int)Math.Ceiling(dt*120));float h=dt/steps;bool collected=false;
        for(int i=0;i<steps;i++)
        {
            Time+=h;float steering=turn,climb=pitch;
            if(assist && turn==0 && pitch==0){var d=Vector3.Normalize(Gate-Position);var desired=MathF.Atan2(d.X,-d.Z);var angle=MathF.Atan2(MathF.Sin(desired-Yaw),MathF.Cos(desired-Yaw));steering=Math.Clamp(angle*.8f,-.45f,.45f);climb=Math.Clamp((MathF.Asin(d.Y)-Pitch)*.8f,-.4f,.4f);}
            Yaw+=steering*.9f*h;Pitch=Math.Clamp(Pitch+climb*.65f*h,-1.1f,.9f);Roll+=(turn*-.65f-Roll)*(1-MathF.Exp(-h*4));
            Speed=Math.Clamp(Speed+((accelerate?18:0)+(23-Speed)*.25f-MathF.Sin(Pitch)*11)*h,12,65);
            var before=Position;Position+=(Forward*Speed+new Vector3(MathF.Sin(Time*.3f)*1.2f,-Math.Abs(Roll)*1.8f,MathF.Cos(Time*.17f)*.5f))*h;
            float ground=Terrain(Position.X,Position.Z)+7;if(Position.Y<ground){Position=new(Position.X,ground,Position.Z);Pitch=Math.Max(Pitch,.18f);}if(Position.Y>170){Position=new(Position.X,170,Position.Z);Pitch=Math.Min(Pitch,-.1f);}
            if(Vector3.Distance(Position,Gate)<9){Score+=10;Collected++;collected=true;if(Collected==Word.Length){Score+=Word.Length*10;Completed++;return true;}PlaceGate();}
            else if(Vector3.Dot(Gate-Position,Forward)<-20)PlaceGate();
        }
        return collected;
    }
}
