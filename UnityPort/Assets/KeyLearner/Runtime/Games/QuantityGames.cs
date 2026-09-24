using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using KeyLearner.Studio;
using NVector2 = System.Numerics.Vector2;

namespace KeyLearner.Unity
{
    // The shared domain owns every answer, timer and progression decision. This
    // presentation layer owns lit spheres, layout, phase narration and pointer edges.
    public abstract class QuantityMinigame : Minigame
    {
        protected struct Token
        {
            public Vector2 Position;
            public float Radius;
            public int Index;
            public Token(Vector2 position, float radius, int index)
            {
                Position = position;
                Radius = radius;
                Index = index;
            }
        }
        readonly List<GameObject> spherePool = new List<GameObject>();
        protected readonly List<Token> Tokens = new List<Token>();
        protected double LastTap = -10;
        readonly HashSet<int> painted = new HashSet<int>();
        readonly Dictionary<int,double> touches = new Dictionary<int,double>();
        Material pearl;
        protected virtual bool BonusRound => false;
        protected void ResetPaint() { painted.Clear(); touches.Clear(); }
        protected int PaintedCount => painted.Count;
        protected void TouchToken(int index)=>touches[index]=S.Now;
        protected object[] TokenTargets => Tokens.Select(t => PointerTarget(t.Index, new DotRect(t.Position.x-t.Radius,t.Position.y-t.Radius,t.Radius*2,t.Radius*2))).ToArray();
        protected bool PaintToken(Vector2 point)
        {
            foreach (var token in Tokens)
                if ((token.Position - point).sqrMagnitude <= token.Radius * token.Radius)
                {
                    if (!painted.Add(token.Index)) painted.Remove(token.Index);
                    touches[token.Index]=S.Now;
                    S.Audio.Play("ball-touch", S.Settings, .22f, .12);
                    return true;
                }
            return false;
        }
        string lastPointerReport = "";
        protected virtual Color Ink => S.Settings.Theme == Mood.BlackAndWhite ? Color.white : Style.Dots;
        protected static Vector2 V(NVector2 p) => new Vector2(p.X, p.Y);
        protected static Rect R(DotRect r) => new Rect(r.X, r.Y, r.Width, r.Height);
        protected static readonly string[] Numbers = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
        protected void SyncSpheres(Vector2 shake)
        {
            for (int i = 0; i < Tokens.Count; i++)
            {
                if (i >= spherePool.Count)
                    spherePool.Add(Visuals.Sphere(Root.transform, Vector3.zero, 1, Ink));
                var go = spherePool[i];
                var token = Tokens[i];
                go.SetActive(true);
                go.transform.localPosition = new Vector3(token.Position.x + shake.x, -token.Position.y - shake.y, 0);
                float pulse=touches.TryGetValue(token.Index,out double touched)?Mathf.Clamp01(1-(float)(S.Now-touched)/.35f):0;
                pulse=Mathf.Sin(pulse*Mathf.PI)*pulse*(S.Settings.GentleMotion?.035f:.10f);
                go.transform.localScale = new Vector3(1+pulse,1-pulse,1)*(token.Radius*2);
                Color tint = painted.Contains(token.Index) ? ThemeColors.At(S.Settings.Theme, 3) : Ink;
                var renderer = go.GetComponent<Renderer>();
                if (BonusRound)
                {
                    if (!pearl) pearl = new Material(Resources.Load<Shader>("Shaders/Pearl"));
                    renderer.sharedMaterial = pearl;
                    var properties = new MaterialPropertyBlock();
                    properties.SetColor("_BaseColor", painted.Contains(token.Index) ? tint : new Color(.91f, .95f, 1));
                    renderer.SetPropertyBlock(properties);
                }
                else
                {
                    if (renderer.HasPropertyBlock()) renderer.SetPropertyBlock(null);
                    renderer.sharedMaterial = Visuals.Material(tint);
                }
            }
            for (int i = Tokens.Count; i < spherePool.Count; i++)
                spherePool[i].SetActive(false);
        }
        protected bool AcceptTap(bool right)
        {
            if (right || Input.touchCount > 1 || S.Now - LastTap < .17)
                return false;
            LastTap = S.Now;
            return true;
        }
        protected void Speak(string text, bool brisk = false)
        {
            if (string.IsNullOrEmpty(text))
                return;
            S.Audio.StopSpeech();
            S.Audio.Say(text, S.Settings, brisk: brisk);
        }
        protected void ToggleMute()
        {
            S.Settings.Sound = !S.Settings.Sound;
            if (!S.Settings.Sound)
                S.Audio.Stop();
            S.Store.Save();
        }
        protected void Button(DotRect rect, string text, bool enabled, bool selected = false, int size = 46)
        {
            Rect r = R(rect);
            bool hover = enabled && r.Contains(Ui.Pointer);
            Ui.Panel(new Rect(r.x, r.y + 5, r.width, r.height), new Color(0, 0, 0, .28f));
            if(hover && Input.GetMouseButton(0))r.y+=3;
            Ui.Panel(r, selected ? Ink : hover ? new Color(.13f, .22f, .34f) : enabled ? new Color(.095f, .15f, .25f) : new Color(.06f, .085f, .14f));
            if (selected)
                Ui.Panel(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), Style.Panel);
            if (!string.IsNullOrEmpty(text))
                Ui.Label(r, text, size, enabled || selected ? Color.white : new Color(.39f, .45f, .55f));
        }
        protected static void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            var previous = GUI.matrix;
            var tint = GUI.color;
            var delta = b - a;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.color = color;
            GUI.DrawTexture(new Rect(0, -width * .5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = previous;
            GUI.color = tint;
        }
        protected static void Ring(Vector2 p, float radius, float width, Color color)
        {
            for (int i = 0; i < 56; i++)
            {
                float a = i * Mathf.PI * 2 / 56, b = (i + 1) * Mathf.PI * 2 / 56;
                Line(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, p + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
            }
        }
        protected void PreviewTap(Vector2 logical)
        {
            Pointer(S.PointerFromScreen(S.ScreenFromLogical(logical)), false);
        }
        protected object PointerTarget(int value, DotRect rect)
        {
            var p = S.ScreenFromLogical(V(rect.Center));
            return new
            {
                Value = value,
                Left = rect.X,
                Top = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                X = (int)Mathf.Round(p.x),
                Y = (int)Mathf.Round(Screen.height - p.y),
                LogicalX = rect.Center.X,
                LogicalY = rect.Center.Y
            };
        }
        protected void PointerReport(string signature, object state)
        {
            if (!S.Preview || signature == lastPointerReport)
                return;
            string scenario = Argument("--scenario");
            if (scenario != "math-pointer" && scenario != "dots-pointer" && scenario != "dots-bonus")
                return;
            string requested = Argument("--interaction-report");
            if (string.IsNullOrEmpty(requested))
            {
                string screenshot = Argument("--screenshot");
                if (string.IsNullOrEmpty(screenshot))
                    return;
                requested = screenshot + ".state.json";
            }
            try
            {
                var path = Path.GetFullPath(requested);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", System.Text.Json.JsonSerializer.Serialize(state));
                if (File.Exists(path))
                    File.Replace(path + ".tmp", path, null);
                else
                    File.Move(path + ".tmp", path);
                lastPointerReport = signature;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // A preview reader can briefly hold the destination without delete
                // sharing. Preserve gameplay and retry the current state next tick.
            }
        }
        protected static string Argument(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
        protected static int? IntegerArgument(string flag)
        {
            return int.TryParse(Argument(flag), out int number) ? (int?)number : null;
        }
        public override void Exit()
        {
            S.Audio.Stop();
            if (pearl) UnityEngine.Object.Destroy(pearl);
            base.Exit();
        }
    }

    public sealed class DotPopGame : QuantityMinigame
    {
        DotRect TouchMute => S.TouchPlay ? new DotRect(36,24,112,94) : DotLayout.Mute;
        SubitizingGame model;
        protected override bool BonusRound => model != null && model.Bonus.Active;
        int previewActions;
        string scenario;
        public override void ResetActivity() => S.Session.Remove("dots");
        public override void Enter(GameServices services)
        {
            base.Enter(services);
            S.CanvasCamera(DotLayout.Width, DotLayout.Height);
            if (S.Session.TryGetValue("dots", out var session))
                model = (SubitizingGame)session;
            else
            {
                model = new SubitizingGame(S.Preview ? 701 : Environment.TickCount);
                S.Session["dots"] = model;
            }
            scenario = S.Preview ? Argument("--scenario") : "";
            if (scenario == "dots-bonus")
            {
                for (int round = 0; round < 5; round++)
                {
                    for (int frame = 0; frame < 100 && !model.CanAnswer; frame++) model.Step(.05);
                    model.Answer(model.Quantity);
                    model.RestartRound();
                }
            }
            Refresh();
        }
        public override void Tick(float dt)
        {
            var before = model.Phase;
            int popped = model.Popped;
            model.Step(dt);
            S.Audio.SetMusic(BonusRound && model.Phase != DotPhase.Reward ? "happy-bonus" : "learning-home", BonusRound ? .16f : .04f);
            if (before != model.Phase && model.Phase == DotPhase.Ready)
                ResetPaint();
            if (before != model.Phase && model.Phase == DotPhase.Go && BonusRound)
                S.Audio.Play("bonus-round", S.Settings, .4f);
            if (before != model.Phase && model.Phase == DotPhase.Answer)
                Speak("How many?");
            if (model.Phase == DotPhase.Reward && model.Popped > popped)
            {
                var cells = DotPatterns.Cells(model.Mask).ToArray();
                for (int i = popped; i < model.Popped; i++)
                {
                    S.Burst(V(DotLayout.Cell(cells[i])), Ink, 24);
                    S.Audio.Play("pop", S.Settings, .75f, .035);
                }
            }
            if (S.Preview && model.CanAnswer)
            {
                if (scenario == "dots-correct" && previewActions == 0)
                {
                    PreviewTap(V(DotLayout.Number(model.Quantity).Center));
                    previewActions++;
                }
                if (scenario == "dots-retry" && previewActions == 0)
                {
                    PreviewTap(V(DotLayout.Number((model.Quantity + 1) % 10).Center));
                    previewActions++;
                }
                else if (scenario == "dots-retry" && previewActions == 1 && model.PhaseTime > .48)
                {
                    PreviewTap(V(DotLayout.Number(model.Quantity).Center));
                    previewActions++;
                }
            }
            PointerReport(model.Phase + "/" + model.Stage + "/" + model.CanAnswer + "/" + model.WrongAttempts + "/" + PaintedCount + "/" + Screen.width + "/" + Screen.height,
                new
                {
                    Phase = model.Phase.ToString(),
                    model.Stage,
                    model.CanAnswer,
                    PaintedCount,
                    Balls = TokenTargets,
                    Bonus = model.Bonus.Active,
                    Points = model.Bonus.Score,
                    Answer = model.Quantity,
                    Choices = Enumerable.Range(0, 10).ToArray(),
                    Targets = Enumerable.Range(0, 10).Select(n => PointerTarget(n, DotLayout.Number(n))).ToArray(),
                    Mute = PointerTarget(-1, TouchMute),
                    LayoutWidth = 720,
                    LayoutHeight = 1080,
                    Width = Screen.width,
                    Height = Screen.height,
                    Correct = model.Phase == DotPhase.Reward,
                    model.WrongAttempts,
                    model.Mastery
                });
            Refresh();
        }
        void Refresh()
        {
            Tokens.Clear();
            var cells = DotPatterns.Cells(model.Mask).ToArray();
            for (int i = 0; i < cells.Length; i++)
                if (model.DotsVisible || model.Phase == DotPhase.Reward && i >= model.Popped)
                    Tokens.Add(new Token(V(DotLayout.Cell(cells[i])), DotLayout.Radius, i));
            var shake = S.Settings.GentleMotion ? Vector2.zero : new Vector2(Mathf.Sin((float)model.PhaseTime * 90) * 3 * (float)model.WrongShake, 0);
            SyncSpheres(shake);
        }
        void Submit(int number)
        {
            if (!model.Answer(number))
                return;
            S.Audio.Stop();
            S.Audio.Play(BonusRound ? "bonus-win" : "cannon", S.Settings, BonusRound ? .5f : .7f);
            if (model.Quantity == 0)
            {
                S.Audio.Play("powerup", S.Settings, .5f);
                S.Burst(new Vector2(360, 490), Ink, 40);
            }
        }
        public override void Pointer(Vector2 logical, bool right)
        {
            if (!AcceptTap(right))
                return;

            var point = new NVector2(logical.x, logical.y);
            if (TouchMute.Contains(point))
            {
                ToggleMute();
                return;
            }
            if (model.DotsVisible && PaintToken(logical))
            {
                Refresh();
                return;
            }
            int n = DotLayout.HitNumber(point);
            if (n >= 0)
                Submit(n);
            Refresh();
        }
        public override void Suspend()
        {
            model.RestartRound();
            S.Audio.Stop();
            LastTap = S.Now;
            Refresh();
        }
        public override string DiagnosticState => "DotPop phase=" + model.Phase + " stage=" + model.Stage + " mastery=" + model.Mastery + " quantity=" + model.Quantity + " mask=" + model.Mask + " errors=" + model.WrongAttempts + " bonus=" + BonusRound + " points=" + model.Bonus.Score + " painted=" + PaintedCount;
        public override void DrawUI()
        {
            Ui.End();
            Ui.Begin(720, 1080);
            Button(TouchMute, S.Settings.Sound ? "MUTE" : "UNMUTE", true, false, 23);
            Ui.Label(new Rect(570, 26, 110, 64), model.Stage.ToString(), 38, Color.white);
            string cue = model.Phase == DotPhase.Ready ? "READY" : model.Phase == DotPhase.Set ? "SET" : model.Phase == DotPhase.Go ? "GO" : model.Phase == DotPhase.Answer ? "HOW MANY?" : model.Phase == DotPhase.Reward ? model.Quantity.ToString() : "";
            Ui.Label(new Rect(30, 126, 660, 105), cue, model.Phase == DotPhase.Reward ? 74 : 55, model.Phase == DotPhase.Go || model.Phase == DotPhase.Reward ? Ink : Color.white);
            Ui.Label(new Rect(175, 42, 360, 48), (BonusRound ? "BONUS ×3   " : "") + model.Bonus.Score + " points", 25, Ink);
            for (int n = 0; n < 10; n++)
                Button(DotLayout.Number(n), n.ToString(), model.CanAnswer, false, 58);
            if (model.Phase == DotPhase.Reward)
            {
                var cells = DotPatterns.Cells(model.Mask).ToArray();
                for (int i = 0; i < cells.Length; i++)
                {
                    Vector2 p = V(DotLayout.Cell(cells[i]));
                    float flight = (float)(model.PhaseTime - SubitizingGame.LaunchAt(i));
                    if (flight >= 0 && flight < .24f)
                    {
                        Vector2 origin = new Vector2(360, 774), tip = Vector2.Lerp(origin, p, flight / .24f);
                        Line(Vector2.Lerp(origin, p, Mathf.Max(0, flight / .24f - .23f)), tip, 8, Ink);
                        Ui.Ball(tip, 8, Color.white);
                    }
                    float age = (float)(model.PhaseTime - SubitizingGame.ImpactAt(i));
                    if (age >= 0 && age < .5f)
                        Ring(p, 50 + age * 160, 5 * (1 - age / .5f), new Color(Ink.r, Ink.g, Ink.b, 1 - age / .5f));
                }
                if (model.PhaseTime < .18)
                    Ring(new Vector2(360, 774), 20 + (float)model.PhaseTime * 300, 5, Ink);
            }
            Ui.End();
            Ui.Begin();
        }
    }

    public sealed class VisualMathGame : QuantityMinigame
    {
        DotRect TouchMathMute => S.TouchPlay ? new DotRect(208,26,150,90) : MathLayout.Mute;
        DotRect TouchMathBack => S.TouchPlay ? new DotRect(36,26,150,90) : MathLayout.Back;
        readonly MathActivity activity;
        protected override Color Ink => ThemeColors.At(S.Settings.Theme, 1);
        MathGame model;
        int hopSelection,lastHopLanding;
        bool wasHopping;
        float hopPulse,recoil;
        Vector2 hopLanding;
        GameObject cannon;
        protected override bool BonusRound => model != null && model.Bonus.Active;
        MathPhase? spokenPhase;
        int countSpoken = -1, savedRevision = -1, previewActions, previewInitialStage;
        string debugState, scenario;
        bool debugApplied;
        public VisualMathGame(MathActivity activity)
        {
            this.activity = activity;
        }
        public override void ResetActivity() => S.Session.Remove("math-" + activity);
        public override void Enter(GameServices services)
        {
            base.Enter(services);
            S.CanvasCamera();
            bool? minus = null;
            string operation = Argument("--math-operation");
            if (operation == "subtract")
                minus = true;
            else if (operation == "add")
                minus = false;
            string sessionKey = "math-" + activity;
            if (S.Session.TryGetValue(sessionKey, out var session))
                model = (MathGame)session;
            else
                model = new MathGame(activity, S.Store.MathLearning, S.Preview ? 701 : Environment.TickCount,
                S.Preview ? IntegerArgument("--math-level") : null, S.Preview ? IntegerArgument("--math-a") : null, S.Preview ? IntegerArgument("--math-b") : null, S.Preview ? minus : null);
            S.Session[sessionKey] = model;
            S.Store.MathLearning.LastActivity = activity;
            debugState = S.Preview ? Argument("--math-state") : "";
            scenario = S.Preview ? Argument("--scenario") : "";
            previewInitialStage = model.Stage;
            if(activity==MathActivity.CannonHop)cannon=S.Content.Spawn("cannon",0,Root.transform,Vector3.zero,70,90);
            SpeakPhase();
            Refresh();
        }
        string Question
        {
            get
            {
                var r = model.Round;
                switch (activity)
                {
                    case MathActivity.Hiding:
                        return "How many are hiding?";
                    case MathActivity.MakeNumber:
                        return "Make " + Numbers[r.B];
                    case MathActivity.Duel:
                        return r.Fewer ? "Which has fewer?" : "Which has more?";
                    case MathActivity.CannonHop:
                        return (r.Subtract ? "Take away " : "") + Numbers[r.B] + (r.Subtract ? "" : " more") + ". Where will it land?";
                    default:
                        return "How many now?";
                }
            }
        }
        public override void Tick(float dt)
        {
            int popped = model.Popped;
            model.Step(dt, S.Audio.Pending > 0);
            if(activity==MathActivity.CannonHop)
            {
                hopPulse=Mathf.Max(0,hopPulse-dt);recoil*=Mathf.Exp(-dt*12);
                var round=model.Round;bool hopping=model.Counting && round.B>0;
                if(hopping && !wasHopping){lastHopLanding=0;recoil=1;S.Audio.Play("cannon",S.Settings,.3f);}
                int landed=hopping?Mathf.Clamp(Mathf.FloorToInt((float)model.Motion*round.B+.0001f),0,round.B):wasHopping?round.B:lastHopLanding;
                if(landed>lastHopLanding)
                {
                    lastHopLanding=landed;hopPulse=.35f;
                    hopLanding=V(MathLayout.HopPoint(round.A+(round.Subtract?-landed:landed)));
                    TouchToken(0);S.Audio.Play("ball-touch",S.Settings,.22f,.06);
                }
                wasHopping=hopping;
                if(cannon)
                {
                    var point=V(MathLayout.HopPoint(round.A));float direction=round.Subtract?-1:1;
                    cannon.SetActive(model.Phase!=MathPhase.Reward);
                    cannon.transform.localPosition=new Vector3(point.x-direction*(38+recoil*8),-453,42);
                    cannon.transform.localRotation=Quaternion.Euler(0,direction*90,-direction*recoil*3);
                }
            }
            S.Audio.SetMusic(BonusRound && model.Phase != MathPhase.Reward ? "happy-bonus" : "learning-home", BonusRound ? .16f : .04f);
            SpeakPhase();
            if (model.Counting && (model.CountQuantity == 0 ? countSpoken != -2 : model.CountIndex != countSpoken))
            {
                countSpoken = model.CountQuantity == 0 ? -2 : model.CountIndex;
                int n = model.CountQuantity == 0 ? 0 : countSpoken + 1;
                if (activity == MathActivity.Duel && n > model.Round.A)
                    n -= model.Round.A;
                Speak(Numbers[Mathf.Clamp(n, 0, 10)]);
            }
            if (model.Phase == MathPhase.Reward)
            {
                if (model.Time >= .17 && model.Time - dt < .17)
                    S.Audio.Play("cannon", S.Settings, .65f);
                var points = RewardPoints();
                for (int i = popped; i < model.Popped && i < points.Count; i++)
                {
                    S.Burst(points[i], Ink, 22);
                    S.Audio.Play("pop", S.Settings, .55f, .03);
                }
                if (model.RewardCount == 0 && model.Time >= .48 && model.Time - dt < .48)
                {
                    S.Burst(new Vector2(720, 454), Ink, 32);
                    S.Audio.Play("powerup", S.Settings, .45f);
                }
            }
            if (model.Revision != savedRevision)
            {
                savedRevision = model.Revision;
                if (!S.Preview)
                    S.Store.Save();
            }
            if (S.Preview && model.CanAnswer)
            {
                if (!debugApplied && !string.IsNullOrEmpty(debugState))
                {
                    model.DebugState(debugState);
                    debugApplied = true;
                    spokenPhase = null;
                    SpeakPhase();
                }
                if (scenario == "math-complete" && previewActions == 0 && S.Now - LastTap > .18)
                {
                    if (model.Stage > previewInitialStage)
                        previewActions = 1;
                    else if (activity == MathActivity.MakeNumber)
                    {
                        for (int i = 0; i < model.Difficulty.Capacity; i++)
                            if ((model.BuiltMask & (1 << i)) == 0)
                            {
                                PreviewTap(V(MathLayout.CellButton(i, model.Difficulty.Capacity).Center));
                                break;
                            }
                    }
                    else
                    {
                        var answer = activity == MathActivity.Duel ? (model.Round.Answer == 2 ? MathLayout.Same : MathLayout.Group(model.Round.Answer)) : activity == MathActivity.CannonHop ? MathLayout.Track(model.Round.Answer) : MathLayout.Choice(Array.IndexOf(model.Round.Choices, model.Round.Answer), model.Round.Choices.Length);
                        PreviewTap(V(answer.Center));
                        previewActions = 1;
                    }
                }
            }
            if (scenario == "math-pointer")
            {
                object[] targets = activity == MathActivity.MakeNumber ? Enumerable.Range(0, model.Difficulty.Capacity).Select(i => PointerTarget(i, MathLayout.CellButton(i, model.Difficulty.Capacity))).ToArray() :
                    activity == MathActivity.Duel ? new[] { PointerTarget(0, MathLayout.Group(0)), PointerTarget(1, MathLayout.Group(1)), PointerTarget(2, MathLayout.Same) } :
                    activity == MathActivity.CannonHop ? Enumerable.Range(0, 11).Select(n => PointerTarget(n, MathLayout.Track(n))).ToArray() : model.Round.Choices.Select((n, i) => PointerTarget(n, MathLayout.Choice(i, model.Round.Choices.Length))).ToArray();
                PointerReport(model.Phase + "/" + model.Stage + "/" + model.CanAnswer + "/" + model.Errors + "/" + hopSelection + "/" + model.BuiltMask + "/" + PaintedCount + "/" + Screen.width + "/" + Screen.height,
                    new
                    {
                        Phase = model.Phase.ToString(),
                        model.Stage,
                        model.CanAnswer,
                        HopSelection = hopSelection,
                        PaintedCount,
                        Balls = TokenTargets,
                        Bonus = model.Bonus.Active,
                        Points = model.Bonus.Score,
                        model.Round.Answer,
                        model.Round.Choices,
                        model.BuiltCount,
                        model.BuiltMask,
                        Targets = targets,
                        Back = PointerTarget(-2, TouchMathBack),
                        Mute = PointerTarget(-1, TouchMathMute),
                        LayoutWidth = 1440,
                        LayoutHeight = 900,
                        Width = Screen.width,
                        Height = Screen.height,
                        Correct = model.Phase == MathPhase.Reward,
                        model.Errors
                    });
            }
            Refresh();
        }
        void SpeakPhase()
        {
            if (spokenPhase == model.Phase)
                return;
            spokenPhase = model.Phase;
            countSpoken = -1;
            if (model.Phase == MathPhase.Ready) { ResetPaint(); hopSelection = model.Round.A; }
            if (model.Phase == MathPhase.Go && BonusRound) S.Audio.Play("bonus-round", S.Settings, .4f);
            if (model.Phase == MathPhase.Reward && BonusRound) S.Audio.Play("bonus-win", S.Settings, .45f);
            string text = "";
            switch (model.Phase)
            {
                case MathPhase.Ready:
                    text = "Ready";
                    break;
                case MathPhase.Set:
                    text = "Set";
                    break;
                case MathPhase.Go:
                    text = "Go";
                    break;
                case MathPhase.Initial:
                    text = activity == MathActivity.MakeNumber ? Question : activity == MathActivity.Duel ? "" : Numbers[model.Round.A];
                    break;
                case MathPhase.Transform:
                    text = activity == MathActivity.Hiding ? "" : (model.Round.Subtract ? "Take away " : "") + Numbers[model.Round.B] + (model.Round.Subtract ? "" : " more");
                    break;
                case MathPhase.Ask:
                    text = Question;
                    break;
                case MathPhase.Reward:
                    text = activity == MathActivity.Duel ? "" : Numbers[model.Round.Answer];
                    break;
                case MathPhase.Incorrect:
                    S.Audio.Stop();
                    S.Rewards.Clear();
                    break;
            }
            if (model.Phase == MathPhase.Ready)
                S.Rewards.Clear();
            Speak(text, model.Phase == MathPhase.Ready || model.Phase == MathPhase.Set || model.Phase == MathPhase.Go);
        }
        public override void Key(KeyEvent e)
        {
            if (!e.Down || S.Keys.Keys.Any(ParentChord.IsModifier))
                return;
            if (e.Key == 27)
            {
                S.Picker();
                return;
            }
            if (!model.CanAnswer || activity == MathActivity.MakeNumber || activity == MathActivity.Duel)
                return;
            if (activity == MathActivity.CannonHop)
            {
                if (e.Key == 37 || e.Key == 39)
                {
                    hopSelection = Mathf.Clamp(hopSelection + (e.Key == 37 ? -1 : 1), 0, 10);
                    S.Audio.Play("ball-touch", S.Settings, .2f);
                    return;
                }
                if (e.Key == 13 || e.Key == 32) { model.Answer(hopSelection); return; }
            }
            int n = e.Key >= 48 && e.Key <= 57 ? e.Key - 48 : e.Key >= 96 && e.Key <= 105 ? e.Key - 96 : -1;
            if (n >= 0 && (activity == MathActivity.CannonHop || model.Round.Choices.Contains(n)))
                model.Answer(n);
        }
        public override void Pointer(Vector2 logical, bool right)
        {
            if (!AcceptTap(right))
                return;
            var p = new NVector2(logical.x, logical.y);
            if (TouchMathBack.Contains(p))
            {
                S.Picker();
                return;
            }
            if (TouchMathMute.Contains(p))
            {
                ToggleMute();
                return;
            }
            if (!model.CanAnswer)
                return;
            if (activity != MathActivity.MakeNumber && activity != MathActivity.Duel && PaintToken(logical))
            { Refresh(); return; }
            if (activity == MathActivity.MakeNumber)
            {
                for (int i = 0; i < model.Difficulty.Capacity; i++)
                    if (MathLayout.CellButton(i, model.Difficulty.Capacity).Contains(p))
                    {
                        if (model.Cell(i) && model.Difficulty.Level <= 2)
                            Speak(Numbers[model.BuiltCount]);
                        break;
                    }
            }
            else if (activity == MathActivity.Duel)
            {
                for (int side = 0; side < 2; side++)
                    if (MathLayout.Group(side).Contains(p))
                    {
                        model.Answer(side);
                        return;
                    }
                if (MathLayout.Same.Contains(p))
                    model.Answer(2);
            }
            else if (activity == MathActivity.CannonHop)
            {
                for (int n = 0; n <= 10; n++)
                    if (MathLayout.Track(n).Contains(p))
                    {
                        model.Answer(n);
                        break;
                    }
            }
            else
                for (int i = 0; i < model.Round.Choices.Length; i++)
                    if (MathLayout.Choice(i, model.Round.Choices.Length).Contains(p))
                    {
                        model.Answer(model.Round.Choices[i]);
                        break;
                    }
            Refresh();
        }
        public override void Suspend()
        {
            model.Restart();
            spokenPhase = null;
            countSpoken = -1;
            LastTap = S.Now;
            S.Audio.Stop();
            Refresh();
        }
        public override string DiagnosticState => activity + " phase=" + model.Phase + " stage=" + model.Stage + " level=" + model.Difficulty.Level + " answer=" + model.Round.Answer + " choices=" + string.Join(",", model.Round.Choices) + " errors=" + model.Errors + " built=" + model.BuiltCount;
        static Vector2 HiddenCell(int i) => new Vector2(170 + i % 5 * 150, 386 + i / 5 * 138);
        List<Vector2> RewardPoints()
        {
            var points = new List<Vector2>();
            var r = model.Round;
            for (int i = 0; i < model.RewardCount; i++)
            {
                if (activity == MathActivity.Hiding)
                    points.Add(HiddenCell(i));
                else if (activity == MathActivity.Duel)
                    points.Add(V(MathLayout.GroupCell(i < r.A ? 0 : 1, i < r.A ? i : i - r.A)));
                else if (activity == MathActivity.CannonHop)
                    points.Add(new Vector2(390 + i % 5 * 165, 278 + i / 5 * 98));
                else
                    points.Add(V(MathLayout.Cell(i, model.Difficulty.Capacity)));
            }
            if (activity == MathActivity.MakeNumber)
            {
                points.Clear();
                for (int i = 0; i < model.Difficulty.Capacity; i++)
                    if ((model.BuiltMask & (1 << i)) != 0)
                        points.Add(V(MathLayout.Cell(i, model.Difficulty.Capacity)));
            }
            return points;
        }
        bool IsCountdown => model.Phase == MathPhase.Ready || model.Phase == MathPhase.Set || model.Phase == MathPhase.Go;
        bool Moving => model.Phase == MathPhase.Transform || model.Phase == MathPhase.Confirm;
        bool AnswerPhase => model.Phase == MathPhase.Ask || model.Phase == MathPhase.AwaitAnswer;
        void Refresh()
        {
            Tokens.Clear();
            BuildTokens();
            Vector2 shake = Vector2.zero;
            if (!S.Settings.GentleMotion && model.Phase == MathPhase.Incorrect)
                shake.x = Mathf.Sin((float)model.Time * 85) * 3;
            SyncSpheres(shake);
        }
        void Ball(Vector2 p, int index, float radius = 48)
        {
            Tokens.Add(new Token(p, radius, index));
        }
        void BuildTokens()
        {
            var r = model.Round;
            if (IsCountdown)
                return;
            if (model.Phase == MathPhase.Reward)
            {
                var points = RewardPoints();
                for (int i = model.Popped; i < points.Count; i++)
                    Ball(points[i], i, activity == MathActivity.Duel || activity == MathActivity.CannonHop ? 35 : 48);
                return;
            }
            if (activity == MathActivity.Duel)
            {
                for (int side = 0; side < 2; side++)
                {
                    bool numeral = !model.Replaying && model.Difficulty.Level >= 6 && (side == 1 || model.Difficulty.Level >= 7);
                    if (!numeral)
                        for (int i = 0; i < (side == 0 ? r.A : r.B); i++)
                            Ball(V(MathLayout.GroupCell(side, i)), i + (side == 0 ? 0 : r.A), 37);
                }
                return;
            }
            if (activity == MathActivity.MakeNumber)
            {
                if (model.Counting)
                {
                    for (int i = 0; i < r.B; i++)
                        Ball(V(MathLayout.Cell(i, model.Difficulty.Capacity)), i);
                }
                else
                    for (int i = 0; i < model.Difficulty.Capacity; i++)
                        if ((model.BuiltMask & (1 << i)) != 0)
                            Ball(V(MathLayout.Cell(i, model.Difficulty.Capacity)), i);
                return;
            }
            if (activity == MathActivity.CannonHop)
            {
                if (model.Counting)
                {
                    for (int i = 0; i < r.Final; i++)
                        Ball(new Vector2(390 + i % 5 * 165, 278 + i / 5 * 98), i, 35);
                    return;
                }
                float location = r.A, arc = 0;
                if (Moving)
                {
                    float hops = model.Motion * r.B;
                    location = r.A + (r.Subtract ? -1 : 1) * hops;
                    arc = Mathf.Sin((hops - Mathf.Floor(hops)) * Mathf.PI) * 90;
                }
                else if (!model.AskFirst && (AnswerPhase || model.Counting))
                    location = r.Final;
                Ball(V(MathLayout.HopPoint(location)) - new Vector2(0, arc), 0, 35);
                return;
            }
            bool initial = model.Phase == MathPhase.Initial || model.AskFirst && AnswerPhase;
            if (activity == MathActivity.Hiding)
            {
                for (int i = 0; i < r.A; i++)
                {
                    bool hidden = i >= r.A - r.B;
                    if (hidden && !initial && !Moving && !model.Counting)
                        continue;
                    Vector2 p = HiddenCell(i);
                    if (Moving && hidden)
                        p = Vector2.Lerp(p, new Vector2(1100, 440), Mathf.SmoothStep(0, 1, model.Motion));
                    Ball(p, i, 43);
                }
                return;
            }
            if (model.Hidden)
                return;
            int count = initial ? r.A : Moving ? Math.Max(r.A, r.Final) : r.Final;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = V(MathLayout.Cell(i, model.Difficulty.Capacity));
                if (Moving)
                {
                    float t = Mathf.SmoothStep(0, 1, model.Motion);
                    if (!r.Subtract && i >= r.A)
                        p = Vector2.Lerp(new Vector2(-120 - (i - r.A) * 90, 650), p, t);
                    if (r.Subtract && i >= r.Final)
                        p += new Vector2(t * 330, t * t * 620);
                }
                Ball(p, i);
            }
        }
        public override void DrawUI()
        {
            var r = model.Round;
            if(activity==MathActivity.CannonHop && hopPulse>0)
                Ring(hopLanding+new Vector2(0,32),38+(1-hopPulse/.35f)*22,2,new Color(Ink.r,Ink.g,Ink.b,hopPulse/.35f*.45f));
            // These translucent outlines leave the real, lit target spheres visible.
            if (activity != MathActivity.Duel && activity != MathActivity.CannonHop && activity != MathActivity.Hiding)
            {
                for (int i = 0; i < model.Difficulty.Capacity; i++)
                {
                    var cell = MathLayout.CellButton(i, model.Difficulty.Capacity);
                    bool hover = activity == MathActivity.MakeNumber && model.CanAnswer && R(cell).Contains(Ui.Pointer);
                    Ui.Panel(R(cell), hover ? new Color(Ink.r, Ink.g, Ink.b, .13f) : new Color(1, 1, 1, .035f));
                    if (activity == MathActivity.MakeNumber && model.CanAnswer && (model.BuiltMask & (1 << i)) == 0)
                        Ui.Label(R(cell), "+", 36, new Color(1, 1, 1, .27f));
                }
            }
            if (activity == MathActivity.Duel)
                for (int side = 0; side < 2; side++)
                {
                    Rect area = R(MathLayout.Group(side));
                    bool selected = model.Phase == MathPhase.Reward && (r.Answer == side || r.Answer == 2);
                    Color edge = selected ? Ink : model.CanAnswer && area.Contains(Ui.Pointer) ? Style.Blue : new Color(.16f, .22f, .32f);
                    Line(new Vector2(area.x, area.y), new Vector2(area.xMax, area.y), 3, edge);
                    Line(new Vector2(area.x, area.yMax), new Vector2(area.xMax, area.yMax), 3, edge);
                    Line(new Vector2(area.x, area.y), new Vector2(area.x, area.yMax), 3, edge);
                    Line(new Vector2(area.xMax, area.y), new Vector2(area.xMax, area.yMax), 3, edge);
                    if (!IsCountdown && model.Phase != MathPhase.Reward && !model.Replaying && model.Difficulty.Level >= 6 && (side == 1 || model.Difficulty.Level >= 7))
                        Ui.Label(area, (side == 0 ? r.A : r.B).ToString(), 134, Color.white);
                }
            if (activity == MathActivity.CannonHop)
            {
                for (int n = 0; n <= 10; n++)
                    Button(MathLayout.Track(n), n.ToString(), model.CanAnswer, model.Phase == MathPhase.Reward && n == r.Final || model.CanAnswer && n == hopSelection, 49);
                Line(new Vector2(116, 634), new Vector2(1316, 634), 3, new Color(1, 1, 1, .17f));
                Ui.Label(new Rect(280, 652, 880, 64), model.CanAnswer ? (S.TouchPlay ? "Tap the landing number" : "Click a number · ← → and Enter · 0–9 keys") : "Watch the ball hop " + (r.Subtract ? "back" : "forward"), 27, Ink);
            }
            if (activity == MathActivity.Hiding && (model.Phase == MathPhase.Transform || AnswerPhase))
            {
                Ui.Panel(new Rect(965, 282, 330, 330), new Color(.1f, .16f, .26f));
                Ui.Panel(new Rect(978, 295, 304, 304), new Color(.12f, .19f, .30f));
                Ui.Label(new Rect(965, 282, 330, 330), "?", 132, Color.white);
                Ui.Ball(new Vector2(1262, 454), 9, Ink);
            }
            if (model.Counting)
            {
                var counting = Tokens.Where(t => activity != MathActivity.Hiding || t.Index >= r.A - r.B).ToArray();
                if (model.CountIndex >= 0 && model.CountIndex < counting.Length)
                {
                    var token = counting[model.CountIndex];
                    Ring(token.Position, token.Radius + 12, 5, Ink);
                    int n = activity == MathActivity.Duel && model.CountIndex >= r.A ? model.CountIndex - r.A + 1 : model.CountIndex + 1;
                    Ui.Label(new Rect(token.Position.x - 70, token.Position.y - 118, 140, 58), n.ToString(), 39, Color.white);
                }
                if (model.CountQuantity == 0)
                    Ui.Label(new Rect(560, 355, 320, 160), "0", 108, Color.white);
            }
            if (model.Phase == MathPhase.Reward)
            {
                var points = RewardPoints();
                float time = (float)model.Time;
                for (int i = 0; i < points.Count; i++)
                {
                    float age = time - (float)MathGame.ImpactAt(i, points.Count), flight = age + .25f;
                    if (flight >= 0 && flight < .25f)
                    {
                        Vector2 origin = new Vector2(720, 676), tip = Vector2.Lerp(origin, points[i], flight / .25f);
                        Line(Vector2.Lerp(origin, points[i], Mathf.Max(0, flight / .25f - .28f)), tip, 7, Ink);
                        Ui.Ball(tip, 8, Color.white);
                    }
                    if (age >= 0 && age < .4f)
                        Ring(points[i], 38 + age * 135, 4 * (1 - age / .4f), new Color(Ink.r, Ink.g, Ink.b, 1 - age / .4f));
                }
                Ui.Panel(new Rect(300, 175, 840, 88), Style.Navy);
                Ui.Label(new Rect(300, 175, 840, 88), activity == MathActivity.MakeNumber && r.A == 0 ? r.B.ToString() : r.Equation, 65, Ink);
            }
            string cue = Question.ToUpperInvariant();
            switch (model.Phase)
            {
                case MathPhase.Ready:
                    cue = "READY";
                    break;
                case MathPhase.Set:
                    cue = "SET";
                    break;
                case MathPhase.Go:
                    cue = "GO";
                    break;
                case MathPhase.Initial:
                    cue = activity == MathActivity.MakeNumber ? Question.ToUpperInvariant() : activity == MathActivity.Duel ? "" : r.A.ToString();
                    break;
                case MathPhase.Transform:
                    cue = activity == MathActivity.Hiding ? "" : (r.Subtract ? "TAKE AWAY " : "") + r.B + (r.Subtract ? "" : " MORE");
                    break;
                case MathPhase.Incorrect:
                case MathPhase.Reward:
                    cue = "";
                    break;
                case MathPhase.Count:
                    cue = "LET'S COUNT";
                    break;
            }
            Ui.Label(new Rect(190, 105, 1060, 90), cue, activity == MathActivity.CannonHop ? 34 : 48, Color.white);
            if (model.Difficulty.Equation && !model.Replaying && !IsCountdown && model.Phase != MathPhase.Reward)
            {
                string equation = activity == MathActivity.Hiding ? (r.A - r.B) + " + ? = " + r.A : activity == MathActivity.MakeNumber ? r.A + " + ? = " + r.B : activity == MathActivity.Duel ? "" : r.A + (r.Subtract ? " - " : " + ") + r.B + " = ?";
                Ui.Label(new Rect(300, 195, 840, 65), equation, 48, Ink);
            }
            if (activity == MathActivity.MakeNumber)
            {
                if (AnswerPhase)
                {
                    Ui.Label(new Rect(550, 665, 340, 82), r.B.ToString(), 64, Ink);
                    for (int i = 0; i < r.B; i++)
                        Ui.Ball(new Vector2(720 - (r.B - 1) * 21 + i * 42, 804), 13, Ink);
                }
            }
            else if (activity == MathActivity.Duel)
                Button(MathLayout.Same, "=   SAME", model.CanAnswer, false, 37);
            else if (activity != MathActivity.CannonHop)
            {
                for (int i = 0; i < r.Choices.Length; i++)
                {
                    int n = r.Choices[i];
                    var choice = MathLayout.Choice(i, r.Choices.Length);
                    Button(choice, model.Difficulty.VisualChoices ? "" : n.ToString(), model.CanAnswer, false, 66);
                    if (model.Difficulty.VisualChoices)
                        for (int k = 0; k < n; k++)
                            Ui.Ball(new Vector2(choice.Center.X - (n - 1) * 29 + k * 58, choice.Center.Y), 20, model.CanAnswer ? Ink : Ink * .3f);
                }
            }
            Ui.Label(new Rect(500, 30, 700, 55), (BonusRound ? "PEARL BONUS ×3   " : "") + model.Bonus.Score + " points", 24, Ink);
            Button(TouchMathBack, "<  GAMES", true, false, 24);
            Button(TouchMathMute, S.Settings.Sound ? "MUTE" : "UNMUTE", true, false, 23);
            Ui.Label(new Rect(1294, 26, 114, 66), (model.Phase == MathPhase.Reward ? model.Stage - 1 : model.Stage).ToString(), 38, Color.white);
        }
    }
}
