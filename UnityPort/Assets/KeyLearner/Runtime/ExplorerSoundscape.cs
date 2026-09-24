using KeyLearner.Studio;
using UnityEngine;
namespace KeyLearner.Unity
{
    // Fixed sources, gentle envelopes and voice ducking. Only the active game drives it.
    public sealed class ExplorerSoundscape
    {
        readonly GameServices services;
        readonly ExplorerKind kind;
        readonly AudioSource ambient, motion, surface;
        float actionClock, previousTurn, previousPitch;
        bool braking;
        readonly System.Random variation=new System.Random(9173);
        float nextWave=3,nextBubble=7;
        float engineBase = .8f;
        public void SetVehicle(string id)
        {
            id = (id ?? "").ToLowerInvariant();
            engineBase = id.Contains("truck") || id.Contains("tractor") || id.Contains("delivery") ? .59f : id.Contains("sport") || id.Contains("race") ? 1.0f : .8f;
        }
        public ExplorerSoundscape(GameServices services, Transform parent, ExplorerKind kind)
        {
            this.services = services; this.kind = kind;
            ambient = Loop(parent, kind == ExplorerKind.Dolphin ? "ambient-ocean" : "ambient-wind");
            motion = Loop(parent, kind == ExplorerKind.Racer ? "engine" : kind == ExplorerKind.Dolphin ? "swim-swish" : "wing-flap");
            surface = Loop(parent, "ambient-wind");
        }
        AudioSource Loop(Transform parent, string clip)
        {
            var go = new GameObject("Soundscape / " + clip); go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = true;
            source.spatialBlend = 0; source.priority = 180; source.volume = 0;
            source.clip = services.Audio.SoundClip(clip); return source;
        }
        void Mix(AudioSource source, float target, float pitch, float dt)
        {
            source.volume = Mathf.MoveTowards(source.volume, target, dt * .35f);
            source.pitch = Mathf.Lerp(source.pitch, pitch, 1 - Mathf.Exp(-dt * 3));
            if (source.volume > .001f && source.clip && !source.isPlaying) source.Play();
            if (target == 0 && source.volume <= .001f) source.Stop();
        }
        public void Tick(float dt, float speed, float turn, float pitch, bool boost, float height, int vehicle)
        {
            var settings = services.Settings;
            if (!settings.Sound || !settings.EffectsSound) { Stop(); return; }
            float gain = settings.Volume / 100f * settings.EffectsVolume / 100f * (services.Audio.Pending > 0 ? .34f : 1);
            float effort = Mathf.Clamp01(speed / (kind == ExplorerKind.Racer ? 130 : 65));
            bool air = kind == ExplorerKind.Dolphin && height > 0;
            Mix(ambient, gain * (kind == ExplorerKind.Racer ? .055f : .14f) * (air ? .25f : 1), 1, dt);
            Mix(motion, gain * (kind == ExplorerKind.Racer ? .13f + effort * .09f : .035f + effort * .045f) * (air ? .1f : 1),
                kind == ExplorerKind.Racer ? engineBase + effort * .75f : .86f + effort * .25f, dt);
            Mix(surface, gain * (air ? .13f : 0), 1, dt);
            if(kind==ExplorerKind.Dolphin)
            {
                nextWave-=dt;nextBubble-=dt;
                if(nextWave<=0)
                {
                    nextWave=6+(float)variation.NextDouble()*8;
                    if(height>-12 && services.Audio.Pending==0)
                        services.Audio.Play("ocean-wave-"+(1+variation.Next(4)),settings,air?.11f:.035f,0,services.Camera.transform.position+services.Camera.transform.right*16+Vector3.up*3);
                }
                if(nextBubble<=0)
                {
                    nextBubble=8+(float)variation.NextDouble()*15;
                    if(!air && services.Audio.Pending==0)services.Audio.Play("swim-swish",settings,.035f,0,services.Camera.transform.position+services.Camera.transform.right*(variation.Next(2)==0?-12:12),.8f+(float)variation.NextDouble()*.3f);
                }
            }
            actionClock -= dt;
            if (kind == ExplorerKind.Racer)
            {
                bool brake = pitch > 0;
                if (brake && !braking && speed > 35) services.Audio.Play("brake", settings, .18f, .5);
                braking = brake;
            }
            else if (actionClock <= 0 && (Mathf.Abs(turn - previousTurn) > .6f || Mathf.Abs(pitch - previousPitch) > .6f || boost))
            {
                services.Audio.Play(kind == ExplorerKind.Dolphin ? "swim-swish" : "wing-flap", settings, .09f, .7);
                actionClock = 1.4f;
            }
            previousTurn = turn; previousPitch = pitch;
        }
        public void Stop()
        {
            foreach (var source in new[] { ambient, motion, surface }) { source.Stop(); source.volume = 0; }
        }
    }
}
