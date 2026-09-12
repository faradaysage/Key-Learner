using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using Microsoft.Xna.Framework.Audio;
namespace KeyLearner.Studio;

/// <summary>Bounded polyphony. New input never cancels audio already playing.</summary>
public sealed class Voice : IDisposable
{
    private sealed record Request(string Text,string Recording,string Executable,string Model,string Selected,int Rate,int Volume,bool Key,double Time);
    private sealed class Lane
    {
        public SpeechSynthesizer? Synth;
        public SoundEffectInstance? Audio;
        public SoundEffect? Clip;
        public int Busy; public Prompt? Prompt;
        public Request? Request;
    }
    private readonly Lane[] lanes=Enumerable.Range(0,8).Select(_=>new Lane()).ToArray();
    private readonly Queue<Request> keys=new(),words=new();
    private readonly BlockingCollection<Request> preparation=new(64);
    private readonly ConcurrentDictionary<string,byte> preparing=new();
    private readonly Thread worker;
    private readonly string cache;
    private volatile bool stopping;
    private Process? process;
    public int Pending=>keys.Count+words.Count+lanes.Count(l=>l.Request!=null);
    public string Status {get;private set;}="Windows speech";
    public string[] Voices {get;private set;}=[];
    public int Requested {get;private set;}
    public int Started {get;private set;}
    public int Completed {get;private set;}
    public int Overflow {get;private set;}
    public int PeakOverlap {get;private set;}
    public double MaximumStartDelay {get;private set;}
    public List<string> Trace {get;}=new();
    private int keyChannels=4,wordChannels=2;
    private static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    public Voice(string root)
    {
        cache=Path.Combine(root,"voice-cache");Directory.CreateDirectory(cache);
        // Warm reusable synthesizers before play instead of constructing one per keystroke.
        foreach(var lane in lanes)
        {
            try {lane.Synth=new SpeechSynthesizer();lane.Synth.SpeakCompleted+=(_,e)=>{if(ReferenceEquals(lane.Prompt,e.Prompt))Interlocked.Exchange(ref lane.Busy,0);};}
            catch(Exception e){Status="Windows speech unavailable: "+e.Message;}
        }
        Voices=lanes[0].Synth?.GetInstalledVoices().Where(v=>v.Enabled).Select(v=>v.VoiceInfo.Name).ToArray()??[];
        var warm=Path.Combine(AppContext.BaseDirectory,"Content","Voice","a.wav");
        if(File.Exists(warm)){try{using var stream=File.OpenRead(warm);using var audio=SoundEffect.FromStream(stream);}catch(Exception e){Debug.WriteLine(e);}}
        worker=new Thread(PrepareAudio){IsBackground=true,Name="KeyLearner offline voice cache"};worker.Start();
    }
    public void Say(string text,Settings settings,string recording="",bool key=false,bool brisk=false)
    {
        if(!settings.Sound || string.IsNullOrWhiteSpace(text))return;
        keyChannels=Math.Clamp(settings.KeyVoiceChannels,1,5);wordChannels=Math.Clamp(settings.WordVoiceChannels,1,3);
        var r=new Request(text,recording,brisk?"":settings.PiperExecutable,brisk?"":settings.PiperModel,settings.WindowsVoice,brisk?Math.Clamp(settings.SpeechRate+5,3,8):settings.SpeechRate,settings.Volume,key,Now);
        Requested++;
        var queue=key?keys:words;
        if(queue.Count>=(key?8:32)){Overflow++;Log("overflow "+text);return;}
        queue.Enqueue(r);Update();
    }
    private void Log(string line){if(Trace.Count>=256)Trace.RemoveAt(0);Trace.Add(line);}
    private string Destination(Request r)=>Path.Combine(cache,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(r.Model+File.GetLastWriteTimeUtc(r.Model).Ticks+r.Text)))+".wav");
    private string? FindAudio(Request r)
    {
        if(File.Exists(r.Recording))return r.Recording;
        var bundled=Path.Combine(AppContext.BaseDirectory,"Content","Voice",r.Text.ToLowerInvariant()+".wav");
        string? fallback=r.Text.All(char.IsAsciiLetterOrDigit) && File.Exists(bundled)?bundled:null;
        if(!File.Exists(r.Model))return fallback;
        var clip=Path.Combine(r.Model+".clips",r.Text.ToLowerInvariant()+".wav");
        if(r.Text.All(char.IsAsciiLetterOrDigit) && File.Exists(clip))return clip;
        var cached=Destination(r);
        if(File.Exists(cached))return cached;
        if(File.Exists(r.Executable) && preparing.TryAdd(cached,0) && !preparation.TryAdd(r))preparing.TryRemove(cached,out _);
        return fallback; // Speak immediately with Windows; prepare neural audio for next time.
    }
    public void Update()
    {
        foreach(var lane in lanes)
        {
            if(lane.Request==null)continue;
            if(lane.Audio is {} audio && audio.State==SoundState.Stopped)Interlocked.Exchange(ref lane.Busy,0);
            if(Volatile.Read(ref lane.Busy)!=0)continue;
            Completed++;Log("complete "+lane.Request.Text);
            lane.Audio?.Dispose();lane.Clip?.Dispose();lane.Audio=null;lane.Clip=null;lane.Request=null;
        }
        for(int i=0;i<lanes.Length;i++)
        {
            var lane=lanes[i];if(lane.Request!=null)continue;
            var key=i<5;if(key?i>=keyChannels:i-5>=wordChannels)continue;
            var queue=key?keys:words;if(queue.Count==0)continue;
            var r=queue.Dequeue();lane.Prompt=null;lane.Request=r;Interlocked.Exchange(ref lane.Busy,1);
            try
            {
                var wav=FindAudio(r);
                if(wav!=null)
                {
                    try
                    {
                        using var stream=File.OpenRead(wav);lane.Clip=SoundEffect.FromStream(stream);lane.Audio=lane.Clip.CreateInstance();
                        lane.Audio.Volume=r.Volume/100f*(r.Key?.7f:1);lane.Audio.Play();Status="Offline voice · overlapping playback";
                    }
                    catch(Exception e) when(e is not OutOfMemoryException){lane.Audio?.Dispose();lane.Clip?.Dispose();lane.Audio=null;lane.Clip=null;Speak(lane,r);}
                }
                else Speak(lane,r);
                Started++;MaximumStartDelay=Math.Max(MaximumStartDelay,Now-r.Time);Log("start "+r.Text);
            }
            catch(Exception e) when(e is not OutOfMemoryException){Status="Voice unavailable: "+e.Message;Log("failed "+r.Text);lane.Request=null;Interlocked.Exchange(ref lane.Busy,0);}
        }
        PeakOverlap=Math.Max(PeakOverlap,lanes.Count(l=>l.Request!=null));
    }
    private void Speak(Lane lane,Request r)
    {
        var synth=lane.Synth??throw new InvalidOperationException("No installed speech engine.");
        synth.Rate=r.Rate;synth.Volume=(int)(r.Volume*(r.Key?.7:1));
        if(Voices.Length>0)synth.SelectVoice(Voices.Contains(r.Selected)?r.Selected:Voices[0]);
        lane.Prompt=synth.SpeakAsync(r.Text.Length==1 && char.IsAsciiLetter(r.Text[0])?r.Text.ToUpperInvariant():r.Text);
        Status="Windows speech · overlapping playback";
    }
    private void PrepareAudio()
    {
        foreach(var r in preparation.GetConsumingEnumerable())
        {
            if(stopping)break;
            var destination=Destination(r);var temp=destination+".tmp.wav";
            try
            {
                var info=new ProcessStartInfo(r.Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                info.ArgumentList.Add("--model");info.ArgumentList.Add(r.Model);info.ArgumentList.Add("--output_file");info.ArgumentList.Add(temp);
                using var p=new Process{StartInfo=info};process=p;if(stopping)break;p.Start();
                var error=p.StandardError.ReadToEndAsync();var output=p.StandardOutput.ReadToEndAsync();p.StandardInput.WriteLine(r.Text);p.StandardInput.Close();
                if(!p.WaitForExit(15000)){p.Kill(true);continue;}
                if(p.ExitCode==0 && File.Exists(temp))File.Move(temp,destination,true);
                foreach(var old in new DirectoryInfo(cache).GetFiles("*.wav").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(500))old.Delete();
            }
            catch(Exception e) when(e is not OutOfMemoryException){Debug.WriteLine(e);}
            finally {process=null;preparing.TryRemove(destination,out _);try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}}
        }
    }
    public void Stop()
    {
        keys.Clear();words.Clear();
        while(preparation.TryTake(out var waiting))preparing.TryRemove(Destination(waiting),out _);
        foreach(var lane in lanes){lane.Prompt=null;lane.Synth?.SpeakAsyncCancelAll();lane.Audio?.Stop();lane.Audio?.Dispose();lane.Clip?.Dispose();lane.Audio=null;lane.Clip=null;lane.Request=null;Interlocked.Exchange(ref lane.Busy,0);}
    }
    public void Dispose()
    {
        Stop();stopping=true;preparation.CompleteAdding();try{if(process is {HasExited:false})process.Kill(true);}catch(InvalidOperationException){}
        worker.Join(2000);foreach(var lane in lanes)lane.Synth?.Dispose();
    }
}
