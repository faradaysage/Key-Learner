using System;
using System.Collections.Generic;
using System.Linq;
using KeyLearner.Studio;
using UnityEngine;
using UnityEngine.Rendering;

namespace KeyLearner.Unity
{
    /// <summary>World-anchored arrangements of licensed scenery, independent of explorer physics.</summary>
    public static class LandEnvironmentComposer
    {
        public static void Populate(GameServices services, Transform parent, float start, int index, bool race, float centerX)
        {
            new Chapter(services, parent, start, index, race, centerX).Compose();
        }

        sealed class Chapter
        {
            readonly GameServices services;
            readonly Transform parent;
            readonly float start, centerX;
            readonly int index, variation;
            readonly bool race;
            readonly Region region;
            readonly System.Random random;
            readonly List<Vector3> reserved = new List<Vector3>(); // X/Z center and radius in Y.
            readonly Dictionary<string, int[]> selections = new Dictionary<string, int[]>();
            int treeCount, detailCount;
            const int TreeBudget = 88, DetailBudget = 72;

            public Chapter(GameServices services, Transform parent, float start, int index, bool race, float centerX)
            {
                this.services = services;
                this.parent = parent;
                this.start = start;
                this.index = index;
                this.race = race;
                this.centerX = centerX;
                variation = Mod(index, 6);
                region = ExplorerWorld.Area(start - 80);
                random = new System.Random(unchecked(index * 7919 + Mathf.RoundToInt(centerX) * 3571 + 153));
            }

            public void Compose()
            {
                // Major features are reserved first; vegetation fills their surroundings.
                switch (region)
                {
                    case Region.City:
                        City();
                        break;
                    case Region.Town:
                        Village();
                        break;
                    case Region.Lakes:
                        Lakeshore();
                        break;
                    case Region.River:
                        Riverbank();
                        break;
                    case Region.Mountains:
                        MountainPass();
                        break;
                    case Region.Tundra:
                        Tundra();
                        break;
                    default:
                        Forest();
                        break;
                }
                DistantVegetation();
            }

            void Forest()
            {
                if (variation == 2)
                    Farm(-1, 84, 116);
                if (variation == 4)
                    RockOutcrop(1, 82, 145, 15);
                // Long irregular edges connect across chapter seams. Clearings belong
                // to individual chapters; the world never becomes a repeated tree avenue.
                WoodlandEdge(-1, 62, 29, 22, 31, variation == 2 ? 84 : variation == 3 ? 106 : -1);
                WoodlandEdge(1, 68, 32, 22, 35, variation == 4 ? 82 : variation == 1 ? 42 : -1);
                WoodlandEdge(variation % 2 == 0 ? 1 : -1, 153, 48, 16, 38, -1, true);
                if (variation == 1 || variation == 3)
                    Meadow(-1, 105, 61, 16);
                if (variation == 2 || variation == 5)
                    BirdFlock(variation == 2 ? 1 : -1, 108);
            }

            void Lakeshore()
            {
                // Submerged placement rejection preserves the actual shoreline and
                // makes the low, open water view alternate with a wooded headland.
                if (variation == 1 || variation == 4)
                    Overlook(variation == 1 ? 1 : -1, 85, 80);
                if (variation == 3)
                    Farm(-1, 93, 265);
                WoodlandEdge(-1, 88, 38, 23, 28, variation == 1 ? 85 : -1);
                WoodlandEdge(1, 162, 46, 24, 33, variation == 4 ? 85 : -1, true);
                Understory(-1, 107, 71, 12);
                RockOutcrop(1, 55, 210, 9);
                if (variation == 1 || variation == 4)
                    BirdFlock(1, 82);
            }

            void MountainPass()
            {
                RockOutcrop(-1, 43, 127, 16);
                RockOutcrop(1, 116, 207, 21);
                Grove(-1, 104, 79, 8, 23, "Pine_", 25);
                Grove(1, 42, 163, 10, 25, "Pine_", 32);
                Grove(-1, 59, 304, 7, 26, "Pine_", 29);
                if (variation == 2 || variation == 5)
                    Overlook(1, 86, 65);
            }

