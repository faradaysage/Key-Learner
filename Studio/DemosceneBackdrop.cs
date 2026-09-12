using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;

/// <summary>Original low-resolution harmonic fields, smoothly scaled by the GPU.</summary>
public sealed class DemosceneBackdrop : IDisposable
{
    private const int W=240,H=150;
    private readonly Texture2D texture,star;
    private readonly StarfieldMotion stars=new();
    private readonly Color[] pixels=new Color[W*H];
    private float time,accumulator,energy;
    private Vector2 focus=new(.5f);
    public DemosceneBackdrop(GraphicsDevice device)
    {
        texture=new(device,W,H);star=new(device,32,32);var data=new Color[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {
            var d=Vector2.Distance(new(x+.5f,y+.5f),new(16))/16;
            data[y*32+x]=Color.White*Math.Clamp((1-d)*4,0,1);
        }
        star.SetData(data);
    }
    public void Excite(InputContext input){energy=Math.Min(1,energy+.16f);focus=Vector2.Lerp(focus,new((float)input.X,(float)input.Y),.12f);}
    public void Update(float dt,Settings settings,Color[] palette)
    {
        stars.Step(dt,(float)settings.StarSpeed,settings.GentleMotion);
        time+=dt*(settings.GentleMotion?.18f:.45f);energy*=MathF.Exp(-dt*.8f);accumulator+=dt;
        if(settings.Backdrop is Backdrop.Starfield or Backdrop.RotatingStars || settings.Theme==Mood.BlackAndWhite || settings.Backdrop==Backdrop.Aurora || accumulator<1f/30)return;
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
    public void Draw(SpriteBatch batch,int width,int height,Settings settings)
    {
        if(settings.Backdrop is Backdrop.Starfield or Backdrop.RotatingStars)
        {
            var rotating=settings.Backdrop==Backdrop.RotatingStars;
            var count=Math.Min(2400,settings.StarCount*(rotating?2:1));
            for(int i=0;i<count;i++)
            {
                var point=stars.Project(i,rotating);
                var position=new Vector2(width*.5f+point.X*height*.62f,height*.5f+point.Y*height*.62f);
                if(position.X<0 || position.X>width || position.Y<0 || position.Y>height)continue;
                var near=rotating?Math.Clamp(1-point.Z/11,0,1):Math.Clamp(1-point.Z/4,0,1);
                var brightness=.13f+near*.68f;
                var diameter=rotating?1+near*3:.7f+near*4;
                var color=Color.White;
                if(rotating && settings.Theme!=Mood.BlackAndWhite)
                    color=Color.Lerp(Color.White,new Color(115,175,255),.25f+.2f*MathF.Sin(stars.Time*.12f+i*.02f));
                batch.Draw(star,position,null,color*brightness,0,new Vector2(16),diameter/32,SpriteEffects.None,0);
                if(near>.65f)batch.Draw(star,position,null,color*(brightness*.07f),0,new Vector2(16),diameter*3/32,SpriteEffects.None,0);
            }
        }
        else if(settings.Theme!=Mood.BlackAndWhite && settings.Backdrop!=Backdrop.Aurora)
            batch.Draw(texture,new Rectangle(0,0,width,height),Color.White);
    }
    public void Dispose(){texture.Dispose();star.Dispose();}
}
