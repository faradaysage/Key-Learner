using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using KeyLearner.Studio;
using UnityEngine;

namespace KeyLearner.Unity.Platform
{
    /// <summary>Unity WAV/effect playback, with warmed offline Windows synthesis isolated in its own helper.</summary>
    public sealed class UnityAudioService : IDisposable
    {
        sealed class Request
        {
            public string Text, Recording, Executable, Model, Voice, SpeechKey, PackId; public VoicePackRegistry.SpeechAsset Asset; public bool SkipRecording; public int Rate, Volume; public bool Key; public double Time;
        }
        sealed class Lane
        {
            public AudioSource Source; public Request Request; public int Ticket; public bool Speech; public double Started;
        }
        readonly Lane[] lanes = new Lane[8];
        readonly AudioSource[] effects = new AudioSource[8];
        readonly System.Random effectVariation = new System.Random();
        readonly AudioSource fire, music;
        float musicGain;
        string musicName = "";
        readonly Queue<Request> keys = new Queue<Request>(), words = new Queue<Request>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, long> speechUse = new Dictionary<string, long>();
        long speechClock;
        readonly Dictionary<string, double> lastEffects = new Dictionary<string, double>();
        readonly BlockingCollection<Request> prepare = new BlockingCollection<Request>(64);
        readonly ConcurrentDictionary<string, byte> preparing = new ConcurrentDictionary<string, byte>();
        readonly ConcurrentQueue<string> replies = new ConcurrentQueue<string>();
        readonly object pipeGate = new object();
        readonly string cache, streaming;
        public VoicePackRegistry SpeechPacks {get;}
        public string LastSpeechKey {get;private set;}="";
        public string LastVoicePackId {get;private set;}="";
        NamedPipeClientStream pipe; StreamWriter writer; Process helper, piper; Thread speechThread, prepareThread;
        volatile bool disposed;
        bool ready;
        int ticket, keyChannels = 4, wordChannels = 2;
        public int Requested
        {
            get; private set;
        }
        public int Started
        {
            get; private set;
        }
        public int Completed
        {
            get; private set;
        }
        public int Overflow
        {
            get; private set;
        }
        public int PeakOverlap
        {
            get; private set;
        }
        public int PreparedStarted { get; private set; }
        public int FallbackStarted { get; private set; }
        public int CachedSpeechClips => speechUse.Count;
        public int MusicFrames { get; private set; }
        public int Pending => keys.Count + words.Count + lanes.Count(l => l.Request != null);
        public string Status { get; private set; } = "Warming offline Windows speech";
        public string[] Voices { get; private set; } = Array.Empty<string>();
        static double Now => KeyboardGuard.Now;
        public UnityAudioService(GameObject owner, string profileRoot, VoicePackRegistry speechPacks = null)
        {
            cache = Path.Combine(profileRoot, "voice-cache");
            Directory.CreateDirectory(cache);
            streaming = Application.streamingAssetsPath;
            SpeechPacks = speechPacks ?? new VoicePackRegistry(Path.Combine(streaming,"Content","Voice"), message => UnityEngine.Debug.LogWarning(message));
            for (int i = 0; i < lanes.Length; i++)
                lanes[i] = new Lane { Source = Source(owner, "Voice " + i) };
            for (int i = 0; i < effects.Length; i++)
                effects[i] = Source(owner, "Effect " + i);
            fire = Source(owner, "Fire ambience");
            fire.loop = true;
            music = Source(owner, "Learning bonus music");
            music.loop = true;
            music.priority = 190;
            foreach (var name in new[] { "pop", "paint", "crack", "shatter", "cannon", "fire", "squawk" })
                LoadClip(Path.Combine(streaming, "Content", "Sounds", name + ".wav"));
            clips["retry"] = Tone("retry", .25, t => Math.Sin(t * Math.PI * 2 * (t < .12 ? 170 : 125)) * Math.Min(1, t * 40) * Math.Max(0, 1 - t * 4));
            clips["powerup"] = Tone("powerup", .8, t => { int note = Math.Min(3, (int)(t / .14)); double phase = t - note * .14, duration = note == 3 ? .38 : .14, hz = new[] { 523.25, 659.25, 783.99, 1046.5 }[note]; return (Math.Sin(Math.PI * 2 * hz * phase) + .25 * Math.Sin(Math.PI * 4 * hz * phase)) * Math.Sin(Math.PI * phase / duration) * .55; });
            clips["sonar"] = Tone("sonar", .45, t => Math.Sin(Math.PI * 2 * (880 * t - 300 * t * t)) * Math.Exp(-8 * t));
            clips["horn"] = Tone("horn", .28, t => (Math.Sin(Math.PI * 2 * 220 * t) + .25 * Math.Sin(Math.PI * 2 * 440 * t)) * Math.Sin(Math.PI * t / .28));
            try
            {
                using (var current = Process.GetCurrentProcess())
                {
                    var name = "KeyLearner-speech-" + Guid.NewGuid().ToString("N");
                    helper = PlatformProcess.Start("--speech " + name + " " + current.Id + " " + current.StartTime.ToUniversalTime().Ticks);
                    pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                    speechThread = new Thread(ReadSpeech) { IsBackground = true, Name = "KeyLearner speech completion" };
                    speechThread.Start();
                }
            }
            catch (Exception e) { Status = "Prepared offline voice available; Windows speech helper unavailable (" + e.GetType().Name + ")."; }
            prepareThread = new Thread(PrepareAudio) { IsBackground = true, Name = "KeyLearner optional Piper cache" };
            prepareThread.Start();
        }
        static AudioSource Source(GameObject owner, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(owner.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            return source;
        }
        void ReadSpeech()
        {
            try
            {
                pipe.Connect(15000);
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
                {
                    lock (pipeGate)
                        writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
                    string line;
                    while (!disposed && (line = reader.ReadLine()) != null)
                        replies.Enqueue(line);
                }
            }
            catch (Exception e) when (e is IOException || e is TimeoutException || e is ObjectDisposedException) { replies.Enqueue("FAILED"); }
        }
        void Send(string message)
        {
            lock (pipeGate)
            {
                try
                {
                    writer?.WriteLine(message);
                }
                catch (IOException) { ready = false; }
                catch (ObjectDisposedException) { ready = false; }
            }
        }
        static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? ""));
        public void Say(string text, Settings settings, string recording = "", bool key = false, bool brisk = false, string voicePackId = null)
            => Enqueue(text,settings,recording,key,brisk,voicePackId);
        void Enqueue(string text, Settings settings, string recording = "", bool key = false, bool brisk = false, string voicePackId = null, string speechKey = null)
        {
            if (disposed || !settings.Sound || string.IsNullOrWhiteSpace(text))
                return;
            keyChannels = Math.Max(1, Math.Min(5, settings.KeyVoiceChannels));
            wordChannels = Math.Max(1, Math.Min(3, settings.WordVoiceChannels));
            var request = new Request { Text = text, Recording = recording, Executable = brisk ? "" : settings.PiperExecutable, Model = brisk ? "" : settings.PiperModel, Voice = settings.WindowsVoice, Rate = brisk ? Math.Max(3, Math.Min(8, settings.SpeechRate + 5)) : settings.SpeechRate, Volume = settings.Volume, Key = key, Time = Now, SpeechKey = speechKey ?? SpeechPacks.KeyForText(text), PackId = SpeechPacks.NormalizeChoice(voicePackId ?? settings.VoicePackId) };
            Requested++;
            var queue = key ? keys : words;
            if (queue.Count >= (key ? 8 : 32))
            {
                Overflow++;
                return;
            }
            queue.Enqueue(request);
        }
        public void SayKey(string speechKey,Settings settings,string voicePackId=null)
        {
            var text=SpeechPacks.TextForKey(speechKey);
            if(text!=null)Enqueue(text,settings,voicePackId:voicePackId,speechKey:speechKey);
            else SpeechPacks.Resolve(speechKey,voicePackId??settings.VoicePackId);
        }
        public void SelectVoice(Settings settings,string voiceId)
        {
            var next=SpeechPacks.NormalizeChoice(voiceId);
            if(settings.VoicePackId!=next)StopSpeech();
            settings.VoicePackId=next;
        }
        public void PreviewVoice(string voiceId,Settings settings)
        {StopSpeech();SayKey(VoicePackRegistry.PreviewKey,settings,voiceId);}
        AudioClip LoadSpeech(Request request)
        {
            for(int attempt=0;attempt<3;attempt++)
            {
                var wav=FindAudio(request);if(wav==null)return null;
                var clip=LoadClip(wav,true);if(clip!=null)return clip;
                if(request.Asset!=null)SpeechPacks.Reject(request.Asset);else request.SkipRecording=true;
            }
            return null;
        }
        public void Update(Settings settings)
        {
            if (disposed)
                return;
            while (replies.TryDequeue(out var reply))
            {
                var parts = reply.Split('\t');
                if (parts[0] == "READY")
                {
                    ready = true;
                    Voices = parts.Length > 1 ? Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
                    Status = "Offline voice · bounded overlapping playback";
                }
                else if (parts[0] == "DONE" && parts.Length == 3 && int.TryParse(parts[1], out var index) && int.TryParse(parts[2], out var serial) && index >= 0 && index < 8 && lanes[index].Ticket == serial)
                    Finish(lanes[index]);
                else if (parts[0] == "FAILED")
                {
                    ready = false;
                    Status = "Windows speech helper disconnected; prepared clips remain available.";
                }
            }
            if (!settings.Sound)
            {
                Stop();
                return;
            }
            float targetMusic = settings.EffectsSound ? musicGain * settings.Volume / 100f * settings.EffectsVolume / 100f * (Pending > 0 ? .25f : 1) : 0;
            music.volume = Mathf.MoveTowards(music.volume, targetMusic, Time.unscaledDeltaTime * .3f);
            if (music.volume > .001f && music.clip && !music.isPlaying) music.Play();
            if (music.volume <= .001f && targetMusic == 0) music.Stop();
            if (music.isPlaying && music.volume > .001f) MusicFrames++;
            foreach (var lane in lanes)
                if (lane.Request != null && ((!lane.Speech && !lane.Source.isPlaying) || Now - lane.Started > 30))
                    Finish(lane);
            for (int i = 0; i < lanes.Length; i++)
            {
                var lane = lanes[i];
                if (lane.Request != null)
                    continue;
                bool key = i < 5;
                if (key ? i >= keyChannels : i - 5 >= wordChannels)
                    continue;
                var queue = key ? keys : words;
                if (queue.Count == 0)
                    continue;
                var request = queue.Peek();
                AudioClip clip = LoadSpeech(request);
                if(clip==null && request.SpeechKey!=null)
                {queue.Dequeue();Completed++;Status="Prepared speech unavailable.";continue;}
                if (clip == null && !ready)
                {
                    if (Now - request.Time > 2)
                    {
                        queue.Dequeue();
                        Overflow++;
                    }
                    continue;
                }
                queue.Dequeue();
                lane.Request = request;
                lane.Ticket = ++ticket;
                lane.Started = Now;
                lane.Speech = clip == null;
                if (clip != null)
                {
                    lane.Source.clip = clip;
                    lane.Source.volume = request.Volume / 100f * (key ? .7f : 1);
                    lane.Source.Play();
                }
                else
                    Send("SPEAK\t" + i + "\t" + lane.Ticket + "\t" + (int)(request.Volume * (key ? .7 : 1)) + "\t" + request.Rate + "\t" + Encode(request.Voice) + "\t" + Encode(request.Text.Length == 1 ? request.Text.ToUpperInvariant() : request.Text));
                if (clip != null && request.Asset != null) {PreparedStarted++;LastSpeechKey=request.Asset.Key;LastVoicePackId=request.Asset.VoiceId;}
                if (clip == null) FallbackStarted++;
                Started++;
            }
            PeakOverlap = Math.Max(PeakOverlap, lanes.Count(l => l.Request != null));
            if (!settings.EffectsSound)
            {
                foreach (var source in effects)
                    source.Stop();
                fire.Stop();
            }
        }
        void Finish(Lane lane)
        {
            if (lane.Request == null)
                return;
            Completed++;
            lane.Request = null;
            lane.Speech = false;
            lane.Source.Stop();
        }
        static bool Simple(string value) => value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9');
        string Destination(Request request)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(request.Model + File.GetLastWriteTimeUtc(request.Model).Ticks + request.Text));
                return Path.Combine(cache, BitConverter.ToString(bytes).Replace("-", "") + ".wav");
            }
        }
        string FindAudio(Request request)
        {
            request.Asset=null;
            if (!request.SkipRecording && File.Exists(request.Recording))
                return request.Recording;
            if(request.SpeechKey!=null){request.Asset=SpeechPacks.Resolve(request.SpeechKey,request.PackId);return request.Asset?.Path;}
            var bundle = Path.Combine(streaming, "Content", "Voice", request.Text.ToLowerInvariant() + ".wav");
            var fallback = Simple(request.Text) && File.Exists(bundle) ? bundle : null;
            if (!File.Exists(request.Model))
                return fallback;
            var prepared = Path.Combine(request.Model + ".clips", request.Text.ToLowerInvariant() + ".wav");
            if (Simple(request.Text) && File.Exists(prepared))
                return prepared;
            var destination = Destination(request);
            if (File.Exists(destination))
                return destination;
            if (File.Exists(request.Executable) && preparing.TryAdd(destination, 0) && !prepare.TryAdd(request))
                preparing.TryRemove(destination, out _);
            return fallback;
        }
        void PrepareAudio()
        {
            foreach (var request in prepare.GetConsumingEnumerable())
            {
                if (disposed)
                    break;
                string destination = Destination(request), temporary = destination + ".tmp.wav";
                try
                {
                    var info = new ProcessStartInfo(request.Executable, "--model " + PlatformProcess.Quote(request.Model) + " --output_file " + PlatformProcess.Quote(temporary)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    using (var process = new Process { StartInfo = info })
                    {
                        piper = process;
                        if (disposed)
                            break;
                        process.Start();
                        var output = process.StandardOutput.ReadToEndAsync();
                        var error = process.StandardError.ReadToEndAsync();
                        process.StandardInput.WriteLine(request.Text);
                        process.StandardInput.Close();
                        if (!process.WaitForExit(15000))
                        {
                            process.Kill();
                            continue;
                        }
                        if (process.ExitCode == 0 && File.Exists(temporary))
                        {
                            if (File.Exists(destination))
                                File.Delete(destination);
                            File.Move(temporary, destination);
                        }
                    }
                    foreach (var old in new DirectoryInfo(cache).GetFiles("*.wav").OrderByDescending(f => f.LastWriteTimeUtc).Skip(500))
                        old.Delete();
                }
                catch (Exception e) when (!(e is OutOfMemoryException)) { }
                finally { piper = null; preparing.TryRemove(destination, out _); try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
            }
        }
        public void Play(string name, Settings settings, float gain = 1, double minimumInterval = .09, Vector3? position = null, float pitch = 1)
        {
            if (disposed || !settings.Sound || !settings.EffectsSound)
                return;
            double now = Now;
            if (lastEffects.TryGetValue(name, out var when) && now - when < minimumInterval)
                return;
            var source = effects.FirstOrDefault(s => !s.isPlaying);
            if (source == null)
                return;
            if (!clips.TryGetValue(name, out var clip))
                clip = LoadClip(Path.Combine(streaming, "Content", "Sounds", name + ".wav"));
            if (clip == null)
                return;
            lastEffects[name] = now;
            source.clip = clip;
            source.spatialBlend = position.HasValue ? 1 : 0;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 12; source.maxDistance = 180;
            if(position.HasValue)source.transform.position=position.Value;
            bool vary=name=="pop" || name=="paint" || name=="ball-touch" || name=="bubble-pop" || name=="surface-splash" || name.StartsWith("dinosaur-step-");
            source.pitch=Mathf.Clamp(pitch*(vary?.96f+(float)effectVariation.NextDouble()*.08f:1),.5f,1.6f);
            source.volume = Mathf.Clamp01(settings.EffectsVolume / 100f * settings.Volume / 100f * gain);
            source.Play();
        }
        public void SetMusic(string name, float gain = .16f)
        {
            if (disposed) return;
            musicGain = string.IsNullOrEmpty(name) ? 0 : gain;
            if (musicGain > 0 && musicName != name)
            {
                music.Stop();
                music.clip = SoundClip(name);
                musicName = name;
            }
        }
        public void SetFire(float heat, Settings settings)
        {
            if (disposed)
                return;
            fire.volume = settings.Sound && settings.EffectsSound ? Mathf.Clamp01(heat * .55f * settings.EffectsVolume / 100f * settings.Volume / 100f) : 0;
            if (fire.volume > .001f)
            {
                if (fire.clip == null)
                    fire.clip = LoadClip(Path.Combine(streaming, "Content", "Sounds", "fire.wav"));
                if (fire.clip != null && !fire.isPlaying)
                    fire.Play();
            }
            else
                fire.Stop();
        }
        public AudioClip SoundClip(string name) => LoadClip(Path.Combine(streaming, "Content", "Sounds", name + ".wav"));
        AudioClip LoadClip(string path, bool speech = false)
        {
            if (clips.TryGetValue(path, out var existing))
            {
                if (speech) speechUse[path] = ++speechClock;
                return existing;
            }
            if (!File.Exists(path))
                return null;
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    if (new string(reader.ReadChars(4)) != "RIFF")
                        return null;
                    reader.ReadInt32();
                    if (new string(reader.ReadChars(4)) != "WAVE")
                        return null;
                    int channels = 0, rate = 0, bits = 0, format = 0;
                    byte[] data = null;
                    while (stream.Position + 8 <= stream.Length)
                    {
                        string chunk = new string(reader.ReadChars(4));
                        int length = reader.ReadInt32();
                        if (length < 0 || stream.Position + length > stream.Length)
                            return null;
                        long next = stream.Position + length + (length & 1);
                        if (chunk == "fmt " && length >= 16)
                        {
                            format = reader.ReadUInt16();
                            channels = reader.ReadUInt16();
                            rate = reader.ReadInt32();
                            reader.ReadInt32();
                            reader.ReadUInt16();
                            bits = reader.ReadUInt16();
                        }
                        else if (chunk == "data")
                            data = reader.ReadBytes(length);
                        stream.Position = Math.Min(next, stream.Length);
                    }
                    if (data == null || channels < 1 || channels > 2 || rate < 8000 || rate > 192000 || !(format == 1 && (bits == 8 || bits == 16 || bits == 24 || bits == 32) || format == 3 && bits == 32))
                        return null;
                    int bytes = bits / 8;
                    var samples = new float[data.Length / bytes];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        int at = i * bytes;
                        if (format == 3)
                            samples[i] = BitConverter.ToSingle(data, at);
                        else if (bits == 8)
                            samples[i] = (data[at] - 128) / 128f;
                        else if (bits == 16)
                            samples[i] = BitConverter.ToInt16(data, at) / 32768f;
                        else if (bits == 24)
                            samples[i] = ((data[at] | data[at + 1] << 8 | data[at + 2] << 16) << 8 >> 8) / 8388608f;
                        else
                            samples[i] = BitConverter.ToInt32(data, at) / 2147483648f;
                    }
                    var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), samples.Length / channels, channels, rate, false);
                    clip.SetData(samples, 0);
                    clips[path] = clip;
                    if (speech)
                    {
                        speechUse[path] = ++speechClock;
                        TrimSpeechCache(path);
                    }
                    return clip;
                }
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return null; }
        }
        // Keep a large vocabulary from retaining thousands of decoded clips. Active lanes
        // are protected; soundscape loops belong to the separate permanent effects cache.
        void TrimSpeechCache(string newest)
        {
            long bytes = speechUse.Keys.Sum(k => (long)clips[k].samples * clips[k].channels * 4);
            foreach (var entry in speechUse.OrderBy(p => p.Value).ToArray())
            {
                if (speechUse.Count <= 64 && bytes <= 32 * 1024 * 1024) break;
                var clip = clips[entry.Key];
                if (entry.Key == newest || lanes.Any(l => l.Request != null && l.Source.clip == clip)) continue;
                foreach (var lane in lanes) if (lane.Source.clip == clip) lane.Source.clip = null;
                bytes -= (long)clip.samples * clip.channels * 4;
                speechUse.Remove(entry.Key);
                clips.Remove(entry.Key);
                UnityEngine.Object.Destroy(clip);
            }
        }
        static AudioClip Tone(string name, double seconds, Func<double, double> wave)
        {
            var samples = new float[(int)(22050 * seconds)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)Math.Max(-.3, Math.Min(.3, wave(i / 22050d) * .2));
            var clip = AudioClip.Create(name, samples.Length, 1, 22050, false);
            clip.SetData(samples, 0);
            return clip;
        }
        public void StopSpeech()
        {
            keys.Clear();
            words.Clear();
            while (prepare.TryTake(out var waiting))
                preparing.TryRemove(Destination(waiting), out _);
            Send("CANCEL");
            foreach (var lane in lanes)
            {
                lane.Ticket = ++ticket;
                lane.Request = null;
                lane.Source.Stop();
                lane.Speech = false;
            }
        }
        public void Stop()
        {
            StopSpeech();
            musicGain = 0;
            music.Stop();
            music.volume = 0;
            foreach (var source in effects)
                source.Stop();
            fire.Stop();
            lastEffects.Clear();
        }
        public void Dispose()
        {
            if (disposed)
                return;
            Stop();
            Send("QUIT");
            disposed = true;
            prepare.CompleteAdding();
            try
            {
                piper?.Kill();
            }
            catch (InvalidOperationException) { }
            pipe?.Dispose();
            speechThread?.Join(2000);
            prepareThread?.Join(2000);
            if (helper != null)
            {
                try
                {
                    if (!helper.WaitForExit(2000))
                        helper.Kill();
                }
                catch (InvalidOperationException) { }
                helper.Dispose();
            }
            foreach (var clip in clips.Values.Distinct())
                UnityEngine.Object.Destroy(clip);
            foreach (var lane in lanes)
                UnityEngine.Object.Destroy(lane.Source.gameObject);
            foreach (var source in effects)
                UnityEngine.Object.Destroy(source.gameObject);
            UnityEngine.Object.Destroy(fire.gameObject);
            UnityEngine.Object.Destroy(music.gameObject);
        }
    }
}
