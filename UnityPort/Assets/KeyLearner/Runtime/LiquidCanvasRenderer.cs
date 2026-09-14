using System;
using System.Collections.Generic;
using KeyLearner.Studio;
using UnityEngine;
using UnityEngine.Rendering;

namespace KeyLearner.Unity
{
    // A bounded, single-draw density field preserves BlobWorld's continuous liquid
    // while allowing LiquidScale to reduce only the expensive surface resolution.
    internal sealed class LiquidCanvasRenderer : IDisposable
    {
        readonly Material density, surface;
        readonly Mesh mesh = new Mesh { name = "Bounded liquid density quads" };
        readonly CommandBuffer commands = new CommandBuffer { name = "Canvas liquid density" };
        readonly List<Vector3> vertices = new List<Vector3>(720);
        readonly List<Vector2> uv = new List<Vector2>(720), motion = new List<Vector2>(720);
        readonly List<Color> colors = new List<Color>(720);
        readonly List<int> triangles = new List<int>(1080);
        readonly GameObject plane;
        RenderTexture field;
        public string Diagnostic => field ? field.width + "x" + field.height : "inactive";
        public LiquidCanvasRenderer(Transform parent)
        {
            density = new Material(Resources.Load<Shader>("Shaders/LiquidDensity"));
            surface = new Material(Resources.Load<Shader>("Shaders/LiquidSurface"));
            mesh.MarkDynamic();
            plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(plane.GetComponent<Collider>());
            plane.name = "Continuous merging liquid";
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = new Vector3(720, -450, 40);
            plane.transform.localScale = new Vector3(1440, 900, 1);
            plane.GetComponent<Renderer>().sharedMaterial = surface;
            plane.SetActive(false);
        }
        public void Update(BlobWorld world, Settings settings)
        {
            int count = Math.Min(180, world.Drops.Count);
            plane.SetActive(count > 0);
            if (count == 0)
                return;
            float fit = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            float quality = fit * (float)(settings.RenderScale * settings.LiquidScale);
            int width = Mathf.Clamp(Mathf.RoundToInt(1440 * quality), 288, 4096);
            int height = Mathf.Clamp(Mathf.RoundToInt(900 * quality), 180, 2560);
            if (!field || field.width != width || field.height != height)
            {
                if (field)
                {
                    field.Release();
                    UnityEngine.Object.Destroy(field);
                }
                field = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = "Liquid field at selected quality",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                field.Create();
                surface.SetTexture("_Field", field);
            }
            vertices.Clear();
            uv.Clear();
            motion.Clear();
            colors.Clear();
            triangles.Clear();
            for (int i = 0; i < count; i++)
            {
                var drop = world.Drops[i];
                float radius = drop.Radius * 2.1f;
                float left = (drop.Position.X - radius) / 1440, right = (drop.Position.X + radius) / 1440;
                float bottom = 1 - (drop.Position.Y + radius) / 900, top = 1 - (drop.Position.Y - radius) / 900;
                int start = vertices.Count;
                vertices.Add(new Vector3(left, bottom));
                vertices.Add(new Vector3(right, bottom));
                vertices.Add(new Vector3(right, top));
                vertices.Add(new Vector3(left, top));
                uv.Add(Vector2.zero);
                uv.Add(Vector2.right);
                uv.Add(Vector2.one);
                uv.Add(Vector2.up);
                float fade = Mathf.Clamp01((drop.Life - drop.Age) / 1.2f);
                var color = new Color(drop.Tint.X * fade, drop.Tint.Y * fade, drop.Tint.Z * fade, fade);
                for (int corner = 0; corner < 4; corner++)
                {
                    colors.Add(color);
                    motion.Add(new Vector2(drop.Age, drop.Wobble));
                }
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
            mesh.SetUVs(1, motion);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(.5f, .5f, 0), new Vector3(4, 4, 1));
            commands.Clear();
            commands.SetRenderTarget(field);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.DrawMesh(mesh, Matrix4x4.identity, density, 0, 0);
            Graphics.ExecuteCommandBuffer(commands);
            for (int i = 0; i < 4; i++)
                surface.SetColor("_Palette" + i, ThemeColors.At(settings.Theme, i));
        }
        public void Clear()
        {
            if (plane)
                plane.SetActive(false);
        }
        public void Dispose()
        {
            commands.Dispose();
            if (field)
            {
                field.Release();
                UnityEngine.Object.Destroy(field);
            }
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(density);
            UnityEngine.Object.Destroy(surface);
            if (plane)
                UnityEngine.Object.Destroy(plane);
        }
    }
}