            void Tundra()
            {
                RockOutcrop(-1, 55, 128, 8);
                Grove(1, 117, 137, 7, 24, "Pine_", 17);
                Grove(-1, 25, 245, 5, 30, "Pine_", 20);
                if (variation == 1)
                    Height("dead-tree", index, Route(start - 80) - 95, start - 80, 12, 25);
                if (variation == 4)
                    Farm(1, 90, 106);
                Understory(-1, 121, 72, 5);
            }

            void Village()
            {
                int side = variation % 2 == 0 ? -1 : 1;
                StreetPath(-1, 26, 6);
                StreetPath(1, 26, 6);
                Courtyard(side, 80, 46, 4);
                Courtyard(-side, 82, 54, 3);
                if (variation == 2 || variation == 5)
                    Farm(side, 109, 241);
                Grove(-side, 34, 173, 8, 23, "CommonTree_", 24);
                Grove(side, 131, 192, 9, 25, "CommonTree_", 29);
                if (race)
                    StreetFurniture(false);
            }

            void City()
            {
                int side = variation % 2 == 0 ? 1 : -1;
                StreetPath(-1, 27, 8);
                StreetPath(1, 27, 8);
                var commercial = services.Content.Category("commercial");
                if (commercial.Length == 0)
                    return;
                // A connected frontage follows one building line. Each real model's
                // footprint determines its setback and maximum width along the street.
                for (int block = 0; block < 4; block++)
                {
                    float z = start - 20 - block * 40;
                    int itemIndex = Selected("commercial", "KenneyCommercial_building-" + (char)('a' + Mod(index * 3 + block, 14)), 0, true);
                    var item = commercial[itemIndex];
                    float height = Mathf.Min(24 + Mod(index + block, 3) * 5, 32 * item.Height / Mathf.Max(.01f, item.Size.x));
                    float halfDepth = item.Size.z * height / item.Height * .5f;
                    float x = Route(z) + side * (35 + halfDepth);
                    if (!Height("commercial", itemIndex, x, z, height, FaceRoute(z, side), true))
                        continue;
                    PathBetween("path", Selected("path", "KenneySuburban_path-long", 0, true), 7, Route(z) + side * 30, z, x - side * halfDepth, z);
                    Height("planter", block, Route(z) + side * 32.5f, z + 12, 2, 0);
                    if (block % 2 == 0)
                        Height("car", Selected("car", "KenneyCars_taxi", 0, true), Route(z) + side * 36, z - 13, 2.2f, FaceRoute(z, side) + 90);
                }
                Courtyard(-side, 80, 47, 3);
                Grove(-side, 43, 122, 8, 20, "CommonTree_", 25);
                for (int tower = 0; tower < 3; tower++)
                {
                    float z = start - 28 - tower * 48;
                    int item = Selected("commercial", "_building-skyscraper", index + tower);
                    Height("commercial", item, Route(z) + side * (164 + tower * 24), z, 48 + tower * 13, FaceRoute(z, side), true, true);
                }
                if (race)
                    StreetFurniture(true);
            }

            void Riverbank()
            {
                int side = variation % 2 == 0 ? -1 : 1;
                Grove(side, 40, 126, 10, 27, "TwistedTree_", 29);
                Grove(-side, 122, 195, 9, 25, "CommonTree_", 25);
                RockOutcrop(-side, 68, 94, 8);
                Understory(side, 106, 72, 14);
                if (variation == 1 || variation == 4)
                    Overlook(-side, 93, 64);
                if (variation == 3)
                    Farm(side, 79, 231);
            }

