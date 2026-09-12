using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;

/// <summary>Low-resolution scalar fields provide continuous liquid surfaces and
/// shaded droplet boundaries, upsampled smoothly by the GPU.</summary>
public sealed class LivingFields : IDisposable
{
    public BlobWorld Blobs {get;}=new();
    public bool SpaceHeld {get;set;}
    public float FireLevel {get;private set;}
    private float pulse,holdTime;
    public float FireDepth {get;private set;}
    public ParticleFire Fire {get;}
    private LiquidRenderer? liquid;
    private readonly GraphicsDevice device;
    public LivingFields(GraphicsDevice device){this.device=device;Fire=new(device);}
    public void Configure(Effect density,Effect surface)=>liquid=new(device,density,surface);
    public void Ignite()=>pulse=.7f;
    public void Clear(){Blobs.Clear();SpaceHeld=false;pulse=0;FireLevel=0;holdTime=0;FireDepth=0;Fire.Clear();}
    public void Update(float dt,Settings settings,int width,int height,int blobBudget)
    {
        dt=Math.Clamp(dt,0,.1f);pulse=Math.Max(0,pulse-dt);
        FireLevel=MathHelper.Lerp(FireLevel,SpaceHeld||pulse>0?1:0,1-MathF.Exp(-dt*5));
        holdTime=SpaceHeld?Math.Min(15,holdTime+dt):Math.Max(0,holdTime-dt*5);
        FireDepth=Math.Min(height*.82f,220+(settings.GrowingFire?holdTime*42:0))*FireLevel;
        Fire.DepthScale=Math.Max(1,FireDepth/220);
        var steps=Math.Max(1,(int)Math.Ceiling(dt*120));
        for(var i=0;i<steps;i++)Blobs.Step(dt/steps,width,height,(float)settings.Gravity*.55f,(float)settings.Bounce,Math.Clamp(blobBudget,0,180));
        Fire.Update(dt,FireLevel,settings,width,height,Math.Max(0,blobBudget-Blobs.Drops.Count));
    }
    public void Prepare(Color[] palette,Settings settings)=>liquid?.Prepare(Blobs,palette,settings);
    public void Draw(SpriteBatch batch,int width,int height)=>liquid?.Draw(batch);
    public void Dispose(){liquid?.Dispose();Fire.Dispose();}
}
