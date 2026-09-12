using System.Numerics;
namespace KeyLearner.Studio;
public enum Region { Forest, Lakes, Mountains, City, River, Town, Tundra }
public static class ExplorerWorld
{
    public const float RegionLength=950;
    static int Index(float z)=>(int)MathF.Floor(-z/RegionLength);
    static Region At(int i)=>(Region)((i%7+7)%7);
    public static Region Area(float z)=>At(Index(z));
    public static float Road(float z)=>MathF.Sin(z*.0028f)*95+MathF.Sin(z*.006f)*24;
    public static float Bed(float x,float z)=>-85+MathF.Sin(x*.014f)*6+MathF.Cos(z*.018f)*5;
    public static float Height(float x,float z)
    {
        int i=Index(z);float t=-z/RegionLength-i;float blend=Math.Clamp(t/.2f,0,1);blend=blend*blend*(3-2*blend);
        return Shape(At(i-1),x,z)*(1-blend)+Shape(At(i),x,z)*blend;
    }
    static float Shape(Region area,float x,float z)=>area switch {
        Region.Mountains=>12+MathF.Pow(MathF.Abs(MathF.Sin(x*.006f)*MathF.Cos(z*.008f)),2)*115,
        Region.Lakes=>-5+MathF.Sin(x*.012f)*8+MathF.Cos(z*.01f)*7,
        Region.River=>15-26*MathF.Exp(-MathF.Pow((x-MathF.Sin(z*.008f)*100)/38,2)),
        Region.City or Region.Town=>6+MathF.Sin(z*.002f),
        Region.Tundra=>8+MathF.Sin(x*.007f)*8+MathF.Cos(z*.008f)*5,
        _=>10+MathF.Sin(x*.012f)*9+MathF.Cos(z*.017f)*7
    };
    public static float Land(float x,float z,bool race){float h=Height(x,z);if(!race)return h;float t=Math.Clamp((MathF.Abs(x-Road(z))-17)/35,0,1);return 5*(1-t)+h*t;}
}
public enum ExplorerKind { Bird, Dolphin, Racer }
public sealed class FlightModel
{
    public ExplorerKind Kind {get;private set;}
    public Vector3 Position=new(0,65,0);
    public float Yaw,Pitch,Roll,Speed=42,Time;
    public float Response=1,TopSpeed=160;
    float rollTime,rollDirection,lastLeft=-10,lastRight=-10,pulseCooldown;
    public float Pulse {get;private set;}
    public int Rolls {get;private set;}
    public Vector3 Forward=>Kind==ExplorerKind.Racer?Vector3.Normalize(new Vector3(MathF.Sin(Yaw)*.5f,0,-1)):new(MathF.Sin(Yaw)*MathF.Cos(Pitch),MathF.Sin(Pitch),-MathF.Cos(Yaw)*MathF.Cos(Pitch));
    public Vector3 Up=>Kind==ExplorerKind.Racer?Vector3.UnitY:new(-MathF.Sin(Yaw)*MathF.Sin(Pitch),MathF.Cos(Pitch),MathF.Cos(Yaw)*MathF.Sin(Pitch));
    public Vector3 Gate {get;private set;}
    public string Word{get;private set;}="cat";
    public int Collected{get;private set;}
    public int Score{get;private set;}
    public int Completed{get;private set;}
    public char Letter=>Word[Math.Min(Collected,Word.Length-1)];
    public FlightModel()=>PlaceGate();
    public void Configure(ExplorerKind kind){if(Kind==kind)return;Kind=kind;Position=new(0,kind==ExplorerKind.Dolphin?-28:kind==ExplorerKind.Racer?7:65,0);Yaw=Pitch=Roll=rollTime=0;Speed=kind==ExplorerKind.Dolphin?30:42;PlaceGate();}
    public void SetWord(string word){Word=string.IsNullOrWhiteSpace(word)?"cat":word.ToLowerInvariant();Collected=0;PlaceGate();}
    public static float Terrain(float x,float z)=>ExplorerWorld.Height(x,z);
    void PlaceGate(){
        Gate=Position+Forward*(Kind==ExplorerKind.Racer?150:100);
        if(Kind==ExplorerKind.Racer)Gate=new(ExplorerWorld.Road(Gate.Z)+(Collected%3-1)*9,10,Gate.Z);
        else if(Kind==ExplorerKind.Dolphin)Gate=new(Gate.X,Math.Clamp(Gate.Y,ExplorerWorld.Bed(Gate.X,Gate.Z)+18,-12),Gate.Z);
        else Gate=new(Gate.X,Math.Max(Terrain(Gate.X,Gate.Z)+22,Gate.Y),Gate.Z);
    }
    public void ResetInputGestures(){lastLeft=lastRight=-10;Pulse=rollTime=pulseCooldown=0;Roll=0;}
    public bool Signal(){if(pulseCooldown>0)return false;Pulse=1;pulseCooldown=1.4f;return true;}
    public void TapTurn(int direction){
        if(Kind==ExplorerKind.Racer)return;float last=direction<0?lastLeft:lastRight;
        if(Time-last<=.32f && rollTime<=0){rollTime=.8f;rollDirection=direction;Rolls++;last=-10;}else last=Time;
        if(direction<0)lastLeft=last;else lastRight=last;
    }
    public bool Step(float dt,float turn,float pitch,bool accelerate,bool assist=false)
    {
        dt=Math.Clamp(dt,0,.1f);int steps=Math.Max(1,(int)Math.Ceiling(dt*120));float h=dt/steps;
        if(Collected>=Word.Length)return false;
        for(int i=0;i<steps;i++){
            Time+=h;Pulse=Math.Max(0,Pulse-h*.7f);pulseCooldown=Math.Max(0,pulseCooldown-h);float steering=turn,climb=pitch;
            if(assist && turn==0 && pitch==0){
                var d=Vector3.Normalize(Gate-Position);float targetYaw=MathF.Atan2(d.X,-d.Z);
                steering=Math.Clamp(MathF.Atan2(MathF.Sin(targetYaw-Yaw),MathF.Cos(targetYaw-Yaw))*1.2f,-.6f,.6f);
                climb=Math.Clamp((MathF.Asin(d.Y)-MathF.Asin(MathF.Sin(Pitch)))*1.3f,-.55f,.55f);
            }
            if(Kind==ExplorerKind.Racer){
                float targetSpeed=pitch>0?22:accelerate?TopSpeed:pitch<0?110:70;Speed+=(targetSpeed-Speed)*(1-MathF.Exp(-h*1.5f));
                float targetX=Position.X+steering*45*h;if(assist && turn==0)targetX+=(Gate.X-targetX)*(1-MathF.Exp(-h*.65f));
                float z=Position.Z-Speed*h;targetX=Math.Clamp(targetX,ExplorerWorld.Road(z)-27,ExplorerWorld.Road(z)+27);
                Position=new(targetX,7,z);Yaw+=(steering*.25f-Yaw)*(1-MathF.Exp(-h*6));Roll=-steering*.06f;
            }else{
                Yaw+=steering*1.65f*Response*h;Pitch+=climb*(Kind==ExplorerKind.Dolphin?1.85f:2.2f)*Response*h;
                Pitch=MathF.IEEERemainder(Pitch,MathF.Tau);Yaw=MathF.IEEERemainder(Yaw,MathF.Tau);
                float bank=turn*-.6f;if(rollTime>0){rollTime=Math.Max(0,rollTime-h);float t=1-rollTime/.8f;bank+=rollDirection*MathF.Tau*(t*t*(3-2*t));}
                Roll=bank;
                float cruise=Kind==ExplorerKind.Dolphin?30:42,cap=Kind==ExplorerKind.Dolphin?TopSpeed*.7f:TopSpeed;
                Speed=Math.Clamp(Speed+((accelerate?80:0)+(cruise-Speed)*.4f-MathF.Sin(Pitch)*15)*h,18,cap);
                Position+=(Forward*Speed+new Vector3(MathF.Sin(Time*.3f)*1.2f,0,MathF.Cos(Time*.17f)*.5f))*h;
                float ground=(Kind==ExplorerKind.Dolphin?ExplorerWorld.Bed(Position.X,Position.Z):Terrain(Position.X,Position.Z))+7;
                if(Position.Y<ground){Position=new(Position.X,ground,Position.Z);if(Forward.Y<0)Pitch=.3f;}
                if(Kind==ExplorerKind.Dolphin && Position.Y> -4){Position=new(Position.X,-4,Position.Z);if(Forward.Y>0)Pitch=-.2f;}
            }
            // Squawk / sonar / horn reveals and gently attracts the next letter for a short time.
            if(Pulse>0){var d=Position+Forward*18-Gate;Gate+=d*(1-MathF.Exp(-h*1.4f))*Pulse;}
            if(Vector3.Distance(Position,Gate)<(Pulse>0?17:Kind==ExplorerKind.Racer?12:10)){
                Score+=10;Collected++;if(Collected==Word.Length){Score+=Word.Length*10;Completed++;}else PlaceGate();return true;
            }
            if(Vector3.Dot(Gate-Position,Forward)<-35)PlaceGate();
        }
        return false;
    }
}
