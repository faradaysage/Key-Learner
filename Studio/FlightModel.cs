using System.Numerics;
namespace KeyLearner.Studio;
public enum ExplorerKind { Bird, Dolphin, Racer }
/// <summary>Shared letter course; movement strategies own the vehicle-specific physics.</summary>
public sealed class FlightModel
{
    IExplorerMovement movement=new BirdMovement();
    readonly LetterCourse course=new();
    public ExplorerKind Kind {get;private set;}
    public Vector3 Position=new(0,65,0);
    public float Yaw,Pitch,Roll,Speed=42,Time;
    public float Response=1,TopSpeed=160;
    float rollTime,rollDirection,lastLeft=-10,lastRight=-10,pulseCooldown;
    public float Pulse {get;private set;}
    public int Rolls {get;private set;}
    public float OffRoad=>Kind==ExplorerKind.Racer?Math.Max(0,Math.Abs(Position.X-ExplorerWorld.Road(Position.Z))-18):0;
    public Vector3 Forward=>Kind==ExplorerKind.Racer?new Vector3(MathF.Sin(Yaw),0,-MathF.Cos(Yaw)):new(MathF.Sin(Yaw)*MathF.Cos(Pitch),MathF.Sin(Pitch),-MathF.Cos(Yaw)*MathF.Cos(Pitch));
    public Vector3 Up=>Kind==ExplorerKind.Racer?Vector3.UnitY:new(-MathF.Sin(Yaw)*MathF.Sin(Pitch),MathF.Cos(Pitch),MathF.Cos(Yaw)*MathF.Sin(Pitch));
    public Vector3 Right=>Vector3.Normalize(Vector3.Cross(Forward,Up));
    // Local +X must always project to the viewer's right, including in a loop.
    public Matrix4x4 LetterFacing=>Matrix4x4.CreateWorld(Vector3.Zero,Forward,Up);
    public Vector3 Gate {get;private set;}
    public string Word=>course.Word;
    public int Collected=>course.Collected;
    public int BonusScore {get;private set;}
    public int Treasures {get;private set;}
    public float ObstacleRemaining {get;private set;}
    public int Score=>course.Score+BonusScore;
    public void AwardBubble()=>BonusScore+=5;
    public void AwardTreasure(){BonusScore+=25;Treasures++;}
    public bool HitObstacle(){if(ObstacleRemaining>0)return false;ObstacleRemaining=1.5f;return true;}
    public int Completed=>course.Completed;
    public float RewardRemaining=>course.RewardRemaining;
    public char Letter=>Word[Math.Min(Collected,Word.Length-1)];
    public bool FirstLetter=>Collected==0;
    public bool LastLetter=>Collected==Word.Length-1;
    public FlightModel()=>PlaceGate();
    public void Configure(ExplorerKind kind){
        if(Kind==kind)return;Kind=kind;
        movement=kind switch {ExplorerKind.Racer=>new CarMovement(),ExplorerKind.Dolphin=>new DolphinMovement(),_=>new BirdMovement()};
        Position=movement.Spawn;Yaw=Pitch=Roll=rollTime=0;Speed=movement.Cruise;PlaceGate();
    }
    public void SetWord(string word){course.Start(word);PlaceGate();}
    public static float Terrain(float x,float z)=>ExplorerWorld.Height(x,z);
    void PlaceGate()=>Gate=movement.NextGate(this);
    public void ResetInputGestures(){course.DismissReward();lastLeft=lastRight=-10;Pulse=rollTime=pulseCooldown=0;Roll=0;}
    public bool Signal(){if(pulseCooldown>0)return false;Pulse=1;pulseCooldown=1.4f;return true;}
    public void TapTurn(int direction){
        if(Kind==ExplorerKind.Racer)return;float last=direction<0?lastLeft:lastRight;
        if(Time-last<=.32f && rollTime<=0){rollTime=.8f;rollDirection=direction;Rolls++;last=-10;}else last=Time;
        if(direction<0)lastLeft=last;else lastRight=last;
    }
    public bool Step(float dt,float turn,float pitch,bool accelerate,bool assist=false)
    {
        dt=Math.Clamp(dt,0,.1f);int steps=Math.Max(1,(int)Math.Ceiling(dt*120));float h=dt/steps;
        for(int i=0;i<steps;i++){
            Time+=h;course.Step(h);ObstacleRemaining=Math.Max(0,ObstacleRemaining-h);Pulse=Math.Max(0,Pulse-h*.7f);pulseCooldown=Math.Max(0,pulseCooldown-h);
            movement.Step(this,h,turn,pitch,accelerate,assist);
            if(rollTime>0){rollTime=Math.Max(0,rollTime-h);float t=1-rollTime/.8f;Roll+=rollDirection*(2*MathF.PI)*(t*t*(3-2*t));}
            if(Collected>=Word.Length)continue;
            if(Pulse>0){var d=Position+Forward*18-Gate;Gate+=d*(1-MathF.Exp(-h*1.4f))*Pulse;}
            if(Vector3.Distance(Position,Gate)<(Pulse>0?17:Kind==ExplorerKind.Racer?12:10)){
                course.Collect();if(Collected<Word.Length)PlaceGate();return true;
            }
            if(Vector3.Dot(Gate-Position,Forward)<-35)PlaceGate();
        }
        return false;
    }
}
