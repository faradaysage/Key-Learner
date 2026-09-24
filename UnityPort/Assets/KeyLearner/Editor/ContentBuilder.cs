using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KeyLearner.Unity.Editor
{
    // Owns only generated adaptations. ThirdParty files and their UVs, meshes,
    // rigs and original material data are never rewritten by this builder.
    public static class ContentBuilder
    {
        const string Sources = "Assets/ThirdParty/";
        const string Prefabs = "Assets/KeyLearner/Resources/World/";
        const string Materials = "Assets/KeyLearner/Generated/Materials/";
        static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        static readonly List<ContentItem> items = new List<ContentItem>();
        static readonly List<string> report = new List<string>();

        [MenuItem("KeyLearner/Rebuild licensed world content")]
        public static void Build()
        {
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            materialCache.Clear();
            items.Clear();
            report.Clear();
            ConfigureTextures();
            AddPack("QuaterniusNature", n =>
                n.StartsWith("DeadTree_") ? "dead-tree" :
                n.Contains("Tree") || n.StartsWith("Pine_") ? "tree" :
                n.StartsWith("Rock_Medium_") ? "rock" :
                n.StartsWith("RockPath_") ? "path" :
                n.StartsWith("Pebble_") ? "pebble" :
                n.StartsWith("Flower") || n.StartsWith("Mushroom") ? "flower" :
                n.StartsWith("Petal") ? null : "bush");
            // Height-normalized scenery must never contain flat paths or loose
            // building parts. These retain separate width-scaled semantic roles.
            AddPack("KenneySuburban", n =>
                n.StartsWith("building-") ? "house" :
                n.StartsWith("driveway-") ? "driveway" :
                n.StartsWith("path-") ? "path" :
                n.StartsWith("fence") ? "fence" :
                n == "planter" ? "planter" : null);
            AddPack("KenneyCommercial", n =>
                n.StartsWith("building-") ? "commercial" :
                n.StartsWith("detail-parasol") ? "parasol" :
                n.StartsWith("detail-") ? "building-detail" : null);
            AddPack("QuaterniusFarm", n => n == "Fence" ? "fence" : n == "Well" ? "well" : n == "ChickenCoop" ? "farm-small" : n == "Barn" || n == "BigBarn" ? "farm-building" : "landmark");
            AddPack("KenneyCars", n => new[] { "race", "race-future", "hatchback-sports", "sedan-sports", "sedan", "taxi", "tractor", "delivery", "truck" }.Contains(n) ? "car" : n == "cone" || n == "box" ? "road-obstacle" : null);
            AddPack("KenneyRoads", n =>
                n.StartsWith("light-") || n.StartsWith("sign-highway") || n == "road-sign-stop" || n == "road-sign-street" || n == "road-sign-warning" ? "roadfurniture" :
                n.StartsWith("road-sign-") ? "sign-panel" :
                n.StartsWith("construction-") ? "construction" :
                n == "electricity-pole" ? "utility" :
                n == "dumpster" ? "prop" : null);
            AddPack("QuaterniusFish", n => n == "Dolphin" ? "dolphin" : n == "Whale" ? "whale" : n == "Shark" ? "shark" : n.StartsWith("Manta") ? "ray" : "fish");
            AddPack("QuaterniusCuteFish", n => "fish");
            AddPack("PantherOneBird", n => "bird");
            AddPack("QuaterniusAnimals", n => "animal");
            AddPack("QuaterniusDinosaurs", n => "dinosaur");
            AddPack("ChistodrakoPteranodon", n => "pterosaur");
            AddPack("YughuesPalms", n => "tropical-tree");
            AddPack("LasquetiBoat", n => n == "FishingBoat" ? "boat" : n.StartsWith("Spectator") ? "spectator" : "reef-shark");
            AddPack("PolyHavenScenery", n => n.StartsWith("fern") ? "fern" : n.StartsWith("coastal") ? "cliff" : "mountainside");
            AddPack("KenchooPolarBear", n => "polar-bear");
            AddPack("WildMeshAnimals", n => "wolf");
            AddPack("KenneyPirate", n => n == "chest" ? "treasure" : n == "cannon-mobile" ? "cannon" : "prop");
            AddPack("MiniPolyCoral", n => "coral");
            AddPack("MohabinsSeaweed", n => "seaweed");
            // Interleave coral families before palette variants so adjacent choices
            // have different silhouettes, while retaining every authored colorway.
            var coralItems = items.Where(i => i.Category == "coral").OrderBy(i => i.Id.Split(new[] { "__" }, StringSplitOptions.None)[1], StringComparer.Ordinal).ToArray();
            items.RemoveAll(i => i.Category == "coral");
            items.AddRange(coralItems);
            var activePaths = new HashSet<string>(items.Select(i => AssetDatabase.GetAssetPath(i.Prefab)), StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!activePaths.Contains(path))
                    AssetDatabase.DeleteAsset(path);
            }
            string libraryPath = "Assets/KeyLearner/Resources/ContentLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<ContentLibrary>(libraryPath);
            if (!library)
            {
                library = ScriptableObject.CreateInstance<ContentLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }
            library.SetItems(items.ToArray());
            library.GroundDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + "PolyHavenGround/Textures/Ground_Diffuse.png");
            library.GroundNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + "PolyHavenGround/Textures/Ground_Normal.png");
            if (!library.GroundDiffuse || !library.GroundNormal) throw new InvalidOperationException("Licensed ground textures are missing.");
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../artifacts/unity");
            File.WriteAllLines("../artifacts/unity/content-import-report.txt", report);
            Debug.Log("KeyLearner content built: " + items.Count + " licensed prefab adaptations; " + string.Join(", ", items.GroupBy(i => i.Category).Select(g => g.Key + "=" + g.Count())));
        }

        static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Sources.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer)
                    continue;
                bool normal = Path.GetFileNameWithoutExtension(path).EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
                bool palette = path.Contains("Kenney");
                bool changed = false;
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != type)
                {
                    importer.textureType = type;
                    changed = true;
                }
                if (importer.alphaIsTransparency != !normal)
                {
                    importer.alphaIsTransparency = !normal;
                    changed = true;
                }
                if (importer.maxTextureSize != 2048)
                {
                    importer.maxTextureSize = 2048;
                    changed = true;
                }
                if (palette && importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    changed = true;
                }
                if (!palette && importer.filterMode != FilterMode.Trilinear)
                {
                    importer.filterMode = FilterMode.Trilinear;
                    changed = true;
                }
                if (importer.anisoLevel != 4)
                {
                    importer.anisoLevel = 4;
                    changed = true;
                }
                if (changed)
                    importer.SaveAndReimport();
            }
        }

        static void AddPack(string pack, Func<string, string> classify)
        {
            string directory = Sources + pack + "/Models";
            if (!Directory.Exists(directory))
            {
                Debug.LogWarning("Content pack absent: " + pack);
                return;
            }
            foreach (string file in Directory.GetFiles(directory, "*.fbx").OrderBy(p => pack == "KenneyCars" && Path.GetFileNameWithoutExtension(p) == "race" ? "" : p, StringComparer.Ordinal))
            {
                string sourcePath = file.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(sourcePath);
                string category = classify(name);
                if (category == null)
                    continue;
                bool animated = category == "bird" || category == "dolphin" || category == "fish" || category == "whale" || category == "shark" || category == "ray" || category == "animal" || category == "polar-bear" || category == "wolf" || category == "dinosaur" || category == "pterosaur" || category == "boat" || category == "spectator" || category == "reef-shark";
                var importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
                if (importer)
                {
                    bool changed = false;
                    if (importer.importCameras)
                    {
                        importer.importCameras = false;
                        changed = true;
                    }
                    if (importer.importLights)
                    {
                        importer.importLights = false;
                        changed = true;
                    }
                    // Only these three small meshes need runtime surface cooking for coral placement.
                    if (pack == "QuaterniusNature" && category == "rock" && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        changed = true;
                    }
                    if (animated && importer.animationType != ModelImporterAnimationType.Legacy)
                    {
                        importer.animationType = ModelImporterAnimationType.Legacy;
                        importer.importAnimation = true;
                        changed = true;
                    }
                    if (animated)
                    {
                        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                        foreach (var clip in clips)
                        {
                            // Blender's single active-action FBX export names this verified
                            // 1-30 frame flight take Scene. Keep the source take binding.
                            if ((pack == "KenchooPolarBear" || pack == "WildMeshAnimals") && clip.name == "Scene") { clip.name = "Walk"; changed = true; }
                            if (pack == "LasquetiBoat" && clip.name == "Scene") { clip.name = category == "reef-shark" ? "Swim" : "Wave"; changed = true; }
                            if (pack == "PantherOneBird" && clip.name == "Scene")
                            {
                                clip.name = "Flight";
                                changed = true;
                            }
                            if (!clip.loopTime || clip.wrapMode != WrapMode.Loop)
                            {
                                clip.loopTime = true;
                                clip.loopPose = true;
                                clip.wrapMode = WrapMode.Loop;
                                changed = true;
                            }
                        }
                        if (changed && clips.Length > 0)
                            importer.clipAnimations = clips;
                    }
                    if (changed)
                        importer.SaveAndReimport();
                }
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (!source)
                    throw new InvalidOperationException("Missing imported FBX: " + sourcePath);
                // The manual coral FBXs are presentation rows, each containing
                // several complete organisms. Keep each original mesh intact;
                // separate objects, never loose branches within an organism.
                var coralParts = category == "coral" ? source.GetComponentsInChildren<MeshRenderer>(true).OrderBy(r => r.name, StringComparer.Ordinal).ToArray() : Array.Empty<MeshRenderer>();
                int count = category == "coral" ? coralParts.Length : 1;
                if (count == 0)
                    throw new InvalidOperationException("Coral source contains no mesh objects: " + sourcePath);
                for (int variant = 0; variant < count; variant++)
                {
                    string partPath = category == "coral" ? AnimationUtility.CalculateTransformPath(coralParts[variant].transform, source.transform) : null;
                    string suffix = category == "coral" ? "__" + variant.ToString("D2") + "_" + coralParts[variant].name.Replace('.', '_') : "";
                    string id = pack + "_" + name.Replace(' ', '_') + suffix;
                    var root = new GameObject(id);
                    try
                    {
                        // A separate normalization transform cannot be overwritten
                        // by source clips that animate their own root transform.
                        var pivot = new GameObject("Pivot").transform;
                        pivot.SetParent(root.transform, false);
                        var model = UnityEngine.Object.Instantiate(source, pivot);
                        model.name = "Art";
                        if (partPath != null)
                            KeepCoralPart(model, partPath);
                        foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                            UnityEngine.Object.DestroyImmediate(collider);
                        foreach (var camera in model.GetComponentsInChildren<Camera>(true))
                            UnityEngine.Object.DestroyImmediate(camera);
                        foreach (var light in model.GetComponentsInChildren<Light>(true))
                            UnityEngine.Object.DestroyImmediate(light);
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => AdaptMaterial(pack, name, m)).ToArray();
                            renderer.shadowCastingMode = ShadowCastingMode.On;
                            renderer.receiveShadows = true;
                        }
                        ConfigureSourceLods(model);
                        var clip = animated ? ConfigureAnimation(model, sourcePath) : null;
                        if(pack=="LasquetiBoat" && category=="reef-shark")
                        {
                            clip.SampleAnimation(model,0);
                            var bones=model.GetComponentsInChildren<Transform>(true);
                            var head=bones.FirstOrDefault(t=>t.name.StartsWith("Head.",StringComparison.Ordinal));
                            var tail=bones.FirstOrDefault(t=>t.name.StartsWith("Spine7.",StringComparison.Ordinal));
                            if(!head || !tail)throw new InvalidOperationException("Shark orientation landmarks missing.");
                            var forward=head.position-tail.position;forward.y=0;
                            if(forward.sqrMagnitude<.00001f)throw new InvalidOperationException("Shark orientation is degenerate.");
                            pivot.localRotation=Quaternion.FromToRotation(forward.normalized,Vector3.forward);
                            report.Add("ORIENTATION "+sourcePath+" | head="+head.name+" tail="+tail.name+" heading="+forward);
                        }
                        var wheels = model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("wheel-", StringComparison.OrdinalIgnoreCase)).ToArray();
                        var blades = model.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("_Blades", StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (wheels.Length > 0 || blades.Length > 0)
                        {
                            var motion = root.AddComponent<SourcePropMotion>();
                            motion.Wheels = wheels;
                            motion.WindmillBlades = blades;
                            if (wheels.Length > 0)
                            {
                                var wheelRenderer = wheels[0].GetComponent<Renderer>();
                                if (wheelRenderer)
                                    motion.WheelRadius = Mathf.Max(.01f, wheelRenderer.bounds.size.y * .5f);
                            }
                        }
                        Bounds bounds = animated ? AnimatedBounds(model, clip) : BoundsOf(model);
                        pivot.localPosition = -new Vector3(bounds.center.x, animated ? bounds.center.y : bounds.min.y, bounds.center.z);
                        float height = Mathf.Max(.01f, bounds.size.y);
                        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + id + ".prefab");
                        items.Add(new ContentItem { Category = category, Id = id, Prefab = prefab, Height = height, Size = bounds.size, Yaw = 0 });
                        report.Add(id + " | " + category + " | bounds=" + bounds.size.ToString("F3") + " | source=" + sourcePath + (partPath != null ? " | object=" + partPath : ""));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            }
        }

        static void ConfigureSourceLods(GameObject model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();
            var near=renderers.Where(r=>r.name.EndsWith("_LOD1",StringComparison.Ordinal)).ToArray();
            var middle=renderers.Where(r=>r.name.EndsWith("_LOD2",StringComparison.Ordinal)).ToArray();
            var far=renderers.Where(r=>r.name.EndsWith("_LOD3",StringComparison.Ordinal)).ToArray();
            if(near.Length==0 || middle.Length==0 || far.Length==0)return;
            foreach(var old in model.GetComponentsInChildren<LODGroup>())UnityEngine.Object.DestroyImmediate(old);
            var group=model.AddComponent<LODGroup>();
            group.SetLODs(new[]{new LOD(.25f,near),new LOD(.10f,middle),new LOD(.018f,far)});
            group.RecalculateBounds();
        }

        static void KeepCoralPart(GameObject model, string path)
        {
            var keep = string.IsNullOrEmpty(path) ? model.transform : model.transform.Find(path);
            if (!keep || !keep.GetComponent<MeshRenderer>())
                throw new InvalidOperationException("Coral organism path unavailable: " + path);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform == keep)
                    continue;
                // Remove only mesh components; retaining hierarchy avoids deleting
                // a selected child if a later source pack nests mesh objects.
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter)
                    UnityEngine.Object.DestroyImmediate(filter);
                UnityEngine.Object.DestroyImmediate(renderer);
            }
        }

        static AnimationClip ConfigureAnimation(GameObject model, string path)
        {
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                UnityEngine.Object.DestroyImmediate(animator);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            var chosen = clips.FirstOrDefault(c => c.name.IndexOf("Swimming_Normal", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault(c => c.name.IndexOf("Swim", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault(c => c.name.IndexOf("Flight", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault(c => c.name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault(c => c.name.IndexOf("Wave", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!chosen)
                throw new InvalidOperationException("Animated hero/fauna has no verified swim/flight/walk clip: " + path);
            var animation = model.GetComponent<Animation>();
            if (!animation)
                animation = model.AddComponent<Animation>();
            foreach (var available in clips)
                animation.AddClip(available, available.name);
            animation.clip = chosen;
            animation.wrapMode = WrapMode.Loop;
            animation.playAutomatically = true;
            animation.cullingType = AnimationCullingType.BasedOnRenderers;
            report.Add("ANIMATION " + path + " | " + chosen.name + " | seconds=" + chosen.length.ToString("F3"));
            return chosen;
        }

        static Bounds AnimatedBounds(GameObject model, AnimationClip clip)
        {
            // Imported renderer bounds can include oversized culling envelopes.
            // Measure actual deformed vertices throughout the gentle locomotion
            // cycle instead. BakeMesh is editor-only here, never a frame cost.
            Bounds bounds = default;
            var cullingBounds = new Dictionary<SkinnedMeshRenderer, Bounds>();
            for (int sample = 0; sample < 8; sample++)
            {
                clip.SampleAnimation(model, clip.length * sample / 8f);
                var frame = BoundsOf(model, cullingBounds);
                if (sample == 0)
                    bounds = frame;
                else
                    bounds.Encapsulate(frame);
            }
            clip.SampleAnimation(model, 0);
            foreach (var entry in cullingBounds)
            {
                var local = entry.Value;
                local.Expand(local.size * .1f);
                entry.Key.localBounds = local;
            }
            return bounds;
        }

        static Bounds BoundsOf(GameObject go, Dictionary<SkinnedMeshRenderer, Bounds> cullingBounds = null)
        {
            bool started = false;
            Bounds bounds = default;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var mesh = new Mesh();
                    try
                    {
                        // true compensates the renderer scale. false includes
                        // scale in vertices and would apply it twice below, making
                        // source FBX rigs normalize to almost invisible animals.
                        skinned.BakeMesh(mesh, true);
                        var cullingFrame = skinned.rootBone ? skinned.rootBone : skinned.transform;
                        foreach (var vertex in mesh.vertices)
                        {
                            var point = skinned.transform.TransformPoint(vertex);
                            if (cullingBounds != null)
                            {
                                var localPoint = cullingFrame.InverseTransformPoint(point);
                                if (cullingBounds.TryGetValue(skinned, out var local))
                                {
                                    local.Encapsulate(localPoint);
                                    cullingBounds[skinned] = local;
                                }
                                else
                                    cullingBounds[skinned] = new Bounds(localPoint, Vector3.zero);
                            }
                            if (!started)
                            {
                                bounds = new Bounds(point, Vector3.zero);
                                started = true;
                            }
                            else
                                bounds.Encapsulate(point);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(mesh); }
                }
                else
                {
                    if (!started)
                    {
                        bounds = renderer.bounds;
                        started = true;
                    }
                    else
                        bounds.Encapsulate(renderer.bounds);
                }
            }
            if (!started)
                throw new InvalidOperationException("Model has no measurable geometry: " + go.name);
            return bounds;
        }

        static Material AdaptMaterial(string pack, string model, Material original)
        {
            string originalName = original ? original.name : "Default";
            // Original Kenney palette variations retain the authored UV color regions.
            int suburbanPalette = pack == "KenneySuburban" && model.StartsWith("building-type-", StringComparison.Ordinal) ? (model[model.Length - 1] - 'a') % 4 : 0;
            string paletteKey = suburbanPalette > 0 ? "Palette" + suburbanPalette + "_" : "";
            // Fish and coral reuse generic slot names with genuinely different colors.
            string key = pack + "_" + paletteKey + (pack == "QuaterniusNature" || pack == "MiniPolyCoral" || pack.StartsWith("Kenney") ? "" : model + "_") + originalName;
            key = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            if (materialCache.TryGetValue(key, out var cached))
                return cached;
            string path = Materials + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                throw new InvalidOperationException("URP Lit shader unavailable");
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
                material.shader = shader;
            Color color = original && original.HasProperty("_Color") ? original.color : Color.white;
            Texture texture = original ? original.mainTexture : null;
            Texture normal = null;
            bool cutout = false;
            if (pack.StartsWith("Kenney"))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Models/Textures/colormap.png");
                color = Color.white;
            }
            if (suburbanPalette > 0)
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/PaletteVariants/variation-" + (char)('a' + suburbanPalette - 1) + ".png");
            if (pack == "PantherOneBird")
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/Bird.png");
                color = Color.white;
            }
            if (pack == "KenchooPolarBear" || pack == "WildMeshAnimals")
            {
                string animal = pack == "KenchooPolarBear" ? "PolarBear" : "Wolf";
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/" + animal + "_Diffuse.png");
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/" + animal + "_Normal.png");
                color = Color.white;
            }
            if (pack == "ChistodrakoPteranodon" || pack == "YughuesPalms" || pack == "LasquetiBoat")
            {
                string stem=pack=="ChistodrakoPteranodon"?"Pteranodon":pack=="YughuesPalms"?"Palm":System.Text.RegularExpressions.Regex.Replace(originalName,@"[^a-zA-Z0-9_-]","_");
                var diffuse=AssetDatabase.LoadAssetAtPath<Texture2D>(Sources+pack+"/Textures/"+stem+"_Diffuse.png");
                if(diffuse){texture=diffuse;color=Color.white;}
                else if(pack!="LasquetiBoat")throw new InvalidOperationException("Required authored texture missing: "+pack+"/"+stem);
                normal=AssetDatabase.LoadAssetAtPath<Texture2D>(Sources+pack+"/Textures/"+stem+"_Normal.png");
                cutout=pack=="YughuesPalms";
            }
            if (pack == "PolyHavenScenery")
            {
                string stem=model.StartsWith("fern")?"fern_02":model;
                texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Sources+pack+"/Textures/"+stem+"_Diffuse.png");
                normal=AssetDatabase.LoadAssetAtPath<Texture2D>(Sources+pack+"/Textures/"+stem+"_Normal.png");
                cutout=model.StartsWith("fern");color=Color.white;
            }
            if (pack == "QuaterniusNature")
            {
                string textureName = originalName;
                if (textureName == "Leaves_Pine")
                    textureName = "Leaf_Pine_C";
                else if (textureName.StartsWith("Leaves_NormalTree"))
                    textureName = "Leaves_NormalTree_C";
                else if (textureName.StartsWith("Leaves_TwistedTree"))
                    textureName = "Leaves_TwistedTree_C";
                else if (textureName == "Rocks")
                    textureName = "Rocks_Diffuse";
                else if (textureName == "PathRocks")
                    textureName = "PathRocks_Diffuse";
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/" + textureName + ".png") ?? texture;
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/" + originalName + "_Normal.png");
                cutout = originalName.Contains("Leav") || originalName.Contains("Grass") || originalName.Contains("Flower") || originalName.Contains("Plant") || originalName.Contains("Fern") || originalName.Contains("Clover");
                color = Color.white;
            }
            if (pack == "MiniPolyCoral")
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/CoralReefSet1/BaseColor.png");
                color = Color.white;
            }
            if (pack == "MohabinsSeaweed")
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/Water Gradients.png");
                color = Color.white;
            }
            if (pack == "MiniPolyCoral")
            {
                material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + pack + "/Textures/CoralReefSet1/Emission.png"));
                material.SetColor("_EmissionColor", Color.white * .15f);
                material.EnableKeyword("_EMISSION");
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_Smoothness", pack == "QuaterniusFish" ? .38f : pack.StartsWith("Kenney") ? .24f : .1f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Surface", 0);
            material.SetFloat("_AlphaClip", cutout ? 1 : 0);
            material.SetFloat("_Cutoff", .42f);
            material.SetFloat("_Cull", cutout || pack == "MohabinsSeaweed" ? (float)CullMode.Off : (float)CullMode.Back);
            material.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            if (cutout)
                material.EnableKeyword("_ALPHATEST_ON");
            else
                material.DisableKeyword("_ALPHATEST_ON");
            if (normal)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");
            material.enableInstancing = true;
            material.doubleSidedGI = cutout;
            EditorUtility.SetDirty(material);
            materialCache.Add(key, material);
            return material;
        }
    }
}
