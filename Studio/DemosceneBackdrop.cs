using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;

/// <summary>Original low-resolution harmonic fields, smoothly scaled by the GPU.</summary>
public sealed class DemosceneBackdrop : IDisposable
{
    private const int W=240,H=150;
    private readonly Texture2D texture;
    private readonly Color[] pixels=new Color[W*H];
    private float time,accumulator,energy;
    private Vector2 focus=new(.5f);
    public DemosceneBackdrop(GraphicsDevice device)=>texture=new(device,W,H);
    public void Excite(InputContext input){energy=Math.Min(1,energy+.16f);focus=Vector2.Lerp(focus,new((float)input.X,(float)input.Y),.12f);}
    public void Update(float dt,Settings settings,Color[] palette)
    {
        time+=dt*(settings.GentleMotion?.18f:.45f);energy*=MathF.Exp(-dt*.8f);accumulator+=dt;
        if(settings.Theme==Mood.BlackAndWhite || settings.Backdrop==Backdrop.Aurora || accumulator<1f/30)return;
        accumulator%=1f/30;
        for(var y=0;y<H;y++)for(var x=0;x<W;x++)
        {
            var u=(x/(float)W-focus.X)*2;var v=(y/(float)H-focus.Y)*1.25f;
            var radius=MathF.Sqrt(u*u+v*v);float wave;
            if(settings.Backdrop==Backdrop.Vortex)
                wave=MathF.Sin(radius*14-time*2+MathF.Sin(MathF.Atan2(v,u)*3+time)*1.4f);
            else
                wave=(MathF.Sin(u*5+time)+MathF.Sin(v*7-time*.8f)+MathF.Sin((u+v)*4+time*.6f)+MathF.Sin(radius*9-time*1.4f))*.25f;
            var band=(wave+1)*1.5f;var index=Math.Clamp((int)band,0,2);
            var color=Color.Lerp(palette[index],palette[index+1],band-index);
            var ribbon=MathF.Pow(Math.Max(0,1-Math.Abs(wave)*2),4);
            pixels[y*W+x]=color*(.025f+ribbon*(.095f+energy*.08f));
        }
        texture.SetData(pixels);
    }
    public void Draw(SpriteBatch batch,int width,int height,Settings settings){if(settings.Theme!=Mood.BlackAndWhite && settings.Backdrop!=Backdrop.Aurora)batch.Draw(texture,new Rectangle(0,0,width,height),Color.White);}
    public void Dispose()=>texture.Dispose();
}
