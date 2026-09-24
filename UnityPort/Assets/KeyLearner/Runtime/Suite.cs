using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using KeyLearner.Studio;
using PlayMode = KeyLearner.Studio.PlayMode;
using KeyLearner.Unity.Platform;

namespace KeyLearner.Unity
{
    // Adding a game is one factory registration plus its own content and presentation.
    public static class MinigameRegistry
    {
        public static Dictionary<PlayMode, Func<Minigame>> Create() => new Dictionary<PlayMode, Func<Minigame>> {
            {PlayMode.SmashGarden,()=>new CanvasGame(PlayMode.SmashGarden)},
            {PlayMode.WordAdventure,()=>new CanvasGame(PlayMode.WordAdventure)},
            {PlayMode.Counting,()=>new CanvasGame(PlayMode.Counting)},
            {PlayMode.BirdFlight,()=>new ExplorerGame(ExplorerKind.Bird)},
            {PlayMode.Racing,()=>new ExplorerGame(ExplorerKind.Racer)},
            {PlayMode.Dolphin,()=>new ExplorerGame(ExplorerKind.Dolphin)},
            {PlayMode.Subitizing,()=>new DotPopGame()},
            {PlayMode.HowManyNow,()=>new VisualMathGame(MathActivity.HowManyNow)},
            {PlayMode.WhatsHiding,()=>new VisualMathGame(MathActivity.Hiding)},
            {PlayMode.MakeNumber,()=>new VisualMathGame(MathActivity.MakeNumber)},
            {PlayMode.DotDuel,()=>new VisualMathGame(MathActivity.Duel)},
            {PlayMode.CannonHop,()=>new VisualMathGame(MathActivity.CannonHop)},
            {PlayMode.Dinosaur,()=>new DinosaurGame()}
        };
    }
    public sealed class Suite : MonoBehaviour
    {
        GameServices services;
        WindowsInputSession input;
        readonly PointerInputGate pointerInput = new PointerInputGate();
        Minigame game;
        Dictionary<PlayMode, Func<Minigame>> factories;
        bool picker = true, studio, ready;
        int filter, selected, page;
        string error = "";
        double hopNoticeUntil;
        readonly List<float> frames = new List<float>();
        readonly Dictionary<PlayMode, Texture2D> previews = new Dictionary<PlayMode, Texture2D>();
        readonly List<string> runtimeErrors = new List<string>();
        double captureAt, reminderUntil; float activeSeconds; bool captured;
        ParentStudio parent;
        SessionDiagnostics diagnostics;
        StartupIntroduction introduction;
        Texture2D brandIcon;
        bool introductionCaptured;
        double nextDiagnostic;
        long pickerKeyEvents, gameKeyEvents, parentKeyEvents, introKeyEvents, guiKeyEvents;
        string buildLabel;
        string BuildLabel => buildLabel ?? (buildLabel = "Version " + Application.version + "  /  Build " +
            (string.IsNullOrEmpty(Application.buildGUID) ? "Editor" : Application.buildGUID.Substring(0, Math.Min(12, Application.buildGUID.Length))));
        void DiagnosticState(string kind)
        {
            diagnostics?.Write(kind, new {
                elapsedSeconds = Time.realtimeSinceStartupAsDouble, frame = Time.frameCount,
                screen = new { width = Screen.width, height = Screen.height, fullscreen = Screen.fullScreen },
                view = error.Length > 0 ? "error" : introduction?.Active == true ? "splash" : studio ? "studio" : picker ? "picker" : "game",
                selected, page, filter, mode = game == null ? -1 : (int)services.Settings.Mode,
                pickerKeyEvents, gameKeyEvents, parentKeyEvents, introKeyEvents, guiKeyEvents,
                input = input?.DiagnosticSnapshot()
            });
        }
        void OpenDiagnosticsAndQuit()
        {
            input?.AuthorizeExit();
            DiagnosticState("open-folder-and-close");
            try
            {
                if (diagnostics != null && Directory.Exists(diagnostics.DirectoryPath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(diagnostics.DirectoryPath) { UseShellExecute = true });
            }
            catch (Exception e) { diagnostics?.Write("folder-open-failed", new { errorType = e.GetType().Name }); }
            Quit();
        }
        public GameServices Services => services;
        public Minigame CurrentGame => game;
        public bool Picking => picker;
        public ParentStudio PreviewParent
        {
            get
            {
                RequirePreview();
                return parent;
            }
        }
        void RequirePreview()
        {
            if (services == null || !services.Preview)
                throw new InvalidOperationException("Preview controls require an isolated preview session.");
        }
        public void PreviewPickerKey(int key)
        {
            RequirePreview();
            PickerKey(key);
        }
        public void PreviewStudio(bool show)
        {
            RequirePreview();
            if (show != studio)
            {
                if (show)
                    OpenStudio();
                else
                    CloseStudio();
            }
        }
        public void PreviewGameKey(int key)
        {
            RequirePreview();
            if (picker || studio)
                throw new InvalidOperationException("A gameplay view is required.");
            DispatchGameKey(new KeyEvent(key, true, services.Now));
        }
        void DispatchGameKey(KeyEvent e)
        {
            if (e.Down && e.Key == 27)
                reminderUntil = services.Now + 7;
            game?.Key(e);
        }
        public void PreviewFocusLoss()
        {
            RequirePreview();
            input.Suspend();
            ResetInteraction();
        }
        public bool PreviewInputActive {get{RequirePreview();return input!=null && input.Active;}}
        public string PreviewState
        {
            get
            {
                RequirePreview();
                return "picker=" + picker + " studio=" + studio + " filter=" + filter + " selected=" + selected + " page=" + page + " game=" + (game?.DiagnosticState ?? "none");
            }
        }
        void ResetInteraction()
        {
            ClearTransient();
            game?.Suspend();
            parent?.Suspend();
        }
        public static Suite Instance;
        void Start()
        {
            Instance = this;
            Application.logMessageReceived += RecordRuntimeError;
            try
            {
                if (WindowsCompatibility.TryRun(Environment.GetCommandLineArgs(), out var diagnosticExit))
                {
                    Application.Quit(diagnosticExit);
                    return;
                }
                var options = LaunchOptions.Parse(Environment.GetCommandLineArgs());
                diagnostics = new SessionDiagnostics(options.DataRoot);
                diagnostics.Write("session", new {
                    version = Application.version, buildGuid = Application.buildGUID, unity = Application.unityVersion, inputBackend = SessionDiagnostics.UnityInputBackend,
                    os = SystemInfo.operatingSystem, gpu = SystemInfo.graphicsDeviceName,
                    protectedPlay = !options.Unprotected, preview = options.Preview, studio = options.Studio,
                    remoteSession = System.Environment.GetEnvironmentVariable("SESSIONNAME")?.StartsWith("RDP-", StringComparison.OrdinalIgnoreCase) == true,
                    processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                    privacy = "Aggregate counts and state only; no key identities, text, profile contents, device identifiers or window titles."
                });
                introduction = options.Preview && !options.Has("--show-intro") ? null : new StartupIntroduction();
                Application.runInBackground = true;
                Application.targetFrameRate = 60;
                var camera = Camera.main;
                if (!camera)
                {
                    camera = new GameObject("Gameplay Camera").AddComponent<Camera>();
                    camera.tag = "MainCamera";
                }
                var contentRoot = Directory.GetParent(Application.dataPath);
                if (Application.isEditor)
                    contentRoot = contentRoot.Parent;
                var store = new Store(options.DataRoot, contentRoot.FullName, Path.Combine(Application.streamingAssetsPath,"Content","Voice"));
                if (options.Has("--mute"))
                    store.Settings.Sound = false;
                services = new GameServices { Store = store, Options = options, Camera = camera, PickerAction = OpenPicker, Content = Resources.Load<ContentLibrary>("ContentLibrary") };
                services.Audio = new UnityAudioService(gameObject, store.Root, store.SpeechPacks);
                services.Rewards = gameObject.AddComponent<RewardEffects>();
                services.CanvasCamera();
                input = new WindowsInputSession(options.Unprotected, store.Root);
                factories = MinigameRegistry.Create();
                parent = new ParentStudio(services, input, CloseStudio, Quit);
                services.ApplyGraphics();
                studio = options.Studio;
                if (options.Preview)
                {
                    int width = int.TryParse(options.Value("--width"), out int requestedWidth) ? Mathf.Clamp(requestedWidth, 480, 3840) : 1366;
                    int height = int.TryParse(options.Value("--height"), out int requestedHeight) ? Mathf.Clamp(requestedHeight, 480, 2160) : 768;
                    if (Screen.width != width || Screen.height != height || Screen.fullScreen)
                        Screen.SetResolution(width, height, FullScreenMode.Windowed);
                }
                QualitySettings.vSyncCount = store.Settings.VSync ? 1 : 0;
                ready = true;
                string requested = options.Value("--mode");
                if (int.TryParse(requested, out int id) && id >= 0 && id < GameCatalog.All.Length)
                    Select((PlayMode)id);
                else if (Enum.TryParse<PlayMode>(requested, true, out var mode))
                    Select(mode);
                if (float.TryParse(options.Value("--seconds"), out float sec))
                    captureAt = Mathf.Max(2, sec);
                PreviewSession.Attach(this);
                Debug.Log("KEYLEARNER_READY modes=" + factories.Count + " preview=" + options.Preview + " profile=" + store.Root + " graphics=" + SystemInfo.graphicsDeviceName);
            }
            catch (Exception e) { error = e.ToString(); Debug.LogException(e); diagnostics?.Write("startup-error", new { errorType = e.GetType().Name }); input?.Dispose(); }
        }
        double revealUntil;
        public void Select(PlayMode mode)
        {
            if (mode == PlayMode.CannonHop && !services.Store.MathLearning.HopUnlocked && !services.Preview)
            {
                hopNoticeUntil = Time.unscaledTimeAsDouble + 8;
                services.Audio.Stop();
                services.Audio.Say("First, play How Many Now. Practice joining and taking away.", services.Settings);
                return;
            }
            ClearTransient();
            game?.Exit();
            game = factories[mode]();
            services.Settings.Mode = mode;
            picker = false;
            studio = false;
            game.Enter(services);
            revealUntil=Time.realtimeSinceStartupAsDouble+.28;
            services.Store.Save();
            Debug.Log("MINIGAME_ENTER id=" + (int)mode + " name=" + GameCatalog.For(mode).Name);
        }
        public void OpenPicker()
        {
            ClearTransient();
            game?.Suspend();
            game?.Exit();
            game = null;
            picker = true;
            studio = false;
            services.CanvasCamera();
        }
        void OpenStudio()
        {
            ClearTransient();
            game?.Suspend();
            parent.Suspend();
            studio = !studio;
        }
        void CloseStudio()
        {
            parent.Suspend();
            studio = false;
            services.ApplyGraphics();
            if (services.Camera.orthographic)
                services.CanvasCamera(services.CanvasWidth, services.CanvasHeight);
            services.Store.Save();
        }
        void ClearTransient()
        {
            reminderUntil = 0;
            pointerInput.Reset();
            services?.Audio?.Stop();
            services?.Rewards?.Clear();
            services?.Feedback.Clear();
            if (services != null)
                services.Keys = default;
        }
        public void Quit()
        {
            input?.AuthorizeExit();
            DiagnosticState("quit");
            services?.Store?.Save();
            Application.Quit();
        }
        void Update()
        {
            if (!ready)
                return;
            try
            {
                services.Feedback.Restore();
                services.UpdateViewport();
                var frame = input.Poll(true);
                if (Time.realtimeSinceStartupAsDouble >= nextDiagnostic)
                {
                    DiagnosticState("heartbeat");
                    nextDiagnostic = Time.realtimeSinceStartupAsDouble + (Time.realtimeSinceStartupAsDouble < 60 ? 1 : 5);
                }
                if (frame.Reset)
                    ResetInteraction();
                services.Keys = frame.Snapshot;
                services.Rewards.Gentle = services.Settings.GentleMotion;
                services.Audio.Update(services.Settings);
                if (frame.ParentAction == ParentAction.Exit)
                {
                    Quit();
                    return;
                }
                if (introduction?.Active == true)
                {
                    introKeyEvents += frame.Events.Count;
                    services.Keys = default;
                    if (services.Preview && !introductionCaptured && introduction.Progress(Time.realtimeSinceStartupAsDouble) >= .35 && services.Options.Value("--intro-screenshot").Length > 0)
                    {
                        introductionCaptured = true;
                        StartCoroutine(CaptureIntroduction());
                    }
                    introduction.Tick(Time.realtimeSinceStartupAsDouble);
                    if (!introduction.Active)
                    {
                        input.Suspend(); // Intro keystrokes must never leak into picker/gameplay.
                        ResetInteraction();
                        DiagnosticState("splash-finished");
                    }
                    return;
                }
                if (frame.ParentAction == ParentAction.Options)
                    OpenStudio();
                if (frame.OpenPicker)
                    OpenPicker();
                // Unity animations/particles share the same suspension as the explicit domain tick.
                // Input, parent controls and verification use unscaled time.
                Time.timeScale=frame.Active && !studio && !picker ? 1 : 0;
                if (frame.Active)
                {
                    services.AdvanceClock(Mathf.Min(Time.unscaledDeltaTime, .1f));
                    foreach (var e in frame.Events)
                    {
                        if (e.Key < 0)
                        {
                            ClearTransient();
                            game?.Suspend();
                            continue;
                        }
                        if (studio)
                        {
                            parentKeyEvents++;
                            parent.Key(e);
                            continue;
                        }
                        if (picker)
                        {
                            pickerKeyEvents++;
                            if (e.Down)
                                PickerKey(e.Key);
                            continue;
                        }
                        gameKeyEvents++;
                        DispatchGameKey(new KeyEvent(e.Key, e.Down, services.Now));
                    }
                    if (!picker && !studio)
                    {
                        game?.Tick(Mathf.Min(Time.unscaledDeltaTime, .1f));
                        services.Feedback.Apply(services.Camera, Mathf.Min(Time.unscaledDeltaTime, .1f), services.Settings.GentleMotion);
                        if (pointerInput.TryGet(out var pointer, out bool right))
                        {
                            var p = services.PointerFromScreen(pointer);
                            game?.Pointer(p, right);
                        }
                    }
                }
                if (frame.Active)
                {
                    activeSeconds += Mathf.Min(Time.unscaledDeltaTime, .1f);
                    if (activeSeconds > 2 && frames.Count < 36000)
                        frames.Add(Time.unscaledDeltaTime * 1000);
                }
                if (services.Preview && captureAt > 0 && activeSeconds >= captureAt && !captured)
                    StartCoroutine(Capture());
            }
            catch (Exception e) { error = e.ToString(); Debug.LogException(e); diagnostics?.Write("runtime-error", new { errorType = e.GetType().Name }); ready = false; input?.Dispose(); }
        }
        System.Collections.IEnumerator Capture()
        {
            captured = true;
            yield return new WaitForEndOfFrame();
            string path = services.Options.Value("--screenshot");
            if (path.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                ScreenCapture.CaptureScreenshot(path);
            }
            frames.Sort();
            var report = new
            {
                mode = (int)services.Settings.Mode,
                picker,
                game = game?.DiagnosticState,
                audio = new { services.Audio.Requested, services.Audio.Started, services.Audio.PreparedStarted, services.Audio.FallbackStarted, services.Audio.CachedSpeechClips, services.Audio.MusicFrames,
                    PlayingSources = UnityEngine.Object.FindObjectsByType<AudioSource>().Count(a => a.isPlaying && a.volume > .001f),
                    SoundEnabled = services.Settings.Sound },
                frameCount = frames.Count,
                activeSeconds,
                p95Milliseconds = frames.Count == 0 ? 0 : frames[(int)(frames.Count * .95)],
                width = Screen.width,
                height = Screen.height,
                gpu = SystemInfo.graphicsDeviceName,
                error,
                runtimeErrors = runtimeErrors.ToArray()
            };
            if (path.Length > 0)
                File.WriteAllText(path + ".json", System.Text.Json.JsonSerializer.Serialize(report));
            Debug.Log("PREVIEW_CAPTURE " + game?.DiagnosticState);
            yield return null;
            yield return null;
            Quit();
        }
        GameDefinition[] VisibleGames() => GameCatalog.All.Where(g => filter == 0 || (filter == 1 ? g.Type == "Explore" : g.Topics.Contains(filter == 2 ? "Letters" : "Numbers"))).ToArray();
        void PickerKey(int key)
        {
            var all = VisibleGames();
            if (key == 9)
            {
                filter = (filter + 1) % 4;
                selected = page = 0;
                return;
            }
            if (key == 37)
                selected--;
            if (key == 39)
                selected++;
            if (key == 38)
                selected -= 3;
            if (key == 40)
                selected += 3;
            selected = (selected + all.Length) % all.Length;
            page = selected / 6;
            if (key == 13)
                Select(all[selected].Mode);
        }
        void OnGUI()
        {
            if (Application.isFocused && (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp)) guiKeyEvents++;
            if (introduction?.Active == true) Ui.Panel(new Rect(0, 0, Screen.width, Screen.height), Style.Navy, 0);
            Ui.Begin();
            if (error.Length > 0)
            {
                Ui.Panel(new Rect(100, 100, 1240, 700), Style.Navy);
                Ui.Label(new Rect(130, 120, 1180, 540), error, 23, Color.white);
                Ui.Button(new Rect(450, 690, 540, 55), "Open diagnostics & close", OpenDiagnosticsAndQuit);
                Ui.Button(new Rect(550, 760, 340, 70), "Close", Quit);
                Ui.End();
                return;
            }
            if (!ready)
            {
                Ui.End();
                return;
            }
            if (introduction?.Active == true)
                DrawIntroduction();
            else if (studio)
                parent.Draw();
            else if (picker)
                DrawPicker();
            else
                game?.DrawUI();
            if(!studio && introduction?.Active!=true && Time.realtimeSinceStartupAsDouble<revealUntil)
                Ui.Panel(new Rect(0,0,1440,900),new Color(.025f,.045f,.08f,(float)((revealUntil-Time.realtimeSinceStartupAsDouble)/.28)),0);
            if (!picker && !studio && services.Now < reminderUntil)
            {
                Ui.Panel(new Rect(80, 808, 1280, 74), Style.Panel);
                Ui.Label(new Rect(98, 811, 1244, 66), "Parents: hold Ctrl + Alt + O (or Ctrl + Shift + O) for 2 seconds.\nHold Ctrl + Alt + Escape to close. Ten O / Escape taps also work. G, G opens games.", 19, Color.white);
            }
            Ui.End();
        }
        System.Collections.IEnumerator CaptureIntroduction()
        {
            yield return new WaitForEndOfFrame();
            string path = Path.GetFullPath(services.Options.Value("--intro-screenshot"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            File.WriteAllText(path + ".json", System.Text.Json.JsonSerializer.Serialize(new {
                version = Application.version, buildGuid = Application.buildGUID,
                unitySplashFinished = UnityEngine.Rendering.SplashScreen.isFinished,
                introductionActive = introduction.Active, progress = introduction.Progress(Time.realtimeSinceStartupAsDouble)
            }));
        }
        void DrawIntroduction()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (Event.current.type == EventType.Repaint) introduction.Painted(now, UnityEngine.Rendering.SplashScreen.isFinished);
            if (!brandIcon) brandIcon = Resources.Load<Texture2D>("Branding/window-icon");
            Ui.Panel(new Rect(564, 111, 312, 312), new Color(.035f, .07f, .14f), 36);
            if (brandIcon) GUI.DrawTexture(new Rect(584, 125, 272, 272), brandIcon, ScaleMode.ScaleToFit);
            Ui.Label(new Rect(200, 422, 1040, 100), "KeyLearner", 76, Color.white);
            Ui.Label(new Rect(200, 522, 1040, 52), "A little world of discovery", 30, new Color(.65f, .79f, .94f));
            Ui.Label(new Rect(200, 599, 1040, 44), BuildLabel, 25, new Color(.92f, .96f, 1));
            Ui.Panel(new Rect(500, 696, 440, 7), Style.Panel, 3);
            Ui.Panel(new Rect(500, 696, Mathf.Max(1, 440 * (float)introduction.Progress(now)), 7), Style.Dots, 3);
            Ui.Label(new Rect(280, 729, 880, 42), "Your next discovery is nearly ready", 22, new Color(.57f, .7f, .84f));
        }
        void DrawPicker()
        {
            Ui.Label(new Rect(80, 35, 1150, 62), "A little world of discovery", 46, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(82, 97, 1060, 38), "Choose something wonderful to play", 23, new Color(.59f, .7f, .83f), TextAnchor.MiddleLeft);
            string[] filters = { "All games", "Explore", "Letters", "Numbers" };
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Ui.Button(new Rect(80 + i * 195, 151, 180, 52), filters[i], () => { filter = n; selected = page = 0; });
                if (filter == i)
                    Ui.Panel(new Rect(88 + i * 195, 209, 164, 4), Style.Dots, 2);
            }
            var all = VisibleGames();
            int pages = Mathf.CeilToInt(all.Length / 6f);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int j = 0; j < 6; j++)
            {
                int index = page * 6 + j;
                if (index >= all.Length)
                    break;
                var g = all[index];
                bool locked = g.Mode == PlayMode.CannonHop && !services.Store.MathLearning.HopUnlocked;
                Rect r = new Rect(80 + j % 3 * 432, 240 + j / 3 * 258, 414, 238);
                bool hover = r.Contains(Ui.Pointer);
                Color accent = Style.Colors[(int)g.Mode % Style.Colors.Length];
                Ui.Panel(new Rect(r.x, r.y + 7, r.width, r.height), new Color(0, 0, 0, .25f));
                Ui.Panel(r, hover || selected == index ? new Color(.10f, .17f, .27f) : Style.Panel);
                if (!previews.TryGetValue(g.Mode, out var preview))
                {
                    preview = Resources.Load<Texture2D>("Previews/mode-" + ((int)g.Mode).ToString("D2"));
                    previews[g.Mode] = preview;
                }
                Rect picture = new Rect(r.x + 18, r.y + 18, 146, 100);
                Ui.Panel(picture, accent, 9);
                if (preview)
                    GUI.DrawTexture(picture, preview, ScaleMode.ScaleAndCrop);
                else
                    Ui.Label(picture, (int)g.Mode >= 6 ? "• •" : "ABC", 33, Style.Navy);
                Ui.Label(new Rect(r.x + 180, r.y + 15, 217, 68), g.Name, 27, Color.white, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(r.x + 181, r.y + 84, 218, 30), "AGES " + g.MinimumAge + "+  /  " + g.Type.ToUpperInvariant(), 14, accent, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(r.x + 24, r.y + 127, r.width - 48, 55), locked ? "Complete joining and taking away to unlock" : g.Description, 21, new Color(.72f, .80f, .9f), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(r.x + 24, r.y + 185, r.width - 48, 30), locked ? "Keep exploring numbers" : "LET'S PLAY  →", 16, accent, TextAnchor.MiddleLeft);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    Select(g.Mode);
            }
            if (Time.unscaledTimeAsDouble < hopNoticeUntil)
            {
                Ui.Panel(new Rect(160, 625, 1120, 140), Style.Navy);
                var progress = services.Store.MathLearning;
                Ui.Label(new Rect(180, 638, 760, 100), "Cannon Hop opens after How Many Now practice.\nJoining: " + Mathf.Min(3, progress.JoiningCompleted) + "/3    Taking away: " + Mathf.Min(3, progress.SeparatingCompleted) + "/3", 25, Color.white);
                Ui.Button(new Rect(968, 666, 280, 65), "Start practice", () => Select(PlayMode.HowManyNow));
            }
            Ui.Label(new Rect(80, 797, 1010, 48), "Arrow keys + Enter to choose   ·   Tab to explore categories   ·   G, G to return", 19, new Color(.50f, .62f, .77f), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(80, 854, 790, 36), BuildLabel, 17, new Color(.56f, .68f, .82f), TextAnchor.MiddleLeft);
            Ui.Button(new Rect(1000, 858, 360, 34), "Open diagnostics & close", OpenDiagnosticsAndQuit);
            if (pages > 1)
                Ui.Button(new Rect(1130, 798, 230, 54), page == 0 ? "More games →" : "← First games", () => { page = (page + 1) % pages; selected = page * 6; });
        }
        void OnApplicationFocus(bool focus)
        {
            diagnostics?.Write("unity-focus", new { focus });
            if (!focus)
            {
                input?.Suspend();
                ResetInteraction();
            }
        }
        void OnApplicationPause(bool pause)
        {
            diagnostics?.Write("unity-pause", new { pause });
            if (pause)
            {
                input?.Suspend();
                ResetInteraction();
            }
        }
        void RecordRuntimeError(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && runtimeErrors.Count < 20)
                runtimeErrors.Add(message);
        }
        void OnDestroy()
        {
            Time.timeScale=1;
            Application.logMessageReceived -= RecordRuntimeError;
            ClearTransient();
            services?.Audio?.Dispose();
            input?.Dispose();
            diagnostics?.Write("disposed", new { normalCleanup = true });
            diagnostics?.Dispose();
        }
    }
}
