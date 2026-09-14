using UnityEngine;
using KeyLearner.Studio;
namespace KeyLearner.Unity
{
    /// <summary>Independent, softly mixed environmental details; no speech/model dependency.</summary>
    public sealed class AmbientLifeAudio
    {
        readonly GameServices services;
        readonly AudioSource bed;
        readonly System.Random random=new System.Random();
        float nextCall=2;
        public AmbientLifeAudio(GameServices services,Transform parent)
        {
            this.services=services;
            var go=new GameObject("Forest sound layer");go.transform.SetParent(parent,false);
            bed=go.AddComponent<AudioSource>();bed.playOnAwake=false;bed.loop=true;bed.volume=0;bed.priority=210;
            bed.clip=services.Audio.SoundClip("forest-birds");
        }
        public void Tick(float dt,Vector3 position,bool forest)
        {
            var settings=services.Settings;
            if(!settings.Sound || !settings.EffectsSound){Stop();return;}
            float gain=settings.Sound && settings.EffectsSound && forest?settings.Volume/100f*settings.EffectsVolume/100f:0;
            float duck=services.Audio.Pending>0?.28f:1;
            bed.volume=Mathf.MoveTowards(bed.volume,gain*.075f*duck,dt*.1f);
            if(bed.volume>.001f && bed.clip && !bed.isPlaying){bed.time=(float)random.NextDouble()*bed.clip.length;bed.Play();}
            if(bed.volume<=.001f)bed.Stop();
            nextCall-=dt;
            if(nextCall<=0)
            {
                nextCall=7+(float)random.NextDouble()*14;
                if(gain>0 && services.Audio.Pending==0)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2;
                    var source=position+new Vector3(Mathf.Sin(angle)*35,10,Mathf.Cos(angle)*35);
                    services.Audio.Play("bird-call-"+random.Next(1,4),settings,.2f,2,source,.93f+(float)random.NextDouble()*.14f);
                }
            }
        }
        public void Stop(){bed.Stop();bed.volume=0;}
    }
}
