using UnityEngine;
namespace KeyLearner.Unity
{
    /// <summary>Short-lived surface spray and expanding wake; shared by marine actors.</summary>
    public sealed class WaterSplash : MonoBehaviour
    {
        LineRenderer ring;
        float age, strength;
        ParticleSystem spray;
        public float Age=>age;
        public bool Entering {get;private set;}
        public int Droplets=>spray?spray.particleCount:0;
        public static WaterSplash Create(GameServices services, Transform parent, Vector3 position, float speed, bool entering)
        {
            var go = new GameObject(entering ? "Dolphin entry splash" : "Dolphin exit splash");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, .12f, position.z);
            var effect = go.AddComponent<WaterSplash>();effect.Entering=entering;
            effect.strength = Mathf.Lerp(.7f, 1.8f, Mathf.InverseLerp(18, 100, speed));
            var ps = go.AddComponent<ParticleSystem>();effect.spray=ps;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = .1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(.24f, .72f);
            main.startColor = new Color(.78f, .96f, 1, .85f);
            main.gravityModifier = 1.2f; main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.75f, .5f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Visuals.ParticleMaterial();
            int count = services.Settings.GentleMotion ? 28 : 72;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f, radius = 1 + (i % 5) * .23f;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                ps.Emit(new ParticleSystem.EmitParams { position = go.transform.position + radial * radius,
                    velocity = (radial * (2.5f + i % 4) + Vector3.up * (entering ? 5 : 3.5f)) * effect.strength }, 1);
            }
            ps.Play();
            effect.ring = go.AddComponent<LineRenderer>();
            effect.ring.useWorldSpace = false; effect.ring.loop = true; effect.ring.positionCount = 65;
            effect.ring.sharedMaterial = Visuals.WakeMaterial();
            effect.ring.widthMultiplier = .25f;
            effect.ring.numCornerVertices = 2;
            services.Audio.Play("surface-splash", services.Settings, Mathf.Clamp(.24f * effect.strength * (entering ? 1.2f : 1), .18f, .48f), .12, go.transform.position);
            services.Audio.Play("ocean-wave-"+(entering?2:3),services.Settings,.11f*effect.strength,.15,go.transform.position,entering?1.15f:1.3f);
            Destroy(go, 1.8f);return effect;
        }
        void Update()
        {
            age += Time.deltaTime;
            float radius = 1.3f + age * 7 * strength;
            for (int i = 0; i < 65; i++)
            {
                float angle = i * Mathf.PI * 2 / 65;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
            }
            var color = new Color(.74f, .95f, 1, Mathf.Clamp01(1 - age / 1.6f) * .7f);
            ring.startColor = ring.endColor = color;
            ring.widthMultiplier = .12f + age * .28f;
        }
    }
}
