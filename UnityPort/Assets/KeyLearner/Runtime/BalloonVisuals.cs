using System.Collections.Generic;
using UnityEngine;

namespace KeyLearner.Unity
{
    // A balloon is a deliberately soft latex silhouette, distinct from the round
    // counting targets: broad shoulders, tapered neck, small knot and light string.
    internal static class BalloonVisuals
    {
        static Mesh skin, knot;
        static Material stringMaterial;
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        public static GameObject Create(Transform parent, Vector3 position, Color color)
        {
            if (!skin)
                skin = Skin();
            if (!knot)
                knot = Knot();
            if (!materials.TryGetValue(color, out var material))
            {
                material = new Material(Visuals.Material(color));
                material.SetFloat("_Smoothness", .8f);
                material.SetFloat("_Metallic", .035f);
                materials[color] = material;
            }
            var root = new GameObject("Glossy balloon with knot and string");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var body = new GameObject("Latex silhouette");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = skin;
            body.AddComponent<MeshRenderer>().sharedMaterial = material;
            var tie = new GameObject("Tied neck");
            tie.transform.SetParent(root.transform, false);
            tie.AddComponent<MeshFilter>().sharedMesh = knot;
            tie.AddComponent<MeshRenderer>().sharedMaterial = material;
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 17;
            line.widthMultiplier = 1.05f;
            if (!stringMaterial)
            {
                stringMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                stringMaterial.SetColor("_BaseColor", new Color(.7f, .76f, .86f));
            }
            line.sharedMaterial = stringMaterial;
            UpdateString(root, 0, 0);
            return root;
        }
        public static void UpdateString(GameObject root, float age, float lean)
        {
            var line = root.GetComponent<LineRenderer>();
            for (int i = 0; i < 17; i++)
            {
                float t = i / 16f;
                float bend = Mathf.Sin(t * Mathf.PI * 1.5f + age * 1.4f) * 7 * t + lean * t * t;
                line.SetPosition(i, new Vector3(bend, -56 - t * 96, 2));
            }
            var body = root.transform.GetChild(0);
            body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 1.3f) * 2.2f);
        }
        static Mesh Skin()
        {
            const int rings = 36, segments = 64;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float theta = ring * Mathf.PI / rings, y = Mathf.Cos(theta), radius = Mathf.Sin(theta) * (.84f + .12f * y);
                for (int side = 0; side <= segments; side++)
                {
                    float angle = side * Mathf.PI * 2 / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius * 49, y * 51, Mathf.Sin(angle) * radius * 45));
                    uv.Add(new Vector2(side / (float)segments, ring / (float)rings));
                }
            }
            for (int ring = 0; ring < rings; ring++)
                for (int side = 0; side < segments; side++)
                {
                    int a = ring * (segments + 1) + side, b = a + segments + 1;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                    triangles.Add(b);
                }
            var result = new Mesh { name = "Soft tapered latex balloon" };
            result.SetVertices(vertices);
            result.SetUVs(0, uv);
            result.SetTriangles(triangles, 0);
            result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }
        static Mesh Knot()
        {
            var result = new Mesh { name = "Tied balloon neck" };
            var points = new List<Vector3>();
            var indices = new List<int>();
            const int segments = 20;
            float[] heights = { -48, -52, -57, -58 };
            float[] radii = { 2.6f, 2, 5, 4.5f };
            for (int ring = 0; ring < 4; ring++)
                for (int side = 0; side < segments; side++)
                {
                    float angle = side * Mathf.PI * 2 / segments;
                    points.Add(new Vector3(Mathf.Cos(angle) * radii[ring], heights[ring], Mathf.Sin(angle) * radii[ring]));
                }
            for (int ring = 0; ring < 3; ring++)
                for (int side = 0; side < segments; side++)
                {
                    int a = ring * segments + side, b = ring * segments + (side + 1) % segments, c = a + segments, d = b + segments;
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);
                    indices.Add(b);
                    indices.Add(d);
                    indices.Add(c);
                }
            result.SetVertices(points);
            result.SetTriangles(indices, 0);
            result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }
    }
}
