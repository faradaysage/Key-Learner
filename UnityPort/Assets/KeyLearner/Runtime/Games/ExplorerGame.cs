using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KeyLearner.Studio;

namespace KeyLearner.Unity
{
    public sealed class ExplorerGame : Minigame
    {
        readonly ExplorerKind kind;
        FlightModel model;
        GameObject hero, gate;
        TextMesh letter;
        LineRenderer ring;
        readonly Dictionary<Vector2Int, GameObject> segments = new Dictionary<Vector2Int, GameObject>();
        readonly List<Swimmer> swimmers = new List<Swimmer>();
        readonly List<Transform> plants = new List<Transform>();
        readonly List<Transform> windmills = new List<Transform>();
        readonly System.Random random = new System.Random(127);
        Material terrainMat, waterMat, skyMaterial, gateMaterial;
        int segmentAnchor = int.MinValue, horizontalAnchor = int.MinValue, terrainDetail;
        float signalTime, particleClock, diagnosticClock;
        bool firstFrame = true, validateWord;
        class Swimmer
        {
            public Transform T; public Vector3 Origin; public float Phase, Speed, Radius;
        }
        public ExplorerGame(ExplorerKind kind)
        {
            this.kind = kind;
        }
        static Vector3 V(System.Numerics.Vector3 p) => new Vector3(p.X, p.Y, p.Z);
        public override void Enter(GameServices services)
        {
            base.Enter(services);
            string key = "explorer." + kind;
            if (S.Session.TryGetValue(key, out var state))
                model = (FlightModel)state;
            else
            {
                model = new FlightModel();
                model.Configure(kind);
                S.Session[key] = model;
                // Disposable previews can inspect later chapters without changing progression or movement.
                if (S.Preview && float.TryParse(S.Options.Value("--preview-distance"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float distance))
                {
                    float z = -Mathf.Clamp(distance, 0, 6500), x = kind == ExplorerKind.Racer ? ExplorerWorld.Road(z) : kind == ExplorerKind.Bird ? ExplorerWorld.Valley(z) : 0;
                    model.Position = new System.Numerics.Vector3(x, kind == ExplorerKind.Racer ? 7 : kind == ExplorerKind.Dolphin ? -35 : ExplorerWorld.Height(x, z) + 65, z);
                }
                PickWord();
            }
            model.Response = (float)S.Settings.FlightResponse;
            model.TopSpeed = (float)S.Settings.FlightTopSpeed;
            if (S.Content == null)
                throw new InvalidOperationException("The local content catalog is missing. Run ProjectSetup.Configure in the Editor.");
            terrainMat = new Material(Shader.Find(kind == ExplorerKind.Dolphin ? "KeyLearner/SeabedCaustics" : "KeyLearner/Terrain"));
            terrainMat.enableInstancing = true;
            if (kind == ExplorerKind.Dolphin)
            {
                terrainMat.SetFloat("_Intensity", .19f);
                terrainMat.SetFloat("_MotionSpeed", S.Settings.GentleMotion ? .06f : .23f);
            }
            waterMat = new Material(Shader.Find("KeyLearner/Water"));
            waterMat.SetColor("_BaseColor", kind == ExplorerKind.Dolphin ? new Color(.08f, .67f, .81f, .35f) : new Color(.05f, .55f, .73f, .72f));
            skyMaterial = new Material(Shader.Find("KeyLearner/Sky"));
            RenderSettings.skybox = skyMaterial;
            S.Camera.clearFlags = kind == ExplorerKind.Dolphin ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            S.Camera.orthographic = false;
            S.Camera.fieldOfView = 62;
            S.Camera.nearClipPlane = .3f;
            S.Camera.farClipPlane = 1600;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = kind == ExplorerKind.Dolphin ? new Color(.035f, .37f, .51f) : new Color(.58f, .79f, .91f);
            RenderSettings.fogDensity = kind == ExplorerKind.Dolphin ? .0028f : .00095f;
            S.Camera.backgroundColor = RenderSettings.fogColor;
            S.ConfigureLighting(kind == ExplorerKind.Dolphin);
            string category = kind == ExplorerKind.Bird ? "bird" : kind == ExplorerKind.Racer ? "car" : "dolphin";
            hero = S.Content.Spawn(category, 0, Root.transform, V(model.Position), kind == ExplorerKind.Bird ? 4.2f : kind == ExplorerKind.Racer ? 3.3f : 5.6f);
            if (!hero)
                throw new InvalidOperationException("Required licensed animated/player asset missing: " + category);
            if (kind == ExplorerKind.Dolphin)
                OceanAtmosphere.Create(S, Root.transform, hero.transform);
            gate = new GameObject("Ordered letter gate");
            gate.transform.SetParent(Root.transform);
            ring = gate.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 80;
            ring.widthMultiplier = .35f;
            gateMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ring.sharedMaterial = gateMaterial;
            for (int i = 0; i < 80; i++)
            {
                float a = i * Mathf.PI * 2 / 80;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 8, Mathf.Sin(a) * 8, 0));
            }
            letter = Visuals.Text(gate.transform, "A", new Vector3(0, 0, -.3f), 14, Style.Dots);
            for (int i = 0; i < 4; i++)
                Visuals.Text(gate.transform, "A", new Vector3(.04f * i, -.04f * i, .15f * i), 14, new Color(.18f, .1f, .02f));
            if (kind != ExplorerKind.Dolphin)
                ExplorerCloudLayer.Create(Root.transform, S.Camera.transform, S.Settings.GentleMotion);
            UpdateSegments();
            Tick(0);
        }
        void PickWord()
        {
            var list = S.Store.Words.Where(w => w.Enabled && w.Adventure).ToArray();
            model.SetWord(list.Length == 0 ? "cat" : list[random.Next(list.Length)].Word);
        }
        public override void Key(KeyEvent e)
        {
            if (!e.Down)
                return;
            if (e.Key == 37)
                model.TapTurn(1);
            if (e.Key == 39)
                model.TapTurn(-1);
            if ((e.Key == 162 || e.Key == 163) && model.Signal())
            {
                signalTime = 2;
                S.Audio.Play(kind == ExplorerKind.Bird ? "squawk" : "powerup", S.Settings, .45f);
            }
        }
        public override void Tick(float dt)
        {
            model.Response = (float)S.Settings.FlightResponse;
            model.TopSpeed = (float)S.Settings.FlightTopSpeed;
            if (validateWord)
            {
                validateWord = false;
                if (!S.Store.Words.Any(w => w.Enabled && w.Adventure && w.Word == model.Word))
                    PickWord();
            }
            int before = model.Collected, completed = model.Completed;
            // The domain is right-handed; the Unity -Z follow view has the opposite screen-right axis.
            float turn = (S.Keys.IsDown(37) ? 1 : 0) - (S.Keys.IsDown(39) ? 1 : 0), pitch = (S.Keys.IsDown(40) ? 1 : 0) - (S.Keys.IsDown(38) ? 1 : 0);
            model.Step(dt, turn, pitch, S.Keys.IsDown(32), S.Settings.FlightAssist);
            if (before != model.Collected)
            {
                if (model.Collected < model.Word.Length)
                    S.Audio.Say(model.Word[before].ToString(), S.Settings, key: true);
                S.Audio.Play("pop", S.Settings, .5f);
                S.Rewards.Burst(V(model.Gate), Style.Dots, 25, 9);
            }
            if (model.Completed > completed)
            {
                S.Feedback.Pulse(.3f);
                var word = S.Store.Words.FirstOrDefault(w => w.Word == model.Word);
                S.Audio.Say(word != null && word.Spoken.Length > 0 ? word.Spoken : model.Word, S.Settings, word?.Recording ?? "");
                S.Audio.Play("powerup", S.Settings);
                S.Rewards.Burst(V(model.Position) + V(model.Forward) * 18, Style.Dots, 85, 20);
            }
            if (model.Collected >= model.Word.Length && model.RewardRemaining <= 0)
                PickWord();
            Vector3 position = V(model.Position), forward = V(model.Forward), up = V(model.Up);
            hero.transform.localPosition = position + (kind == ExplorerKind.Racer ? Vector3.down * 1.2f : Vector3.zero);
            Quaternion facing = Quaternion.LookRotation(forward, up) * Quaternion.AngleAxis(-model.Roll * Mathf.Rad2Deg, Vector3.forward);
            float yaw = S.Content.Category(kind == ExplorerKind.Bird ? "bird" : kind == ExplorerKind.Racer ? "car" : "dolphin")[0].Yaw;
            hero.transform.localRotation = facing * Quaternion.Euler(0, yaw, 0);
            float follow = kind == ExplorerKind.Racer ? 24 : kind == ExplorerKind.Bird ? 24 : 23;
            Vector3 cameraPosition = position - forward * follow + up * (kind == ExplorerKind.Racer ? 10 : 9);
            Vector3 look = position + forward * (kind == ExplorerKind.Racer ? 35 : 28) + up * 1;
            if (firstFrame)
            {
                S.Camera.transform.position = cameraPosition;
                firstFrame = false;
            }
            else
                S.Camera.transform.position = Vector3.Lerp(S.Camera.transform.position, cameraPosition, 1 - Mathf.Exp(-dt * 7));
            S.Camera.transform.rotation = Quaternion.LookRotation(look - S.Camera.transform.position, up);
            gate.SetActive(model.Collected < model.Word.Length);
            gate.transform.localPosition = V(model.Gate);
            gate.transform.rotation = S.Camera.transform.rotation;
            foreach (var text in gate.GetComponentsInChildren<TextMesh>())
                text.text = model.Letter.ToString().ToUpperInvariant();
            gateMaterial.color = model.LastLetter ? Style.Dots : model.FirstLetter ? Style.Mint : Style.Blue;
            signalTime = Mathf.Max(0, signalTime - dt);
            gate.transform.localScale = Vector3.one * (1 + signalTime * .12f);
            UpdateSegments();
            WriteInteraction(dt);
            float t = (float)S.Now * (S.Settings.GentleMotion ? .38f : 1);
            for (int i = swimmers.Count - 1; i >= 0; i--)
            {
                var fish = swimmers[i];
                if (!fish.T)
                {
                    swimmers.RemoveAt(i);
                    continue;
                }
                bool visible = (fish.Origin - position).sqrMagnitude < 350 * 350;
                if (fish.T.gameObject.activeSelf != visible)
                    fish.T.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                float phase = t * fish.Speed + fish.Phase;
                Vector3 delta = new Vector3(Mathf.Sin(phase) * fish.Radius, Mathf.Sin(phase * .73f) * 2, Mathf.Cos(phase) * fish.Radius * .35f);
                fish.T.localPosition = fish.Origin + delta;
                fish.T.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(phase), .1f * Mathf.Cos(phase * .73f), -.35f * Mathf.Sin(phase)));
            }
            for (int i = plants.Count - 1; i >= 0; i--)
            {
                if (!plants[i])
                {
                    plants.RemoveAt(i);
                    continue;
                }
                var euler = plants[i].localEulerAngles;
                euler.z = Mathf.Sin(t * (S.Settings.GentleMotion ? .25f : .7f) + i) * (S.Settings.GentleMotion ? 1 : 3);
                plants[i].localEulerAngles = euler;
            }
            particleClock += dt;
            if (particleClock > .2f)
            {
                particleClock = 0;
                if (kind == ExplorerKind.Dolphin)
                    S.Rewards.Burst(position + forward * 20 + new Vector3(random.Next(-20, 21), -10, 0), new Color(.55f, .88f, 1, .6f), 3, 2);
                if (kind == ExplorerKind.Racer && model.OffRoad > 0)
                    S.Rewards.Burst(position - forward * 2, new Color(.65f, .48f, .27f), 7, 7);
            }
        }
        void WriteInteraction(float dt)
        {
            if (!S.Preview)
                return;
            diagnosticClock += dt;
            if (diagnosticClock < .08f)
                return;
            diagnosticClock = 0;
            string path = S.Options.Value("--interaction-report");
            if (path.Length == 0)
                return;
            Vector3 position = V(model.Position), road = new Vector3(ExplorerWorld.Road(model.Position.Z), 7, model.Position.Z);
            var heroScreen = S.Camera.WorldToScreenPoint(position);
            var roadScreen = S.Camera.WorldToScreenPoint(road);
            var renderers = hero.GetComponentsInChildren<Renderer>();
            var report = new
            {
                time = S.Now,
                mode = (int)S.Settings.Mode,
                x = model.Position.X,
                z = model.Position.Z,
                heading = model.Yaw,
                heroX = heroScreen.x,
                roadX = roadScreen.x,
                lateralPixels = heroScreen.x - roadScreen.x,
                rightHeld = S.Keys.IsDown(39),
                position = position.ToString(),
                renderers = renderers.Select(r => new { name = r.name, visible = r.isVisible, enabled = r.enabled, bounds = r.bounds.ToString(), scale = r.transform.lossyScale.ToString() }).ToArray()
            };
            // Preview diagnostics must never interrupt gameplay when a reader briefly locks the file.
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
                string temp = path + ".tmp";
                System.IO.File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(report));
                if (System.IO.File.Exists(path))
                    System.IO.File.Replace(temp, path, null);
                else
                    System.IO.File.Move(temp, path);
            }
            catch (System.IO.IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        void UpdateSegments()
        {
            if (terrainDetail != S.Settings.TerrainDetail)
            {
                terrainDetail = S.Settings.TerrainDetail;
                foreach (var segment in segments.Values)
                    UnityEngine.Object.Destroy(segment);
                segments.Clear();
                segmentAnchor = horizontalAnchor = int.MinValue;
            }
            int current = Mathf.FloorToInt(-model.Position.Z / 160), across = kind == ExplorerKind.Racer ? 0 : Mathf.FloorToInt((model.Position.X + 750) / 1500);
            if (segmentAnchor == current && horizontalAnchor == across)
                return;
            segmentAnchor = current;
            horizontalAnchor = across;
            int breadth = kind == ExplorerKind.Racer ? 0 : 1, forward = kind == ExplorerKind.Dolphin ? 5 : 7;
            foreach (var old in segments.Keys.Where(i => i.y < current - 2 || i.y > current + forward || i.x < across - breadth || i.x > across + breadth).ToArray())
            {
                UnityEngine.Object.Destroy(segments[old]);
                segments.Remove(old);
            }
            for (int x = across - breadth; x <= across + breadth; x++)
                for (int z = current - 2; z <= current + forward; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!segments.ContainsKey(cell))
                        segments[cell] = CreateSegment(z, x * 1500);
                }
        }
        GameObject CreateSegment(int index, float centerX)
        {
            var go = new GameObject((kind == ExplorerKind.Dolphin ? "Reef garden " : "Landscape chapter ") + index + " / " + centerX);
            go.transform.SetParent(Root.transform);
            var rng = new System.Random(unchecked(index * 7919 + Mathf.RoundToInt(centerX) * 153 + 127));
            float start = -index * 160;
            bool ocean = kind == ExplorerKind.Dolphin, race = kind == ExplorerKind.Racer;
            Terrain(go.transform, start, ocean, race, centerX);
            if (ocean)
            {
                Reef(go.transform, start, rng, index, centerX);
                return go;
            }
            Water(go.transform, start, 0, 1500, 160, centerX);
            if (race)
                Road(go.transform, start);
            else if (ExplorerWorld.Area(start - 80) == Region.City || ExplorerWorld.Area(start - 80) == Region.Town)
                TownStreet(go.transform, start, centerX);
            LandEnvironmentComposer.Populate(S, go.transform, start, index, race, centerX);
            return go;
        }
        void Reef(Transform parent, float start, System.Random rng, int index, float centerX)
        {
            // Asymmetric shelves frame a clear swimming corridor. Source boulders determine coral contact.
            for (int garden = 0; garden < 6; garden++)
            {
                float z = start - 16 - garden * 25 + rng.Next(-5, 6), side = (garden + index) % 2 == 0 ? -1 : 1;
                float x = centerX + side * (34 + (garden % 3) * 31) + rng.Next(-9, 10);
                float bed = ExplorerWorld.Bed(x, z), height = 18 + (garden % 3) * 7;
                var rock = S.Content.Spawn("rock", garden + index, parent, new Vector3(x, bed - 2, z), height, rng.Next(360));
                if (!rock)
                    continue;
                rock.transform.localScale = Vector3.Scale(rock.transform.localScale, new Vector3(1.2f + garden % 3 * .12f, .72f + garden % 2 * .24f, 1.05f + garden % 2 * .23f));
                var surfaces = new List<MeshCollider>();
                foreach (var filter in rock.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    surfaces.Add(collider);
                }
                Physics.SyncTransforms();
                int coralCount = 8 + (garden + index) % 4;
                for (int j = 0; j < coralCount; j++)
                {
                    float angle = j * 2.399f + garden, rad = 2 + Mathf.Sqrt(j) * 3.3f;
                    float px = x + Mathf.Cos(angle) * rad, pz = z + Mathf.Sin(angle) * rad, ground = ExplorerWorld.Bed(px, pz);
                    var ray = new Ray(new Vector3(px, 5, pz), Vector3.down);
                    foreach (var collider in surfaces)
                        if (collider.Raycast(ray, out var hit, 160))
                            ground = Mathf.Max(ground, hit.point.y);
                    S.Content.Spawn("coral", index * 11 + garden * 5 + j, parent, new Vector3(px, ground - .55f, pz), 6f + (j % 4) * 2f, rng.Next(360));
                }
                foreach (var collider in surfaces)
                {
                    collider.enabled = false;
                    UnityEngine.Object.Destroy(collider);
                }
                // Low coral fans bridge boulder bases into the sand instead of isolated display plinths.
                for (int j = 0; j < 7; j++)
                {
                    float angle = j * 1.9f + index, rad = 13 + j % 3 * 3;
                    float px = x + Mathf.Cos(angle) * rad, pz = z + Mathf.Sin(angle) * rad;
                    S.Content.Spawn("coral", index * 17 + garden * 7 + j + 13, parent, new Vector3(px, ExplorerWorld.Bed(px, pz) - .4f, pz), 3.8f + j % 4 * 1.3f, rng.Next(360));
                }
                // Seaweed roots follow their own seabed sample; small clusters have clear sandy openings.
                for (int j = 0; j < 5; j++)
                {
                    float px = x + side * (15 + j % 3 * 6), pz = z - 10 + j / 3 * 13 + rng.Next(-3, 4);
                    var plant = S.Content.Spawn("seaweed", 0, parent, new Vector3(px, ExplorerWorld.Bed(px, pz) - .45f, pz), 20 + (j % 3) * 7, rng.Next(360));
                    if (plant)
                        plants.Add(plant.transform);
                }
                for (int j = 0; j < 7; j++)
                {
                    Vector3 origin = new Vector3(x * .75f + centerX * .25f + (j % 3 - 1) * 3, bed + 35 + (garden % 3) * 8 + j / 3 * 2, z + j % 3 * 4);
                    var fish = S.Content.Spawn("fish", index * 3 + garden, parent, origin, 1.8f + (garden % 3) * .7f, 90);
                    if (fish)
                        swimmers.Add(new Swimmer { T = fish.transform, Origin = origin, Phase = j * .12f + garden * 1.3f, Radius = 9 + garden * 2, Speed = .13f + garden * .025f });
                }
            }
            string category = (index % 3 + 3) % 3 == 0 ? "whale" : (index % 3 + 3) % 3 == 1 ? "ray" : "shark";
            Vector3 big = new Vector3(centerX + (index % 2 == 0 ? 72 : -72), category == "whale" ? -21 : -42, start - 98);
            var animal = S.Content.Spawn(category, 0, parent, big, category == "whale" ? 10 : category == "ray" ? 3.8f : 4.6f, 90);
            if (animal)
                swimmers.Add(new Swimmer { T = animal.transform, Origin = big, Phase = index, Radius = 29, Speed = .09f });
            Water(parent, start, 0, 1500, 160, centerX);
        }
        void Terrain(Transform parent, float start, bool ocean, bool race, float centerX)
        {
            int cols = Mathf.Clamp(terrainDetail, 24, 100), rows = Mathf.Max(8, cols / 4);
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            int sides = race ? 2 : 1;
            for (int sideIndex = 0; sideIndex < sides; sideIndex++)
            {
                int offset = vertices.Count;
                for (int row = 0; row <= rows; row++)
                    for (int col = 0; col <= cols; col++)
                    {
                        float z = start - row * 160 / rows, x;
                        if (race)
                            x = ExplorerWorld.Road(z) + (sideIndex == 0 ? -1 : 1) * (20 + col * 700f / cols);
                        else
                            x = centerX - 750 + col * 1500f / cols;
                        float y = ocean ? ExplorerWorld.Bed(x, z) : ExplorerWorld.Land(x, z, race);
                        if (race && col == 0)
                            y = 5.7f;
                        vertices.Add(new Vector3(x, y, z));
                        float shade = .5f + .5f * Mathf.PerlinNoise(x * .027f, z * .027f);
                        Region region = ExplorerWorld.Area(z);
                        Color c = ocean ? Color.Lerp(new Color(.31f, .63f, .58f), new Color(.82f, .81f, .55f), shade) : region == Region.Tundra ? Color.Lerp(new Color(.67f, .79f, .78f), Color.white, shade) : region == Region.Mountains && y > 95 ? Color.Lerp(new Color(.48f, .55f, .54f), new Color(.87f, .91f, .90f), shade) : Color.Lerp(new Color(.16f, .36f, .14f), new Color(.52f, .66f, .27f), shade);
                        if (!ocean && y < 5)
                            c = new Color(.69f, .68f, .39f);
                        colors.Add(c.linear);
                    }
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < cols; col++)
                    {
                        int a = offset + row * (cols + 1) + col, b = a + cols + 1;
                        if (race && sideIndex == 0)
                        {
                            triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                        }
                        else
                            triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
            }
            MeshObject("Sculpted terrain", parent, vertices, triangles, terrainMat, colors);
        }
        void TownStreet(Transform parent, float start, float centerX)
        {
            // Aerial settlements use the same composed boulevard as their sidewalks.
            // This is scenery only: it adds no collision or changes to the shared flight model.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row <= 16; row++)
            {
                float z = start - row * 10, x = centerX + ExplorerWorld.Valley(z) * .35f;
                foreach (int side in new[] { -1, 1 })
                    vertices.Add(new Vector3(x + side * 12, ExplorerWorld.Height(x + side * 12, z) + .12f, z));
                if (row < 16)
                {
                    int a = row * 2;
                    triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
                if (row % 2 == 0 && row < 16)
                    Visuals.Box(parent, new Vector3(x, ExplorerWorld.Height(x, z) + .18f, z), new Vector3(.25f, .03f, 4), new Color(.91f, .88f, .64f));
            }
            MeshObject("Town boulevard", parent, vertices, triangles, Visuals.Material(new Color(.23f, .28f, .3f)));
        }
        void Road(Transform parent, float start)
        {
            var v = new List<Vector3>();
            var tr = new List<int>();
            for (int j = 0; j <= 32; j++)
            {
                float z = start - j * 5;
                float x = ExplorerWorld.Road(z);
                v.Add(new Vector3(x - 20, 5.82f, z));
                v.Add(new Vector3(x + 20, 5.82f, z));
                if (j < 32)
                {
                    int a = j * 2;
                    tr.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
            MeshObject("Continuous protected road", parent, v, tr, Visuals.Material(new Color(.16f, .22f, .28f)));
            foreach (int side in new[] { -1, 1 })
            {
                var edge = new List<Vector3>();
                for (int j = 0; j <= 32; j++)
                {
                    float z = start - j * 5, x = ExplorerWorld.Road(z) + side * 19.2f;
                    edge.Add(new Vector3(x - .12f, 5.91f, z));
                    edge.Add(new Vector3(x + .12f, 5.91f, z));
                }
                MeshObject("Road edge marking", parent, edge, tr, Visuals.Material(new Color(.89f, .91f, .84f)));
            }
            for (int j = 0; j < 16; j++)
            {
                float z = start - j * 10 - 3, x = ExplorerWorld.Road(z);
                Visuals.Box(parent, new Vector3(x, 5.89f, z), new Vector3(.35f, .03f, 4), new Color(.91f, .88f, .64f));
            }
        }
        void Water(Transform parent, float start, float y, float width, float length, float centerX)
        {
            MeshObject("Water", parent, new List<Vector3> { new Vector3(centerX - width / 2, y, start), new Vector3(centerX + width / 2, y, start), new Vector3(centerX - width / 2, y, start - length), new Vector3(centerX + width / 2, y, start - length) }, new List<int> { 0, 1, 2, 1, 3, 2 }, waterMat);
        }
        static void MeshObject(string name, Transform parent, List<Vector3> v, List<int> tr, Material material, List<Color> colors = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(tr, 0);
            if (colors != null)
                mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<TransientMesh>().Mesh = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        public override void DrawUI()
        {
            Ui.Panel(new Rect(40, 30, 510, 128), new Color(.025f, .065f, .12f, .92f));
            Ui.Label(new Rect(64, 41, 460, 30), GameCatalog.All.First(g => g.Explorer == kind).Name.ToUpperInvariant(), 18, Style.Mint, TextAnchor.MiddleLeft);
            for (int i = 0; i < model.Word.Length; i++)
            {
                float size = Mathf.Min(58, 450f / model.Word.Length);
                var rect = new Rect(65 + i * size, 80, size - 6, 58);
                Ui.Panel(rect, i < model.Collected ? Style.Mint : i == model.Collected ? Style.Dots : Style.Panel);
                Ui.Label(rect, model.Word[i].ToString().ToUpperInvariant(), 33, i <= model.Collected ? Style.Navy : Color.white);
            }
            Ui.Panel(new Rect(1160, 30, 240, 96), new Color(.025f, .065f, .12f, .9f));
            Ui.Label(new Rect(1180, 43, 200, 35), model.Score + " points", 27, Color.white);
            Ui.Label(new Rect(1180, 82, 200, 25), model.Completed + " words discovered", 15, Style.Mint);
            string region = kind == ExplorerKind.Dolphin ? "THE CORAL GARDENS" : ExplorerWorld.Area(model.Position.Z).ToString().ToUpperInvariant();
            Ui.Panel(new Rect(30, 781, 1370, 91), new Color(.025f, .065f, .12f, .7f), 12);
            Ui.Label(new Rect(42, 785, 830, 40), region, 21, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(42, 828, 1300, 38), kind == ExplorerKind.Racer ? "← → Steer    ↑ Accelerate    ↓ Brake    SPACE Boost    CTRL Find a letter    G G Games" : "← → Turn    ↑ Dive    ↓ Climb    SPACE Boost    Double-tap ← or → to roll    CTRL Call    G G Games", 19, new Color(.85f, .96f, 1), TextAnchor.MiddleLeft);
            if (model.RewardRemaining > 0)
                Ui.Label(new Rect(280, 280, 880, 150), model.Word.ToUpperInvariant() + "!", 84, Style.Dots);
        }
        public override void Suspend()
        {
            model?.ResetInputGestures();
            signalTime = 0;
            validateWord = true;
        }
        public override void Exit()
        {
            base.Exit();
            if (terrainMat)
                UnityEngine.Object.Destroy(terrainMat);
            if (waterMat)
                UnityEngine.Object.Destroy(waterMat);
            if (skyMaterial)
                UnityEngine.Object.Destroy(skyMaterial);
            if (gateMaterial)
                UnityEngine.Object.Destroy(gateMaterial);
        }
        public override string DiagnosticState => "explorer=" + kind + " letters=" + model.Collected + " score=" + model.Score + " words=" + model.Completed + " position=" + model.Position + " segments=" + segments.Count + " swimmers=" + swimmers.Count;
    }
}








