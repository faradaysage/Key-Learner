using System.Numerics;
namespace KeyLearner.Studio;
/// <summary>Ground travel and spelling rules, independent of camera, sound and Unity.</summary>
public sealed class DinosaurModel
{
    public readonly LetterCourse Course=new();
    public Vector3 Position=new(0,DinosaurWorld.Ground(0,0),0);
    public Vector3 Gate {get;private set;}
    public float Yaw,Speed,Stride;
    float roarCooldown;
    public int Footfalls {get;private set;}
    public int Roars {get;private set;}
    public Vector3 Forward=>new(MathF.Sin(Yaw),0,MathF.Cos(Yaw));
    public DinosaurModel()=>PlaceGate();
    public void SetWord(string word){Course.Start(word);PlaceGate();}
    void PlaceGate(){float z=Position.Z+52;Gate=new(DinosaurWorld.Path(z),DinosaurWorld.Ground(DinosaurWorld.Path(z),z)+7,z);}
    public bool Roar(){if(roarCooldown>0)return false;roarCooldown=5;Roars++;return true;}
    public void Step(float dt,float turn,float drive,bool boost,bool assist,float response=1){
        dt=Math.Clamp(dt,0,.1f);int steps=Math.Max(1,(int)Math.Ceiling(dt*120));float h=dt/steps;
        for(int i=0;i<steps;i++){
            Course.Step(h);roarCooldown=Math.Max(0,roarCooldown-h);
            float steer=turn,target=drive<0?0:boost?23:drive>0?16:9;
            var delta=Gate-Position;delta.Y=0;
            if(assist && turn==0 && drive==0 && Course.Collected<Course.Word.Length){
                float error=MathF.Atan2(MathF.Sin(MathF.Atan2(delta.X,delta.Z)-Yaw),MathF.Cos(MathF.Atan2(delta.X,delta.Z)-Yaw));
                steer=Math.Clamp(error*2,-1,1);if(MathF.Abs(error)>.5f)target=3;
            }
            Yaw+=steer*1.1f*Math.Clamp(response,.5f,2)*h;
            Speed+=(target-Speed)*(1-MathF.Exp(-h*2.5f));
            float traveled=Speed*h;Position+=Forward*traveled;Position.Y=DinosaurWorld.Ground(Position.X,Position.Z);
            Stride+=traveled;Footfalls=(int)(Stride/5.2f);
            if(Course.Collected<Course.Word.Length && Vector2.Distance(new(Position.X,Position.Z),new(Gate.X,Gate.Z))<6.5f){
                Course.Collect();if(Course.Collected<Course.Word.Length)PlaceGate();
            }
        }
    }
}
public static class DinosaurWorld
{
    public static float Path(float z)=>MathF.Sin(z*.006f)*24+MathF.Sin(z*.0018f)*32;
    public static int Biome(float z)=>((int)MathF.Floor(z/800)%3+3)%3;
    static float Shape(int biome,float x,float z){
        float side=MathF.Abs(x-Path(z)),baseHeight=8+MathF.Sin(z*.013f)*2+MathF.Sin(x*.019f)*1.5f;
        if(biome==1)baseHeight+=MathF.Pow(Math.Clamp((side-25)/100,0,1),1.4f)*(30+MathF.Sin(z*.02f)*10);
        if(biome==2)baseHeight-=14*MathF.Exp(-MathF.Pow((side-85)/38,2));
        return baseHeight;
    }
    public static float Ground(float x,float z){
        int biome=Biome(z);float part=z/800-MathF.Floor(z/800),t=Math.Clamp(part/.18f,0,1);t=t*t*(3-2*t);
        return Shape((biome+2)%3,x,z)*(1-t)+Shape(biome,x,z)*t;
    }
}
