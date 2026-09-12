using System.Numerics;
namespace KeyLearner.Studio;
public enum Region { Forest, Lakes, Mountains, City, River, Town, Tundra }
public static class ExplorerWorld
{
    public const float RegionLength=950;
    static int Index(float z)=>(int)MathF.Floor(-z/RegionLength);
    static Region At(int i)=>(Region)((i%7+7)%7);
    public static Region Area(float z)=>At(Index(z));
    public static float Valley(float z)=>MathF.Sin(z*.003f)*100;
    public static float Road(float z)=>MathF.Sin(z*.0028f)*95+MathF.Sin(z*.006f)*24;
    public static float Bed(float x,float z)=>-85+MathF.Sin(x*.014f)*6+MathF.Cos(z*.018f)*5;
    public static float Height(float x,float z)
    {
        int i=Index(z);float t=-z/RegionLength-i;float blend=Math.Clamp(t/.2f,0,1);blend=blend*blend*(3-2*blend);
        return Shape(At(i-1),x,z)*(1-blend)+Shape(At(i),x,z)*blend;
    }
    static float Shape(Region area,float x,float z)=>area switch {
        Region.Mountains=>18+MathF.Pow(Math.Clamp(MathF.Abs(x-Valley(z))/230,0,1),1.4f)*(160+55*MathF.Sin(z*.006f)),
        Region.Lakes=>-5+MathF.Sin(x*.012f)*8+MathF.Cos(z*.01f)*7,
        Region.River=>15-26*MathF.Exp(-MathF.Pow((x-MathF.Sin(z*.008f)*100)/38,2)),
        Region.City or Region.Town=>6+MathF.Sin(z*.002f),
        Region.Tundra=>8+MathF.Sin(x*.007f)*8+MathF.Cos(z*.008f)*5,
        _=>10+MathF.Sin(x*.012f)*9+MathF.Cos(z*.017f)*7
    };
    public static float Land(float x,float z,bool race){float h=Height(x,z);if(!race)return h;float t=Math.Clamp((MathF.Abs(x-Road(z))-20)/35,0,1);return 5*(1-t)+h*t;}
    public static Vector3 RoadTerrainPoint(int side,int column,float z,int columns){float offset=side*(20+630f*column/columns),x=Road(z)+offset;return new(x,column==0?5.7f:Land(x,z,true),z);}
    public static bool Driveable(float x,float z)=>Land(x,z,true)>2.6f;
}
