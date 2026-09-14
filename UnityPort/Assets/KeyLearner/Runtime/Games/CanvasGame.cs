using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using KeyLearner.Studio;
using N2 = System.Numerics.Vector2;
using N3 = System.Numerics.Vector3;
using PlayMode = KeyLearner.Studio.PlayMode;

namespace KeyLearner.Unity
{
    public sealed class CanvasGame : Minigame
    {
        readonly PlayMode mode;
        GuidedSpelling guided;
        readonly CountingRecognizer counting = new CountingRecognizer();
        readonly FireworkSchedule fireworks = new FireworkSchedule();
        BalloonReward reward;
        readonly GestureAnalyzer gestures = new GestureAnalyzer();
        readonly GlassSheet glass = new GlassSheet();
        readonly BlobWorld blobs = new BlobWorld();
        readonly StarfieldMotion stars = new StarfieldMotion();
        readonly System.Random random = new System.Random(73);
        WordRecognizer words;
        WordEntry target;
        Dictionary<string, string> icons;
        readonly List<Glyph> glyphs = new List<Glyph>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<GameObject> panes = new List<GameObject>();
        readonly List<Paint> paint = new List<Paint>();
        readonly List<Rocket> rockets = new List<Rocket>();
        readonly List<(int Number, Vector2 Position, double Until)> countFlashes = new List<(int, Vector2, double)>();
        double hundredAt = double.PositiveInfinity, celebrationUntil, nextCelebrationBurst;
        ParticleSystem starSystem, glowSystem; ParticleSystem.Particle[] starParticles; readonly ParticleSystem.Particle[] glowParticles = new ParticleSystem.Particle[250];
        Glyph current;
        int activeKey = -1, wordIndex, lastPanes;
        string message = ""; double messageUntil;
        bool typed, refreshOnResume, pointerReady;
        float fire, trailClock, fireHold, backgroundClock, lastShot = -10, lastPaint = -10, lastCrack = -10;
        SessionState session;
        Texture2D wordImage, backgroundField, glowTexture;
        Material glowMaterial, glassFill, glassEdge, backgroundMaterial, paintMaterial;
        Font classicFont; readonly List<Mesh> paneMeshes = new List<Mesh>();
        LiquidCanvasRenderer liquidRenderer; CanvasFireRenderer flames;
        readonly List<EffectRecipe> recipes = new List<EffectRecipe>();
        readonly List<Mote> motes = new List<Mote>();
        readonly List<Glow> glows = new List<Glow>();
        readonly List<PreviewStroke> previewStrokes = new List<PreviewStroke>();
        readonly HashSet<int> previewHeld = new HashSet<int>();
        double entered; int previewCursor, previewPops, recognized, launched, blasts, shattered, poppedGlyphs;
        string scenario = "";
        class SessionState
        {
            public GuidedSpelling Guided = new GuidedSpelling(); public BalloonReward Reward = new BalloonReward(); public int WordIndex; public string Target = ""; public bool SelectNext;
        }
        class Mote
        {
            public Vector2 P, V; public Color Color; public float Age, Life, Size, Curl, Gravity, Trail; public Celebration Kind;
        }
        class Glow
        {
            public Vector2 P, V; public Color Color; public float Age, Size;
        }
        class PreviewStroke
        {
            public double Time; public int Key; public bool Down;
        }
        Vector2 previousPointer;
        class Glyph
        {
            public GameObject Object; public BalloonMotion Balloon = new BalloonMotion(); public Vector2 P, V; public Color Color; public float Age, Life, Burn, Radius = 46; public bool Reward; public int Key;
        }
        class Shot
        {
            public GameObject Object; public Vector2 P, V;
        }
        class Rocket
        {
            public GameObject Object; public Vector2 P, Target; public float Time; public Color Color;
        }
        class Paint
        {
            public Vector2 P; public Color Color; public float Age, Radius; public GameObject Object; public Renderer Renderer; public MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        }
        static readonly string[] Friendly = { "face-smile", "star", "heart", "sun", "moon", "cloud", "rainbow", "snowflake", "cat", "dog", "fish", "frog", "hippo", "otter", "dove", "dragon", "paw", "apple-whole", "carrot", "lemon", "ice-cream", "cookie", "cake-candles", "car", "bicycle", "rocket", "plane", "sailboat", "train", "house", "tree", "leaf", "seedling", "flower", "music", "bell", "gift", "balloon", "futbol", "basketball", "volleyball", "baseball", "umbrella", "crown", "gem", "puzzle-piece", "robot", "shapes" };
        public CanvasGame(PlayMode mode)
        {
            this.mode = mode;
        }
        public static bool RestartSpelling(GameServices services, bool resetSessionProgress = false)
        {
            if (!services.Session.TryGetValue("canvas-learning", out var state) || !(state is SessionState session))
                return false;
            session.Reward.Clear();
            if (resetSessionProgress)
            {
                session.Guided.Restart();
                session.SelectNext = true;
            }
            else
                session.Guided.Start(session.Target);
            return true;
        }
        public override void Enter(GameServices services)
        {
            base.Enter(services);
            S.CanvasCamera();
            words = new WordRecognizer(S.Store);
            if (S.Session.TryGetValue("canvas-learning", out var saved))
                session = (SessionState)saved;
            else
            {
                session = new SessionState();
                S.Session["canvas-learning"] = session;
            }
            guided = session.Guided;
            reward = session.Reward;
            reward.Clear();
            wordIndex = session.WordIndex;
            guided.Timed = S.Settings.TimedSpelling;
            recipes.AddRange(EffectRecipe.Load(S.Store.Root));
            entered = S.Now;
            var iconPath = Path.Combine(Application.streamingAssetsPath, "Content", "Icons", "catalog.json");
            icons = File.Exists(iconPath) ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(iconPath)) : new Dictionary<string, string>();
            target = S.Store.Words.FirstOrDefault(w => w.Enabled && w.Adventure && w.Word == session.Target);
            if (target == null || session.SelectNext)
            {
                session.SelectNext = false;
                NextWord();
            }
            else
                guided.Start(target.Word);
            var go = new GameObject("Forward stars");
            go.transform.SetParent(Root.transform);
            starSystem = go.AddComponent<ParticleSystem>();
            var main = starSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startSpeed = 0;
            main.maxParticles = 2400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = starSystem.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Visuals.Material(Color.white);
            starParticles = new ParticleSystem.Particle[2400];
            PrepareCanvasMaterials();
            ConfigurePreview();
        }
        Color ColorAt(int i) => ThemeColors.At(S.Settings.Theme, i);
        void NextWord()
        {
            var available = S.Store.Words.Where(w => w.Enabled && w.Adventure).ToArray();
            if (available.Length > 0)
            {
                var easy = available.Where(w => w.Word.Length <= guided.MaxWordLength).ToArray();
                available = easy.Length > 0 ? easy : available.Where(w => w.Word.Length == available.Min(x => x.Word.Length)).ToArray();
            }
            target = available.Length == 0 ? new WordEntry { Word = "" } : available[wordIndex++ % available.Length];
            session.WordIndex = wordIndex;
            session.Target = target.Word;
            guided.Start(target.Word);
        }
        public override void Key(KeyEvent e)
        {
            if (!e.Down)
                return;
            if ((e.Key == 27 || e.Key == 79) && S.Keys.Keys.Any(ParentChord.IsControl) && S.Keys.Keys.Any(k => ParentChord.IsAlt(k) || k == 160 || k == 161))
                return;
            typed = true;
            if (activeKey != e.Key)
            {
                current?.Balloon.Release();
                current = null;
                activeKey = e.Key;
            }
            var c = KeyboardMap.Character(e.Key);
            var context = gestures.Add(e.Key, S.Now, S.Keys.Keys.Count(k => !ParentChord.IsModifier(k)), S.Store.Gestures.Network, S.Settings.UseGestureCalibration);
            if (mode == PlayMode.WordAdventure)
            {
                if (reward.Remaining > 0)
                    return;
                if (e.Key == 8)
                {
                    guided.Backspace();
                    return;
                }
                if (c.HasValue && c.Value >= 'a' && c.Value <= 'z')
                {
                    if (guided.Update(S.Now))
                        S.Audio.Play("retry", S.Settings, .45f);
                    Emit(e.Key, c, context);
                    if (guided.Add(c.Value, S.Now))
                    {
                        if (S.Settings.AdaptiveLearning)
                            S.Store.Profile.WordCounts[target.Word] = Math.Min(100000, S.Store.Profile.WordCounts.GetValueOrDefault(target.Word) + 1);
                        Announce(target);
                        reward.Start(target.Word.Length + target.Word.Count(letter => "jqxz".Contains(letter)));
                        for (int i = 0; i < reward.Remaining; i++)
                            MakeBalloon(i, reward.Remaining);
                    }
                    else if (S.Settings.SpeakLetters)
                        S.Audio.Say(c.Value.ToString(), S.Settings, key: true);
                }
                return;
            }
            bool gestured = false;
            if (S.Settings.GestureEffects && context.Gesture != Gesture.Deliberate && context.Gesture != Gesture.Rapid)
            {
                gestured = true;
                foreach (var glyph in glyphs.Where(g => !g.Reward && g.Age < .16f).ToArray())
                {
                    UnityEngine.Object.Destroy(glyph.Object);
                    glyphs.Remove(glyph);
                }
                current = null;
                var p = new Vector2((float)(.08 + context.X * .84) * 1440, (float)(.12 + context.Y * .7) * 900);
                if (context.Gesture == Gesture.BroadMash && (float)S.Now - lastCrack > .035f)
                {
                    lastCrack = (float)S.Now;
                    bool broke = glass.Hit(new N2(p.x, p.y), (float)context.Energy);
                    if (broke)
                    {
                        shattered++;
                        S.Audio.Play("shatter", S.Settings);
                    }
                    else
                        S.Audio.Play("crack", S.Settings, .55f);
                }
                if (context.Gesture == Gesture.Cluster && (float)S.Now - lastPaint > .07f)
                {
                    lastPaint = (float)S.Now;
                    if (paint.Count >= 24)
                    {
                        UnityEngine.Object.Destroy(paint[0].Object);
                        paint.RemoveAt(0);
                    }
                    AddPaint(p, ColorAt(e.Key));
                    S.Audio.Play("paint", S.Settings, .65f);
                }
                if (context.Gesture == Gesture.Sweep)
                {
                    var tint = ColorAt(e.Key);
                    for (int i = 0; i < 3; i++)
                        blobs.Add(new N2(p.x + random.Next(-15, 16), p.y + random.Next(-15, 16)), new N2((float)context.Dx * 320, (float)context.Dy * 220 - 55), random.Next(23, 36), new N3(tint.r, tint.g, tint.b), 7, Math.Min(S.Settings.ParticleLimit, 180), 1);
                }
            }
            if (!gestured && e.Key != 32)
                Emit(e.Key, c, context);
            if (ParentChord.IsModifier(e.Key))
                return;
            if (e.Key == 8)
            {
                words.Backspace();
                return;
            }
            if (e.Key == 13 || e.Key == 32)
            {
                var w = words.Flush();
                if (w != null)
                    Announce(w);
                return;
            }
            if (!c.HasValue)
            {
                if (S.Settings.SpeakLetters && !gestured)
                    S.Audio.Say(IconName(e.Key).Replace("face-", "").Replace("-", " "), S.Settings, key: true);
                return;
            }
            if (c.Value >= '0' && c.Value <= '9')
            {
                words.Reset();
                var n = counting.Add(c.Value, S.Now);
                if (n.HasValue)
                    Count(n.Value);
                else if (counting.Pending.Length == 0 && S.Settings.SpeakLetters)
                    S.Audio.Say(c.Value.ToString(), S.Settings, key: true);
                return;
            }
            if (mode == PlayMode.Counting)
                return;
            var completed = words.Add(c.Value, S.Now, context);
            if (completed != null)
                Announce(completed);
            else if (S.Settings.SpeakLetters && !gestured)
                S.Audio.Say(c.Value.ToString(), S.Settings, key: true);
        }
        string IconName(int key)
        {
            if (S.Settings.KeyIcons.TryGetValue(key, out var name) && icons.ContainsKey(name))
                return name;
            if (key == 112)
                return "face-smile";
            var choices = Friendly.Where(icons.ContainsKey).ToArray();
            return choices.Length == 0 ? "star" : choices[Math.Abs(key - 112) % choices.Length];
        }
        void Emit(int key, char? c, InputContext context)
        {
            if (current != null && glyphs.Contains(current))
            {
                current.Balloon.Inflate();
                current.Age = 0;
                current.V.y = Mathf.Max(-260, current.V.y - 85);
                return;
            }
            string text = c?.ToString().ToUpperInvariant();
            bool icon = !c.HasValue;
            if (icon)
            {
                string name = IconName(key);
                text = icons.TryGetValue(name, out var hex) ? char.ConvertFromUtf32(Convert.ToInt32(hex, 16)) : "✦";
            }
            var p = icon ? new Vector2(random.Next(110, 1330), random.Next(130, 680)) : new Vector2((float)(.1 + context.X * .8) * 1440, (float)(.26 + context.Y * .4) * 900);
            var color = ColorAt(key);
            var obj = new GameObject("Toy glyph");
            obj.transform.SetParent(Root.transform);
            // Layered copies give font-silhouette extrusion without changing letter shape or readability.
            for (int i = S.Settings.ExtrudedAssets ? 7 : 0; i >= 0; i--)
            {
                var t = Visuals.Text(obj.transform, text, new Vector3(i * .8f, -i * .8f, i * 1.3f), 115, i == 0 ? color : Color.Lerp(color, Style.Navy, S.Settings.ToonAssets ? .6f : .27f));
                if (icon)
                    t.font = Resources.Load<Font>("Fonts/fa-solid-900");
                else if (S.Settings.Font == LetterFont.Baloo)
                    t.font = Resources.Load<Font>("Fonts/BalooBhai2-Play");
                else if (S.Settings.Font == LetterFont.Classic)
                {
                    if (!classicFont)
                        classicFont = Font.CreateDynamicFontFromOSFont("Georgia", 96);
                    t.font = classicFont;
                }
                t.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
            }
            current = new Glyph { Object = obj, P = p, V = new Vector2(random.Next(-45, 46), -90), Color = color, Key = key, Life = (float)S.Settings.LetterLifetime };
            glyphs.Add(current);
            if (glyphs.Count > 55)
            {
                UnityEngine.Object.Destroy(glyphs[0].Object);
                glyphs.RemoveAt(0);
            }
            S.Burst(p, color, 5);
        }
        void MakeBalloon(int i, int count)
        {
            var p = new Vector2((i + 1) * 1440f / (count + 1), 900 - random.Next(20, 130));
            var color = ColorAt(i);
            var obj = BalloonVisuals.Create(Root.transform, new Vector3(p.x, -p.y, 0), color);
            glyphs.Add(new Glyph { Object = obj, P = p, V = new Vector2(random.Next(-45, 46), -random.Next(70, 120)), Color = color, Reward = true, Life = 10000, Radius = random.Next(34, 52) });
        }
        void Count(int n)
        {
            S.Audio.Say(n.ToString(), S.Settings);
            fireworks.Add(n, S.Now);
            if (n == 100) hundredAt = S.Now + 4.8;
            message = n.ToString();
            messageUntil = S.Now + 4;
        }
        void Announce(WordEntry w)
        {
            recognized++;
            S.Audio.Say(w.Spoken.Length > 0 ? w.Spoken : w.Word, S.Settings, w.Recording);
            message = w.Word.ToUpperInvariant();
            messageUntil = S.Now + (mode == PlayMode.WordAdventure ? .65 : 3.8);
            foreach (var glyph in glyphs.Where(g => !g.Reward).ToArray())
            {
                UnityEngine.Object.Destroy(glyph.Object);
                glyphs.Remove(glyph);
            }
            current = null;
            var recipe = recipes.FirstOrDefault(r => r.Name == w.EffectPreset);
            MakeEffect(new Vector2(720, 387), recipe?.Shape ?? w.Effect, recipe?.Count ?? 150, recipe);
            if (wordImage)
                UnityEngine.Object.Destroy(wordImage);
            wordImage = null;
            if (w.Image.Length > 0)
                try
                {
                    if (new FileInfo(w.Image).Length > 8000000)
                        throw new IOException("Image must be smaller than 8 MB.");
                    var texture = new Texture2D(2, 2);
                    if (texture.LoadImage(File.ReadAllBytes(w.Image)))
                        wordImage = texture;
                    else
                        UnityEngine.Object.Destroy(texture);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { Debug.LogWarning("Family word image unavailable: " + e.GetType().Name); }
            S.Store.Save();
        }
        public override void Tick(float dt)
        {
            if (refreshOnResume)
            {
                refreshOnResume = false;
                words.RefreshDictionary();
                recipes.Clear();
                recipes.AddRange(EffectRecipe.Load(S.Store.Root));
                if (mode == PlayMode.WordAdventure)
                {
                    var valid = S.Store.Words.FirstOrDefault(w => w.Enabled && w.Adventure && w.Word == target.Word);
                    if (valid == null || session.SelectNext)
                    {
                        session.SelectNext = false;
                        NextWord();
                    }
                    else
                    {
                        target = valid;
                        guided.Start(target.Word);
                    }
                }
            }
            RunPreview();
            UpdateBackground(dt);
            UpdateMotes(dt);
            stars.Step(dt, (float)S.Settings.StarSpeed, S.Settings.GentleMotion);
            int starCount = S.Settings.Theme == Mood.BlackAndWhite || (S.Settings.Backdrop != Backdrop.Starfield && S.Settings.Backdrop != Backdrop.RotatingStars) ? 0 : Mathf.Min(S.Settings.StarCount * (S.Settings.Backdrop == Backdrop.RotatingStars ? 2 : 1), 2400);
            for (int i = 0; i < starCount; i++)
            {
                var p = stars.Project(i, S.Settings.Backdrop == Backdrop.RotatingStars);
                float twinkle = .35f + .25f * Mathf.Sin(i + stars.Time);
                starParticles[i].position = new Vector3(720 + p.X * 700, -450 + p.Y * 500, 260);
                starParticles[i].startSize = 1.5f + 4 / Mathf.Max(.4f, p.Z);
                starParticles[i].startColor = new Color(.5f, .67f, 1, twinkle);
                starParticles[i].remainingLifetime = 100;
            }
            starSystem.SetParticles(starParticles, starCount);
            counting.Update(S.Now);
            guided.Timed = S.Settings.TimedSpelling;
            if (guided.Update(S.Now))
                S.Audio.Play("retry", S.Settings, .45f);
            if (mode == PlayMode.SmashGarden)
            {
                var word = words.Update(S.Now);
                if (word != null)
                    Announce(word);
            }
            if (S.Now >= hundredAt)
            {
                hundredAt = double.PositiveInfinity;
                celebrationUntil = S.Now + 6;
                S.Audio.Say("Congratulations! You counted to one hundred!", S.Settings);
                S.Audio.Play("celebration", S.Settings, .5f);
            }
            S.Audio.SetMusic(mode == PlayMode.Counting && S.Now < celebrationUntil ? "happy-bonus" : "learning-home", mode == PlayMode.Counting && S.Now < celebrationUntil ? .22f : .035f);
            if (S.Now < celebrationUntil && S.Now >= nextCelebrationBurst)
            {
                nextCelebrationBurst = S.Now + (S.Settings.GentleMotion ? .7 : .4);
                S.Rewards.Firework(new Vector2(random.Next(210, 1230), random.Next(210, 650)), ColorAt(random.Next(6)), (float)S.Now);
                S.Audio.Play("pop", S.Settings, .18f, .3);
            }
            countFlashes.RemoveAll(f => S.Now >= f.Until);
            var dueCounts = fireworks.DueNumbers(S.Now);
            for (int i = 0; i < dueCounts.Length; i++)
            {
                launched++;
                var p = new Vector2(180 + (dueCounts[i] - 1) % 10 * 120, 920);
                countFlashes.Add((dueCounts[i], new Vector2(p.x, 832), S.Now + .38));
                if (countFlashes.Count > 30) countFlashes.RemoveAt(0);
                rockets.Add(new Rocket { Object = Visuals.Sphere(Root.transform, new Vector3(p.x, -p.y, 0), 7, Color.white), P = p, Target = new Vector2(random.Next(170, 1270), random.Next(150, 560)), Color = ColorAt(i + random.Next(6)) });
            }
            for (int i = rockets.Count - 1; i >= 0; i--)
            {
                var r = rockets[i];
                r.Time += dt;
                var p = Vector2.Lerp(r.P, r.Target, r.Time / .75f);
                r.Object.transform.position = new Vector3(p.x, -p.y, 0);
                if (r.Time >= .75f)
                {
                    S.Burst(r.Target, r.Color, 45);
                    UnityEngine.Object.Destroy(r.Object);
                    rockets.RemoveAt(i);
                    S.Audio.Play("pop", S.Settings, .25f);
                }
            }
            float canvasScale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            Vector2 mouse = (new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) - new Vector2((Screen.width - 1440 * canvasScale) / 2, (Screen.height - 900 * canvasScale) / 2)) / canvasScale;
            if (!pointerReady)
            {
                pointerReady = true;
                previousPointer = mouse;
            }
            for (int i = glyphs.Count - 1; i >= 0; i--)
            {
                var g = glyphs[i];
                g.Age += dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120));
                for (int step = 0; step < steps; step++)
                    g.Balloon.Step(dt / steps, (float)S.Settings.BalloonDeflateSeconds, (float)S.Settings.BalloonPopSize);
                if (g.Balloon.Size > 1.03f)
                    g.Age = Mathf.Min(g.Age, Mathf.Max(0, g.Life - 1.3f));
                if (S.Settings.MousePlay)
                {
                    var d = g.P - mouse;
                    if (d.sqrMagnitude < 180 * 180 && d.sqrMagnitude > 1)
                        g.V += d.normalized * dt * 400 * (1 - d.magnitude / 180);
                }
                if (!g.Reward)
                    g.V.y += (float)S.Settings.Gravity * dt;
                else
                {
                    g.V += new Vector2(Mathf.Sin((float)S.Now * 1.5f + g.Key) * 12, -9) * dt;
                    g.V *= Mathf.Exp(-dt * .18f);
                }
                g.P += g.V * dt * (g.Reward && S.Settings.GentleMotion ? .55f : 1);
                float radius = g.Radius * g.Balloon.Size;
                if (g.P.x < radius || g.P.x > 1440 - radius)
                {
                    g.P.x = Mathf.Clamp(g.P.x, radius, 1440 - radius);
                    g.V.x *= -.7f;
                }
                if (g.P.y > 900 - radius)
                {
                    g.P.y = 900 - radius;
                    g.V.y = -Mathf.Abs(g.V.y) * (float)S.Settings.Bounce;
                }
                if (g.P.y < radius + 15)
                {
                    g.P.y = radius + 15;
                    g.V.y = Mathf.Abs(g.V.y);
                }
                g.Object.transform.localPosition = new Vector3(g.P.x, -g.P.y, 0);
                g.Object.transform.localScale = g.Reward ? Vector3.one * (g.Radius / 49f) : new Vector3(1 - g.Balloon.Squeeze, 1 + g.Balloon.Squeeze, 1) * g.Balloon.Size * (float)S.Settings.FontScale;
                if (g.Reward)
                    BalloonVisuals.UpdateString(g.Object, g.Age, -g.V.x * .08f);
                if (fire > 60 && g.P.y > 900 - fire * .7f)
                    g.Burn += dt;
                if (g.Burn > 0)
                {
                    g.Burn += dt * .4f;
                    if (random.NextDouble() < dt * 20)
                    {
                        MakeEffect(g.P, Celebration.Embers, 2);
                        flames.Lick(g.P);
                    }
                }
                if (g.Balloon.Popped || g.Burn > (g.Reward ? 1 : 1.5f))
                {
                    Pop(g);
                    continue;
                }
                if (!g.Reward && g.Age > g.Life && g.Balloon.Size < 1.05f)
                {
                    UnityEngine.Object.Destroy(g.Object);
                    glyphs.RemoveAt(i);
                }
            }
            ResolveBalloonContacts();
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                var next = shot.P + shot.V * dt;
                Glyph hit = null;
                float first = 2;
                foreach (var g in glyphs)
                {
                    var t = ProjectileMath.HitFraction(new N2(g.P.x, g.P.y), g.Radius * g.Balloon.Size, new N2(shot.P.x, shot.P.y), new N2(next.x, next.y));
                    if (t.HasValue && t.Value < first)
                    {
                        first = t.Value;
                        hit = g;
                    }
                }
                shot.P = hit != null ? Vector2.Lerp(shot.P, next, first) : next;
                shot.Object.transform.position = new Vector3(shot.P.x, -shot.P.y, -20);
                if (hit != null)
                    Blast(shot.P, 185, true);
                if (hit != null || next.x < -100 || next.x > 1540 || next.y < -100 || next.y > 1000)
                {
                    UnityEngine.Object.Destroy(shot.Object);
                    shots.RemoveAt(i);
                }
            }
            bool space = S.Keys.IsDown(32);
            fireHold = space ? Mathf.Min(15, fireHold + dt) : Mathf.Max(0, fireHold - dt * 5);
            fire = Mathf.MoveTowards(fire, space ? Mathf.Min(738, 220 + (S.Settings.GrowingFire ? fireHold * 42 : 0)) : 0, dt * 800);
            trailClock += dt;
            S.Audio.SetFire(fire / 738, S.Settings);
            flames.Update(dt, Mathf.Clamp01(fire / 220), fire, S.Settings, S.Settings.ParticleLimit - blobs.Drops.Count - motes.Count);
            if (S.Settings.MousePlay && (trailClock > .025f || Input.GetMouseButton(0)) && (mouse - previousPointer).sqrMagnitude > 9)
            {
                trailClock = 0;
                int n = Mathf.Clamp((int)Vector2.Distance(mouse, previousPointer) / 9, 3, 30);
                for (int i = 1; i <= n; i++)
                    glows.Add(new Glow { P = Vector2.Lerp(previousPointer, mouse, i / (float)n), V = new Vector2(random.Next(-150, 151), random.Next(-150, 151)), Size = random.Next(35, 80) * (float)S.Settings.MouseTrailSize, Color = ColorAt(random.Next(4)) });
            }
            previousPointer = mouse;
            if (glows.Count > 250)
                glows.RemoveRange(0, glows.Count - 250);
            for (int i = glows.Count - 1; i >= 0; i--)
            {
                var glow = glows[i];
                glow.Age += dt;
                glow.P += glow.V * dt;
                if (glow.Age > 2.2f)
                    glows.RemoveAt(i);
            }
            for (int i = 0; i < glows.Count; i++)
            {
                var glow = glows[i];
                glowParticles[i].position = new Vector3(glow.P.x, -glow.P.y, -18);
                glowParticles[i].startSize = glow.Size * 2;
                var color = glow.Color;
                color.a = Mathf.Clamp01(1 - glow.Age / 2.2f) * .8f;
                glowParticles[i].startColor = color;
                glowParticles[i].remainingLifetime = 1;
                glowParticles[i].startLifetime = 1;
            }
            glowSystem.SetParticles(glowParticles, glows.Count);
            for (int i = paint.Count - 1; i >= 0; i--)
            {
                var p = paint[i];
                p.Age += dt;
                p.P.y += dt * (12 + p.Age * 3);
                if (p.Age > 7)
                {
                    UnityEngine.Object.Destroy(p.Object);
                    paint.RemoveAt(i);
                    continue;
                }
                p.Object.transform.localPosition = new Vector3(p.P.x, -p.P.y, -65);
                p.Object.transform.localScale = new Vector3(240, 240, 1) * Mathf.Min(1, .35f + p.Age * 12);
                var tint = p.Color;
                tint.a = Mathf.Clamp01((7 - p.Age) / 2);
                p.Properties.SetColor("_Tint", tint);
                p.Renderer.SetPropertyBlock(p.Properties);
            }
            int liquidSteps = Mathf.Max(1, Mathf.CeilToInt(dt * 120));
            for (int i = 0; i < liquidSteps; i++)
                blobs.Step(dt / liquidSteps, 1440, 900, (float)S.Settings.Gravity * .55f, (float)S.Settings.Bounce, 180);
            UpdateDrops();
            glass.Step(dt, S.Settings.GentleMotion);
            UpdateGlass();
        }
        void UpdateDrops() => liquidRenderer?.Update(blobs, S.Settings);
        void UpdateGlass()
        {
            if (glass.Panes.Count != lastPanes)
            {
                foreach (var p in panes)
                    UnityEngine.Object.Destroy(p);
                foreach (var mesh in paneMeshes)
                    UnityEngine.Object.Destroy(mesh);
                paneMeshes.Clear();
                panes.Clear();
                lastPanes = glass.Panes.Count;
                if (lastPanes <= 1)
                    return;
                foreach (var p in glass.Panes)
                {
                    var go = new GameObject("Connected glass pane");
                    go.transform.SetParent(Root.transform, false);
                    var line = go.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.loop = true;
                    line.positionCount = p.Points.Length;
                    line.widthMultiplier = 1.8f;
                    line.numCornerVertices = 2;
                    line.sharedMaterial = glassEdge;
                    var origin = p.Center;
                    var vertices = new Vector3[p.Points.Length];
                    var uv = new Vector2[p.Points.Length];
                    for (int j = 0; j < p.Points.Length; j++)
                    {
                        vertices[j] = new Vector3(p.Points[j].X - origin.X, -p.Points[j].Y + origin.Y, 0);
                        uv[j] = new Vector2(p.Points[j].X / 1440, 1 - p.Points[j].Y / 900);
                        line.SetPosition(j, vertices[j]);
                    }
                    var triangles = new int[Mathf.Max(0, p.Points.Length - 2) * 3];
                    for (int j = 0; j < p.Points.Length - 2; j++)
                    {
                        triangles[j * 3] = 0;
                        triangles[j * 3 + 1] = j + 1;
                        triangles[j * 3 + 2] = j + 2;
                    }
                    var mesh = new Mesh { name = "Same polygon as visible crack" };
                    mesh.vertices = vertices;
                    mesh.uv = uv;
                    mesh.triangles = triangles;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    paneMeshes.Add(mesh);
                    var face = new GameObject("Transparent face inside crack boundary");
                    face.transform.SetParent(go.transform, false);
                    face.transform.localPosition = new Vector3(0, 0, 1);
                    face.AddComponent<MeshFilter>().sharedMesh = mesh;
                    face.AddComponent<MeshRenderer>().sharedMaterial = glassFill;
                    panes.Add(go);
                }
            }
            for (int i = 0; i < panes.Count; i++)
            {
                var p = glass.Panes[i];
                panes[i].transform.localPosition = new Vector3(p.Center.X, -p.Center.Y, -25);
                panes[i].transform.localRotation = Quaternion.Euler(0, 0, -p.Angle * Mathf.Rad2Deg);
                panes[i].GetComponentInChildren<MeshRenderer>().enabled = S.Settings.GlassShader;
            }
        }
        void AddPaint(Vector2 position, Color color)
        {
            var p = new Paint { P = position, Radius = random.Next(65, 100), Color = color };
            p.Object = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(p.Object.GetComponent<Collider>());
            p.Object.name = "Continuous wet paint spatter";
            p.Object.transform.SetParent(Root.transform, false);
            p.Object.transform.localPosition = new Vector3(position.x, -position.y, -65);
            p.Object.transform.localScale = new Vector3(84, 84, 1);
            p.Renderer = p.Object.GetComponent<Renderer>();
            p.Renderer.sharedMaterial = paintMaterial;
            p.Properties.SetColor("_Tint", color);
            p.Properties.SetFloat("_Seed", p.Radius);
            p.Renderer.SetPropertyBlock(p.Properties);
            paint.Add(p);
        }
        void ResolveBalloonContacts()
        {
            for (int i = 0; i < glyphs.Count; i++)
            {
                var a = glyphs[i];
                if (!a.Reward)
                    continue;
                for (int j = i + 1; j < glyphs.Count; j++)
                {
                    var b = glyphs[j];
                    if (!b.Reward)
                        continue;
                    var delta = b.P - a.P;
                    float length = delta.magnitude, reach = a.Radius + b.Radius;
                    if (length >= reach)
                        continue;
                    var normal = length > .01f ? delta / length : Vector2.right;
                    a.P -= normal * (reach - length) * .5f;
                    b.P += normal * (reach - length) * .5f;
                    float speed = Vector2.Dot(b.V - a.V, normal);
                    if (speed < 0)
                    {
                        a.V += normal * speed * .8f;
                        b.V -= normal * speed * .8f;
                    }
                    a.Object.transform.localPosition = new Vector3(a.P.x, -a.P.y, 0);
                    b.Object.transform.localPosition = new Vector3(b.P.x, -b.P.y, 0);
                }
            }
        }
        void Pop(Glyph g)
        {
            if (!glyphs.Remove(g))
                return;
            poppedGlyphs++;
            S.Burst(g.P, g.Color, 40);
            S.Audio.Play("pop", S.Settings, .55f);
            UnityEngine.Object.Destroy(g.Object);
            if (current == g)
                current = null;
            if (g.Reward && reward.Pop())
            {
                S.Feedback.Pulse(5f);
                S.Audio.Play("powerup", S.Settings);
                NextWord();
            }
        }
        void Blast(Vector2 p, float radius, bool cannon)
        {
            blasts++;
            S.Audio.Play(cannon ? "pop" : "paint", S.Settings, .65f);
            MakeEffect(p, Celebration.Embers, 45);
            foreach (var g in glyphs.ToArray())
            {
                var delta = g.P - p;
                float distance = delta.magnitude;
                var normal = distance > 1 ? delta / distance : Vector2.up;
                if (g.Reward)
                {
                    if (distance < radius * .65f)
                    {
                        Pop(g);
                        continue;
                    }
                    if (distance < radius + g.Radius)
                        g.V += normal * 600;
                }
                else if (distance < radius)
                {
                    g.V += normal * (1 - distance / radius) * 1000;
                    if (cannon && distance < 65)
                    {
                        g.Balloon.Release();
                        g.Age = g.Life;
                    }
                }
            }
        }
        public override void Pointer(Vector2 p, bool right)
        {
            if (!S.Settings.MousePlay)
                return;
            if (right)
            {
                Blast(p, 190, false);
                return;
            }
            if ((float)S.Now - lastShot < .22f || shots.Count >= 250)
                return;
            lastShot = (float)S.Now;
            Vector2 origin = new Vector2(720, 910);
            Vector2 d = p - origin;
            if (d.sqrMagnitude < 1)
                d = Vector2.up;
            d.Normalize();
            shots.Add(new Shot { P = origin, V = d * 1050, Object = Visuals.Sphere(Root.transform, new Vector3(origin.x, -origin.y, -20), 9, Color.white) });
            S.Audio.Play("cannon", S.Settings, .7f);
        }
        void PrepareCanvasMaterials()
        {
            glowTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float distance = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32;
                    pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Max(0, 1 - distance), 2));
                }
            glowTexture.SetPixels(pixels);
            glowTexture.Apply();
            glowMaterial = new Material(Visuals.ParticleMaterial());
            starSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial = Visuals.ParticleMaterial();
            var glowObject = new GameObject("Additive radial pointer trails");
            glowObject.transform.SetParent(Root.transform, false);
            glowSystem = glowObject.AddComponent<ParticleSystem>();
            var glowMain = glowSystem.main;
            glowMain.playOnAwake = false;
            glowMain.loop = false;
            glowMain.startSpeed = 0;
            glowMain.maxParticles = 250;
            glowMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var glowEmission = glowSystem.emission;
            glowEmission.enabled = false;
            glowSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMaterial;
            var glassShader = Resources.Load<Shader>("Shaders/GlassPane");
            glassFill = new Material(glassShader);
            glassFill.SetColor("_Tint", new Color(.24f, .67f, .85f, .065f));
            glassEdge = new Material(glassShader);
            glassEdge.SetColor("_Tint", new Color(.56f, .84f, .97f, .68f));
            glassEdge.SetFloat("_Shine", 0);
            glassEdge.renderQueue = 3010;
            backgroundField = new Texture2D(192, 120, TextureFormat.RGB24, false);
            backgroundField.filterMode = FilterMode.Bilinear;
            var field = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(field.GetComponent<Collider>());
            field.name = "Parent-selected harmonic backdrop";
            field.transform.SetParent(Root.transform, false);
            field.transform.localPosition = new Vector3(720, -450, 850);
            field.transform.localScale = new Vector3(1440, 900, 1);
            backgroundMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            backgroundMaterial.SetTexture("_BaseMap", backgroundField);
            field.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;
            liquidRenderer = new LiquidCanvasRenderer(Root.transform);
            flames = new CanvasFireRenderer(Root.transform);
            paintMaterial = new Material(Resources.Load<Shader>("Shaders/WetPaint"));
        }
        void UpdateBackground(float dt)
        {
            backgroundClock += dt;
            if (backgroundClock < 1f / 30 || backgroundField == null)
                return;
            backgroundClock = 0;
            var data = new Color[192 * 120];
            float time = (float)(S.Now - entered) * (S.Settings.GentleMotion ? .18f : .45f);
            var backdrop = S.Settings.Backdrop;
            var bg = S.Settings.Theme == Mood.BlackAndWhite ? Color.black : S.Settings.Theme == Mood.Lagoon ? new Color(.031f, .11f, .165f) : S.Settings.Theme == Mood.Sunset ? new Color(.121f, .074f, .165f) : S.Settings.Theme == Mood.Candy ? new Color(.102f, .09f, .192f) : Style.Navy;
            for (int y = 0; y < 120; y++)
                for (int x = 0; x < 192; x++)
                {
                    Color color = bg;
                    if (S.Settings.Theme != Mood.BlackAndWhite && backdrop != Backdrop.Starfield && backdrop != Backdrop.RotatingStars)
                    {
                        float u = (x / 192f - .5f) * 2, v = (y / 120f - .5f) * 1.25f, radius = Mathf.Sqrt(u * u + v * v), wave;
                        if (backdrop == Backdrop.Vortex)
                            wave = Mathf.Sin(radius * 14 - time * 2 + Mathf.Sin(Mathf.Atan2(v, u) * 3 + time) * 1.4f);
                        else if (backdrop == Backdrop.Aurora)
                            wave = Mathf.Sin(v * 8 + Mathf.Sin(u * 2 + time) * 1.6f + Mathf.Sin(u * 5 - time * .7f) * .5f);
                        else
                            wave = (Mathf.Sin(u * 5 + time) + Mathf.Sin(v * 7 - time * .8f) + Mathf.Sin((u + v) * 4 + time * .6f) + Mathf.Sin(radius * 9 - time * 1.4f)) * .25f;
                        float band = (wave + 1) * 1.5f;
                        int index = Mathf.Clamp((int)band, 0, 2);
                        float ribbon = Mathf.Pow(Mathf.Max(0, 1 - Mathf.Abs(wave) * 2), 4);
                        color += Color.Lerp(ColorAt(index), ColorAt(index + 1), band - index) * (.025f + ribbon * .095f);
                    }
                    data[y * 192 + x] = color;
                }
            backgroundField.SetPixels(data);
            backgroundField.Apply();
        }
        void MakeEffect(Vector2 position, Celebration kind, int count, EffectRecipe recipe = null)
        {
            count = Mathf.RoundToInt(count * (float)S.Settings.EffectStrength * (S.Settings.GentleMotion ? .35f : 1));
            int limit = Mathf.Max(0, S.Settings.ParticleLimit - blobs.Drops.Count);
            for (int i = 0; i < count && motes.Count < limit; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2, speed = random.Next(45, 220) * (float)(recipe?.Speed ?? 1);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                if (kind == Celebration.Embers)
                    velocity = new Vector2(velocity.x * .85f, -Mathf.Abs(velocity.y) - 90);
                motes.Add(new Mote { P = position, V = velocity, Color = ColorAt(random.Next(4)), Life = (float)(recipe?.Lifetime ?? (3 + random.NextDouble() * 2)), Curl = (float)(recipe?.Curl ?? (kind == Celebration.Orbit ? 100 : 18)), Gravity = (float)(recipe?.Gravity ?? (kind == Celebration.Embers ? -.35 : kind == Celebration.Bubbles ? -.2 : 1)), Trail = (float)(recipe?.Trail ?? .25), Size = kind == Celebration.Embers ? random.Next(2, 6) : random.Next(4, 13), Kind = kind });
            }
        }
        void UpdateMotes(float dt)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120));
            float h = dt / steps;
            for (int step = 0; step < steps; step++)
                foreach (var m in motes)
                {
                    m.Age += h;
                    m.V += new Vector2(Mathf.Sin(m.P.y * .008f + m.Age), Mathf.Cos(m.P.x * .008f - m.Age)) * m.Curl * h;
                    m.V.y += (float)S.Settings.Gravity * m.Gravity * h;
                    m.V *= Mathf.Exp(-.22f * h);
                    m.P += m.V * h * (S.Settings.GentleMotion ? .4f : 1);
                }
            motes.RemoveAll(m => m.Age > m.Life);
        }
        void DrawGlow(Vector2 p, float size, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(p.x - size, p.y - size, size * 2, size * 2), glowTexture);
            GUI.color = previous;
        }
        void DrawEffects()
        {
            // Pointer trails render through their additive particle material; only celebration motes use this UI overlay.
            foreach (var m in motes)
            {
                float fade = Mathf.Clamp01((m.Life - m.Age) * 1.5f);
                var color = m.Color;
                color.a = fade;
                if (m.Trail > 0)
                {
                    var tail = color;
                    tail.a *= m.Trail * .4f;
                    DrawGlow(m.P - m.V * .04f, m.Size * 2, tail);
                }
                if (m.Kind == Celebration.Embers)
                    DrawGlow(m.P, m.Size * 3, color);
                else if (m.Kind == Celebration.Bubbles)
                {
                    Ui.Ball(m.P, m.Size, color);
                }
                else
                {
                    var matrix = GUI.matrix;
                    GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(m.P.x, m.P.y, 0), Quaternion.Euler(0, 0, m.Age * 50 + m.Size), Vector3.one);
                    Ui.Panel(new Rect(-m.Size * .6f, -m.Size, m.Size * 1.2f, m.Size * 2), color, 2);
                    GUI.matrix = matrix;
                }
            }
        }
        void ConfigurePreview()
        {
            if (!S.Preview)
                return;
            scenario = S.Options.Value("--scenario");
            void Stroke(int key, double at, double hold = .07)
            {
                previewStrokes.Add(new PreviewStroke { Key = key, Time = at, Down = true });
                previewStrokes.Add(new PreviewStroke { Key = key, Time = at + hold, Down = false });
            }
            void Type(string text, double at, double cadence = .22)
            {
                for (int i = 0; i < text.Length; i++)
                    Stroke(char.ToUpperInvariant(text[i]), at + i * cadence, Math.Min(.06, cadence * .7));
            }
            if (scenario == "rapid-milk" || scenario == "milk")
                Type("milk", .6, scenario == "rapid-milk" ? .07 : .3);
            if (scenario == "mommy")
                Type("mommy", .6, .17);
            if (scenario == "balloon")
                for (int i = 0; i < 20; i++)
                    Stroke(65, .4 + i * .12, .05);
            if (scenario == "balloon-sequence")
                Type("qqqzqqq", .4, .24);
            if (scenario == "icons")
                for (int i = 0; i < 8; i++)
                    Stroke(112 + i, .4 + i * .35);
            if (scenario == "twenty-keys")
                for (int i = 0; i < 20; i++)
                    Stroke(65 + i, .5 + i * .003, 1);
            if (scenario == "cluster")
                for (int burst = 0; burst < 9; burst++)
                    foreach (int key in new[] { 65, 83, 68 })
                        Stroke(key, .5 + burst * .28, .17);
            if (scenario == "glass" || scenario == "fracture")
                for (int burst = 0; burst < 32; burst++)
                    foreach (int key in new[] { 81, 87, 69, 73, 79, 80 })
                        Stroke(key, .3 + burst * .14, .1);
            if (scenario == "swipe")
                for (int burst = 0; burst < 4; burst++)
                    Type("qwertyuiop", .4 + burst * .7, .047);
            if (scenario == "fire" || scenario == "fire-tap")
            {
                Stroke(32, .4, scenario == "fire" ? 5 : .2);
                Type("abcdef", 1, .3);
            }
            if (scenario == "fireworks" || scenario == "counting" || scenario == "counting-hundred")
            {
                double at = .4;
                for (int n = 1; n <= (scenario == "counting-hundred" ? 100 : 24); n++)
                {
                    Type(n.ToString(), at, .075);
                    at += n < 10 ? .14 : .25;
                }
            }
            if (scenario == "word-balloons" || scenario == "word-pop" || scenario == "guided-mommy" || scenario == "patient-red")
            {
                string word = scenario == "guided-mommy" ? "mommy" : scenario == "patient-red" ? "red" : "cat";
                target = S.Store.Words.FirstOrDefault(w => w.Word == word) ?? new WordEntry { Word = word, Adventure = true };
                guided.Start(word);
                Type(word, .65, scenario == "patient-red" ? 3 : .22);
            }
            if (scenario == "spelling-timeout")
            {
                for (int n = 0; n < 8; n++)
                {
                    guided.Start("red");
                    guided.Add('r', 0);
                    guided.Add('e', 0);
                    guided.Add('d', 0);
                }
                target = new WordEntry { Word = "red" };
                guided.Start("red");
                guided.Add('r', S.Now - 44);
                guided.Add('e', S.Now - 44);
            }
            previewStrokes.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
        void RunPreview()
        {
            if (!S.Preview)
                return;
            double elapsed = S.Now - entered;
            while (previewCursor < previewStrokes.Count && previewStrokes[previewCursor].Time <= elapsed)
            {
                var stroke = previewStrokes[previewCursor++];
                if (stroke.Down)
                    previewHeld.Add(stroke.Key);
                else
                    previewHeld.Remove(stroke.Key);
                S.Keys = KeySnapshot.From(previewHeld);
                Key(new KeyEvent(stroke.Key, stroke.Down, S.Now));
            }
            if (previewHeld.Count > 0)
                S.Keys = KeySnapshot.From(previewHeld);
            if (scenario == "word-pop" && reward.Remaining > 0 && elapsed > 1.8 + previewPops * .3)
            {
                var g = glyphs.FirstOrDefault(x => x.Reward);
                if (g != null)
                {
                    Pointer(g.P, false);
                    previewPops++;
                }
            }
            if (scenario == "cannon-miss" && previewPops == 0 && elapsed > .5)
            {
                Pointer(new Vector2(1400, 40), false);
                previewPops++;
            }
        }
        public override void DrawUI()
        {
            foreach (var p in paint)
            {
                var tint = p.Color;
                tint.a = Mathf.Clamp01((7 - p.Age) / 2);
                for (int i = 0; i < 9; i++)
                {
                    float angle = i * Mathf.PI * 2 / 9 + p.Radius;
                    Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (85 + (i * 17 + (int)p.Radius) % 29);
                    Ui.Ball(p.P + offset, 2 + (i * 7 + (int)p.Radius) % 4, tint);
                }
                for (int i = 0; i < 5; i++)
                {
                    float length = p.Age * (16 + i * 4);
                    Ui.Panel(new Rect(p.P.x + (i - 2) * 17 - 3, p.P.y, 6, length), tint, 3);
                    Ui.Ball(p.P + new Vector2((i - 2) * 17, length), 5, tint);
                }
            }
            DrawEffects();
            if (mode == PlayMode.WordAdventure)
            {
                Ui.Label(new Rect(90, 40, 1100, 55), reward.Remaining > 0 ? "Pop the balloons!" : "Find the glowing key", 36, Color.white, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(1130, 42, 230, 55), (guided.Score + reward.Score) + " points", 25, ColorAt(1));
                for (int i = 0; i < guided.Target.Length; i++)
                {
                    float w = Mathf.Min(110, 1000f / guided.Target.Length);
                    float shake = S.Now < guided.FeedbackUntil && !S.Settings.GentleMotion ? Mathf.Sin((float)S.Now * 65) * 7 : 0;
                    var r = new Rect(720 - guided.Target.Length * w / 2 + i * w + shake, 150, w - 12, 115);
                    Ui.Panel(r, i < guided.Progress ? (S.Settings.Theme == Mood.BlackAndWhite ? Color.white : Style.Mint) : i == guided.Progress ? ColorAt(1) : Style.Panel);
                    Ui.Label(r, guided.Target[i].ToString().ToUpperInvariant(), 58, i <= guided.Progress ? Style.Navy : Color.white);
                }
                if (reward.Remaining == 0)
                {
                    if (guided.Progress < guided.Target.Length)
                    {
                        float w = Mathf.Min(110, 1000f / guided.Target.Length);
                        Ui.Panel(new Rect(720 - guided.Target.Length * w / 2 + guided.Progress * w, 273, (w - 12) * (float)guided.Remaining(S.Now), 5), ColorAt(1), 2);
                    }
                    string hint = target.Word.Length == 0 ? "Choose adventure words in Parent Studio." : S.Now < guided.FeedbackUntil ? "Time for that letter ran out. Let's try it again." : double.IsPositiveInfinity(guided.LetterSeconds) ? "Find the glowing letter. Take all the time you need." : "Find the glowing letter. " + guided.LetterSeconds.ToString("0") + " seconds per letter.";
                    Ui.Label(new Rect(160, 304, 1120, 62), hint, 26, new Color(.76f, .82f, .91f));
                    Ui.Label(new Rect(350, 370, 740, 42), "Words discovered  " + guided.Completed, 23, ColorAt(2));
                }
                if (reward.Remaining == 0 && S.Settings.ShowKeyboard)
                    Ui.Keyboard(guided.Progress < guided.Target.Length ? guided.Target[guided.Progress] : ' ', S.Keys, S.Settings.Theme);
            }
            else if (mode == PlayMode.Counting)
            {
                Ui.Label(new Rect(100, 45, 1240, 75), "Counting Stars", 40, Color.white);
                Ui.Label(new Rect(400, 165, 640, 170), S.Now < celebrationUntil || !double.IsPositiveInfinity(hundredAt) ? "100" : counting.Expected.ToString(), 130, ColorAt(1));
                foreach (var flash in countFlashes)
                {
                    var tint = ColorAt(flash.Number);
                    tint.a = Mathf.Clamp01((float)((flash.Until - S.Now) / .38));
                    Ui.Label(new Rect(flash.Position.x - 50, flash.Position.y, 100, 60), flash.Number.ToString(), 38, tint);
                }
                if (S.Now < celebrationUntil)
                    Ui.Label(new Rect(170, 455, 1100, 115), "Congratulations!", 74, Style.Dots);
                Ui.Label(new Rect(300, 357, 840, 55), S.Now < celebrationUntil ? "You counted all the way to one hundred!" : "Type the next number", 28, Color.white);
            }
            else if (!typed)
            {
                Ui.Label(new Rect(100, 240, 1240, 110), "Smash Garden", 70, Color.white);
                Ui.Label(new Rect(180, 365, 1080, 65), "Every little key opens a world of wonder", 30, new Color(.61f, .73f, .88f));
            }
            if (messageUntil > S.Now)
            {
                Ui.Label(new Rect(300, 460, 840, 115), message, 75, ColorAt(1));
                if (wordImage)
                    GUI.DrawTexture(new Rect(555, 180, 330, 250), wordImage, ScaleMode.ScaleToFit, true);
            }
        }
        public override void Suspend()
        {
            refreshOnResume = true;
            words?.Reset();
            gestures.Reset();
            counting.Reset();
            fireworks.Clear();
            countFlashes.Clear();
            hundredAt = double.PositiveInfinity;
            celebrationUntil = 0;
            glass.Reset();
            blobs.Clear();
            current = null;
            activeKey = -1;
            fire = fireHold = 0;
            message = "";
            messageUntil = 0;
            typed = false;
            pointerReady = false;
            glows.Clear();
            if (glowSystem)
                glowSystem.Clear();
            motes.Clear();
            S.Audio.Stop();
            foreach (var g in glyphs)
                if (g.Object)
                    UnityEngine.Object.Destroy(g.Object);
            glyphs.Clear();
            reward.Clear();
            foreach (var s in shots)
                if (s.Object)
                    UnityEngine.Object.Destroy(s.Object);
            shots.Clear();
            foreach (var r in rockets)
                if (r.Object)
                    UnityEngine.Object.Destroy(r.Object);
            rockets.Clear();
            foreach (var p in paint)
                if (p.Object)
                    UnityEngine.Object.Destroy(p.Object);
            paint.Clear();
            foreach (var p in panes)
                if (p)
                    p.SetActive(false);
            lastPanes = -1;
            liquidRenderer?.Clear();
            flames?.Clear();
            if (mode == PlayMode.WordAdventure)
                guided.Start(target.Word);
            if (wordImage)
                UnityEngine.Object.Destroy(wordImage);
            wordImage = null;
        }
        public override void Exit()
        {
            Suspend();
            if (backgroundField)
                UnityEngine.Object.Destroy(backgroundField);
            if (glowTexture)
                UnityEngine.Object.Destroy(glowTexture);
            if (glowMaterial)
                UnityEngine.Object.Destroy(glowMaterial);
            liquidRenderer?.Dispose();
            flames?.Dispose();
            if (backgroundMaterial)
                UnityEngine.Object.Destroy(backgroundMaterial);
            if (glassFill)
                UnityEngine.Object.Destroy(glassFill);
            if (glassEdge)
                UnityEngine.Object.Destroy(glassEdge);
            if (paintMaterial)
                UnityEngine.Object.Destroy(paintMaterial);
            if (classicFont)
                UnityEngine.Object.Destroy(classicFont);
            foreach (var mesh in paneMeshes)
                UnityEngine.Object.Destroy(mesh);
            base.Exit();
        }
        public override string DiagnosticState => "mode=" + mode + " glyphs=" + glyphs.Count + " score=" + (guided.Score + reward.Score) + " completed=" + guided.Completed + " count=" + counting.Expected + " pending=" + counting.Pending + " balloons=" + reward.Remaining + " recognized=" + recognized + " celebrating=" + (S.Now < celebrationUntil) + " rockets=" + launched + " blasts=" + blasts + " shots=" + shots.Count + " paint=" + paint.Count + " cracks=" + glass.Panes.Count + " shattered=" + shattered + " drops=" + blobs.Drops.Count + " liquidRT=" + liquidRenderer?.Diagnostic + " popped=" + poppedGlyphs + " largest=" + (glyphs.Count == 0 ? 0 : glyphs.Max(g => g.Balloon.Size)).ToString("0.00") + " flames=" + flames.Count + " fire=" + fire.ToString("0");
    }
}
