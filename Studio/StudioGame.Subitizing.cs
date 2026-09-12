using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
namespace KeyLearner.Studio;

public sealed partial class StudioGame
{
    readonly SubitizingGame dots=new(Environment.TickCount);
    Texture2D? dotDisc;
    SoundEffects? dotSounds;
    bool IsDotGame=>S.Mode==PlayMode.Subitizing;
    bool IsDotPlay=>IsDotGame && !parent && !picker && calibration<0;
    double lastDotTouch=-10;
    bool multipleTouches;
    int replayDotStage;
    static Vector2 V(System.Numerics.Vector2 p)=>new(p.X,p.Y);
    void ResetDotSession(){dots.RestartRound();dotSounds?.Stop();lastDotTouch=now;multipleTouches=false;}
    void DotTap(Vector2 screen){
        var point=DotLayout.Unproject(new(screen.X,screen.Y),GraphicsDevice.PresentationParameters.BackBufferWidth,GraphicsDevice.PresentationParameters.BackBufferHeight);
        if(DotLayout.Mute.Contains(point)){S.Sound=!S.Sound;if(!S.Sound){voice?.Stop();dotSounds?.Stop();}store.Save();return;}
        int number=DotLayout.HitNumber(point);
        if(number>=0 && dots.Answer(number)){voice?.Stop();dotSounds?.Play("cannon",S,.7f);if(dots.Quantity==0)dotSounds?.Play("powerup",S,.5f);}
    }
    void UpdateDots(float dt,MouseState mouse){
        dotSounds??=new();
        var before=dots.Phase;int popped=dots.Popped;
        dots.Step(dt);
        if(dots.Phase==DotPhase.Answer && before!=DotPhase.Answer)voice?.Say("How many?",S);
        if(dots.Phase==DotPhase.Reward && dots.Popped>popped)dotSounds.Play("pop",S,.75f,.035);
        dotSounds.Update(S,0);
        // Native touch and mouse-emulated taps share one edge-triggered action path.
        var touch=TouchPanel.GetState();
        var active=touch.Where(t=>t.State is TouchLocationState.Pressed or TouchLocationState.Moved).ToArray();
        if(active.Length>1)multipleTouches=true;
        if(active.Length==0)multipleTouches=false;
        if(touch.Count>0)lastDotTouch=now;
        if(!multipleTouches && active.Length==1 && active[0].State==TouchLocationState.Pressed)DotTap(active[0].Position);
        else if(now-lastDotTouch>.25 && mouse.LeftButton==ButtonState.Pressed && previousMouse.LeftButton==ButtonState.Released)DotTap(new(mouse.X,mouse.Y));
        if(scenario=="dots-correct" && dots.CanAnswer && replayDotStage==0){DotTap(DotScreen(DotLayout.Number(dots.Quantity).Center));replayDotStage++;}
        if(scenario=="dots-retry" && dots.CanAnswer){
            if(replayDotStage==0){DotTap(DotScreen(DotLayout.Number((dots.Quantity+1)%10).Center));replayDotStage++;}
            else if(replayDotStage==1 && dots.PhaseTime>.42){DotTap(DotScreen(DotLayout.Number(dots.Quantity).Center));replayDotStage++;}
        }
    }
    Vector2 DotScreen(System.Numerics.Vector2 p){var (s,o)=DotLayout.Fit(GraphicsDevice.PresentationParameters.BackBufferWidth,GraphicsDevice.PresentationParameters.BackBufferHeight);return V(p*s+o);}
    void EnsureDotDisc(){
        if(dotDisc!=null)return;const int size=128;var data=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new(x+.5f,y+.5f),new(size/2f));data[y*size+x]=Color.White*Math.Clamp(size/2f-d,0,1);}
        dotDisc=new(GraphicsDevice,size,size);dotDisc.SetData(data);
    }
    void DotCircle(Vector2 p,float radius,Color color)=>batch.Draw(dotDisc!,p,null,color,0,new(64),radius/64,SpriteEffects.None,0);
    void DotLine(Vector2 a,Vector2 b,float width,Color color){var d=b-a;batch.Draw(pixel,a,null,color,MathF.Atan2(d.Y,d.X),new(0,.5f),new Vector2(d.Length(),width),SpriteEffects.None,0);}
    void DotRounded(DotRect r,Color color){
        // Non-overlapping pieces keep translucent cards uniformly shaded.
        int x=(int)r.X,y=(int)r.Y,w=(int)r.Width,h=(int)r.Height,c=Math.Min(22,Math.Min(w,h)/2);
        Fill(new(x+c,y,w-2*c,h),color);Fill(new(x,y+c,c,h-2*c),color);Fill(new(x+w-c,y+c,c,h-2*c),color);
        for(int side=0;side<2;side++)for(int row=0;row<2;row++)
            batch.Draw(dotDisc!,new Rectangle(x+side*(w-c),y+row*(h-c),c,c),new Rectangle(side*64,row*64,64,64),color);
    }

    void DotText(string text,Vector2 center,float scale,Color color,SpriteFont? font=null){font??=title;batch.DrawString(font,text,center-font.MeasureString(text)*scale/2,color,0,Vector2.Zero,scale,SpriteEffects.None,0);}
    void DotRing(Vector2 center,float radius,float width,Color color){for(int i=0;i<64;i++){float a=i*MathF.Tau/64,b=(i+1)*MathF.Tau/64;DotLine(center+new Vector2(MathF.Cos(a),MathF.Sin(a))*radius,center+new Vector2(MathF.Cos(b),MathF.Sin(b))*radius,width,color);}}
    void DotBurst(Vector2 center,float age,int seed){
        if(age<0 || age>.7f)return;float fade=Math.Clamp(1-age/.7f,0,1);
        DotRing(center,50+age*210,5*fade,canvas.Palette[1]*fade);
        for(int i=0;i<22;i++){float angle=i*2.399963f+seed,speed=90+(i%7)*27;
            var velocity=new Vector2(MathF.Cos(angle),MathF.Sin(angle))*speed;var p=center+velocity*age+new Vector2(0,170*age*age);
            DotLine(p-velocity*.027f,p,3+fade*4,canvas.Palette[i%4]*fade);
        }
    }
    void DrawDots(){
        batch.End();EnsureDotDisc();
        GraphicsDevice.Clear(S.Theme==Mood.BlackAndWhite?Color.Black:new Color(8,12,24));
        var (scale,offset)=DotLayout.RenderFit(GraphicsDevice.PresentationParameters.BackBufferWidth,GraphicsDevice.PresentationParameters.BackBufferHeight,GraphicsDevice.Viewport.Width,GraphicsDevice.Viewport.Height);
        var shake=S.GentleMotion?Vector2.Zero:new Vector2(MathF.Sin((float)dots.PhaseTime*90)*3*(float)dots.WrongShake,0);
        batch.Begin(samplerState:SamplerState.LinearClamp,transformMatrix:Matrix.CreateScale(scale.X,scale.Y,1)*Matrix.CreateTranslation(offset.X+shake.X*scale.X,offset.Y,0));
        Color ink=S.Theme==Mood.BlackAndWhite?Color.White:new(255,221,62);
        DotRounded(DotLayout.Mute,S.Theme==Mood.BlackAndWhite?new Color(35,35,35):new Color(26,34,49));DotText(S.Sound?"MUTE":"UNMUTE",V(DotLayout.Mute.Center),.44f,Color.White,ui);
        string stage=dots.Stage.ToString();DotText(stage,new(652-title.MeasureString(stage).X*.32f,63),.64f,Color.White);
        string cue=dots.Phase switch{DotPhase.Ready=>"READY",DotPhase.Set=>"SET",DotPhase.Go=>"GO",DotPhase.Answer=>"HOW MANY?",DotPhase.Reward=>dots.Quantity.ToString(),_=>""};
        float punch=dots.Phase==DotPhase.Reward?1+.16f*MathF.Exp(-(float)dots.PhaseTime*8):1;
        DotText(cue,new(360,184),.94f*punch,dots.Phase is DotPhase.Go or DotPhase.Reward?ink:Color.White);
        var cells=DotPatterns.Cells(dots.Mask).ToArray();
        for(int i=0;i<cells.Length;i++){
            var p=V(DotLayout.Cell(cells[i]));
            if(dots.DotsVisible || dots.Phase==DotPhase.Reward && i>=dots.Popped)DotCircle(p,DotLayout.Radius,ink);
            if(dots.Phase!=DotPhase.Reward)continue;
            float t=(float)(dots.PhaseTime-SubitizingGame.LaunchAt(i));
            if(t>=0 && t<.24f){Vector2 origin=new(360,774),tip=Vector2.Lerp(origin,p,t/.24f);DotLine(Vector2.Lerp(origin,p,Math.Max(0,t/.24f-.23f)),tip,10,ink);DotCircle(tip,10,Color.White);}
            DotBurst(p,(float)(dots.PhaseTime-SubitizingGame.ImpactAt(i)),cells[i]);
        }
        if(dots.Phase==DotPhase.Reward){
            float time=(float)dots.PhaseTime;
            if(dots.Quantity==0)DotBurst(new(360,490),time,0);
            if(time<.14f)DotCircle(new(360,774),20+time*160,Color.White*(1-time/.14f));
            // A single short recoil and expanding flash, no sustained camera motion.
            if(time<.18f)DotRing(new(360,774),20+time*360,7*(1-time/.18f),ink*(1-time/.18f));
        }
        for(int n=0;n<=9;n++){
            bool active=dots.CanAnswer;var r=DotLayout.Number(n);DotRounded(r,S.Theme==Mood.BlackAndWhite?(active?new Color(48,48,48):new Color(26,26,26)):(active?new Color(36,48,70):new Color(23,29,42)));
            DotText(n.ToString(),V(r.Center),1.02f,Color.White*(active?1:.38f));
        }
        batch.End();RenderSpace.Begin(batch);
    }
}
