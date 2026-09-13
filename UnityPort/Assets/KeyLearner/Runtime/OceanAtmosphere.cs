using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>Bounded water particles, separate from learning rewards and source animal animation.</summary>
    public sealed class OceanAtmosphere : MonoBehaviour
    {
        GameServices services;
        ParticleSystem bubbles, suspended;
        Material bubbleMaterial;
        bool gentle;
        public static void Create(GameServices services, Transform parent, Transform dolphin)
        {
            var host = new GameObject("Water atmosphere");
            host.transform.SetParent(parent, false);
            var effect = host.AddComponent<OceanAtmosphere>();
            effect.services = services;
            effect.bubbleMaterial = new Material(Resources.Load<Shader>("Shaders/Bubble"));
            effect.bubbles = effect.Particles("Rising dolphin bubbles", host.transform, 45, 5, .48f, new Color(.72f, .95f, 1, .6f), effect.bubbleMaterial);
            var bubbleShape = effect.bubbles.shape;
            bubbleShape.shapeType = ParticleSystemShapeType.Sphere;
            bubbleShape.radius = .35f;
            var velocity = effect.bubbles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(2, 3.5f);
            effect.bubbles.transform.SetParent(dolphin, false);
            effect.bubbles.transform.localPosition = new Vector3(0, 0, -2);
            effect.suspended = effect.Particles("Suspended water light", host.transform, 220, 17, .14f, new Color(.65f, .88f, 1, .24f), Visuals.ParticleMaterial());
            var shape = effect.suspended.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(105, 65, 125);
            effect.SetMotion();
            effect.PlaceVolume();
            effect.suspended.Emit(100);
        }
        ParticleSystem Particles(string name, Transform parent, int maximum, float lifetime, float size, Color color, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maximum;
            main.startLifetime = lifetime;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(size * .6f, size * 1.3f);
            main.startColor = color;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            system.Play();
            return system;
        }
        void SetMotion()
        {
            gentle = services.Settings.GentleMotion;
            var bubbleEmission = bubbles.emission;
            bubbleEmission.rateOverTime = gentle ? 3 : 5;
            var emission = suspended.emission;
            emission.rateOverTime = gentle ? 6 : 11;
        }
        void PlaceVolume()
        {
            suspended.transform.SetPositionAndRotation(services.Camera.transform.position + services.Camera.transform.forward * 65, services.Camera.transform.rotation);
        }
        void LateUpdate()
        {
            if (services.Settings.GentleMotion != gentle)
                SetMotion();
            PlaceVolume();
        }
        void OnDestroy()
        {
            if (bubbles)
                Destroy(bubbles.gameObject);
            if (bubbleMaterial)
                Destroy(bubbleMaterial);
        }
    }
}