            void Courtyard(int side, float depth, float setback, int houses)
            {
                var houseItems = services.Content.Category("house");
                if (houseItems.Length == 0)
                    return;
                float centerZ = start - depth;
                for (int house = 0; house < houses; house++)
                {
                    float z = Mathf.Clamp(centerZ + (house - (houses - 1) * .5f) * 32, start - 142, start - 18);
                    float x = Route(z) + side * (setback + (house % 2) * 5);
                    int selected = Mod(index * 5 + house, houseItems.Length);
                    var item = houseItems[selected];
                    float height = 11.5f + house * 1.2f;
                    float halfDepth = item.Size.z * height / item.Height * .5f;
                    if (!Height("house", selected, x, z, height, FaceRoute(z, side), true))
                        continue;
                    float doorX = x - side * halfDepth;
                    PathBetween("driveway", house, 6, Route(z) + side * 22.5f, z, doorX, z);
                    Height("planter", 0, doorX - side * 3, z + 9, 1.5f, 0);
                    if ((house + variation) % 2 == 0)
                        Height("car", Selected("car", house % 2 == 0 ? "KenneyCars_sedan" : "KenneyCars_delivery", 0, true), doorX - side * 4, z, house % 2 == 0 ? 2.1f : 2.7f, FaceRoute(z, side) + 180);
                    FenceLine(x - side * (halfDepth + 3), z + 15, x + side * (halfDepth + 5), z + 15, false);
                    Height("bush", Selected("bush", "Bush_Common", house), Route(z) + side * 33, z - 12, 2.4f, house * 37);
                    Height("flower", Selected("flower", "_Group", house), Route(z) + side * 36, z + 11, 1.5f, house * 53);
                }
                float courtX = Route(centerZ) + side * (setback + 27);
                MeadowAt(courtX, centerZ, 8);
                Grove(side, depth, setback + 52, 6, 24, "CommonTree_", 20);
            }

            void Farm(int side, float depth, float setback)
            {
                float z = start - depth, x = Route(z) + side * setback;
                if (!Height("farm-building", variation, x, z, 13 + variation % 2 * 3, FaceRoute(z, side), true))
                    return;
                Height("farm-small", 0, x + side * 26, z + 26, 4.2f, FaceRoute(z, side), true);
                Height("well", 0, x - side * 24, z + 23, 4.6f, 20);
                Height("landmark", variation, x + side * 35, z - 30, 23 + variation % 3 * 5, FaceRoute(z, side), true);
                Width("driveway", 0, x - side * 17, z, 10, FaceRoute(z, side));
                Height("car", Selected("car", "KenneyCars_tractor", 0, true), x - side * 24, z - 6, 3.1f, FaceRoute(z, side) + 155);
                FenceLine(x - side * 21, z + 39, x + side * 39, z + 39, true);
                FenceLine(x + side * 42, z + 36, x + side * 42, z - 16, true);
                // A small planted garden has intentional rows and open paths through it.
                for (int row = 0; row < 3; row++)
                    for (int plant = 0; plant < 4; plant++)
                        Height("flower", row % 4, x - side * (13 + row * 4), z - 17 - plant * 4, 1.15f, random.Next(360));
                Grove(side, depth + 31, setback + 83, 8, 22, "CommonTree_", 28);
            }

            void Overlook(int side, float depth, float setback)
            {
                float z = start - depth, x = Route(z) + side * setback;
                Height("rock", variation, x + side * 22, z - 16, 6, 45);
                Height("parasol", variation, x, z, 4.2f, random.Next(360), true);
                Height("planter", 0, x - side * 9, z + 7, 1.7f, 0);
                Width("path", Selected("path", "KenneySuburban_path-long", 0), x - side * 13, z, 3.4f, FaceRoute(z, side));
                FenceLine(x + side * 8, z + 18, x + side * 8, z - 18, false);
                MeadowAt(x + side * 19, z + 11, 9);
            }

            void StreetFurniture(bool city)
            {
                for (int i = 0; i < (city ? 3 : 2); i++)
                {
                    float z = start - 22 - i * (city ? 52 : 89);
                    int side = (i + variation) % 2 == 0 ? -1 : 1;
                    int item = Selected("roadfurniture", city ? "_light-" : "_road-sign-", index + i);
                    Height("roadfurniture", item, Route(z) + side * 29, z, city ? 10 : 4, FaceRoute(z, side));
                }
            }

            void RockOutcrop(int side, float depth, float setback, float height)
            {
                float z = start - depth, x = Route(z) + side * setback;
                for (int rock = 0; rock < 3; rock++)
                    Height("rock", rock + index, x + side * rock * height * .64f, z + rock * 9 - 10, height * (1 - rock * .19f), random.Next(360));
            }

