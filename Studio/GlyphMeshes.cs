using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;
/// <summary>Cached watertight voxel extrusions of the bundled font silhouettes, lit on the GPU.</summary>
public sealed class GlyphMeshes : IDisposable
{
    readonly GraphicsDevice device;readonly BasicEffect effect;
    readonly Dictionary<(SpriteFont,char),(VertexBuffer Buffer,int Count)> cache=new();
    public GlyphMeshes(GraphicsDevice device){this.device=device;effect=new(device);effect.EnableDefaultLighting();effect.PreferPerPixelLighting=true;effect.AmbientLightColor=new(.4f);effect.SpecularColor=new(.6f);effect.SpecularPower=24;}
    (VertexBuffer,int) Mesh(SpriteFont font,char c)
    {
        if(cache.TryGetValue((font,c),out var found))return found;
        var glyph=font.GetGlyphs().GetValueOrDefault(c,font.GetGlyphs()['?']);var rect=glyph.BoundsInTexture;var colors=new Color[rect.Width*rect.Height];font.Texture.GetData(0,rect,colors,0,colors.Length);
        const int step=1;int w=(rect.Width+step-1)/step,h=(rect.Height+step-1)/step;var mask=new bool[w,h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)mask[x,y]=colors[Math.Min(rect.Height-1,y*step)*rect.Width+Math.Min(rect.Width-1,x*step)].A>100;
        bool Solid(int x,int y)=>x>=0&&y>=0&&x<w&&y<h&&mask[x,y];var vertices=new List<VertexPositionNormalTexture>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n){foreach(var p in new[]{a,b,c,a,c,d})vertices.Add(new(p,n,Vector2.Zero));}
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(mask[x,y]){int begin=x;while(x+1<w&&mask[x+1,y])x++;float l=begin-rect.Width/2f,r=x+1-rect.Width/2f,t=y-rect.Height/2f,b=t+1;Quad(new(l,t,8),new(r,t,8),new(r,b,8),new(l,b,8),Vector3.UnitZ);Quad(new(r,t,-8),new(l,t,-8),new(l,b,-8),new(r,b,-8),-Vector3.UnitZ);}
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(mask[x,y])
        {
            float l=x*step-rect.Width/2f,r=l+step,t=y*step-rect.Height/2f,b=t+step,z=8;

            if(!Solid(x-1,y))Quad(new(l,t,-z),new(l,t,z),new(l,b,z),new(l,b,-z),-Vector3.UnitX);
            if(!Solid(x+1,y))Quad(new(r,t,z),new(r,t,-z),new(r,b,-z),new(r,b,z),Vector3.UnitX);
            if(!Solid(x,y-1))Quad(new(l,t,-z),new(r,t,-z),new(r,t,z),new(l,t,z),-Vector3.UnitY);
            if(!Solid(x,y+1))Quad(new(l,b,z),new(r,b,z),new(r,b,-z),new(l,b,-z),Vector3.UnitY);
        }
        if(vertices.Count==0)vertices.AddRange(new VertexPositionNormalTexture[3]);
        if(cache.Count>=128){var key=cache.Keys.First();cache[key].Buffer.Dispose();cache.Remove(key);}
        var buffer=new VertexBuffer(device,typeof(VertexPositionNormalTexture),vertices.Count,BufferUsage.WriteOnly);buffer.SetData(vertices.ToArray());return cache[(font,c)]=(buffer,vertices.Count/3);
    }
    public void Draw(SpriteFont font,char c,Matrix world,Matrix view,Matrix projection,Color color,float alpha=1)
    {
        var (buffer,count)=Mesh(font,c);device.SetVertexBuffer(buffer);device.DepthStencilState=DepthStencilState.Default;device.BlendState=BlendState.AlphaBlend;device.RasterizerState=RasterizerState.CullNone;
        effect.World=world;effect.View=view;effect.Projection=projection;effect.DiffuseColor=color.ToVector3();effect.Alpha=alpha;foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawPrimitives(PrimitiveType.TriangleList,0,count);}device.SetVertexBuffer(null);
    }
    public void Dispose(){foreach(var mesh in cache.Values)mesh.Buffer.Dispose();effect.Dispose();}
}
