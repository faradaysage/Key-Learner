using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;
public sealed class FlightRenderer : IDisposable
{
    public Effect? ToonShader {set=>glyphs.ToonShader=value;}
    readonly GraphicsDevice device;readonly BasicEffect effect;readonly GlyphMeshes glyphs;readonly SpriteFont font;
    VertexPositionColor[] scenery=[];int cellX=int.MinValue,cellZ=int.MinValue,detail;ExplorerKind builtKind;
    readonly List<VertexPositionColor> vertices=new();
    bool monochrome,builtMono;
    public FlightRenderer(GraphicsDevice device,SpriteFont font){this.device=device;this.font=font;effect=new(device){VertexColorEnabled=true,FogEnabled=true,FogStart=200,FogEnd=650};glyphs=new(device);}
    static Vector3 V(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
    void Tri(Vector3 a,Vector3 b,Vector3 c,Color tint){var n=Vector3.Cross(b-a,c-a);float light=n.LengthSquared()<.00001f?1:.64f+.36f*MathF.Abs(Vector3.Dot(Vector3.Normalize(n),Vector3.Normalize(new Vector3(-.3f,1,.5f))));var color=new Color(tint.ToVector3()*light);if(monochrome){byte gray=(byte)(color.R*.299f+color.G*.587f+color.B*.114f);color=new(gray,gray,gray);}vertices.Add(new(a,color));vertices.Add(new(b,color));vertices.Add(new(c,color));}
    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Tri(a,b,c,color);Tri(a,c,d,color);}
    void Box(Vector3 p,Vector3 size,Color color){var a=p+new Vector3(-size.X,0,-size.Z)*.5f;var b=a+new Vector3(size.X,0,0);var c=b+new Vector3(0,0,size.Z);var d=a+new Vector3(0,0,size.Z);var up=new Vector3(0,size.Y,0);Quad(a,b,b+up,a+up,color);Quad(b,c,c+up,b+up,color);Quad(c,d,d+up,c+up,color);Quad(d,a,a+up,d+up,color);Quad(a+up,b+up,c+up,d+up,color);}
    void Cone(Vector3 p,float radius,float height,Color color){for(int i=0;i<6;i++){float a=i*MathF.Tau/6,b=(i+1)*MathF.Tau/6;Tri(p+new Vector3(MathF.Cos(a)*radius,0,MathF.Sin(a)*radius),p+new Vector3(0,height,0),p+new Vector3(MathF.Cos(b)*radius,0,MathF.Sin(b)*radius),color);}}
    static Color Ground(Region r)=>r switch{Region.Mountains=>new(120,131,142),Region.City=>new(83,101,105),Region.Lakes=>new(110,169,101),Region.River=>new(94,159,85),Region.Town=>new(160,185,94),Region.Tundra=>new(216,235,242),_=>new(47,124,77)};
    void Build(FlightModel f,int resolution)
    {
        int x=(int)MathF.Floor(f.Position.X/50),z=(int)MathF.Floor(f.Position.Z/50);if(x==cellX&&z==cellZ&&resolution==detail&&f.Kind==builtKind&&monochrome==builtMono)return;builtMono=monochrome;cellX=x;cellZ=z;detail=resolution;builtKind=f.Kind;vertices.Clear();
        bool ocean=f.Kind==ExplorerKind.Dolphin,race=f.Kind==ExplorerKind.Racer;float span=1300,step=span/resolution;
        Vector3 P(int i,int j){float px=x*50-span/2+i*step,pz=z*50-span/2+j*step;return new(px,ocean?ExplorerWorld.Bed(px,pz):ExplorerWorld.Land(px,pz,race),pz);}
        if(race){
            // Terrain strips share the road's longitudinal samples and stop at its shoulders.
            // No coarse terrain triangle spans the asphalt, at any detail setting.
            int columns=Math.Max(8,resolution/2);
            foreach(int side in new[]{-1,1})for(int i=0;i<columns;i++)for(float rz=z*50-650;rz<z*50+650;rz+=10){
                Vector3 T(int col,float pz)=>V(ExplorerWorld.RoadTerrainPoint(side,col,pz,columns));
                var p=T(i,rz);Quad(p,T(i+1,rz),T(i+1,rz+10),T(i,rz+10),Ground(ExplorerWorld.Area(rz)));
            }
        }else for(int i=0;i<resolution;i++)for(int j=0;j<resolution;j++){var p=P(i,j);var color=ocean?new Color(192,176,112):Ground(ExplorerWorld.Area(p.Z));if(!ocean&&p.Y>75)color=new(232,238,243);Quad(p,P(i+1,j),P(i+1,j+1),P(i,j+1),color);}
        for(int i=-16;i<=16;i++)for(int j=-16;j<=16;j++){
            int gx=(int)MathF.Floor(x*50/32f)+i,gz=(int)MathF.Floor(z*50/32f)+j;float seed=MathF.Sin(gx*127.1f+gz*311.7f)*43758.5453f;seed-=MathF.Floor(seed);
            float px=gx*32+seed*17,pz=gz*32+seed*13,py=ocean?ExplorerWorld.Bed(px,pz):ExplorerWorld.Land(px,pz,race);var area=ExplorerWorld.Area(pz);
            if(race&&Math.Abs(px-ExplorerWorld.Road(pz))<40)continue;
            if(ocean){if(seed<.3f){Cone(new(px,py,pz),4,12+seed*35,new(224,113,161));Cone(new(px+4,py,pz),3,8,new(253,177,76));}else if(seed>.55f){for(int k=0;k<3;k++)Quad(new(px+k,py,pz),new(px+k+2,py,pz),new(px+k+MathF.Sin(seed*20)*5,py+20+seed*22,pz),new(px+k-1,py+18+seed*22,pz),new(32,145,111));}continue;}
            if(py<3)continue;
            if(area is Region.City or Region.Town){
                if(seed<.18f)continue;float tall=area==Region.City?16+seed*80:7+seed*8;var p=new Vector3(px,py,pz);Box(p,new(12,tall,12),area==Region.City?new(90+(int)(seed*90),119,160):new(236,199,145));
                if(area==Region.Town)Cone(p+new Vector3(0,tall,0),10,6,new(192,74,63));else {for(int floor=4;floor<tall;floor+=7)Box(p+new Vector3(0,floor,-6.1f),new(7,2,.2f),new(255,229,134));}
                Quad(new(px-16,py+.15f,pz-16),new(px+16,py+.15f,pz-16),new(px+16,py+.15f,pz-12),new(px-16,py+.15f,pz-12),new(58,64,76));
            }else if(area==Region.Forest || seed>.78f && area!=Region.Tundra){Box(new(px,py,pz),new(1.5f,8,1.5f),new(99,72,48));Cone(new(px,py+3,pz),5+seed*4,16+seed*15,new(22,92+(int)(seed*40),62));}
            else if(area==Region.Tundra&&seed>.85f)Box(new(px,py,pz),new(8,5,6),new(146,174,190));
        }
        if(!ocean){float l=x*50-650,t=z*50-650;Quad(new(l,2,t),new(l+1300,2,t),new(l+1300,2,t+1300),new(l,2,t+1300),new(38,139,191));}
        if(race){
            for(float rz=z*50-650;rz<z*50+650;rz+=10){float x1=ExplorerWorld.Road(rz),x2=ExplorerWorld.Road(rz+10);Quad(new(x1-18,5.6f,rz),new(x1+18,5.6f,rz),new(x2+18,5.6f,rz+10),new(x2-18,5.6f,rz+10),new(48,52,66));
                Color stripe=((int)MathF.Floor(rz/10)&1)==0?Color.White:new(237,64,68);
                foreach(int side in new[]{-1,1})Quad(new(x1+side*18,5.7f,rz),new(x1+side*20,5.7f,rz),new(x2+side*20,5.7f,rz+10),new(x2+side*18,5.7f,rz+10),stripe);
                if(((int)MathF.Floor(rz/10)&1)==0)foreach(int lane in new[]{-1,1})Quad(new(x1+lane*6-.2f,5.8f,rz),new(x1+lane*6+.2f,5.8f,rz),new(x2+lane*6+.2f,5.8f,rz+6),new(x2+lane*6-.2f,5.8f,rz+6),Color.White);
            }
        }
        scenery=vertices.ToArray();vertices.Clear();
    }
    void DrawMesh(VertexPositionColor[] mesh){if(mesh.Length==0)return;foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,mesh,0,mesh.Length/3);}}
    public void Draw(FlightModel f,Settings settings,Color[] palette)
    {
        bool ocean=f.Kind==ExplorerKind.Dolphin,race=f.Kind==ExplorerKind.Racer,mono=settings.Theme==Mood.BlackAndWhite;
        var sky=ocean?new Color(13,100,142):new Color(124,187,217);if(mono)sky=new(105,105,105);device.Clear(sky);monochrome=mono;Build(f,settings.TerrainDetail);
        var pos=V(f.Position);var forward=V(f.Forward);var up=V(f.Up);var camera=pos-forward*(race?18:22)+up*(race?7:6);
        var view=Matrix.CreateLookAt(camera,pos+forward*35,up);var projection=Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(race?72:70),device.Viewport.AspectRatio,.3f,900);
        device.DepthStencilState=DepthStencilState.Default;device.RasterizerState=RasterizerState.CullNone;device.BlendState=BlendState.Opaque;
        effect.World=Matrix.Identity;effect.View=view;effect.Projection=projection;effect.FogColor=sky.ToVector3();effect.FogStart=ocean?80:240;effect.FogEnd=ocean?330:700;effect.DiffuseColor=Vector3.One;DrawMesh(scenery);
        glyphs.UseToon=settings.ToonAssets;var gate=V(f.Gate);var m=f.LetterFacing;var face=new Matrix(m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44);
        if(f.Collected<f.Word.Length)glyphs.Draw(font,char.ToUpperInvariant(f.Letter),Matrix.CreateScale(.13f,-.13f,.13f)*face*Matrix.CreateTranslation(gate),view,projection,palette[f.Collected%4]);
        vertices.Clear();float flap=MathF.Sin(f.Time*(f.Speed>55?10:5))*.65f;
        if(race){Box(new(0,-.9f,0),new(3.4f,1.1f,6),new(235,51,66));Box(new(0,.2f,.1f),new(2.5f,.75f,2.4f),new(62,173,223));foreach(int side in new[]{-1,1})foreach(int wheel in new[]{-1,1})Box(new(side*1.8f,-1,wheel*1.7f),new(.7f,1,1.2f),new(22,25,36));Box(new(0,.3f,2.6f),new(4,.25f,.7f),new(252,211,54));}
        else if(ocean){
            Color blue=new(103,184,212);for(int i=0;i<8;i++){float a=i*MathF.Tau/8,b=(i+1)*MathF.Tau/8;var p=new Vector3(MathF.Cos(a),MathF.Sin(a)*.7f,0);var q=new Vector3(MathF.Cos(b),MathF.Sin(b)*.7f,0);Tri(new(0,0,-4),p,q,blue);Tri(new(0,flap*.3f,3.5f),q,p,blue);}
            Tri(new(0,.5f,.5f),new(0,2,1.5f),new(0,.4f,2),blue);Tri(new(0,flap*.3f,3),new(-2.6f,flap,4),new(2.6f,flap,4),blue);Tri(new(-.7f,0,-.4f),new(-3,-.2f,1.5f),new(-.5f,0,1.4f),blue);Tri(new(.7f,0,-.4f),new(.5f,0,1.4f),new(3,-.2f,1.5f),blue);
        }else{
            var color=palette[2];Tri(new(0,0,-3),new(-.8f,.4f,1),new(.8f,.4f,1),color);Tri(new(0,0,-3),new(.8f,.4f,1),new(0,-.65f,1),color);Tri(new(0,0,-3),new(0,-.65f,1),new(-.8f,.4f,1),color);Tri(new(-.4f,0,-.6f),new(-5,flap,1),new(-1.2f,.15f,1.6f),color);Tri(new(.4f,0,-.6f),new(1.2f,.15f,1.6f),new(5,flap,1),color);Tri(new(0,0,1),new(-1.4f,.2f,3),new(1.4f,.2f,3),color);Cone(new(0,0,-3),.4f,1,new(255,131,59));
        }
        effect.World=Matrix.CreateRotationZ(f.Roll)*Matrix.CreateRotationX(f.Pitch)*Matrix.CreateRotationY(-f.Yaw)*Matrix.CreateTranslation(pos);DrawMesh(vertices.ToArray());vertices.Clear();
        if(f.Collected<f.Word.Length){
        float radius=8+(f.Pulse>0?MathF.Sin(f.Time*10):0);for(int i=0;i<48;i++){float a=i*MathF.Tau/48,b=(i+1)*MathF.Tau/48;Quad(new(MathF.Cos(a)*radius,MathF.Sin(a)*radius,0),new(MathF.Cos(b)*radius,MathF.Sin(b)*radius,0),new(MathF.Cos(b)*(radius+.35f),MathF.Sin(b)*(radius+.35f),0),new(MathF.Cos(a)*(radius+.35f),MathF.Sin(a)*(radius+.35f),0),f.Pulse>0?Color.White:f.LastLetter?Color.Gold:f.FirstLetter?new Color(80,245,155):palette[1]);}
        effect.World=face*Matrix.CreateTranslation(gate);DrawMesh(vertices.ToArray());vertices.Clear();
        if(f.LastLetter){for(int i=0;i<12;i++){float a=i*MathF.Tau/12;Cone(new(MathF.Cos(a)*10,MathF.Sin(a)*10,0),.6f,1.3f,Color.Gold);}effect.World=face*Matrix.CreateTranslation(gate);DrawMesh(vertices.ToArray());vertices.Clear();}
        }
        if(f.RewardRemaining>0){
            float t=2-f.RewardRemaining;var center=pos+forward*46+up*12;
            for(int i=0;i<f.Word.Length;i++){
                float spread=(i-(f.Word.Length-1)*.5f)*(8+t*9);
                var location=center+V(f.Right)*spread+up*(t*9-MathF.Abs(spread)*t*.12f);
                glyphs.Draw(font,char.ToUpperInvariant(f.Word[i]),Matrix.CreateScale(.11f,-.11f,.11f)*face*Matrix.CreateTranslation(location),view,projection,palette[i%4],Math.Clamp(f.RewardRemaining,0,1));
            }
            for(int i=0;i<100;i++){
                float angle=i*2.399963f,speed=9+i%13;var p=new Vector3(MathF.Cos(angle)*speed*t,MathF.Sin(angle)*speed*t+9*t-5*t*t,(i%9-4)*t);
                float size=.35f+f.RewardRemaining*.25f;Tri(p+new Vector3(-size,0,0),p+new Vector3(size,0,0),p+new Vector3(0,size*2,0),palette[i%4]);
            }
            effect.World=face*Matrix.CreateTranslation(center);effect.Alpha=Math.Min(1,f.RewardRemaining);device.BlendState=BlendState.Additive;DrawMesh(vertices.ToArray());effect.Alpha=1;device.BlendState=BlendState.Opaque;vertices.Clear();
        }
        if(race && f.OffRoad>.5f){
            for(int i=0;i<48;i++){
                float age=(f.Time*1.7f+i*.618f)%1;int side=i%2==0?-1:1;
                var p=pos-forward*(3+age*18)+V(f.Right)*(side*(1.7f+age*3))+Vector3.UnitY*(age*4-age*age*3-.8f);
                float size=.12f+age*.55f;Tri(p,p+new Vector3(size,size,0),p+new Vector3(-size,size,0),new Color(164,111,64)*(1-age));
            }
            effect.World=Matrix.Identity;DrawMesh(vertices.ToArray());vertices.Clear();
        }
        if(f.Pulse>0){
            float wave=4+(1-f.Pulse)*65;
            for(int i=0;i<48;i++){float a=i*MathF.Tau/48,b=(i+1)*MathF.Tau/48;Quad(new(MathF.Cos(a)*wave,MathF.Sin(a)*wave,0),new(MathF.Cos(b)*wave,MathF.Sin(b)*wave,0),new(MathF.Cos(b)*(wave+1),MathF.Sin(b)*(wave+1),0),new(MathF.Cos(a)*(wave+1),MathF.Sin(a)*(wave+1),0),ocean?new Color(99,230,255):palette[1]);}
            effect.World=face*Matrix.CreateTranslation(pos+forward*8);effect.Alpha=f.Pulse*.45f;device.BlendState=BlendState.Additive;DrawMesh(vertices.ToArray());effect.Alpha=1;device.BlendState=BlendState.Opaque;vertices.Clear();
        }
        if(ocean){for(int i=0;i<45;i++){float phase=i*2.399f;var p=pos+new Vector3(MathF.Sin(phase)*50,MathF.Sin(f.Time*.5f+i)*20, -20-(i%9)*16);Tri(p,p+new Vector3(1.5f,.6f,0),p+new Vector3(1.5f,-.6f,0),new(241,199,83));}effect.World=Matrix.Identity;DrawMesh(vertices.ToArray());vertices.Clear();}
    }
    public void Dispose(){effect.Dispose();glyphs.Dispose();}
}
