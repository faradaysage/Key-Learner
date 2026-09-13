using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>Layered soft particles form a drifting cloud volume; no obstacles or collisions.</summary>
    public sealed class ExplorerCloudLayer : MonoBehaviour
    {
        public Transform Follow;
        public bool GentleMotion;
        public float Height = 260;
        ParticleSystem system;
        ParticleSystem.Particle[] particles;
        Vector3[] anchors;
        Vector3 origin;
        Material cloudMaterial;
        float drift;
        public static ExplorerCloudLayer Create(Transform parent, Transform follow, bool gentleMotion)
        {
            var go = new GameObject("Layered drifting clouds");
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<ExplorerCloudLayer>();
            layer.Follow = follow;
            layer.GentleMotion = gentleMotion;
            layer.Initialize();
            return layer;
        }
        void Initialize()
        {
            var shader = Shader.Find("KeyLearner/SoftCloud");
            if (!shader)
            {
                Debug.LogError("Soft cloud shader is missing");
                enabled = false;
                return;
            }
            cloudMaterial = new Material(shader);
            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 100000;
            main.startSpeed = 0;
            main.startSize3D = true;
            main.maxParticles = 144;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = cloudMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 1;
            renderer.minParticleSize = 0;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles = new ParticleSystem.Particle[144];
            anchors = new Vector3[144];
            var random = new System.Random(29017);
            origin = Follow ? Follow.position : Vector3.zero;
            for (int cluster = 0; cluster < 36; cluster++)
            {
                Vector3 center = new Vector3((float)random.NextDouble() * 2200 - 1100, Height + (float)random.NextDouble() * 100, (float)random.NextDouble() * 2200 - 1100);
                float size = 65 + (float)random.NextDouble() * 62;
                for (int puff = 0; puff < 4; puff++)
                {
                    int index = cluster * 4 + puff;
                    anchors[index] = center + new Vector3((puff - 1.5f) * size * .42f, Mathf.Sin(puff * 2.1f) * size * .16f, puff % 2 * size * .21f);
                    particles[index].startColor = new Color(.96f, .985f, 1, .52f);
                    particles[index].startSize3D = new Vector3(size, size * (.53f + (puff % 2) * .17f), size);
                    particles[index].startLifetime = 100000;
                    particles[index].remainingLifetime = 100000;
                    particles[index].randomSeed = (uint)(index + 1);
                }
            }
            UpdateParticles();
            system.Play();
        }
        void LateUpdate()
        {
            if (!system || !Follow)
                return;
            drift += Time.deltaTime * (GentleMotion ? .25f : 1.1f);
            UpdateParticles();
        }
        void UpdateParticles()
        {
            Vector3 focus = Follow ? Follow.position : origin;
            for (int i = 0; i < particles.Length; i++)
            {
                Vector3 p = anchors[i];
                p.x = focus.x + Mathf.Repeat(origin.x + p.x + drift - focus.x + 1100, 2200) - 1100;
                p.z = focus.z + Mathf.Repeat(origin.z + p.z - focus.z + 1100, 2200) - 1100;
                particles[i].position = p;
                particles[i].remainingLifetime = 100000;
            }
            system.SetParticles(particles, particles.Length);
        }
        void OnDestroy()
        {
            if (cloudMaterial)
                Destroy(cloudMaterial);
        }
    }
}