            void WoodlandEdge(int side, float setback, float width, int count, float height, float clearingDepth, bool middle = false)
            {
                for (int tree = 0; tree < count; tree++)
                {
                    float depth = 8 + (tree + Range(.1f, .9f)) * 144 / count;
                    if (clearingDepth > 0 && Mathf.Abs(depth - clearingDepth) < 23)
                        continue;
                    float z = start - depth;
                    // The curve uses world coordinates to flow naturally through the
                    // streaming boundary; lateral jitter breaks a visible planted row.
                    float curve = Mathf.Sin(z * .008f + side * 1.7f + centerX * .001f) * 17;
                    float x = Route(z) + side * (setback + curve + Range(-width, width));
                    string family = tree % 11 == 0 ? "TwistedTree_" : tree % 4 == 0 ? "Pine_" : "CommonTree_";
                    float canopy = height * Range(.63f, 1.34f) * (tree % 13 == 0 ? 1.22f : 1);
                    bool placed = Height("tree", Selected("tree", family, index * 7 + tree), x, z, canopy, random.Next(360), false, middle);
                    if (placed && !middle && tree % 2 == 0)
                        WoodlandFloor(x, z, tree);
                }
            }

            void WoodlandFloor(float x, float z, int seed)
            {
                int shrub = Selected("bush", "Bush_Common", seed);
                Height("bush", shrub, x + Range(-7, 7), z + Range(-5, 5), Range(2.6f, 4.8f), random.Next(360));
                if (seed % 4 == 0)
                {
                    Height("bush", Selected("bush", "Fern_", seed), x - 6, z + 5, Range(1.4f, 2.4f), random.Next(360));
                    Height("flower", Selected("flower", "_Group", seed), x + 8, z - 3, Range(1.2f, 2), random.Next(360));
                }
                if (seed % 6 == 0)
                    Height("rock", seed + index, x + 9, z + 5, Range(2.3f, 5.3f), random.Next(360));
            }

            void Grove(int side, float depth, float setback, int count, float radius, string family, float height, bool far = false)
            {
                float z = start - depth, x = Route(z) + side * setback;
                float phase = Range(0, Mathf.PI * 2), stretch = Range(.64f, 1.65f);
                for (int tree = 0; tree < count; tree++)
                {
                    float angle = phase + tree * 2.399963f;
                    float r = radius * Mathf.Sqrt((tree + .5f) / count);
                    float px = x + Mathf.Cos(angle) * r * stretch + Range(-4, 4);
                    float pz = z + Mathf.Sin(angle) * r / stretch + Range(-4, 4);
                    string species = family;
                    if (region != Region.Tundra && region != Region.Mountains)
                        species = tree % 5 == 0 ? "Pine_" : tree % 7 == 0 ? "TwistedTree_" : family;
                    int item = Selected("tree", species, index + tree + variation);
                    float canopy = height * Range(.58f, 1.38f) * (tree % 8 == 0 ? 1.13f : 1);
                    if (Height("tree", item, px, pz, canopy, random.Next(360), false, far) && !far && tree % 3 == 0)
                        WoodlandFloor(px, pz, tree);
                }
            }

            void DistantVegetation()
            {
                string family = region == Region.Mountains || region == Region.Tundra ? "Pine_" : region == Region.River ? "TwistedTree_" : "CommonTree_";
                int count = region == Region.City ? 5 : region == Region.Forest || region == Region.Lakes || region == Region.Tundra ? 6 : 9;
                Grove(-1, 32 + variation * 3, 323 + variation * 11, count, 50, family, region == Region.Tundra ? 22 : 39, true);
                Grove(1, 117 - variation * 4, 416 - variation * 9, count + 2, 59, family, region == Region.Mountains ? 43 : 42, true);
            }

            void BirdFlock(int side, float setback)
            {
                int count = 3 + variation % 3;
                for (int bird = 0; bird < count; bird++)
                {
                    float z = start - 80 - bird * 7, x = Route(z) + side * (setback + bird * 5);
                    var center = new Vector3(x, Mathf.Max(8, ExplorerWorld.Land(x, z, race)) + 58 + bird * 3, z);
                    var go = services.Content.Spawn("bird", 0, parent, center, 1.4f + bird * .13f);
                    if (!go)
                        continue;
                    Finish(go, true);
                    go.AddComponent<SceneryFlight>().Configure(services, center, 27 + bird * 3, index * .7f + bird * .45f, .15f + bird * .009f, services.Content.Category("bird")[0].Yaw);
                }
            }

