using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;
public sealed class FlightRenderer : IDisposable
{
    readonly GraphicsDevice device;readonly BasicEffect effect;readonly GlyphMeshes glyphs;readonly SpriteFont font;
    VertexPositionNormalTexture[] terrain=[],forest=[],water=[];int detail;int cellX=int.MinValue,cellZ=int.MinValue;
    readonly List<VertexPositionNormalTexture> bird=new();
    public FlightRenderer(GraphicsDevice device,SpriteFont font){this.device=device;this.font=font;effect=new(device);effect.EnableDefaultLighting();effect.PreferPerPixelLighting=true;effect.AmbientLightColor=new(.35f);effect.FogEnabled=true;effect.FogColor=new(.48f,.73f,.84f);effect.FogStart=140;effect.FogEnd=550;glyphs=new(device);}
    static Vector3 V(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
    void Terrain(FlightModel flight,int resolution)
    {
        int x=(int)MathF.Floor(flight.Position.X/60),z=(int)MathF.Floor(flight.Position.Z/60);if(x==cellX&&z==cellZ&&resolution==detail)return;cellX=x;cellZ=z;detail=resolution;
        var vertices=new List<VertexPositionNormalTexture>();float span=1100,step=span/resolution;
        Vector3 P(int i,int j){float px=x*60-span/2+i*step,pz=z*60-span/2+j*step;return new(px,FlightModel.Terrain(px,pz),pz);}
        void Triangle(Vector3 a,Vector3 b,Vector3 c){var n=Vector3.Normalize(Vector3.Cross(b-a,c-a));if(n.Y<0)n=-n;foreach(var p in new[]{a,b,c})vertices.Add(new(p,n,Vector2.Zero));}
        for(int i=0;i<resolution;i++)for(int j=0;j<resolution;j++){Triangle(P(i,j),P(i+1,j),P(i,j+1));Triangle(P(i+1,j),P(i+1,j+1),P(i,j+1));}terrain=vertices.ToArray();
        vertices.Clear();
        for(int i=-12;i<=12;i++)for(int j=-12;j<=12;j++){
            int gx=(int)MathF.Floor(x*60/40f)+i,gz=(int)MathF.Floor(z*60/40f)+j;float seed=MathF.Sin(gx*127.1f+gz*311.7f)*43758.5453f;seed-=MathF.Floor(seed);if(seed<.45f)continue;
            float px=gx*40+seed*25,pz=gz*40+seed*17,py=FlightModel.Terrain(px,pz);if(py<3)continue;float tall=8+seed*10;
            for(int tier=0;tier<2;tier++)for(int side=0;side<7;side++){float a=side*MathF.Tau/7,b=(side+1)*MathF.Tau/7,r=(tier==0?5:3.5f)*(1+seed*.5f),y0=py+tier*tall*.35f;Triangle(new(px+MathF.Cos(a)*r,y0,pz+MathF.Sin(a)*r),new(px,y0+tall*.8f,pz),new(px+MathF.Cos(b)*r,y0,pz+MathF.Sin(b)*r));}
        }
        forest=vertices.ToArray();vertices.Clear();float left=x*60-550,top=z*60-550;Triangle(new(left,2,top),new(left+1100,2,top),new(left,2,top+1100));Triangle(new(left+1100,2,top),new(left+1100,2,top+1100),new(left,2,top+1100));water=vertices.ToArray();
    }
    public void Draw(FlightModel flight,Settings settings,Color[] palette)
    {
        bool mono=settings.Theme==Mood.BlackAndWhite;device.Clear(mono?new Color(65,65,65):new Color(124,187,217));effect.FogColor=mono?new Vector3(.25f):new Vector3(.48f,.73f,.84f);Terrain(flight,settings.TerrainDetail);var pos=V(flight.Position);var forward=V(flight.Forward);var camera=pos-forward*20+new Vector3(0,7,0);var view=Matrix.CreateLookAt(camera,pos+forward*30,Vector3.Up);var projection=Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(65),device.Viewport.AspectRatio,.3f,800);
        device.DepthStencilState=DepthStencilState.Default;device.RasterizerState=RasterizerState.CullNone;device.BlendState=BlendState.Opaque;effect.World=Matrix.Identity;effect.View=view;effect.Projection=projection;effect.DiffuseColor=mono?new(.55f):new(.26f,.55f,.26f);
        foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,terrain,0,terrain.Length/3);}
        effect.DiffuseColor=mono?new(.2f):new(.11f,.34f,.19f);foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,forest,0,forest.Length/3);}
        effect.DiffuseColor=mono?new(.75f):new(.12f,.42f,.65f);effect.SpecularColor=new(.6f);effect.SpecularPower=64;foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,water,0,water.Length/3);}effect.SpecularColor=Vector3.Zero;
        // Distant hills and alternating terrain lighting supply depth without texture downloads.
        var gate=V(flight.Gate);glyphs.Draw(font,char.ToUpperInvariant(flight.Letter),Matrix.CreateScale(.095f,-.095f,.095f)*Matrix.CreateRotationY(-flight.Yaw)*Matrix.CreateTranslation(gate),view,projection,palette[flight.Collected%4]);
        bird.Clear();float flap=MathF.Sin(flight.Time*(flight.Speed>30?9:4))*.5f;
        void Triangle(Vector3 a,Vector3 b,Vector3 c){var normal=Vector3.Normalize(Vector3.Cross(b-a,c-a));foreach(var p in new[]{a,b,c})bird.Add(new(p,normal,Vector2.Zero));}
        Triangle(new(0,0,-3),new(-.8f,.4f,1),new(.8f,.4f,1));Triangle(new(0,0,-3),new(.8f,.4f,1),new(0,-.65f,1));Triangle(new(0,0,-3),new(0,-.65f,1),new(-.8f,.4f,1));
        Triangle(new(-.4f,0,-.6f),new(-5,flap,1),new(-1.2f,.15f,1.6f));Triangle(new(.4f,0,-.6f),new(1.2f,.15f,1.6f),new(5,flap,1));Triangle(new(0,0,1),new(-1.4f,.2f,3),new(1.4f,.2f,3));
        effect.World=Matrix.CreateRotationZ(flight.Roll)*Matrix.CreateRotationX(flight.Pitch)*Matrix.CreateRotationY(-flight.Yaw)*Matrix.CreateTranslation(pos);effect.DiffuseColor=palette[2].ToVector3();foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,bird.ToArray(),0,bird.Count/3);}
        // A target halo makes collection distance and direction legible to younger players.
        var ring=new List<VertexPositionNormalTexture>();for(int i=0;i<48;i++){float a=i*MathF.Tau/48,b=(i+1)*MathF.Tau/48;foreach(var (angle,radius) in new[]{(a,8f),(b,8f),(b,8.3f),(a,8f),(b,8.3f),(a,8.3f)})ring.Add(new(new Vector3(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius,0),Vector3.UnitZ,Vector2.Zero));}
        effect.World=Matrix.CreateRotationY(-flight.Yaw)*Matrix.CreateTranslation(gate);effect.DiffuseColor=palette[1].ToVector3();foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,ring.ToArray(),0,ring.Count/3);}
    }
    public void Dispose(){effect.Dispose();glyphs.Dispose();}
}
