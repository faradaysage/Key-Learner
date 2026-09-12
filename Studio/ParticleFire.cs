using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;

/// <summary>Layered additive flame billboards with buoyancy, curling and cooling.</summary>
public sealed class ParticleFire : IDisposable
{
    private sealed class Flame {public Vector2 P,V;public float Age,Life,Size,Phase;public int Sprite;}
    private readonly List<Flame> flames=new();
    private readonly Random random=new(907);
    private readonly Texture2D atlas;
    private float emission,clock;
    public float DepthScale {get;set;}=1;
    public void Lick(Vector2 p){if(flames.Count<650)flames.Add(new(){P=p,V=new(random.Next(-20,21),-90),Life=.65f,Size=45,Phase=clock,Sprite=random.Next(4)});}
    public int Count=>flames.Count;
    public ParticleFire(GraphicsDevice device){using var stream=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Content","Effects","fire-atlas.png"));atlas=Texture2D.FromStream(device,stream);}
    public void Update(float dt,float fuel,Settings settings,int width,int height,int budget)
    {
        clock+=dt;emission+=dt*fuel*(settings.GentleMotion?70:190)*(float)settings.EffectStrength;
        while(emission>=1)
        {
            emission--;
            if(flames.Count>=Math.Clamp(budget,0,650))continue;
            var jet=random.Next(12);var phase=(float)random.NextDouble()*MathF.Tau;
            flames.Add(new(){P=new((jet+.5f)*width/12+random.Next(-48,49),height+random.Next(0,35)),V=new(random.Next(-15,16),-random.Next(105,195)*DepthScale),Life=1.1f+(float)random.NextDouble()*1.25f,Size=random.Next(60,125),Phase=phase,Sprite=random.Next(4)});
        }
        foreach(var f in flames)
        {
            f.Age+=dt;f.V.X+=MathF.Sin(f.Phase+f.P.Y*.026f+clock*2)*65*dt;f.V.Y-=22*dt;
            f.P+=f.V*dt*(settings.GentleMotion?.6f:1);
        }
        flames.RemoveAll(f=>f.Age>=f.Life);
        if(flames.Count>budget)flames.RemoveRange(0,flames.Count-Math.Max(0,budget));
    }
    // Caller restores its normal alpha blend batch afterwards.
    public void Draw(SpriteBatch batch,Color[] palette)
    {
        batch.End();RenderSpace.Begin(batch,BlendState.Additive);
        foreach(var f in flames)
        {
            var t=f.Age/f.Life;
            var opacity=Math.Min(f.Age*9,1)*MathF.Pow(1-t,.85f)*.62f;
            var color=t<.25f?Color.Lerp(Color.White,palette[3],t*4):Color.Lerp(palette[3],palette[1],(t-.25f)/.75f);
            var size=f.Size*(.7f+MathF.Sin(t*MathF.PI)*.6f);
            // RGB intensity with full alpha: the source atlas has a black additive background.
            batch.Draw(atlas,f.P,new Rectangle(f.Sprite*64,0,64,64),new Color(color.ToVector3()*opacity),MathF.Sin(f.Phase+f.Age*2)*.25f,new Vector2(32),new Vector2(size,size*(1.35f+t*.9f))/64,SpriteEffects.None,0);
        }
        batch.End();RenderSpace.Begin(batch);
    }
    public void Clear(){flames.Clear();emission=0;}
    public void Dispose()=>atlas.Dispose();
}
