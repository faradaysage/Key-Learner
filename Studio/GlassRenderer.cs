using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;
public sealed class GlassRenderer
{
    readonly GraphicsDevice device;readonly Effect effect;
    public GlassRenderer(GraphicsDevice device,Effect effect){this.device=device;this.effect=effect;}
    public void Draw(GlassSheet sheet,Texture2D scene,SpriteBatch batch,Texture2D pixel,bool shading,bool gentle)
    {
        if(sheet.Panes.Count<=1)return;
        var matrix=Matrix.CreateOrthographicOffCenter(0,1440,900,0,-1000,1000);effect.Parameters["MatrixTransform"].SetValue(matrix);effect.Parameters["Texture"].SetValue(scene);
        device.BlendState=BlendState.AlphaBlend;device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
        var edges=new List<(Vector2 A,Vector2 B,float Alpha)>();
        foreach(var pane in sheet.Panes)
        {
            var origin=pane.Points.Aggregate(System.Numerics.Vector2.Zero,(a,b)=>a+b)/pane.Points.Length;
            var tilt=sheet.Falling?new Vector2(MathF.Sin(pane.Angle),MathF.Sin(pane.Angle*.7f)) :new Vector2(MathF.Sin(pane.Center.X*.01f),MathF.Cos(pane.Center.Y*.01f))*.12f*sheet.Pressure;
            var rotation=Matrix.CreateRotationX(sheet.Falling?pane.Angle*.7f:tilt.Y*.08f)*Matrix.CreateRotationY(sheet.Falling?pane.Angle:tilt.X*.08f)*Matrix.CreateRotationZ(sheet.Falling?pane.Angle*.25f:0);
            Vector3 Position(System.Numerics.Vector2 p)=>Vector3.Transform(new Vector3(p.X-origin.X,p.Y-origin.Y,0),rotation)+new Vector3(pane.Center.X,pane.Center.Y,0);
            var fade=sheet.Falling?Math.Clamp(1-pane.Age/3,0,1):Math.Min(1,sheet.Pressure*3);
            if(shading){var vertices=new VertexPositionColorTexture[(pane.Points.Length-2)*3];int at=0;for(int i=1;i<pane.Points.Length-1;i++)foreach(var p in new[]{pane.Points[0],pane.Points[i],pane.Points[i+1]})vertices[at++]=new(Position(p),Color.White,new(p.X/1440,p.Y/900));effect.Parameters["Tilt"].SetValue(tilt);effect.Parameters["Opacity"].SetValue(fade*(sheet.Falling?.82f:.45f));foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,vertices.Length/3);}}
            for(int i=0;i<pane.Points.Length;i++){var a=Position(pane.Points[i]);var b=Position(pane.Points[(i+1)%pane.Points.Length]);edges.Add((new(a.X,a.Y),Vector2.Lerp(new(a.X,a.Y),new(b.X,b.Y),pane.Reveal),fade));}
        }
        RenderSpace.Begin(batch);foreach(var edge in edges){var d=edge.B-edge.A;batch.Draw(pixel,edge.A,null,new Color(190,224,241)*(edge.Alpha*.55f),MathF.Atan2(d.Y,d.X),Vector2.Zero,new Vector2(d.Length(),1.25f),SpriteEffects.None,0);}batch.End();
    }
}
