using Microsoft.Xna.Framework.Audio;
namespace KeyLearner.Studio;
/// <summary>A separate, bounded effects bus; it never stops or queues speech.</summary>
public sealed class SoundEffects : IDisposable
{
    readonly Dictionary<string,SoundEffect> clips=new();
    readonly List<SoundEffectInstance> playing=new();
    readonly Dictionary<string,double> last=new();
    SoundEffectInstance? fire;
    public SoundEffects(){try{foreach(var name in new[]{"pop","paint","crack","shatter","cannon","fire","squawk"}){using var s=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Content","Sounds",name+".wav"));clips[name]=SoundEffect.FromStream(s);}fire=clips["fire"].CreateInstance();fire.IsLooped=true;}catch(Exception e) when(e is IOException or NoAudioHardwareException or InvalidOperationException){}}
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
