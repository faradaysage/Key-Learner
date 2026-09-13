using System;
using System.Collections.Generic;
using KeyLearner.Studio;
using UnityEngine;

namespace KeyLearner.Unity
{
    // The existing flame atlas stays in use. One bounded mesh draws
    // curling, rising and cooling layers without allocating a GameObject per flame.
    internal sealed class CanvasFireRenderer : IDisposable
    {
        sealed class Flame
        {
            public Vector2 P, V; public float Age, Life, Size, Phase; public int Sprite;
        }
        readonly List<Flame> flames = new List<Flame>();
        readonly System.Random random = new System.Random(907);
        readonly Mesh mesh = new Mesh { name = "Layered additive flame billboards" };
        readonly Material material;
        readonly GameObject display;
        readonly List<Vector3> vertices = new List<Vector3>(2600);
        readonly List<Vector2> uv = new List<Vector2>(2600);
        readonly List<Color> colors = new List<Color>(2600);
        readonly List<int> triangles = new List<int>(3900);
        float emission, clock;
        public int Count => flames.Count;
        public CanvasFireRenderer(Transform parent)
        {
            material = new Material(Resources.Load<Shader>("Shaders/FlameAtlas"));
            material.SetTexture("_MainTex", Resources.Load<Texture2D>("Effects/FireAtlas"));
            display = new GameObject("Curling layered flames");
            display.transform.SetParent(parent, false);
            display.AddComponent<MeshFilter>().sharedMesh = mesh;
            display.AddComponent<MeshRenderer>().sharedMaterial = material;
            mesh.MarkDynamic();
        }
        public void Lick(Vector2 position)
        {
            if (flames.Count < 650)
                flames.Add(new Flame { P = position, V = new Vector2(random.Next(-20, 21), -90), Life = .65f, Size = 45, Phase = clock, Sprite = random.Next(4) });
        }
        public void Update(float dt, float fuel, float depth, Settings settings, int budget)
        {
            budget = Mathf.Clamp(budget, 0, 650);
            clock += dt;
            float depthScale = Mathf.Max(1, depth / 220);
            emission += dt * fuel * (settings.GentleMotion ? 70 : 190) * (float)settings.EffectStrength;
            while (emission >= 1)
            {
                emission--;
                if (flames.Count >= budget)
                    continue;
                int jet = random.Next(12);
                flames.Add(new Flame { P = new Vector2((jet + .5f) * 1440 / 12 + random.Next(-48, 49), 900 + random.Next(0, 35)), V = new Vector2(random.Next(-15, 16), -random.Next(105, 195) * depthScale), Life = 1.1f + (float)random.NextDouble() * 1.25f, Size = random.Next(60, 125), Phase = (float)random.NextDouble() * Mathf.PI * 2, Sprite = random.Next(4) });
            }
            foreach (var flame in flames)
            {
                flame.Age += dt;
                flame.V.x += Mathf.Sin(flame.Phase + flame.P.y * .026f + clock * 2) * 65 * dt;
                flame.V.y -= 22 * dt;
                flame.P += flame.V * dt * (settings.GentleMotion ? .6f : 1);
            }
            flames.RemoveAll(flame => flame.Age >= flame.Life);
            if (flames.Count > budget)
                flames.RemoveRange(0, flames.Count - budget);
            vertices.Clear();
            uv.Clear();
            colors.Clear();
            triangles.Clear();
            foreach (var flame in flames)
            {
                float age = flame.Age / flame.Life, opacity = Mathf.Min(flame.Age * 9, 1) * Mathf.Pow(1 - age, .85f) * .62f;
                Color hot = settings.Theme == Mood.PrimaryColors ? new Color(1, .64f, .12f) : ThemeColors.At(settings.Theme, 3);
                Color cool = settings.Theme == Mood.PrimaryColors ? new Color(1, .20f, .025f) : ThemeColors.At(settings.Theme, 1);
                Color tint = age < .25f ? Color.Lerp(Color.white, hot, age * 4) : Color.Lerp(hot, cool, (age - .25f) / .75f);
                tint *= opacity;
                float size = flame.Size * (.7f + Mathf.Sin(age * Mathf.PI) * .6f), height = size * (1.35f + age * .9f);
                float angle = Mathf.Sin(flame.Phase + flame.Age * 2) * .25f, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                int start = vertices.Count;
                void Vertex(float x, float y, float u, float v)
                {
                    vertices.Add(new Vector3(flame.P.x + x * cos - y * sin, -flame.P.y - x * sin - y * cos, -28));
                    uv.Add(new Vector2((flame.Sprite + u) / 4, v));
                    colors.Add(tint);
                }
                Vertex(-size * .5f, -height * .5f, 0, 1);
                Vertex(size * .5f, -height * .5f, 1, 1);
                Vertex(size * .5f, height * .5f, 1, 0);
                Vertex(-size * .5f, height * .5f, 0, 0);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
            mesh.Clear(false);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(720, -450, -28), new Vector3(1800, 1500, 40));
            display.SetActive(flames.Count > 0);
        }
        public void Clear()
        {
            flames.Clear();
            emission = 0;
            mesh.Clear();
            display.SetActive(false);
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(material);
            if (display)
                UnityEngine.Object.Destroy(display);
        }
    }
}
