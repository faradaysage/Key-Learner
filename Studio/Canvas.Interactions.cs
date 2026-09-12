using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;

public sealed partial class Canvas
{
    sealed class Paint {public Vector2 P;public Color Color;public float Age;public float[] Lobes= [];}
    sealed class Crack {public Vector2 A,B;}
    sealed class Shard {public Vector2 P,V;public float Age,Angle,Spin,Size;}
    sealed class Glow {public Vector2 P,V;public Color Color;public float Age,Size;}
    sealed class Shot {public Vector2 P,V,Target;public float Age;public bool Rocket;public Color Color;}
    sealed class Reward {public Vector2 P,V;public float Phase,Radius,Burn;public Color Color;}
    readonly List<Paint> paint=new();readonly List<Crack> cracks=new();readonly List<Shard> shards=new();
    readonly List<Glow> mouseGlow=new();readonly List<Shot> shots=new();readonly List<Reward> rewards=new();
    readonly SoundEffects sounds=new();readonly GlassDamage glass=new();readonly FireworkSchedule fireworks=new();
    readonly BalloonReward reward=new();
    Vector2 pointer,oldPointer;bool pointerActive;float playClock,lastPaint=-1,lastCrack=-1,lastShot=-1,shake;
    public Vector2? FirstRewardPosition=>rewards.Count>0?rewards[0].P:null;
    public int Score=>reward.Score;public int RewardRemaining=>reward.Remaining;
    public int PaintCount=>paint.Count;public int ShatterCount{get;private set;}public int RocketsLaunched{get;private set;}
    public float GlassAmount=>glass.Amount;
    public Vector2 Shake=>settings.GentleMotion?Vector2.Zero:new(MathF.Sin(playClock*79)*shake*9,MathF.Cos(playClock*93)*shake*6);
    public bool GestureEffect(InputContext context,int width,int height)
    {
        if(!settings.GestureEffects || context.Gesture is Gesture.Deliberate or Gesture.Rapid)return false;
        // Retract the first few symbols of a simultaneous cluster once there is enough evidence.
        letters.RemoveAll(g=>g.Age<.16f);activeGlyph=null;
        var p=new Vector2((float)(.08+context.X*.84)*width,(float)(.12+context.Y*.7)*height);
        if(context.Gesture==Gesture.Cluster && playClock-lastPaint>.07f)
        {
            lastPaint=playClock;if(paint.Count>=24)paint.RemoveAt(0);
            paint.Add(new(){P=p,Color=Palette[random.Next(4)],Lobes=Enumerable.Range(0,13).Select(_=>random.Next(18,47)*1f).ToArray()});sounds.Play("paint",settings,.65f);
        }
        if(context.Gesture==Gesture.BroadMash)
        {
            if(glass.Hit((float)context.Energy))
            {
                ShatterCount++;cracks.Clear();sounds.Play("shatter",settings);shake=1;
                for(int i=0;i<45;i++)shards.Add(new(){P=new(random.Next(width),random.Next(height)),V=new(random.Next(-170,171),random.Next(-110,100)),Size=random.Next(20,100),Spin=random.Next(-4,5),Angle=i});
                if(shards.Count>90)shards.RemoveRange(0,shards.Count-90);
            }
            else if(playClock-lastCrack>.055f && glass.Amount>0)
            {
                lastCrack=playClock;sounds.Play("crack",settings,.55f);
                for(int ray=0;ray<5;ray++)
                {
                    var a=p;var angle=ray*MathF.Tau/5+(float)random.NextDouble();
                    for(int j=0;j<4;j++){var b=a+new Vector2(MathF.Cos(angle),MathF.Sin(angle))*random.Next(15,70);cracks.Add(new(){A=a,B=b});if(j%2==0)cracks.Add(new(){A=b,B=b+new Vector2(MathF.Cos(angle+.8f),MathF.Sin(angle+.8f))*22});a=b;angle+=(float)(random.NextDouble()-.5);}
                }
                if(cracks.Count>600)cracks.RemoveRange(0,cracks.Count-600);
            }
        }
        if(context.Gesture==Gesture.Sweep)
        {
            var tint=Palette[random.Next(4)].ToVector3();
            for(int i=0;i<3;i++)Fields.Blobs.Add(new(p.X+random.Next(-15,16),p.Y+random.Next(-15,16)),new((float)context.Dx*320,(float)context.Dy*220-55),random.Next(23,36),new(tint.X,tint.Y,tint.Z),7,Math.Min(settings.ParticleLimit,180),1);
        }
        return true;
    }
    public void Pointer(Vector2 p,bool active,bool moved,bool left,bool right,int width,int height)
    {
        pointer=p;pointerActive=active&&settings.MousePlay;
        if(!pointerActive){oldPointer=p;return;}
        if(moved){int n=Math.Clamp((int)Vector2.Distance(oldPointer,p)/9,1,30);for(int i=1;i<=n;i++)mouseGlow.Add(new(){P=Vector2.Lerp(oldPointer,p,i/(float)n),V=new(random.Next(-20,21),-random.Next(20,70)),Size=random.Next(30,65),Color=Palette[random.Next(4)]});}
        oldPointer=p;if(mouseGlow.Count>250)mouseGlow.RemoveRange(0,mouseGlow.Count-250);
        if(right || left&&settings.Mode==PlayMode.WordAdventure)Cannon(p,width,height);
        else if(left)Blast(p,190,false);
    }
    void Cannon(Vector2 p,int width,int height)
    {
        if(playClock-lastShot<.22f || shots.Count>=250)return;lastShot=playClock;
        var origin=new Vector2(width*.5f,height+10);var delta=p-origin;if(delta.LengthSquared()<1)delta=-Vector2.UnitY;
        shots.Add(new(){P=origin,V=Vector2.Normalize(delta)*1050,Target=p,Color=Palette[3]});sounds.Play("cannon",settings,.7f);
    }
    public void CountFireworks(int count){if(fireworks.Pending<3200)fireworks.Add(count,playClock);}
    public void ReleaseWordBalloons(string word,int width,int height)
    {
        rewards.Clear();reward.Start(word.Length+word.Count(c=>"jqxz".Contains(c)));
        for(int i=0;i<reward.Remaining;i++)rewards.Add(new(){P=new((i+1)*width/(reward.Remaining+1f),height-random.Next(20,130)),V=new(random.Next(-45,46),-random.Next(70,120)),Radius=random.Next(34,52),Phase=i*1.7f,Color=Palette[i%4]});
    }
    void PopReward(Reward r)
    {
        if(!rewards.Remove(r))return;
        sounds.Play("pop",settings,.8f);Burst(r.P,Celebration.Confetti,22,new(Gesture.Deliberate,1,.5,.5,0,0,1,0,[]));
        rings.Add(new(){Position=r.P,Radius=r.Radius,Color=r.Color});if(rings.Count>20)rings.RemoveAt(0);
        if(reward.Pop()){shake=1.4f;Burst(new(720,450),Celebration.Embers,85,new(Gesture.Deliberate,1,.5,.5,0,0,1,0,[]));}
    }
    void Blast(Vector2 p,float radius,bool cannon)
    {
        sounds.Play(cannon?"pop":"paint",settings,.65f);
        rings.Add(new(){Position=p,Radius=25,Color=Palette[3]});if(rings.Count>20)rings.RemoveAt(0);
        Burst(p,Celebration.Embers,45,new(Gesture.Deliberate,1,.5,.5,0,0,1,0,[]));
        foreach(var g in letters){var d=g.P-p;var distance=d.Length();if(distance>radius)continue;g.V+=(distance>1?d/distance:-Vector2.UnitY)*(1-distance/radius)*1000;if(cannon && distance<65){g.Age=g.Life;g.Balloon.Release();}}
        foreach(var r in rewards.ToArray()){var d=r.P-p;var distance=d.Length();if(distance<radius*.65f){PopReward(r);continue;}if(distance<radius+r.Radius)r.V+=(distance>1?d/distance:-Vector2.UnitY)*600;}
    }
    void UpdateInteractions(float dt,int width,int height)
    {
        playClock+=dt;shake=Math.Max(0,shake-dt*1.4f);glass.Step(dt);if(glass.Amount==0)cracks.Clear();
        sounds.Update(settings,Fields.FireLevel);
        foreach(var p in paint){p.Age+=dt;p.P.Y+=dt*(12+p.Age*3);}paint.RemoveAll(p=>p.Age>7);
        foreach(var s in shards){s.Age+=dt;s.V.Y+=480*dt;s.P+=s.V*dt;s.Angle+=s.Spin*dt;}shards.RemoveAll(s=>s.Age>3);
        foreach(var g in mouseGlow){g.Age+=dt;g.P+=g.V*dt;}mouseGlow.RemoveAll(g=>g.Age>1.1f);
        foreach(var g in letters)
        {
            if(pointerActive)RepelAsset(g.P,ref g.V,dt);
            if(Fields.FireLevel>.3f && g.P.Y>height-Fields.FireDepth*.7f)g.Burn+=dt;
            if(g.Burn>0){g.Burn+=dt*.4f;if(random.NextDouble()<dt*20)Fields.Fire.Lick(g.P);if(g.Burn>1.5f){g.Balloon.Release();g.Age=g.Life;}}
        }
        foreach(var r in rewards.ToArray())
        {
            if(pointerActive)RepelAsset(r.P,ref r.V,dt);
            r.V+=new Vector2(MathF.Sin(playClock*1.5f+r.Phase)*12,-9)*dt;r.V*=MathF.Exp(-dt*.18f);r.P+=r.V*dt*(settings.GentleMotion?.55f:1);Bounce(ref r.P,ref r.V,r.Radius+15,width,height-10);
            if(Fields.FireLevel>.3f && r.P.Y>height-Fields.FireDepth*.7f)r.Burn+=dt;
            if(r.Burn>0){r.Burn+=dt*.4f;if(random.NextDouble()<dt*20)Fields.Fire.Lick(r.P);if(r.Burn>1)PopReward(r);}
        }
        for(int i=0;i<rewards.Count;i++)for(int j=i+1;j<rewards.Count;j++)
        {var a=rewards[i];var b=rewards[j];var d=b.P-a.P;var length=d.Length();var reach=a.Radius+b.Radius;if(length<reach){var n=length>.01f?d/length:Vector2.UnitX;a.P-=n*(reach-length)*.5f;b.P+=n*(reach-length)*.5f;var speed=Vector2.Dot(b.V-a.V,n);if(speed<0){a.V+=n*speed*.8f;b.V-=n*speed*.8f;}}}
        var due=fireworks.Due(playClock);
        for(int i=0;i<due;i++){RocketsLaunched++;var p=new Vector2(random.Next(50,width-50),height+10);var target=new Vector2(random.Next(80,width-80),random.Next(90,height/2));shots.Add(new(){P=p,Target=target,V=(target-p)/.75f,Rocket=true,Color=Palette[random.Next(4)]});}
        foreach(var s in shots.ToArray())
        {
            s.Age+=dt;var previous=s.P;s.P+=s.V*dt;
            if(random.NextDouble()<dt*60 && mouseGlow.Count<250)mouseGlow.Add(new(){P=s.P,V=new(0,15),Color=s.Color,Size=25});
            bool hit=!s.Rocket&&(letters.Any(g=>SegmentDistance(g.P,previous,s.P)<35*g.Balloon.Size)||rewards.Any(r=>SegmentDistance(r.P,previous,s.P)<r.Radius));
            if(hit || Vector2.Dot(s.Target-s.P,s.V)<=0 || s.Age>1.4f)
            {
                shots.Remove(s);
                if(s.Rocket){Burst(s.P,Celebration.Bubbles,48,new(Gesture.Deliberate,1,.5,.5,0,0,1,0,[]),new(){Shape=Celebration.Bubbles,Speed=2.1,Gravity=.6,Curl=0,Lifetime=1.8,Trail=.7});rings.Add(new(){Position=s.P,Radius=5,Color=s.Color});if(rings.Count>20)rings.RemoveAt(0);sounds.Play("pop",settings,.4f);}
                else Blast(s.P,185,true);
            }
        }
    }
    static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.Distance(p,a+d*Math.Clamp(Vector2.Dot(p-a,d)/Math.Max(.001f,d.LengthSquared()),0,1));}

    void RepelAsset(Vector2 p,ref Vector2 v,float dt){var d=p-pointer;var distance=d.Length();if(distance<155)v+=(distance>.01f?d/distance:-Vector2.UnitY)*(1-distance/155)*1100*dt;}
    void Line(SpriteBatch b,Vector2 a,Vector2 c,Color color,float width=2){var d=c-a;b.Draw(pixel,a,null,color,MathF.Atan2(d.Y,d.X),Vector2.Zero,new Vector2(d.Length()+1,width),SpriteEffects.None,0);}
    void DrawInteractions(SpriteBatch b,float time,int width,int height)
    {
        foreach(var p in paint)
        {
            var alpha=Math.Clamp((7-p.Age)/2,0,1);var c=p.Color*alpha;
            for(int i=0;i<p.Lobes.Length;i++){var a=i*MathF.Tau/p.Lobes.Length;var v=p.P+new Vector2(MathF.Cos(a),MathF.Sin(a))*35;var size=p.Lobes[i];b.Draw(disc,v,null,c,0,new(32),size/32,SpriteEffects.None,0);if(i%3==0){var end=v+new Vector2(MathF.Sin(i)*5,p.Age*(14+size));Line(b,v,end,c,size*.19f);b.Draw(disc,end,null,c,0,new(32),size*.1f/32,SpriteEffects.None,0);}}
            for(int i=0;i<9;i++){var a=i*MathF.Tau/9;var radial=new Vector2(MathF.Cos(a),MathF.Sin(a));var spread=Math.Min(1,p.Age*12);var start=p.P+radial*45;var end=p.P+radial*(70+p.Lobes[i])*spread;Line(b,start,end,c,3);b.Draw(disc,end+new Vector2(0,p.Age*4),null,c,0,new(32),.09f+p.Lobes[i]/400,SpriteEffects.None,0);}
            b.Draw(disc,p.P,null,c,0,new(32),1.7f,SpriteEffects.None,0);b.Draw(glow,p.P-new Vector2(15),null,Color.White*(alpha*.4f),0,new(32),1.6f,SpriteEffects.None,0);
        }
        foreach(var r in rewards)
        {
            var scale=new Vector2(r.Radius,r.Radius*1.22f)/32;var angle=MathF.Sin(time*2+r.Phase)*.08f;
            for(int i=0;i<6;i++)Line(b,r.P+new Vector2(MathF.Sin(time*2+i*.7f+r.Phase)*5,r.Radius*1.2f+i*9),r.P+new Vector2(MathF.Sin(time*2+(i+1)*.7f+r.Phase)*5,r.Radius*1.2f+(i+1)*9),Color.White*.3f,1);
            b.Draw(disc,r.P,null,Color.Lerp(r.Color,Color.Black,.4f),angle,new(32),scale*1.03f,SpriteEffects.None,0);
            b.Draw(disc,r.P-new Vector2(2,3),null,r.Color,angle,new(32),scale*.94f,SpriteEffects.None,0);
            b.Draw(glow,r.P-new Vector2(r.Radius*.24f,r.Radius*.35f),null,Color.White*.85f,angle,new(32),scale*.8f,SpriteEffects.None,0);
            b.Draw(disc,r.P-new Vector2(r.Radius*.32f,r.Radius*.45f),null,Color.White*.55f,-.45f,new(32),new Vector2(.11f,.26f)*r.Radius/32,SpriteEffects.None,0);
        }
        foreach(var c in cracks){Line(b,c.A+Vector2.One,c.B+Vector2.One,Color.Black*(glass.Amount*.6f),3);Line(b,c.A,c.B,new Color(200,233,255)*Math.Min(.9f,glass.Amount*1.8f),1);}
        foreach(var s in shards)
        {
            var alpha=Math.Clamp(1-s.Age/3,0,1);var a=s.P+new Vector2(MathF.Cos(s.Angle),MathF.Sin(s.Angle))*s.Size;var c=s.P+new Vector2(MathF.Cos(s.Angle+2.1f),MathF.Sin(s.Angle+2.1f))*s.Size*.65f;
            // Translucent triangular panes, with a bright fracture edge.
            for(int i=0;i<12;i++)Line(b,Vector2.Lerp(s.P,a,i/12f),Vector2.Lerp(s.P,c,i/12f),new Color(164,210,237)*(alpha*.12f),Math.Max(1,s.Size/12));
            Line(b,s.P,a,Color.White*(alpha*.55f),1);Line(b,a,c,Color.White*(alpha*.35f),1);Line(b,c,s.P,Color.White*(alpha*.4f),1);
        }
        foreach(var s in shots){Line(b,s.P-s.V*.025f,s.P,s.Color,3);b.Draw(disc,s.P,null,Color.White,0,new(32),.12f,SpriteEffects.None,0);}
        b.End();b.Begin(SpriteSortMode.Deferred,BlendState.Additive,SamplerState.LinearClamp);
        foreach(var g in mouseGlow)b.Draw(glow,g.P,null,g.Color*(Math.Clamp(1-g.Age/1.1f,0,1)*.55f),0,new(32),g.Size/32,SpriteEffects.None,0);
        b.End();b.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp);
    }
    void ClearInteractions(){paint.Clear();cracks.Clear();shards.Clear();shots.Clear();mouseGlow.Clear();rewards.Clear();reward.Clear();fireworks.Clear();glass.Clear();pointerActive=false;shake=0;sounds.Update(settings,0);}
}

