using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>Bounded water particles, separate from learning rewards and source animal animation.</summary>
    public sealed class OceanAtmosphere : MonoBehaviour
    {
        GameServices services;
        ParticleSystem bubbles, suspended;
        Material bubbleMaterial;
        bool gentle, above;
        Vector2 previousPointer;
        Transform dolphin;
        Vector3 previousDolphin;
        float splashCooldown;
        public WaterSplash LastSplash {get;private set;}
        public int Exits { get; private set; }
        public int Entries { get; private set; }
        readonly ParticleSystem.Particle[] motes = new ParticleSystem.Particle[220];
        public static OceanAtmosphere Create(GameServices services, Transform parent, Transform dolphin)
        {
            var host = new GameObject("Water atmosphere");
            host.transform.SetParent(parent, false);
            var effect = host.AddComponent<OceanAtmosphere>();
            effect.services = services;
            effect.dolphin = dolphin;
            effect.previousDolphin = dolphin.position;
            effect.bubbleMaterial = new Material(Resources.Load<Shader>("Shaders/Bubble"));
            effect.bubbles = effect.Particles("Rising dolphin bubbles", host.transform, 45, 5, .48f, new Color(.72f, .95f, 1, .6f), effect.bubbleMaterial);
            var bubbleShape = effect.bubbles.shape;
            bubbleShape.shapeType = ParticleSystemShapeType.Sphere;
            bubbleShape.radius = .35f;
            var velocity = effect.bubbles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0, 0);
            velocity.y = new ParticleSystem.MinMaxCurve(2, 3.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
            effect.bubbles.transform.SetParent(dolphin, false);
            effect.bubbles.transform.localPosition = new Vector3(0, 0, -2);
            effect.suspended = effect.Particles("Suspended water light", host.transform, 220, 17, .14f, new Color(.65f, .88f, 1, .24f), Visuals.ParticleMaterial());
            var shape = effect.suspended.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(105, 65, 125);
            effect.SetMotion();
            effect.PlaceVolume();
            effect.suspended.Emit(100);
            return effect;
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
        public void FishWake(Vector3 position)
        {
            if(position.y>-.5f || above || services.Settings.GentleMotion)return;
            bubbles.Emit(new ParticleSystem.EmitParams {position=position,velocity=Vector3.up*2,startSize=.2f,startLifetime=2.5f},1);
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
            if(Time.deltaTime<=0)return;
            if (services.Settings.GentleMotion != gentle)
                SetMotion();
            PlaceVolume();
            splashCooldown = Mathf.Max(0, splashCooldown - Time.deltaTime);
            var current = dolphin.position;
            if ((current.y > 0) != (previousDolphin.y > 0) && splashCooldown <= 0)
            {
                float fraction = Mathf.Clamp01(-previousDolphin.y / (current.y - previousDolphin.y));
                var crossing = Vector3.Lerp(previousDolphin, current, fraction);
                float speed = Vector3.Distance(previousDolphin, current) / Mathf.Max(.001f, Time.deltaTime);
                LastSplash=WaterSplash.Create(services, transform, crossing, speed, current.y <= 0);
                if (current.y > 0) Exits++; else Entries++;
                splashCooldown = .15f;
            }
            previousDolphin = current;
            bool air = services.Camera.transform.position.y > 0;
            if (air != above)
            {
                above = air;
                if (air) suspended.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else suspended.Play();
            }
            var bubbleEmission = bubbles.emission;
            bubbleEmission.enabled = bubbles.transform.position.y < -1;
            Vector2 pointer = Input.mousePosition, movement = pointer - previousPointer;
            previousPointer = pointer;
            if (services.Settings.MousePlay && !air && movement.sqrMagnitude > .01f)
            {
                var camera = services.Camera;
                var point = camera.ScreenPointToRay(pointer).GetPoint(50);
                Vector3 push = camera.transform.right * movement.x + camera.transform.up * movement.y;
                push = Vector3.ClampMagnitude(push * (gentle ? .006f : .02f), gentle ? .2f : .6f);
                int count = suspended.GetParticles(motes);
                for (int i = 0; i < count; i++)
                {
                    float influence = Mathf.Clamp01(1 - Vector3.Distance(motes[i].position, point) / 24);
                    motes[i].velocity = Vector3.Lerp(motes[i].velocity, push, influence * .2f);
                }
                suspended.SetParticles(motes, count);
            }
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
