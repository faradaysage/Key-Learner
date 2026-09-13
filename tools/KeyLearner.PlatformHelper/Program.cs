using System.Diagnostics;
using System.IO.Pipes;
using System.Speech.Synthesis;
using System.Text;
using KeyLearner.Studio;

// Kept out of Unity's Mono runtime: Windows speech is warmed once per bounded lane.
// Protocol is local, current-user-only, and never stores spoken/key text on disk.
internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            if(args.Length >= 4 && args[0] == "--restore-accessibility") { AccessibilitySession.Watch(args); return 0; }
            if(args.Length == 4 && args[0] == "--speech") { RunSpeech(args[1], int.Parse(args[2]), long.Parse(args[3])); return 0; }
            if(args.Length == 1 && args[0] == "--probe-speech")
            {
                using var speech = new SpeechSynthesizer();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "speech-probe.txt"), $"PASS: warmed Windows speech; {speech.GetInstalledVoices().Count(v => v.Enabled)} installed voices.");
                return 0;
            }
            return 2;
        }
        catch(Exception error)
        {
            // Exception type only: never log private speech or input text.
            File.WriteAllText(Path.Combine(Path.GetTempPath(),"KeyLearner-platform-error.log"), error.GetType().Name);
            return 1;
        }
    }
    static void RunSpeech(string pipeName,int parentId,long startTicks)
    {
        using var parent = Process.GetProcessById(parentId);
        if(parent.StartTime.ToUniversalTime().Ticks != startTicks) return;
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        pipe.WaitForConnectionAsync(cancel.Token).GetAwaiter().GetResult();
        using var reader = new StreamReader(pipe,Encoding.UTF8,false,4096,true);
        using var writer = new StreamWriter(pipe,new UTF8Encoding(false),4096,true) { AutoFlush=true };
        var outputGate = new object();
        void Send(string message) { lock(outputGate) try {writer.WriteLine(message);}catch(IOException){}catch(ObjectDisposedException){} }
        var synths = new SpeechSynthesizer?[8];
        var tickets = new System.Collections.Concurrent.ConcurrentDictionary<Prompt,int>();
        for(int i=0;i<synths.Length;i++)
        {
            var lane=i;
            try {var synth=new SpeechSynthesizer();synths[i]=synth;
                synth.SpeakCompleted += (_,e) => { if(tickets.TryRemove(e.Prompt,out var ticket)) Send($"DONE\t{lane}\t{ticket}"); };
            } catch(PlatformNotSupportedException){} catch(InvalidOperationException){}
        }
        var voices=synths.FirstOrDefault(s=>s!=null)?.GetInstalledVoices().Where(v=>v.Enabled).Select(v=>v.VoiceInfo.Name).ToArray() ?? [];
        Send("READY\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("\n",voices))));
        using var monitor = new Timer(_ => {try{if(parent.HasExited)pipe.Dispose();}catch(InvalidOperationException){pipe.Dispose();}},null,1000,1000);
        try
        {
            while(reader.ReadLine() is {} line)
            {
                var parts=line.Split('\t');
                if(parts[0]=="QUIT") break;
                if(parts[0]=="CANCEL") { foreach(var s in synths)s?.SpeakAsyncCancelAll(); continue; }
                if(parts.Length!=7 || parts[0]!="SPEAK" || !int.TryParse(parts[1],out var lane) || lane<0 || lane>=8 || !int.TryParse(parts[2],out var ticket))continue;
                try
                {
                    var speech=synths[lane] ?? throw new InvalidOperationException();
                    speech.Volume=Math.Clamp(int.Parse(parts[3]),0,100);speech.Rate=Math.Clamp(int.Parse(parts[4]),-10,10);
                    var voice=Encoding.UTF8.GetString(Convert.FromBase64String(parts[5]));
                    if(voices.Length>0)speech.SelectVoice(voices.Contains(voice)?voice:voices[0]);
                    var text=Encoding.UTF8.GetString(Convert.FromBase64String(parts[6]));
                    if(text.Length>256)text=text[..256];
                    var prompt=new Prompt(text);
                    tickets[prompt]=ticket;speech.SpeakAsync(prompt);
                }
                catch(Exception e) when(e is not OutOfMemoryException) { Send($"DONE\t{lane}\t{ticket}"); }
            }
        }
        catch(IOException){}catch(ObjectDisposedException){}
        finally {foreach(var synth in synths){synth?.SpeakAsyncCancelAll();synth?.Dispose();}}
    }
}

