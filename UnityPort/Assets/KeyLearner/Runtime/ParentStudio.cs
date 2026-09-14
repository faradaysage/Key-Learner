using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using KeyLearner.Studio;
using KeyLearner.Unity.Platform;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KeyLearner.Unity
{
    /// <summary>Explicit parent controls; native key events also edit text while protected input is suppressed.</summary>
    public sealed class ParentStudio
    {
        readonly GameServices services;
        readonly WindowsInputSession input;
        readonly Action close, quit;
        readonly GestureAnalyzer analyzer = new GestureAnalyzer();
        readonly HashSet<int> held = new HashSet<int>();
        readonly string[] tabs = { "Experience", "Voice", "Learning", "Dictionary", "Developer", "Icons", "Balloons", "Play & safety", "Graphics" };
        readonly Dictionary<string, string> icons;
        readonly string[] iconNames;
        readonly Font iconFont;
        int tab, row, wordIndex, iconPage, iconKey = 112, calibration = -1;
        bool captureKey, showCredits; Vector2 creditsScroll; string creditsText;
        string notice = "Settings stay on this computer.", query = "", iconQuery = "", editTitle = "", editValue;
        Action<string> editCommit;
        double calibrateUntil;
        List<SettingRow> currentRows = new List<SettingRow>();
        GameObject benchmarkRoot;
        bool benchmarking;
        double benchmarkStart, lastBenchmarkFrame;
        readonly List<double> benchmarkFrames = new List<double>();
        Texture2D readback;
        Vector3 savedCameraPosition; Quaternion savedCameraRotation; bool savedOrthographic, savedFog; float savedCameraSize, savedFov; Color savedBackground;
        int savedVsync, savedFps; float savedRenderScale;
        string benchmarkResult = "Run a six-second graphics workload.", recommendation = "";
        Settings S => services.Settings;
        sealed class SettingRow
        {
            public string Label; public Func<string> Value; public Action<int> Change;
        }
        public ParentStudio(GameServices services, WindowsInputSession input, Action close, Action quit)
        {
            this.services = services;
            this.input = input;
            this.close = close;
            this.quit = quit;
            var path = Path.Combine(Application.streamingAssetsPath, "Content", "Icons", "catalog.json");
            try
            {
                icons = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            }
            catch (IOException) { icons = new Dictionary<string, string>(); }
            iconNames = Friendly.Where(icons.ContainsKey).Concat(icons.Keys.Except(Friendly).OrderBy(n => n)).Distinct().ToArray();
            iconFont = Resources.Load<Font>("Fonts/fa-solid-900");
        }
        public static readonly string[] Friendly = { "face-smile", "star", "heart", "sun", "moon", "cloud", "rainbow", "snowflake", "cat", "dog", "fish", "frog", "hippo", "otter", "dove", "dragon", "paw", "apple-whole", "carrot", "lemon", "ice-cream", "cookie", "cake-candles", "car", "bicycle", "rocket", "plane", "sailboat", "train", "house", "tree", "leaf", "seedling", "flower", "music", "bell", "gift", "balloon", "futbol", "basketball", "volleyball", "baseball", "umbrella", "crown", "gem", "puzzle-piece", "robot", "shapes" };
        void RequirePreview()
        {
            if (!services.Preview)
                throw new InvalidOperationException("Parent probes require preview mode.");
        }
        public void PreviewTab(int index)
        {
            RequirePreview();
            Suspend();
            showCredits = false;
            tab = Mathf.Clamp(index, 0, tabs.Length - 1);
            row = 0;
        }
        public string PreviewState
        {
            get
            {
                RequirePreview();
                return "tab=" + tab + " name=" + tabs[tab] + " editing=" + (editValue != null) + " calibration=" + calibration + " benchmarking=" + benchmarking;
            }
        }
        public void PreviewEdit()
        {
            RequirePreview();
            Edit("Preview focus recovery", "temporary text", _ => { });
        }
        public void PreviewCredits()
        {
            RequirePreview();
            showCredits = true;
        }
        public void Suspend()
        {
            held.Clear();
            analyzer.Reset();
            editValue = null;
            editCommit = null;
            captureKey = false;
            if (calibration >= 0)
            {
                calibration = -1;
                notice = "Calibration stopped when the window changed. Start another sample to continue.";
            }
            if (benchmarking)
                FinishBenchmark(true);
        }
        public void Key(KeyEvent e)
        {
            if (e.Key < 0)
            {
                Suspend();
                return;
            }
            if (e.Down)
                held.Add(e.Key);
            else
                held.Remove(e.Key);
            if (!e.Down)
                return;
            bool control = held.Contains(162) || held.Contains(163), shift = held.Contains(160) || held.Contains(161);
            if (calibration >= 0)
            {
                var context = analyzer.Add(e.Key, services.Now, held.Count, null, false);
                services.Store.Gestures.Network.Train(context.Features, calibration);
                return;
            }
            if (captureKey)
            {
                if (KeyboardMap.Character(e.Key) == null && e.Key != 32 && !ParentChord.IsModifier(e.Key))
                {
                    iconKey = e.Key;
                    captureKey = false;
                    notice = "Choose a picture for virtual key " + iconKey + ".";
                }
                return;
            }
            if (editValue != null)
            {
                if (e.Key == 27)
                {
                    editValue = null;
                    editCommit = null;
                    return;
                }
                if (e.Key == 13)
                {
                    CommitEdit();
                    return;
                }
                if (e.Key == 8)
                {
                    if (editValue.Length > 0)
                        editValue = editValue.Substring(0, editValue.Length - 1);
                    return;
                }
                if (control && e.Key == 86)
                {
                    editValue = (editValue + GUIUtility.systemCopyBuffer);
                    if (editValue.Length > 2048)
                        editValue = editValue.Substring(0, 2048);
                    return;
                }
                if (control && e.Key == 65)
                {
                    editValue = "";
                    return;
                }
                if (control || held.Contains(164) || held.Contains(165))
                    return;
                char? c = Character(e.Key, shift);
                if (c.HasValue && editValue.Length < 2048)
                    editValue += c.Value;
                return;
            }
            if (showCredits)
            {
                if (e.Key == 27)
                    showCredits = false;
                return;
            }
            if (e.Key == 9)
            {
                tab = (tab + 1) % tabs.Length;
                row = 0;
                return;
            }
            if(tab==1 && e.Key==32){services.Audio.PreviewVoice(S.VoicePackId,S);return;}
            // Tab and arrow events can arrive before the next OnGUI repaint.
            // Resolve rows from the current tab, never a stale rendered section.
            currentRows = Rows();
            if (e.Key == 38)
                row = Math.Max(0, row - 1);
            if (e.Key == 40)
                row = Math.Min(currentRows.Count - 1, row + 1);
            if (currentRows.Count > 0 && row >= 0 && row < currentRows.Count && (e.Key == 37 || e.Key == 39 || e.Key == 13))
            {
                currentRows[row].Change(e.Key == 37 ? -1 : 1);
                Changed();
            }
        }
        static char? Character(int key, bool shift)
        {
            if (key >= 65 && key <= 90)
                return (char)(shift ? key : key + 32);
            if (key >= 48 && key <= 57)
                return shift ? ")!@#$%^&*("[key - 48] : (char)key;
            if (key >= 96 && key <= 105)
                return (char)('0' + key - 96);
            switch (key)
            {
                case 32:
                    return ' ';
                case 186:
                    return shift ? ':' : ';';
                case 187:
                    return shift ? '+' : '=';
                case 188:
                    return shift ? '<' : ',';
                case 189:
                    return shift ? '_' : '-';
                case 190:
                    return shift ? '>' : '.';
                case 191:
                    return shift ? '?' : '/';
                case 192:
                    return shift ? '~' : '`';
                case 219:
                    return shift ? '{' : '[';
                case 220:
                    return shift ? '|' : '\\';
                case 221:
                    return shift ? '}' : ']';
                case 222:
                    return shift ? '"' : '\'';
                case 110:
                    return '.';
                case 111:
                    return '/';
                case 106:
                    return '*';
                case 107:
                    return '+';
                case 109:
                    return '-';
                default:
                    return null;
            }
        }
        void Changed()
        {
            S.Normalize();
            ApplyGraphics();
            notice = "Unsaved changes. Save & return to keep them.";
        }
        void ApplyGraphics()
        {
            if (benchmarking)
                return;
            QualitySettings.vSyncCount = S.VSync ? 1 : 0;
            services.Rewards.Gentle = S.GentleMotion;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline)
            {
                pipeline.renderScale = (float)S.RenderScale;
                pipeline.msaaSampleCount = S.SmoothEdges ? 4 : 1;
            }
        }
        void Edit(string title, string value, Action<string> commit)
        {
            editTitle = title;
            editValue = value ?? "";
            editCommit = commit;
        }
        void CommitEdit()
        {
            var commit = editCommit;
            var value = editValue;
            editValue = null;
            editCommit = null;
            commit?.Invoke(value);
            Changed();
        }
        void SaveClose()
        {
            if (benchmarking)
                FinishBenchmark(true);
            calibration = -1;
            services.Store.Save();
            services.Audio.Stop();
            close();
        }
        public void Draw()
        {
            held.Clear();
            held.UnionWith(services.Keys.Keys);
            TickBenchmark();
            if (showCredits)
            {
                DrawCredits();
                return;
            }
            if (calibration >= 0)
            {
                DrawCalibration();
                return;
            }
            if (benchmarking)
            {
                Ui.Panel(new Rect(45, 35, 800, 150), Style.Navy);
                Ui.Label(new Rect(65, 45, 750, 70), "Testing graphics · " + Math.Max(0, 6 - (int)(services.Now - benchmarkStart)) + " seconds", 35, Color.white);
                Ui.Label(new Rect(65, 112, 750, 45), "Your settings will be restored before you choose Apply.", 23, Style.Mint);
                return;
            }
            Ui.Panel(new Rect(0, 0, 1440, 900), Style.Navy);
            Ui.Label(new Rect(55, 25, 1100, 65), "The Parent Studio", 44, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(58, 92, 1260, 37), "A little world, tuned to your child", 24, new Color(.62f, .72f, .84f), TextAnchor.MiddleLeft);
            for (int i = 0; i < tabs.Length; i++)
            {
                int selected = i;
                Ui.Button(new Rect(55 + i * 148, 155, 140, 57), tabs[i], () => { tab = selected; row = 0; });
                if (tab == i)
                    Ui.Panel(new Rect(65 + i * 148, 219, 120, 4), Style.Dots, 2);
            }
            currentRows = Rows();
            if (tab == 3)
                DrawDictionary();
            else if (tab == 5)
                DrawIcons();
            else
                DrawRows();
            Ui.Panel(new Rect(0, 810, 1440, 90), Style.Panel);
            Ui.Label(new Rect(55, 819, 790, 35), notice, 20, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(55, 855, 1060, 33), "Tab: section · Arrows: select / adjust · Enter: edit · " + input.Status, 16, new Color(.60f, .69f, .79f), TextAnchor.MiddleLeft);
            Ui.Button(new Rect(865, 827, 235, 55), "Art & licenses", () => showCredits = true);
            Ui.Button(new Rect(1122, 827, 266, 55), "Save & return", SaveClose);
            if (editValue != null)
                DrawEdit();
        }
        void DrawRows()
        {
            for (int i = 0; i < currentRows.Count; i++)
            {
                int index = i;
                var setting = currentRows[i];
                float y = 250 + i * 53;
                Ui.Panel(new Rect(55, y, 1040, 47), row == i ? new Color(.10f, .20f, .30f) : Style.Panel);
                Ui.Label(new Rect(74, y + 2, 375, 43), setting.Label, 23, Color.white, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(435, y + 2, 525, 43), setting.Value(), 21, Style.Mint, TextAnchor.MiddleCenter);
                Ui.Button(new Rect(974, y, 48, 44), "−", () => { row = index; setting.Change(-1); Changed(); });
                Ui.Button(new Rect(1031, y, 48, 44), "+", () => { row = index; setting.Change(1); Changed(); });
            }
            if (tab == 1)
            {
                Side(255, "Try this voice", () => services.Audio.PreviewVoice(S.VoicePackId, S));
                Ui.Label(new Rect(1125, 329, 250, 155), "Changes apply now. Save & return keeps them. Space: preview narrator.", 23, Color.white);
                Ui.Label(new Rect(55, 740, 1050, 45), services.Audio.Status, 19, Style.Mint, TextAnchor.MiddleLeft);
            }
            if (tab == 2)
            {
                Ui.Label(new Rect(55, 632, 1050, 37), "Optional supervised calibration · 12 seconds for each pattern", 23, Style.Mint, TextAnchor.MiddleLeft);
                for (int i = 0; i < 5; i++)
                {
                    int gesture = i;
                    Ui.Button(new Rect(55 + i * 207, 683, 196, 52), ((Gesture)i).ToString(), () => { calibration = gesture; calibrateUntil = services.Now + 12; held.Clear(); analyzer.Reset(); S.UseGestureCalibration = true; services.Audio.Stop(); });
                }
                Side(257, "Reset gesture training", () => { services.Store.ResetGestureTraining(); notice = "Gesture calibration reset; word habits kept."; });
                Side(327, "Reset word learning", () => { services.Store.ResetWordLearning(); notice = "Word habits reset; gesture calibration kept."; });
                Side(590, "Restart spelling", () => { notice = CanvasGame.RestartSpelling(services, true) ? "Spelling session restarted; learned word habits kept." : "Spelling is ready to begin; no session score to reset."; });
                Ui.Label(new Rect(1125, 410, 250, 170), "Training samples: " + services.Store.Gestures.Network.Samples + "\nWord timing learns separately during play.", 22, Color.white);
            }
            if (tab == 7)
            {
                Side(260, "Touchpad setup & quit", () => { input.AuthorizeExit(); services.Store.Save(); Process.Start(new ProcessStartInfo("ms-settings:devices-touchpad") { UseShellExecute = true }); quit(); });
                Side(334, "Save & quit", () => { services.Store.Save(); quit(); });
                Ui.Label(new Rect(55, 650, 1040, 117), "Hold exact Ctrl + Alt + O or Ctrl + Shift + O for 2 seconds: parents.\nHold exact Ctrl + Alt + Escape for 2 seconds: quit.\nTen complete O / Escape taps recover options / exit.", 22, Color.white, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(1125, 422, 250, 290), "Windows session protection cannot block secure desktops or all touchpad gestures. Set three- and four-finger touchpad actions to Nothing before play.", 22, new Color(.66f, .76f, .85f));
            }
            if (tab == 6)
                Ui.Label(new Rect(55, 430, 1040, 160), "Consecutive repeats inflate the same balloon. A different key retires it, so returning to a letter creates a fresh one.\nThe default pop size is 3×; each inflation step deflates over 3 seconds.", 27, Color.white);
            if (tab == 8)
            {
                Side(255, "Run benchmark", StartBenchmark);
                Ui.Label(new Rect(1125, 325, 250, 170), benchmarkResult, 22, Color.white);
                if (recommendation.Length > 0)
                    Side(505, "Apply " + recommendation, () => { S.RenderScale = recommendation == "Performance" ? .75 : 1; S.LiquidScale = recommendation == "High" ? 1 : recommendation == "Balanced" ? .7 : .5; S.TerrainDetail = recommendation == "High" ? 80 : recommendation == "Balanced" ? 56 : 32; Changed(); });
                Ui.Label(new Rect(1125, 596, 250, 130), "Includes GPU readback. This measures this laptop and workload.", 21, new Color(.67f, .75f, .85f));
            }
        }
        void Side(float y, string text, Action action) => Ui.Button(new Rect(1125, y, 250, 57), text, action);
        List<SettingRow> Rows()
        {
            var rows = new List<SettingRow>();
            void Toggle(string label, Func<bool> get, Action<bool> set) => rows.Add(new SettingRow { Label = label, Value = () => get() ? "On" : "Off", Change = _ => set(!get()) });
            void Number(string label, Func<double> get, Action<double> set, double step) => rows.Add(new SettingRow { Label = label, Value = () => get().ToString("0.##", CultureInfo.InvariantCulture), Change = d => set(get() + d * step) });
            void Text(string label, Func<string> get, Action<string> set) => rows.Add(new SettingRow { Label = label, Value = () => string.IsNullOrWhiteSpace(get()) ? "Automatic / not configured" : get(), Change = _ => Edit(label, get(), v => set(v.Trim().Trim('"'))) });
            void Cycle<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum => rows.Add(new SettingRow { Label = label, Value = () => get().ToString(), Change = d => { var values = (T[])Enum.GetValues(typeof(T)); set(values[(Array.IndexOf(values, get()) + d + values.Length) % values.Length]); } });
            switch (tab)
            {
                case 0:
                    Cycle("Palette", () => S.Theme, v => S.Theme = v);
                    Cycle("Font", () => S.Font, v => S.Font = v);
                    Toggle("Sound", () => S.Sound, v => S.Sound = v);
                    Toggle("Gentle motion", () => S.GentleMotion, v => S.GentleMotion = v);
                    Toggle("Show keyboard", () => S.ShowKeyboard, v => S.ShowKeyboard = v);
                    Number("Letter scale", () => S.FontScale, v => S.FontScale = v, .1);
                    Cycle("Backdrop", () => S.Backdrop, v => S.Backdrop = v);
                    break;
                case 1:
                    rows.Add(new SettingRow { Label = "Narrator", Value = () => services.Store.SpeechPacks.DisplayName(S.VoicePackId), Change = d => {
                        var values=services.Store.SpeechPacks.Packs.Select(v=>v.Id).ToArray();var index=Array.IndexOf(values,services.Store.SpeechPacks.NormalizeChoice(S.VoicePackId));
                        services.Audio.SelectVoice(S,values[(index+d+values.Length)%values.Length]);
                    } });
                    rows.Add(new SettingRow { Label = "Custom Windows voice", Value = () => S.WindowsVoice.Length > 0 ? S.WindowsVoice : "Automatic", Change = d => { var values = new[] { "" }.Concat(services.Audio.Voices).ToArray(); S.WindowsVoice = values[(Math.Max(0, Array.IndexOf(values, S.WindowsVoice)) + d + values.Length) % values.Length]; } });
                    Number("Custom speech rate", () => S.SpeechRate, v => S.SpeechRate = (int)v, 1);
                    Number("Volume", () => S.Volume, v => S.Volume = (int)v, 5);
                    Toggle("Speak letters", () => S.SpeakLetters, v => S.SpeakLetters = v);
                    Text("Optional Piper executable", () => S.PiperExecutable, v => S.PiperExecutable = v);
                    Text("Optional Piper model", () => S.PiperModel, v => S.PiperModel = v);
                    Number("Key voice channels", () => S.KeyVoiceChannels, v => S.KeyVoiceChannels = (int)v, 1);
                    Number("Word voice channels", () => S.WordVoiceChannels, v => S.WordVoiceChannels = (int)v, 1);
                    break;
                case 2:
                    Toggle("Forgiving spelling", () => S.ForgivingSpelling, v => S.ForgivingSpelling = v);
                    Toggle("Learn word habits", () => S.AdaptiveLearning, v => S.AdaptiveLearning = v);
                    Number("Word pause", () => S.WordPause, v => S.WordPause = v, .1);
                    Number("Prefix pause", () => S.PrefixPause, v => S.PrefixPause = v, .1);
                    Toggle("Use gesture calibration", () => S.UseGestureCalibration, v => S.UseGestureCalibration = v);
                    Toggle("Timed spelling", () => S.TimedSpelling, v => S.TimedSpelling = v);
                    Number("Flight / swim response", () => S.FlightResponse, v => S.FlightResponse = v, .25);
                    break;
                case 4:
                    Number("Particle limit", () => S.ParticleLimit, v => S.ParticleLimit = (int)v, 50);
                    Number("Gravity", () => S.Gravity, v => S.Gravity = v, 10);
                    Number("Bounce", () => S.Bounce, v => S.Bounce = v, .05);
                    Number("Effect strength", () => S.EffectStrength, v => S.EffectStrength = v, .1);
                    Number("Letter lifetime", () => S.LetterLifetime, v => S.LetterLifetime = v, .5);
                    Toggle("Show input context", () => S.ShowContext, v => S.ShowContext = v);
                    Number("Star count", () => S.StarCount, v => S.StarCount = (int)v, 50);
                    Number("Star speed", () => S.StarSpeed, v => S.StarSpeed = v, .1);
                    break;
                case 6:
                    Number("Balloon pop size", () => S.BalloonPopSize, v => S.BalloonPopSize = v, .25);
                    Number("Deflation seconds per step", () => S.BalloonDeflateSeconds, v => S.BalloonDeflateSeconds = v, .25);
                    break;
                case 7:
                    Toggle("Gesture effects", () => S.GestureEffects, v => S.GestureEffects = v);
                    Toggle("Mouse play", () => S.MousePlay, v => S.MousePlay = v);
                    Toggle("Growing fire", () => S.GrowingFire, v => S.GrowingFire = v);
                    Toggle("Effects sound", () => S.EffectsSound, v => S.EffectsSound = v);
                    Number("Effects volume", () => S.EffectsVolume, v => S.EffectsVolume = (int)v, 5);
                    Number("Mouse trail size", () => S.MouseTrailSize, v => S.MouseTrailSize = v, .1);
                    Number("Boost speed limit", () => S.FlightTopSpeed, v => S.FlightTopSpeed = v, 20);
                    break;
                case 8:
                    Number("Render scale", () => S.RenderScale, v => S.RenderScale = v, .1);
                    Number("Liquid scale", () => S.LiquidScale, v => S.LiquidScale = v, .1);
                    Number("Terrain detail", () => S.TerrainDetail, v => S.TerrainDetail = (int)v, 8);
                    Toggle("Extruded letters / icons", () => S.ExtrudedAssets, v => S.ExtrudedAssets = v);
                    Toggle("Glass shading", () => S.GlassShader, v => S.GlassShader = v);
                    Toggle("Smooth edges", () => S.SmoothEdges, v => S.SmoothEdges = v);
                    Toggle("VSync", () => S.VSync, v => S.VSync = v);
                    Toggle("Toon shading", () => S.ToonAssets, v => S.ToonAssets = v);
                    Toggle("Flight assistance", () => S.FlightAssist, v => S.FlightAssist = v);
                    break;
            }
            return rows;
        }
        void DrawCredits()
        {
            if (creditsText == null)
            {
                var asset = Resources.Load<TextAsset>("AssetCredits");
                creditsText = asset ? asset.text : "See THIRD_PARTY_ASSETS.md and bundled license files beside this game.";
            }
            Ui.Panel(new Rect(0, 0, 1440, 900), Style.Navy);
            Ui.Label(new Rect(70, 30, 1200, 65), "The artists who made this world", 40, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(70, 105, 1200, 43), "All gameplay art and audio are included locally. Thank you to these generous creators.", 23, Style.Mint, TextAnchor.MiddleLeft);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true, richText = false };
            style.normal.textColor = new Color(.86f, .90f, .95f);
            float height = style.CalcHeight(new GUIContent(creditsText), 1200) + 40;
            creditsScroll = GUI.BeginScrollView(new Rect(65, 175, 1310, 610), creditsScroll, new Rect(0, 0, 1260, Mathf.Max(600, height)));
            GUI.Label(new Rect(20, 10, 1200, height), creditsText, style);
            GUI.EndScrollView();
            Ui.Button(new Rect(1055, 820, 310, 57), "Back to Parent Studio", () => showCredits = false);
        }
        void DrawCalibration()
        {
            if (services.Now >= calibrateUntil)
            {
                calibration = -1;
                services.Store.Save();
                notice = "Calibration saved. Add balanced samples for all five patterns.";
                return;
            }
            Ui.Panel(new Rect(0, 0, 1440, 900), Style.Navy);
            Ui.Label(new Rect(100, 170, 1240, 120), "Show me: " + ((Gesture)calibration), 60, Style.Dots);
            Ui.Label(new Rect(100, 315, 1240, 70), Math.Ceiling(calibrateUntil - services.Now) + " seconds remaining", 34, Color.white);
            Ui.Label(new Rect(100, 410, 1240, 65), "Only these parent-labelled samples train the gesture network.", 28, Color.white);
            Ui.Label(new Rect(100, 510, 1240, 55), "Held keys: " + held.Count + " · Samples: " + services.Store.Gestures.Network.Samples, 28, Style.Mint);
            Ui.Button(new Rect(545, 675, 350, 65), "Stop calibration", () => { calibration = -1; analyzer.Reset(); notice = "Calibration stopped. Samples remain available."; });
        }
        void DrawDictionary()
        {
            Ui.Button(new Rect(55, 250, 500, 51), query.Length > 0 ? "Search: " + query : "Search dictionary…", () => Edit("Search words", query, v => { query = v; wordIndex = 0; }));
            Ui.Button(new Rect(575, 250, 235, 51), "+ Add word", () => Edit("New word · 2–24 letters", "", v => { v = v.Trim().ToLowerInvariant(); if (!Store.ValidWord(v)) { notice = "Use 2–24 letters A–Z."; return; } if (!services.Store.Words.Any(w => w.Word == v)) services.Store.Words.Add(new WordEntry { Word = v }); query = v; wordIndex = 0; }));
            var words = services.Store.Words.Where(w => w.Word.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(w => w.Word).ToArray();
            wordIndex = Mathf.Clamp(wordIndex, 0, Math.Max(0, words.Length - 1));
            int start = wordIndex / 7 * 7;
            for (int i = start; i < Math.Min(start + 7, words.Length); i++)
            {
                int index = i;
                Ui.Button(new Rect(55, 326 + (i - start) * 57, 500, 50), (words[i].Enabled ? "" : "[off] ") + words[i].Word, () => wordIndex = index);
                if (index == wordIndex)
                    Ui.Panel(new Rect(57, 334 + (i - start) * 57, 5, 33), Style.Dots);
            }
            Ui.Button(new Rect(55, 751, 150, 44), "Previous", () => wordIndex = Math.Max(0, wordIndex - 7));
            Ui.Button(new Rect(220, 751, 150, 44), "Next", () => wordIndex = Math.Min(words.Length - 1, wordIndex + 7));
            Ui.Label(new Rect(390, 751, 170, 44), words.Length + " words", 21, Color.white);
            if (words.Length == 0)
                return;
            var word = words[wordIndex];
            Ui.Label(new Rect(594, 319, 740, 55), word.Word, 42, Style.Dots, TextAnchor.MiddleLeft);
            Ui.Button(new Rect(595, 391, 780, 49), "Spoken phrase / " + (word.Spoken.Length > 0 ? word.Spoken : word.Word), () => Edit("What should the voice say?", word.Spoken, v => word.Spoken = v));
            Ui.Button(new Rect(595, 450, 780, 49), "Celebration / " + (!string.IsNullOrEmpty(word.EffectPreset) ? word.EffectPreset : word.Effect.ToString()), () => { word.Effect = (Celebration)(((int)word.Effect + 1) % 5); word.EffectPreset = ""; Changed(); });
            Ui.Button(new Rect(595, 509, 780, 49), "WAV recording / " + (word.Recording.Length > 0 ? word.Recording : "Use speech"), () => Edit("Absolute path to a WAV recording", word.Recording, v => word.Recording = v.Trim().Trim('"')));
            Ui.Button(new Rect(595, 568, 780, 49), "Picture / " + (word.Image.Length > 0 ? word.Image : "None"), () => Edit("Absolute path to a PNG or JPG", word.Image, v => word.Image = v.Trim().Trim('"')));
            Ui.Button(new Rect(595, 628, 378, 49), "Enabled / " + word.Enabled, () => { word.Enabled = !word.Enabled; Changed(); });
            Ui.Button(new Rect(993, 628, 382, 49), "Adventure / " + word.Adventure, () => { word.Adventure = !word.Adventure; Changed(); });
            Ui.Button(new Rect(595, 697, 378, 49), "Preview voice", () => services.Audio.Say(word.Spoken.Length > 0 ? word.Spoken : word.Word, S, word.Recording));
            Ui.Button(new Rect(993, 697, 382, 49), "Rename word", () => Edit("Rename word", word.Word, v => { v = v.Trim().ToLowerInvariant(); if (Store.ValidWord(v) && !services.Store.Words.Any(w => w != word && w.Word == v)) word.Word = v; else notice = "Invalid or duplicate word."; }));
            Ui.Button(new Rect(595, 758, 780, 38), "Data-only effect recipe / " + (!string.IsNullOrEmpty(word.EffectPreset) ? word.EffectPreset : "built-in"), () => Edit("Optional effect recipe name", word.EffectPreset, v => word.EffectPreset = v.Trim()));
        }
        void DrawIcons()
        {
            Ui.Button(new Rect(55, 255, 330, 57), captureKey ? "Press a non-letter key…" : "Choose key / " + iconKey, () => captureKey = true);
            Ui.Button(new Rect(55, 329, 330, 50), "Restore this key's default", () => { S.KeyIcons.Remove(iconKey); Changed(); });
            string selected = S.KeyIcons.TryGetValue(iconKey, out var assigned) && icons.ContainsKey(assigned) ? assigned : iconKey == 112 ? "face-smile" : Friendly.Where(icons.ContainsKey).DefaultIfEmpty("star").ToArray()[Math.Abs(iconKey - 112) % Math.Max(1, Friendly.Count(icons.ContainsKey))];
            DrawIcon(new Rect(105, 435, 230, 180), selected, 125);
            Ui.Label(new Rect(55, 650, 330, 80), selected, 24, Color.white);
            Ui.Button(new Rect(420, 255, 955, 57), iconQuery.Length > 0 ? "Search: " + iconQuery : "Search " + iconNames.Length + " icons…", () => Edit("Find an icon", iconQuery, v => { iconQuery = v; iconPage = 0; }));
            var names = iconNames.Where(n => n.IndexOf(iconQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            iconPage = Mathf.Clamp(iconPage, 0, Math.Max(0, (names.Length - 1) / 24));
            for (int i = iconPage * 24; i < Math.Min(names.Length, (iconPage + 1) * 24); i++)
            {
                string name = names[i];
                int local = i % 24;
                var rect = new Rect(420 + local % 6 * 160, 335 + local / 6 * 100, 147, 90);
                Ui.Button(rect, "", () => { S.KeyIcons[iconKey] = name; Changed(); });
                DrawIcon(new Rect(rect.x + 35, rect.y + 5, 78, 55), name, 43);
                Ui.Label(new Rect(rect.x + 3, rect.y + 60, 141, 27), name, 13, Color.white);
            }
            Ui.Button(new Rect(420, 756, 200, 44), "Previous icons", () => iconPage--);
            Ui.Button(new Rect(635, 756, 200, 44), "Next icons", () => iconPage++);
            Ui.Label(new Rect(850, 756, 400, 44), (iconPage + 1) + " / " + Math.Max(1, (names.Length + 23) / 24), 23, Color.white);
        }
        void DrawIcon(Rect rect, string name, int size)
        {
            if (!icons.TryGetValue(name, out var value) || !iconFont)
                return;
            var style = new GUIStyle(GUI.skin.label) { font = iconFont, fontSize = size, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Style.Dots;
            GUI.Label(rect, char.ConvertFromUtf32(Convert.ToInt32(value, 16)), style);
        }
        void DrawEdit()
        {
            Ui.Panel(new Rect(0, 0, 1440, 900), new Color(0, 0, 0, .82f));
            Ui.Panel(new Rect(130, 210, 1180, 455), Style.Panel);
            Ui.Label(new Rect(175, 244, 1090, 65), editTitle, 35, Color.white, TextAnchor.MiddleLeft);
            Ui.Panel(new Rect(175, 331, 1090, 145), Style.Navy);
            Ui.Label(new Rect(195, 338, 1040, 130), editValue + "▏", 25, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(175, 490, 1090, 43), "Type or Ctrl+V to paste · Ctrl+A clears · Backspace removes · Enter saves", 20, new Color(.65f, .75f, .86f));
            Ui.Button(new Rect(595, 565, 300, 62), "Cancel", () => { editValue = null; editCommit = null; });
            Ui.Button(new Rect(915, 565, 300, 62), "Keep this change", CommitEdit);
        }
        void StartBenchmark()
        {
            if (benchmarking)
                return;
            services.Audio.Stop();
            benchmarking = true;
            benchmarkFrames.Clear();
            recommendation = "";
            benchmarkStart = services.Now;
            lastBenchmarkFrame = KeyboardGuard.Now;
            var camera = services.Camera;
            savedCameraPosition = camera.transform.position;
            savedCameraRotation = camera.transform.rotation;
            savedOrthographic = camera.orthographic;
            savedCameraSize = camera.orthographicSize;
            savedFov = camera.fieldOfView;
            savedBackground = camera.backgroundColor;
            savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
            savedVsync = QualitySettings.vSyncCount;
            savedFps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            savedRenderScale = pipeline ? pipeline.renderScale : 1;
            if (pipeline)
                pipeline.renderScale = 1;
            camera.orthographic = false;
            camera.fieldOfView = 65;
            camera.backgroundColor = new Color(.33f, .66f, .87f);
            camera.transform.position = new Vector3(0, 17, -55);
            camera.transform.LookAt(new Vector3(0, 5, 25));
            benchmarkRoot = new GameObject("Six second graphics workload");
            Visuals.Box(benchmarkRoot.transform, new Vector3(0, -1, 20), new Vector3(180, 2, 180), new Color(.24f, .57f, .30f));
            var items = services.Content ? services.Content.Items : Array.Empty<ContentItem>();
            for (int i = 0; i < 180; i++)
            {
                if (items.Length > 0)
                {
                    var item = items[i % items.Length];
                    if (!item.Prefab)
                        continue;
                    var go = UnityEngine.Object.Instantiate(item.Prefab, benchmarkRoot.transform);
                    go.transform.localPosition = new Vector3((i % 18 - 9) * 8, 0, (i / 18) * 10);
                    go.transform.localScale = Vector3.one * (5 / Mathf.Max(.1f, item.Height));
                }
                else
                    Visuals.Sphere(benchmarkRoot.transform, new Vector3((i % 18 - 9) * 8, 3, (i / 18) * 10), 2, Style.Colors[i % Style.Colors.Length]);
            }
            readback = new Texture2D(1, 1, TextureFormat.RGB24, false);
        }
        void TickBenchmark()
        {
            if (!benchmarking || Event.current.type != EventType.Repaint)
                return;
            if (services.Now - benchmarkStart >= 6)
            {
                FinishBenchmark(false);
                return;
            }
            if (benchmarkRoot)
                benchmarkRoot.transform.rotation = Quaternion.Euler(0, (float)(services.Now - benchmarkStart) * 2, 0);
            readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            readback.Apply(false);
            double now = KeyboardGuard.Now;
            if (services.Now - benchmarkStart > .5)
                benchmarkFrames.Add((now - lastBenchmarkFrame) * 1000);
            lastBenchmarkFrame = now;
        }
        void FinishBenchmark(bool interrupted)
        {
            if (!benchmarking)
                return;
            benchmarking = false;
            if (benchmarkRoot)
                UnityEngine.Object.Destroy(benchmarkRoot);
            if (readback)
                UnityEngine.Object.Destroy(readback);
            var camera = services.Camera;
            camera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            camera.orthographic = savedOrthographic;
            camera.orthographicSize = savedCameraSize;
            camera.fieldOfView = savedFov;
            camera.backgroundColor = savedBackground;
            RenderSettings.fog = savedFog;
            QualitySettings.vSyncCount = savedVsync;
            Application.targetFrameRate = savedFps;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline)
                pipeline.renderScale = savedRenderScale;
            if (interrupted || benchmarkFrames.Count < 30)
            {
                benchmarkResult = "Test interrupted. Your graphics settings were restored.";
                return;
            }
            benchmarkFrames.Sort();
            double p95 = benchmarkFrames[(int)((benchmarkFrames.Count - 1) * .95)];
            recommendation = p95 < 9 ? "High" : p95 < 16 ? "Balanced" : "Performance";
            benchmarkResult = "95% of frames: " + p95.ToString("0.0") + " ms.\nSuggested: " + recommendation + ".\nYour settings are restored.";
        }
    }
}
