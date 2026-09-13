using System;
using System.Collections.Generic;
using UnityEngine;
using KeyLearner.Studio;
using KeyLearner.Unity.Platform;

namespace KeyLearner.Unity
{
    public abstract class Minigame
    {
        protected GameServices S;
        protected GameObject Root;
        public virtual void Enter(GameServices services)
        {
            S = services;
            Root = new GameObject(GetType().Name);
        }
        public abstract void Tick(float dt);
        public virtual void Key(KeyEvent e)
        {
        }
        public virtual void Pointer(Vector2 logical, bool right)
        {
        }
        public virtual void DrawUI()
        {
        }
        public virtual void Suspend()
        {
        }
        public virtual void Exit()
        {
            if (Root)
                UnityEngine.Object.Destroy(Root);
        }
        public virtual string DiagnosticState => GetType().Name;
    }
    public sealed class GameServices
    {
        public Store Store;
        public readonly Dictionary<string, object> Session = new Dictionary<string, object>();
        public Settings Settings => Store.Settings;
        public UnityAudioService Audio;
        public Camera Camera;
        public LaunchOptions Options;
        public KeySnapshot Keys;
        public double Now
        {
            get; private set;
        }
        public void AdvanceClock(float activeDelta)
        {
            Now += activeDelta;
        }
        public bool Preview => Options.Preview || Application.isEditor;
        public Action PickerAction;
        public RewardEffects Rewards;
        public readonly CameraFeedback Feedback = new CameraFeedback();
        public ContentLibrary Content;
        public void Picker() => PickerAction();
        public float CanvasWidth { get; private set; } = 1440;
        public float CanvasHeight { get; private set; } = 900;
        public void ApplyGraphics()
        {
            QualitySettings.vSyncCount = Settings.VSync ? 1 : 0;
            if (Camera.orthographic)
                ConfigureLighting(false);
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline)
            {
                pipeline.renderScale = (float)Settings.RenderScale;
                pipeline.msaaSampleCount = Settings.SmoothEdges ? 4 : 1;
            }
            var cameraData = Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData)
                cameraData.antialiasing = Settings.SmoothEdges ? UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing : UnityEngine.Rendering.Universal.AntialiasingMode.None;
        }
        public void UpdateViewport()
        {
            if (Camera.orthographic)
                Camera.orthographicSize = Mathf.Max(CanvasHeight * .5f, CanvasWidth * .5f / Camera.aspect);
        }
        public Vector2 PointerFromScreen(Vector2 bottomLeftPixels)
        {
            float scale = Mathf.Min(Screen.width / CanvasWidth, Screen.height / CanvasHeight);
            return (new Vector2(bottomLeftPixels.x, Screen.height - bottomLeftPixels.y) - new Vector2((Screen.width - CanvasWidth * scale) / 2, (Screen.height - CanvasHeight * scale) / 2)) / scale;
        }
        public Vector2 ScreenFromLogical(Vector2 logical)
        {
            float scale = Mathf.Min(Screen.width / CanvasWidth, Screen.height / CanvasHeight);
            var p = logical * scale + new Vector2((Screen.width - CanvasWidth * scale) / 2, (Screen.height - CanvasHeight * scale) / 2);
            return new Vector2(p.x, Screen.height - p.y);
        }
        public void ConfigureLighting(bool underwater)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            bool monochrome = Camera.orthographic && Settings.Theme == Mood.BlackAndWhite;
            RenderSettings.ambientSkyColor = monochrome ? new Color(.67f, .67f, .67f) : underwater ? new Color(.29f, .63f, .70f) : new Color(.55f, .66f, .78f);
            RenderSettings.ambientEquatorColor = monochrome ? new Color(.44f, .44f, .44f) : underwater ? new Color(.17f, .40f, .45f) : new Color(.38f, .46f, .53f);
            RenderSettings.ambientGroundColor = monochrome ? new Color(.25f, .25f, .25f) : underwater ? new Color(.08f, .21f, .29f) : new Color(.23f, .29f, .33f);
            if (RenderSettings.sun)
            {
                RenderSettings.sun.color = monochrome ? Color.white : underwater ? new Color(.61f, .93f, 1) : new Color(1, .95f, .84f);
                RenderSettings.sun.intensity = underwater ? 1.65f : 1.7f;
            }
        }
        public void CanvasCamera(float width = 1440, float height = 900)
        {
            CanvasWidth = width;
            CanvasHeight = height;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.orthographic = true;
            Camera.orthographicSize = Mathf.Max(height * .5f, width * .5f / Camera.aspect);
            Camera.transform.SetPositionAndRotation(new Vector3(width * .5f, -height * .5f, -1200), Quaternion.identity);
            Camera.nearClipPlane = .3f;
            Camera.farClipPlane = 2500;
            Camera.backgroundColor = Settings.Theme == Mood.BlackAndWhite ? Color.black : Style.Navy;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            ConfigureLighting(false);
        }
        public void Burst(Vector2 logical, Color color, int count = 24) => Rewards.Burst(new Vector3(logical.x, -logical.y, -30), color, count, 100);
    }
    public static class Style
    {
        public static readonly Color Navy = new Color(.025f, .045f, .095f);
        public static readonly Color Panel = new Color(.065f, .105f, .19f);
        public static readonly Color Dots = new Color(1, .77f, .16f);
        public static readonly Color Mint = new Color(.23f, .89f, .72f);
        public static readonly Color Blue = new Color(.20f, .55f, 1);
        public static readonly Color[] Colors = { new Color(1, .24f, .25f), Dots, Blue, Mint, new Color(.76f, .44f, 1), new Color(1, .48f, .72f) };
    }
    public static class Ui
    {
        static Font font;
        static Texture2D disc;
        static Matrix4x4 oldMatrix;
        public static Vector2 Pointer
        {
            get; private set;
        }
        public static void Begin(float width = 1440, float height = 900)
        {
            oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / width, Screen.height / height);
            Vector2 offset = new Vector2((Screen.width - width * scale) / 2, (Screen.height - height * scale) / 2);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
            Pointer = (new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) - offset) / scale;
            if (!font)
                font = Resources.Load<Font>("Fonts/Fredoka-Play");
        }
        public static void End()
        {
            GUI.matrix = oldMatrix;
        }
        static Texture2D Shape(bool circle)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = circle ? new Vector2(x - 31.5f, y - 31.5f).magnitude - 30 : Mathf.Max(0, Mathf.Abs(x - 31.5f) - 19) * Mathf.Max(0, Mathf.Abs(x - 31.5f) - 19) + Mathf.Max(0, Mathf.Abs(y - 31.5f) - 19) * Mathf.Max(0, Mathf.Abs(y - 31.5f) - 19) - 144;
                    pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(-d));
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        public static void Panel(Rect rect, Color color, float radius = 20)
        {
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f));
            GUI.color = old;
        }
        public static void Label(Rect rect, string text, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var st = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = alignment, wordWrap = true };
            st.normal.textColor = color;
            GUI.Label(rect, text, st);
        }
        public static void Button(Rect rect, string text, Action action, bool enabled = true)
        {
            bool hover = enabled && rect.Contains(Pointer);
            Panel(new Rect(rect.x, rect.y + 5, rect.width, rect.height), new Color(0, 0, 0, .3f));
            Panel(rect, hover ? new Color(.14f, .25f, .39f) : Style.Panel);
            Label(rect, text, Mathf.RoundToInt(Mathf.Min(32, rect.height * .37f)), enabled ? Color.white : new Color(.5f, .56f, .65f));
            bool was = GUI.enabled;
            GUI.enabled = enabled;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                action();
            GUI.enabled = was;
        }
        public static void Ball(Vector2 p, float radius, Color color)
        {
            if (!disc)
                disc = Shape(true);
            var c = GUI.color;
            GUI.color = new Color(0, 0, 0, .25f);
            GUI.DrawTexture(new Rect(p.x - radius + 4, p.y - radius + 8, radius * 2, radius * 2), disc);
            GUI.color = color;
            GUI.DrawTexture(new Rect(p.x - radius, p.y - radius, radius * 2, radius * 2), disc);
            GUI.color = Color.Lerp(color, Color.white, .7f);
            GUI.DrawTexture(new Rect(p.x - radius * .53f, p.y - radius * .61f, radius * .55f, radius * .4f), disc);
            GUI.color = c;
        }
        public static void Keyboard(char next, KeySnapshot held, Mood theme = Mood.Aurora)
        {
            string[] rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                {
                    char c = rows[r][i];
                    Rect rect = new Rect(420 + r * 19 + i * 57, 695 + r * 57, 51, 49);
                    Panel(rect, char.ToUpperInvariant(next) == c ? ThemeColors.At(theme, 3) : held.IsDown(c) ? ThemeColors.At(theme, 1) : Style.Panel);
                    Label(rect, c.ToString(), 26, char.ToUpperInvariant(next) == c ? Style.Navy : Color.white);
                }
        }
    }
    public static class Visuals
    {
        static Material particle;
        public static Material ParticleMaterial()
        {
            if (!particle)
                particle = new Material(Shader.Find("KeyLearner/Particle"));
            return particle;
        }
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        public static Material Material(Color c)
        {
            if (materials.TryGetValue(c, out var m))
                return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = c;
            m.SetFloat("_Smoothness", .32f);
            m.enableInstancing = true;
            materials.Add(c, m);
            return m;
        }
        public static GameObject Sphere(Transform parent, Vector3 pos, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * radius * 2;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            return go;
        }
        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            return go;
        }
        public static TextMesh Text(Transform parent, string text, Vector3 pos, float size, Color color)
        {
            var go = new GameObject("Letter " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMesh>();
            t.font = Resources.Load<Font>("Fonts/Fredoka-Play");
            t.text = text;
            t.fontSize = 96;
            t.characterSize = size / 12;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
            return t;
        }
    }
    public sealed class RewardEffects : MonoBehaviour
    {
        readonly List<ParticleSystem> active = new List<ParticleSystem>();
        public bool Gentle;
        public void Burst(Vector3 position, Color color, int count = 30, float speed = 12)
        {
            if (active.Count >= 24)
                return;
            var go = new GameObject("Celebration");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .15f;
            main.startLifetime = 1.1f;
            main.startSpeed = Gentle ? speed * .5f : speed;
            main.startSize = speed * .045f;
            main.startColor = color;
            main.gravityModifier = speed > 50 ? .7f : 0;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .1f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Visuals.ParticleMaterial();
            ps.Emit(Mathf.Min(count, 120));
            active.Add(ps);
            Destroy(go, 1.8f);
        }
        void Update()
        {
            active.RemoveAll(p => !p);
        }
        public void Clear()
        {
            foreach (var p in active)
                if (p)
                    Destroy(p.gameObject);
            active.Clear();
        }
    }
}












