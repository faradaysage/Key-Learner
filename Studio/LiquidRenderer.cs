using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;
public sealed class LiquidRenderer : IDisposable
{
    readonly GraphicsDevice device;readonly SpriteBatch batch;readonly Texture2D pixel;readonly Effect density,surface;
    readonly BlendState add=new(){ColorSourceBlend=Blend.One,ColorDestinationBlend=Blend.One,AlphaSourceBlend=Blend.One,AlphaDestinationBlend=Blend.One};
    RenderTarget2D? field;
    public LiquidRenderer(GraphicsDevice device,Effect density,Effect surface){this.device=device;this.density=density;this.surface=surface;batch=new(device);pixel=new(device,1,1);pixel.SetData(new[]{Color.White});}
    public void Prepare(BlobWorld world,Color[] palette,Settings settings)
    {
        int w=Math.Clamp((int)(device.PresentationParameters.BackBufferWidth*settings.RenderScale*settings.LiquidScale),288,4096),h=Math.Clamp((int)(device.PresentationParameters.BackBufferHeight*settings.RenderScale*settings.LiquidScale),180,4096);
        if(field==null || field.Width!=w || field.Height!=h){field?.Dispose();field=new(device,w,h,false,SurfaceFormat.HalfVector4,DepthFormat.None);}
        var targets=device.GetRenderTargets();device.SetRenderTarget(field);device.Clear(Color.Transparent);
        density.Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(0,w,h,0,0,1));
        batch.Begin(SpriteSortMode.Immediate,add,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,density);
        foreach(var d in world.Drops){density.Parameters["Time"].SetValue(d.Age);density.Parameters["Wobble"].SetValue(d.Wobble);var r=d.Radius*2.1f;var alpha=Math.Clamp((d.Life-d.Age)/1.2f,0,1);batch.Draw(pixel,new Rectangle((int)((d.Position.X-r)*w/1440),(int)((d.Position.Y-r)*h/900),(int)(r*2*w/1440),(int)(r*2*h/900)),new Color(new Vector4(d.Tint.X*alpha,d.Tint.Y*alpha,d.Tint.Z*alpha,alpha)));}
        batch.End();device.SetRenderTargets(targets);
        for(int i=0;i<4;i++)surface.Parameters["C"+i].SetValue(palette[i].ToVector3());surface.Parameters["Texel"].SetValue(new Vector2(1f/w,1f/h));
    }
    public void Draw(SpriteBatch b)
    {
        if(field==null)return;b.End();var v=device.Viewport;surface.Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(0,v.Width,v.Height,0,0,1));
        b.Begin(SpriteSortMode.Immediate,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,surface);b.Draw(field,v.Bounds,Color.White);b.End();RenderSpace.Begin(b);
    }
    public void Dispose(){field?.Dispose();batch.Dispose();pixel.Dispose();add.Dispose();}
}
