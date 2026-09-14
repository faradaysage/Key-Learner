using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;

public readonly record struct MathToken(Vector2 Position,float Radius,Color Color,int Index);
/// <summary>One cached sphere mesh, the suite's existing toon effect, and a flat orthographic camera.</summary>
public sealed class MathBallRenderer : IDisposable
{
    readonly GraphicsDevice device;readonly Effect toon;readonly VertexBuffer sphere;readonly int triangles;
    public MathBallRenderer(GraphicsDevice device,Effect toon){
        this.device=device;this.toon=toon;
        var vertices=new List<VertexPositionNormalTexture>();const int rows=16,columns=24;
        Vector3 Point(int row,int col){float lat=row*MathF.PI/rows,lon=col*MathF.Tau/columns;return new(MathF.Sin(lat)*MathF.Cos(lon),MathF.Cos(lat),MathF.Sin(lat)*MathF.Sin(lon));}
        void Add(Vector3 p)=>vertices.Add(new(p,p,Vector2.Zero));
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){var a=Point(y,x);var b=Point(y+1,x);var c=Point(y+1,x+1);var d=Point(y,x+1);foreach(var p in new[]{a,b,c,a,c,d})Add(p);}
        triangles=vertices.Count/3;sphere=new(device,typeof(VertexPositionNormalTexture),vertices.Count,BufferUsage.WriteOnly);sphere.SetData(vertices.ToArray());
    }
    public void Draw(IEnumerable<MathToken> balls){
        var projection=Matrix.CreateOrthographicOffCenter(0,1440,900,0,-500,500);
        device.BlendState=BlendState.AlphaBlend;device.RasterizerState=RasterizerState.CullNone;device.SetVertexBuffer(sphere);
        foreach(var b in balls){
            var world=Matrix.CreateScale(b.Radius)*Matrix.CreateTranslation(b.Position.X,b.Position.Y,0);
            toon.Parameters["WorldViewProjection"].SetValue(world*projection);toon.Parameters["NormalMatrix"].SetValue(Matrix.Transpose(Matrix.Invert(world)));toon.Parameters["Tint"].SetValue(b.Color.ToVector3());toon.Parameters["Alpha"].SetValue(1f);toon.Parameters["OutlineWidth"].SetValue(1.8f);
            device.DepthStencilState=DepthStencilState.None;toon.CurrentTechnique=toon.Techniques["Outline"];foreach(var pass in toon.CurrentTechnique.Passes){pass.Apply();device.DrawPrimitives(PrimitiveType.TriangleList,0,triangles);}
            device.DepthStencilState=DepthStencilState.Default;toon.CurrentTechnique=toon.Techniques["Toon"];foreach(var pass in toon.CurrentTechnique.Passes){pass.Apply();device.DrawPrimitives(PrimitiveType.TriangleList,0,triangles);}
        }
        device.SetVertexBuffer(null);
    }
    public void Dispose()=>sphere.Dispose();
}
