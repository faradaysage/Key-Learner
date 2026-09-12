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
    private const int BW=360,BH=225;
    private readonly float[] density=new float[BW*BH];
    private readonly Vector3[] tint=new Vector3[BW*BH],edges=new Vector3[BW*BH];
    private readonly Color[] blobPixels=new Color[BW*BH];
    private readonly Texture2D blobTexture;
    private readonly Random random=new(421);
    public LivingFields(GraphicsDevice device) {blobTexture=new(device,BW,BH);Fire=new(device);}
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
    public void Prepare(Color[] palette)
    {
        Array.Clear(density);Array.Clear(tint);Array.Clear(edges);
        foreach(var drop in Blobs.Drops)
        {
            var dropColor=new Vector3(drop.Tint.X,drop.Tint.Y,drop.Tint.Z);
            var nearest=0;var distance=float.MaxValue;
            for(var k=0;k<palette.Length;k++){var d=Vector3.DistanceSquared(dropColor,palette[k].ToVector3());if(d<distance){distance=d;nearest=k;}}
            var edgeColor=palette[(nearest+1)%palette.Length].ToVector3();
            var p=drop.Position/4;var radius=drop.Radius/4*2.1f;
            var fade=Math.Clamp((drop.Life-drop.Age)/1.2f,0,1);
            for(var y=Math.Max(0,(int)(p.Y-radius));y<Math.Min(BH,(int)(p.Y+radius+1));y++)
                for(var x=Math.Max(0,(int)(p.X-radius));x<Math.Min(BW,(int)(p.X+radius+1));x++)
                {
                    var d2=(new Vector2(x+.5f,y+.5f)-new Vector2(p.X,p.Y)).LengthSquared()/(radius*radius);
                    var angle=MathF.Atan2(y-p.Y,x-p.X);
                    var shape=1+drop.Wobble*(.13f*MathF.Sin(angle*3+drop.Age*2)+.09f*MathF.Sin(angle*5-drop.Age*3));
                    d2/=shape*shape;
                    if(d2>=1)continue;
                    var contribution=(1-d2)*(1-d2)*fade;var index=y*BW+x;
                    edges[index]+=edgeColor*contribution;density[index]+=contribution;tint[index]+=new Vector3(drop.Tint.X,drop.Tint.Y,drop.Tint.Z)*contribution;
                }
        }
        for(var i=0;i<density.Length;i++)
        {
            var d=density[i];
            if(d<.025f){blobPixels[i]=Color.Transparent;continue;}
            var color=tint[i]/d;
            var edge=edges[i]/d;
            var surface=MathHelper.SmoothStep(0,1,Math.Clamp((d-.12f)/.10f,0,1));
            var core=MathHelper.SmoothStep(0,1,Math.Clamp((d-.48f)/.16f,0,1));
            var rim=Vector3.Lerp(edge*.8f,Vector3.One,.18f);
            var shaded=Vector3.Lerp(rim,color*.72f,core);
            var x=i%BW;var y=i/BW;
            var gradient=x>0 && y>0?density[i-BW]-density[i-1]:0;
            var shine=Math.Clamp(gradient*3,0,.55f)*core;
            shaded=Vector3.Lerp(shaded,Vector3.One,shine);
            var alpha=Math.Clamp(d*.10f+surface*.88f,0,.98f);
            blobPixels[i]=new Color(shaded)*alpha;        }
        blobTexture.SetData(blobPixels);
    }
    public void Draw(SpriteBatch batch,int width,int height)
    {
        batch.Draw(blobTexture,new Rectangle(0,0,width,height),Color.White);

    }
    public void Dispose(){blobTexture.Dispose();Fire.Dispose();}
}