            void Understory(int side, float depth, float setback, int count)
            {
                float z = start - depth, x = Route(z) + side * setback;
                for (int i = 0; i < count; i++)
                    Height("bush", Selected("bush", i % 3 == 0 ? "Fern_" : "Bush_Common", i + index), x + Range(-21, 21), z + Range(-22, 22), Range(1.6f, 3.6f), random.Next(360));
            }
            void Meadow(int side, float depth, float setback, int count)
            {
                float z = start - depth;
                MeadowAt(Route(z) + side * setback, z, count);
            }
            void MeadowAt(float x, float z, int count)
            {
                for (int i = 0; i < count; i++)
                    Height("flower", i % 4, x + Range(-16, 16), z + Range(-14, 14), Range(.8f, 1.65f), random.Next(360));
            }

            void StreetPath(int side, float setback, float width)
            {
                int path = Selected("path", "KenneySuburban_path-long", 0, true);
                for (int stretch = 0; stretch < 4; stretch++)
                {
                    float z1 = start - stretch * 40, z2 = z1 - 40;
                    PathBetween("path", path, width, Route(z1) + side * setback, z1, Route(z2) + side * setback, z2);
                }
            }

            void PathBetween(string category, int itemIndex, float width, float x1, float z1, float x2, float z2)
            {
                var entries = services.Content.Category(category);
                if (entries.Length == 0)
                    return;
                var item = entries[Mod(itemIndex, entries.Length)];
                Vector2 delta = new Vector2(x2 - x1, z2 - z1);
                float sourceAspect = item.Size.z / Mathf.Max(.001f, item.Size.x);
                float length = width * sourceAspect;
                int count = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / length));
                // A small uniform adjustment joins exact endpoints without stretching
                // source proportions or accidentally making thin paving into a slab.
                float tileWidth = delta.magnitude / count / Mathf.Max(.001f, sourceAspect);
                float yaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
                for (int i = 0; i < count; i++)
                {
                    float t = (i + .5f) / count;
                    Width(category, itemIndex, Mathf.Lerp(x1, x2, t), Mathf.Lerp(z1, z2, t), tileWidth * 1.025f, yaw);
                }
            }

