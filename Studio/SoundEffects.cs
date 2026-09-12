using Microsoft.Xna.Framework.Audio;
namespace KeyLearner.Studio;
/// <summary>A separate, bounded effects bus; it never stops or queues speech.</summary>
public sealed class SoundEffects : IDisposable
{
    readonly Dictionary<string,SoundEffect> clips=new();
    readonly List<SoundEffectInstance> playing=new();
    readonly Dictionary<string,double> last=new();
    SoundEffectInstance? fire;
    public SoundEffects(){try{
        var pcm=new byte[22050/4*2];for(int i=0;i<pcm.Length/2;i++){double t=i/22050d;short v=(short)(Math.Sin(t*Math.PI*2*(t<.12?170:125))*Math.Min(1,t*40)*Math.Max(0,1-t*4)*5000);pcm[i*2]=(byte)v;pcm[i*2+1]=(byte)(v>>8);}clips["retry"]=new SoundEffect(pcm,22050,AudioChannels.Mono);
        clips["powerup"]=Tone(.8,t=>{int note=Math.Min(3,(int)(t/.14));double phase=t-note*.14,duration=note==3?.38:.14,hz=new[]{523.25,659.25,783.99,1046.5}[note];return (Math.Sin(Math.PI*2*hz*phase)+.25*Math.Sin(Math.PI*4*hz*phase))*Math.Sin(Math.PI*phase/duration)*.55;});
        clips["sonar"]=Tone(.45,t=>Math.Sin(Math.PI*2*(880*t-300*t*t))*Math.Exp(-8*t));
        clips["horn"]=Tone(.28,t=>(Math.Sin(Math.PI*2*220*t)+.25*Math.Sin(Math.PI*2*440*t))*Math.Sin(Math.PI*t/.28));
        foreach(var name in new[]{"pop","paint","crack","shatter","cannon","fire","squawk"}){using var s=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Content","Sounds",name+".wav"));clips[name]=SoundEffect.FromStream(s);}fire=clips["fire"].CreateInstance();fire.IsLooped=true;}catch(Exception e) when(e is IOException or NoAudioHardwareException or InvalidOperationException){}}
    static SoundEffect Tone(double seconds,Func<double,double> wave){var data=new byte[(int)(22050*seconds)*2];for(int i=0;i<data.Length/2;i++){short sample=(short)(Math.Clamp(wave(i/22050d),-1,1)*6500);data[i*2]=(byte)sample;data[i*2+1]=(byte)(sample>>8);}return new SoundEffect(data,22050,AudioChannels.Mono);}
    public void Play(string name,Settings s,float gain=1)
    {
        Update(s,0,false);var now=KeyboardGuard.Now;
        if(!s.Sound || !s.EffectsSound || !clips.ContainsKey(name) || playing.Count>=8 || last.TryGetValue(name,out var at)&&now-at<.09)return;
        last[name]=now;var instance=clips[name].CreateInstance();instance.Volume=Math.Clamp(s.EffectsVolume/100f*s.Volume/100f*gain,0,1);instance.Play();playing.Add(instance);
    }
    public void Update(Settings s,float heat,bool updateFire=true){foreach(var p in playing.Where(p=>p.State==SoundState.Stopped).ToArray()){p.Dispose();playing.Remove(p);}if(!s.Sound || !s.EffectsSound){foreach(var p in playing)p.Stop();}if(fire!=null && updateFire){fire.Volume=s.Sound&&s.EffectsSound?Math.Clamp(heat*.55f*s.EffectsVolume/100f*s.Volume/100f,0,1):0;if(fire.Volume>.001f && fire.State!=SoundState.Playing)fire.Play();if(fire.Volume<=.001f)fire.Stop();}}
    public void Stop(){foreach(var p in playing){p.Stop();p.Dispose();}playing.Clear();last.Clear();fire?.Stop();}
    public void Dispose(){foreach(var p in playing)p.Dispose();fire?.Dispose();foreach(var c in clips.Values)c.Dispose();}
}
