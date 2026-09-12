using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;

public sealed partial class Canvas : IDisposable
{
    private sealed class Mote { public Vector2 P,V; public float Age,Life,Size,Spin; public Color Color; public Celebration Effect; public float Curl,Gravity=1,Trail; }
    private sealed class Glyph { public string Text=""; public Vector2 P,V; public float Age,Life,Rotation,Burn; public Color Color; public SpriteFont? Font; public BalloonMotion Balloon=new(); }
    private readonly List<Mote> motes=new();
    private readonly List<Glyph> letters=new();
    private Glyph? activeGlyph;
    private int? activeKey;
    private sealed class PopRing {public Vector2 Position;public float Age,Radius;public Color Color;}
    private readonly List<PopRing> rings=new();
    public int PoppedCount {get;private set;}
    public int CountGlyph(string text)=>letters.Count(g=>g.Text==text);
    public void BeginKey(int key){if(activeKey!=key){activeGlyph?.Balloon.Release();activeGlyph=null;}activeKey=key;}
    private readonly Random random=new(73);
    private readonly Texture2D pixel,glow,disc;
    private readonly SpriteFont[] fonts;
    private readonly GlyphMeshes meshes;
    public Effect? ToonShader {set=>meshes.ToonShader=value;}
    private readonly Settings settings;
    public LivingFields Fields {get;}
    private float emberClock;
    private readonly DemosceneBackdrop backdrop;
    public int GlyphCount=>letters.Count;
    public float LargestGlyph=>letters.Count==0?0:letters.Max(g=>g.Balloon.Size);
    public void Prepare()=>Fields.Prepare(Palette,settings);
    public Color[] Palette => settings.Theme switch {
        Mood.PrimaryColors => [new(255,38,50),new(255,215,0),new(25,100,255),new(255,215,0)],
        Mood.BlackAndWhite => [Color.White,new(210,210,210),Color.White,new(160,160,160)],
        Mood.Lagoon => [new(74,231,215),new(90,173,255),new(181,244,196),new(254,231,161)],
        Mood.Sunset => [new(255,155,119),new(246,104,161),new(255,214,135),new(166,144,250)],
        Mood.Candy => [new(243,143,221),new(128,205,255),new(251,229,147),new(171,240,212)],
        _ => [new(137,244,208),new(181,154,255),new(255,179,198),new(255,222,143)] };
    public Color Background => settings.Theme switch { Mood.BlackAndWhite=>Color.Black,Mood.PrimaryColors=>new(8,12,24),Mood.Lagoon=>new(8,28,42),Mood.Sunset=>new(31,19,42),Mood.Candy=>new(26,23,49),_=>new(13,19,38) };
    public int ParticleCount=>motes.Count+Fields.Blobs.Drops.Count+Fields.Fire.Count;
    public Canvas(GraphicsDevice device,SpriteFont[] fonts,Settings settings)
    {
        this.fonts=fonts;meshes=new(device); this.settings=settings; Fields=new(device); backdrop=new(device);
        pixel=new(device,1,1); pixel.SetData(new[]{Color.White});
        glow=MakeDisc(device,true); disc=MakeDisc(device,false);
    }
    private static Texture2D MakeDisc(GraphicsDevice device,bool soft)
    {
        const int n=64; var data=new Color[n*n];
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            var d=Vector2.Distance(new(x+.5f,y+.5f),new(n/2f))/ (n/2f);
            var a=soft?MathF.Pow(Math.Max(0,1-d),2):Math.Clamp((1-d)*20,0,1);
            data[y*n+x]=Color.White*a;
        }
        var texture=new Texture2D(device,n,n); texture.SetData(data); return texture;
    }
    public void Emit(string text,InputContext context,int width,int height,SpriteFont? iconFont=null)
    {
        backdrop.Excite(context);
        var p=new Vector2((float)(.1+context.X*.8)*width,(float)(.26+context.Y*.40)*height);
        if(iconFont!=null)p=new Vector2(random.Next(110,width-110),random.Next(130,height-210));
        var color=Palette[random.Next(4)];
        if(text.Length>0) {
            var existing=activeGlyph is {} candidate && letters.Contains(candidate) && candidate.Age<candidate.Life && candidate.Text==text.ToUpperInvariant() && candidate.Font==iconFont?candidate:null;
            if(existing!=null){existing.Balloon.Inflate();existing.Age=0;existing.V.Y=Math.Max(-260,existing.V.Y-85);p=existing.P;}
            else {
            activeGlyph?.Balloon.Release();
            if(letters.Count>=55) letters.RemoveAt(0);
            activeGlyph=new() {Text=text.ToUpperInvariant(),P=p,V=new((float)(context.Dx*130)+random.Next(-45,46),-90),Life=(float)settings.LetterLifetime,Color=color,Font=iconFont}; letters.Add(activeGlyph); }
        }
                var count=(int)((5+context.Energy*8)*settings.EffectStrength*(settings.GentleMotion?.4:1));
        for(var i=0;i<count;i++)
        {
            var angle=(float)(random.NextDouble()*Math.Tau);var speed=random.Next(25,180);
            var velocity=new System.Numerics.Vector2(MathF.Cos(angle)*speed+(float)context.Dx*180,MathF.Sin(angle)*speed-40);
            var tint=Palette[random.Next(4)].ToVector3();
            Fields.Blobs.Add(new(p.X+random.Next(-18,19),p.Y+random.Next(-18,19)),velocity,random.Next(6,13),new(tint.X,tint.Y,tint.Z),5,Math.Clamp(settings.ParticleLimit-motes.Count,0,180));
        }
    }
    public void Celebrate(Celebration effect,int width,int height,EffectRecipe? recipe=null) { activeGlyph=null;letters.Clear();
        var keep=Math.Max(0,settings.ParticleLimit-Math.Min(150,settings.ParticleLimit));
        if(Fields.Blobs.Drops.Count>keep)Fields.Blobs.Drops.RemoveRange(0,Fields.Blobs.Drops.Count-keep);
        if(motes.Count>keep)motes.RemoveRange(0,motes.Count-keep);
        Burst(new(width*.5f,height*.43f),recipe?.Shape??effect,recipe?.Count??150,new InputContext(Gesture.Deliberate,1,.5,.5,0,0,1,0,[]),recipe); }
    private void Burst(Vector2 p,Celebration effect,int count,InputContext context,EffectRecipe? recipe=null)
    {
        count=(int)(count*settings.EffectStrength*(settings.GentleMotion?.35:1));
        for(var i=0;i<count && motes.Count<Math.Max(0,settings.ParticleLimit-Fields.Blobs.Drops.Count);i++)
        {
            var angle=(float)(random.NextDouble()*Math.Tau); var speed=random.Next(45,220)*(float)(recipe?.Speed??1);
            var startVelocity=new Vector2(MathF.Cos(angle),MathF.Sin(angle))*speed+new Vector2((float)context.Dx*200,(float)context.Dy*100);
            if(effect==Celebration.Embers) startVelocity=new Vector2(startVelocity.X*.85f,-Math.Abs(startVelocity.Y)-90);
            motes.Add(new() {P=p,V=startVelocity,Age=0,Life=(float)(recipe?.Lifetime??(3+random.NextDouble()*2)),Curl=(float)(recipe?.Curl??(effect==Celebration.Orbit?100:18)),Gravity=(float)(recipe?.Gravity??(effect==Celebration.Embers?-.35:effect==Celebration.Bubbles?-.2:1)),Trail=(float)(recipe?.Trail??.25),Size=effect==Celebration.Embers?random.Next(2,6):random.Next(4,13),Spin=(float)random.NextDouble()*4,Color=Palette[random.Next(4)],Effect=effect});
        }
    }
        public void Update(float elapsed,int width,int height)
    {
        Fields.Update(elapsed,settings,width,height,settings.ParticleLimit-motes.Count);
        backdrop.Update(Math.Clamp(elapsed,0,.1f),settings,Palette);
        UpdateInteractions(Math.Clamp(elapsed,0,.1f),width,height);
        emberClock+=Math.Clamp(elapsed,0,.1f)*Fields.FireLevel*(settings.GentleMotion?10:45);
        while(emberClock>=1)
        {
            Burst(new(random.Next(width),height-random.Next(5,30)),Celebration.Embers,1,new(Gesture.Deliberate,1,.5,1,0,0,.2,1,[]));
            emberClock--;
        }
        // Fixed substeps keep damping, bounce and curl motion stable on slower laptops.
        var dt=Math.Clamp(elapsed,0,.1f);
        var steps=Math.Max(1,(int)Math.Ceiling(dt/(1f/120)));
        var h=dt/steps;
        for(var step=0;step<steps;step++)
        {
            foreach(var m in motes)
            {
                m.Age+=h;
                var curl=new Vector2(MathF.Sin(m.P.Y*.008f+m.Age),MathF.Cos(m.P.X*.008f-m.Age));
                m.V+=curl*m.Curl*h;
                m.V.Y+=(float)settings.Gravity*m.Gravity*h;
                m.V*=MathF.Exp(-.22f*h);
                m.P+=m.V*h*(settings.GentleMotion?.4f:1);
                Bounce(ref m.P,ref m.V,m.Size,width,height);
            }
            foreach(var g in letters)
            {
                if(g.Balloon.Popped || g.Age>=g.Life)continue;
                g.Age+=h;g.Balloon.Step(h,(float)settings.BalloonDeflateSeconds,(float)settings.BalloonPopSize);
                if(g.Balloon.Size>1.03f)g.Age=Math.Min(g.Age,Math.Max(0,g.Life-1.3f));
                if(g.Balloon.Popped)
                {
                    sounds.Play("pop",settings);PoppedCount++;g.Age=g.Life;if(activeGlyph==g)activeGlyph=null;
                    if(rings.Count>=12)rings.RemoveAt(0);
                    rings.Add(new(){Position=g.P,Radius=25*g.Balloon.Size,Color=g.Color});
                    var keep=Math.Max(0,settings.ParticleLimit-120);
                    if(motes.Count>keep)motes.RemoveRange(0,motes.Count-keep);
                    if(Fields.Blobs.Drops.Count>keep)Fields.Blobs.Drops.RemoveRange(0,Fields.Blobs.Drops.Count-keep);
                    Burst(g.P,Celebration.Embers,100,new(Gesture.Deliberate,1,.5,.5,0,0,1,0,[]));
                    Burst(g.P,Celebration.Confetti,35,new(Gesture.Deliberate,1,.5,.5,0,0,1,0,[]));
                    continue;
                }
                var inflated=g.Balloon.Size>1.08f;
                if(inflated)g.V.Y=MathHelper.Lerp(g.V.Y,-45-(g.Balloon.Size-1)*75,1-MathF.Exp(-h*4));
                else g.V.Y+=(float)settings.Gravity*h;
                g.P+=g.V*h*(settings.GentleMotion?.45f:1);
                var radius=Math.Min(45*g.Balloon.Size*(float)settings.FontScale,(height-100)*.45f);
                Bounce(ref g.P,ref g.V,radius,width,height);
                if(inflated && g.P.Y<=radius+.1f)g.V.Y=0;
                g.Rotation=settings.GentleMotion?0:MathF.Sin(g.Age*1.4f)*.09f;
            }
        }
        foreach(var ring in rings)ring.Age+=dt;
        rings.RemoveAll(r=>r.Age>=.8f);
        motes.RemoveAll(m=>m.Age>=m.Life);
        var remaining=Math.Max(0,settings.ParticleLimit-Fields.Blobs.Drops.Count);
        if(motes.Count>remaining) motes.RemoveRange(0,motes.Count-remaining);
        letters.RemoveAll(g=>g.Age>=g.Life);
    }
    private void Bounce(ref Vector2 p,ref Vector2 v,float radius,int width,int height)
    {
        if(p.X<radius) {p.X=radius; v.X=Math.Abs(v.X)*(float)settings.Bounce;}
        if(p.X>width-radius) {p.X=width-radius; v.X=-Math.Abs(v.X)*(float)settings.Bounce;}
        if(p.Y>height-radius) {p.Y=height-radius; v.Y=-Math.Abs(v.Y)*(float)settings.Bounce; v.X*=.96f;}
        if(p.Y<radius) {p.Y=radius; v.Y=Math.Abs(v.Y)*(float)settings.Bounce;}
    }
    public void Draw(SpriteBatch b,float time,int width,int height)
    {
        var palette=Palette;
        backdrop.Draw(b,width,height,settings);
        // Slow harmonic curtains, sparse stars, and layered glow create depth without flashing.
        for(var layer=0;layer<(settings.Theme==Mood.BlackAndWhite || settings.Backdrop is Backdrop.Starfield or Backdrop.RotatingStars?0:3);layer++)
            for(var i=0;i<38;i++)
            {
                var x=i*width/37f;
                var y=height*(.35f+layer*.15f)+MathF.Sin(i*.17f+time*.15f+layer)*height*.1f+MathF.Sin(i*.37f-time*.11f)*32;
                b.Draw(glow,new Vector2(x,y),null,palette[layer]*.055f,0,new(32),new Vector2(width/12f,height/2f)/64,SpriteEffects.None,0);
            }
        for(var i=0;i<(settings.Theme==Mood.BlackAndWhite || settings.Backdrop is Backdrop.Starfield or Backdrop.RotatingStars?0:65);i++)
        {
            var x=(i*137.57f)%width; var y=(i*79.31f)%height;
            b.Draw(disc,new Vector2(x,y),null,Color.White*(.08f+.06f*MathF.Sin(time*.4f+i)),0,new(32),.04f,SpriteEffects.None,0);
        }
        Fields.Draw(b,width,height);
        Fields.Fire.Draw(b,palette);
        foreach(var m in motes)
        {
            var alpha=Math.Clamp((m.Life-m.Age)/1.4f,0,1);
            for(var trail=1;trail<=3 && !settings.GentleMotion;trail++) b.Draw(glow,m.P-m.V*(trail*.025f),null,m.Color*(alpha*m.Trail/(trail+2)),0,new(32),m.Size/12,SpriteEffects.None,0);
            b.Draw(glow,m.P,null,m.Color*(alpha*.30f),0,new(32),m.Size/8,SpriteEffects.None,0);
            if(m.Effect==Celebration.Confetti || m.Effect==Celebration.Rain)
                b.Draw(pixel,m.P,null,m.Color*alpha,m.Spin*m.Age,Vector2.Zero,new Vector2(m.Size*.55f,m.Size*(m.Effect==Celebration.Rain?2:1)),SpriteEffects.None,0);
            else b.Draw(disc,m.P,null,m.Color*(alpha*.7f),0,new(32),m.Size/32,SpriteEffects.None,0);
        }
        foreach(var ring in rings)
        {
            var fade=Math.Clamp(1-ring.Age/.8f,0,1);
            var radius=ring.Radius+ring.Age*(settings.GentleMotion?60:220);
            for(int i=0;i<64;i++)
            {
                var a=i*MathF.Tau/64;var next=(i+1)*MathF.Tau/64;
                var start=ring.Position+new Vector2(MathF.Cos(a),MathF.Sin(a))*radius;
                var end=ring.Position+new Vector2(MathF.Cos(next),MathF.Sin(next))*radius;
                var delta=end-start;
                b.Draw(pixel,start,null,Color.Lerp(ring.Color,Color.White,.6f)*fade,MathF.Atan2(delta.Y,delta.X),Vector2.Zero,new Vector2(delta.Length()+1,4*fade),SpriteEffects.None,0);
            }
        }
        var font=fonts[(int)settings.Font];
        foreach(var g in letters)
        {
            var alpha=Math.Clamp((g.Life-g.Age)/1.2f,0,1);
            var pop=1+MathF.Sin(Math.Min(g.Age*7,MathF.PI))*.2f;
            var scale= new Vector2(1-g.Balloon.Squeeze,1+g.Balloon.Squeeze)*(float)settings.FontScale*pop*(g.Font==null?.7f:1.4f)*g.Balloon.Size;
            var glyphFont=g.Font??font;
            var origin=glyphFont.MeasureString(g.Text)/2;
            b.Draw(glow,g.P,null,g.Color*(alpha*.35f),0,new(32),3f*g.Balloon.Size,SpriteEffects.None,0);
            if(settings.ExtrudedAssets)continue;
            b.DrawString(glyphFont,g.Text,g.P+new Vector2(0,5),new Color(0,0,0)*alpha,g.Rotation,origin,scale,SpriteEffects.None,0);
            b.DrawString(glyphFont,g.Text,g.P,g.Color*alpha,g.Rotation,origin,scale,SpriteEffects.None,0);
            b.DrawString(glyphFont,g.Text,g.P-new Vector2(1,2)*g.Balloon.Size,Color.Lerp(g.Color,Color.White,.65f)*(alpha*.32f),g.Rotation,origin,scale*.985f,SpriteEffects.None,0);
        }
        if(settings.ExtrudedAssets){meshes.UseToon=settings.ToonAssets;b.End();foreach(var g in letters){if(g.Text.Length!=1)continue;float scale=(float)settings.FontScale*(g.Font==null?.7f:1.4f)*g.Balloon.Size;var world=Matrix.CreateScale(scale)*Matrix.CreateRotationX(settings.GentleMotion?0:MathF.Sin(g.Age*.8f)*.22f)*Matrix.CreateRotationY(settings.GentleMotion?0:MathF.Sin(g.Age*.6f)*.35f)*Matrix.CreateRotationZ(g.Rotation)*Matrix.CreateTranslation(g.P.X,g.P.Y,0);meshes.Draw(g.Font??font,g.Text[0],world,Matrix.Identity,Matrix.CreateOrthographicOffCenter(0,width,height,0,-500,500),g.Color,Math.Clamp((g.Life-g.Age)/1.2f,0,1));}RenderSpace.Begin(b);}
        DrawInteractions(b,time,width,height);
    }
    public void Clear() { ClearInteractions(); activeGlyph=null;activeKey=null;rings.Clear();letters.Clear(); motes.Clear(); Fields.Clear(); }
    public void Dispose() { meshes.Dispose();sounds.Dispose(); Fields.Dispose(); backdrop.Dispose(); pixel.Dispose(); glow.Dispose(); disc.Dispose(); }
}