            void FenceLine(float x1, float z1, float x2, float z2, bool farm)
            {
                Vector2 delta = new Vector2(x2 - x1, z2 - z1);
                int itemIndex = Selected("fence", farm ? "QuaterniusFarm_Fence" : "KenneySuburban_fence", 0, true);
                var fences = services.Content.Category("fence");
                if (fences.Length == 0)
                    return;
                var item = fences[itemIndex];
                float span = (farm ? 2.1f : 1.9f) * item.Size.x / Mathf.Max(.001f, item.Height);
                int count = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / span));
                float width = delta.magnitude / count;
                float yaw = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg;
                for (int i = 0; i < count; i++)
                {
                    float t = (i + .5f) / count;
                    Width("fence", itemIndex, Mathf.Lerp(x1, x2, t), Mathf.Lerp(z1, z2, t), width, yaw);
                }
            }

            bool Height(string category, int itemIndex, float x, float z, float height, float yaw, bool reserve = false, bool far = false)
            {
                bool tree = category == "tree" || category == "dead-tree";
                bool detail = category == "bush" || category == "flower";
                if ((tree && treeCount >= TreeBudget) || (detail && detailCount >= DetailBudget))
                    return false;
                var entries = services.Content.Category(category);
                if (entries.Length == 0)
                    return false;
                var item = entries[Mod(itemIndex, entries.Length)];
                float scale = height / Mathf.Max(.001f, item.Height);
                float radius = Mathf.Max(item.Size.x, item.Size.z) * scale * .5f;
                float y = Ground(x, z, radius, reserve, category == "car" || category == "planter");
                if (float.IsNaN(y))
                    return false;
                var go = services.Content.Spawn(category, Mod(itemIndex, entries.Length), parent, new Vector3(x, y - (category == "tree" ? .65f : .25f), z), height, yaw);
                if (go && category == "rock")
                {
                    float step = Mathf.Clamp(radius * .5f, 2, 12);
                    float dx = ExplorerWorld.Land(x + step, z, race) - ExplorerWorld.Land(x - step, z, race);
                    float dz = ExplorerWorld.Land(x, z + step, race) - ExplorerWorld.Land(x, z - step, race);
                    Vector3 normal = new Vector3(-dx, step * 2, -dz).normalized;
                    go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal) * go.transform.localRotation;
                    go.transform.localPosition -= Vector3.up * height * .22f;
                }
                if (go && category == "car" && (region == Region.City || region == Region.Town))
                    go.transform.localPosition += Vector3.up * .55f;
                Finish(go, far);
                if (go)
                {
                    if (tree)
                        treeCount++;
                    if (detail)
                        detailCount++;
                }
                if (reserve)
                    reserved.Add(new Vector3(x, radius + 3, z));
                return go;
            }

            void Width(string category, int itemIndex, float x, float z, float width, float yaw)
            {
                float y = Ground(x, z, width * .5f, false, true);
                if (float.IsNaN(y))
                    return;
                var go = services.Content.SpawnWidth(category, itemIndex, parent, new Vector3(x, y + .035f, z), width, yaw);
                if (go && (category == "path" || category == "driveway"))
                {
                    float step = Mathf.Max(1, width * .5f);
                    float dx = ExplorerWorld.Land(x + step, z, race) - ExplorerWorld.Land(x - step, z, race);
                    float dz = ExplorerWorld.Land(x, z + step, race) - ExplorerWorld.Land(x, z - step, race);
                    go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(-dx, step * 2, -dz).normalized) * go.transform.localRotation;
                }
                Finish(go, false);
            }

            float Ground(float x, float z, float radius, bool structure, bool groundDetail = false)
            {
                if (z > start - 3 || z < start - 157)
                    return float.NaN;
                float y = ExplorerWorld.Land(x, z, race);
                if (y < 3.2f)
                    return float.NaN;
                float routeDistance = Mathf.Abs(x - ExplorerWorld.Road(z));
                if (race && routeDistance < 22 + (groundDetail ? 0 : Mathf.Min(radius, 24)))
                    return float.NaN;
                if (!groundDetail)
                    foreach (var area in reserved)
                        if (new Vector2(x - area.x, z - area.z).sqrMagnitude < Mathf.Pow(area.y + radius * .35f, 2))
                            return float.NaN;
                if (structure)
                {
                    float a = ExplorerWorld.Land(x - radius * .7f, z, race), b = ExplorerWorld.Land(x + radius * .7f, z, race);
                    float c = ExplorerWorld.Land(x, z - radius * .7f, race), d = ExplorerWorld.Land(x, z + radius * .7f, race);
                    if (Mathf.Min(a, b, c, d) < 3 || Mathf.Max(a, b, c, d) - Mathf.Min(a, b, c, d) > 3.5f)
                        return float.NaN;
                }
                return y;
            }

            void Finish(GameObject go, bool far)
            {
                if (!go)
                    return;
                foreach (var motion in go.GetComponentsInChildren<SourcePropMotion>())
                    motion.MotionScale = services.Settings.GentleMotion ? .3f : 1;
                if (far)
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            int Selected(string category, string namePart, int choice, bool exact = false)
            {
                string key = category + ":" + namePart + ":" + exact;
                if (!selections.TryGetValue(key, out var matches))
                {
                    matches = services.Content.Category(category).Select((item, i) => new { item, i }).Where(p => exact ? p.item.Id == namePart : p.item.Id.Contains(namePart)).Select(p => p.i).ToArray();
                    selections[key] = matches;
                }
                return matches.Length == 0 ? 0 : matches[Mod(choice, matches.Length)];
            }
            float Route(float z)
            {
                return race ? ExplorerWorld.Road(z) : centerX + ExplorerWorld.Valley(z) * .35f;
            }
            float FaceRoute(float z, int side)
            {
                float slope = (Route(z + 4) - Route(z - 4)) / 8;
                return Mathf.Atan2(-side, side * slope) * Mathf.Rad2Deg;
            }
            float Range(float min, float max)
            {
                return min + (float)random.NextDouble() * (max - min);
            }
            static int Mod(int value, int count)
            {
                return (int)(((long)value % count + count) % count);
            }
        }
    }
}
