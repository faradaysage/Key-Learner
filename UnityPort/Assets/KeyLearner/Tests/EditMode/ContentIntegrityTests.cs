using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace KeyLearner.Unity.Tests.EditMode
{
    public sealed class ContentIntegrityTests
    {
        [Test]
        public void BuildSceneKeepsItsSuiteBindingWithoutStartingPlatformServices()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            const string path = "Assets/KeyLearner/Scenes/KeyLearner.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var suites = roots.SelectMany(r => r.GetComponentsInChildren<Suite>(true)).ToArray();
                Assert.That(suites.Length, Is.EqualTo(1));
                Assert.That(suites[0].Services, Is.Null, "EditMode inspection must never start the production services.");
                foreach (var root in roots)
                    Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).All(b => b), Is.True, root.name + " has a missing script");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("bird")]
        [TestCase("car")]
        [TestCase("dolphin")]
        [TestCase("fish")]
        [TestCase("shark")]
        [TestCase("whale")]
        [TestCase("tree")]
        [TestCase("house")]
        [TestCase("landmark")]
        [TestCase("coral")]
        [TestCase("seaweed")]
        [TestCase("animal")]
        [TestCase("polar-bear")]
        [TestCase("wolf")]
        [TestCase("dinosaur")]
        [TestCase("pterosaur")]
        [TestCase("tropical-tree")]
        [TestCase("fern")]
        [TestCase("cliff")]
        [TestCase("mountainside")]
        [TestCase("boat")]
        [TestCase("spectator")]
        [TestCase("treasure")]
        [TestCase("road-obstacle")]
        [TestCase("cannon")]
        [TestCase("reef-shark")]
        public void ExplorerCatalogContainsRequiredAuthoredContent(string category)
        {
            var library = Resources.Load<ContentLibrary>("ContentLibrary");
            Assert.That(library, Is.Not.Null, "The checked-in content library must survive a fresh import.");
            Assert.That(library.Category(category), Is.Not.Empty, category);
        }

        [Test]
        public void ImportedPrefabsHaveValidMeshesMaterialsAndNormalizationMetadata()
        {
            var library = Resources.Load<ContentLibrary>("ContentLibrary");
            Assert.That(library, Is.Not.Null);
            Assert.That(library.Items.Length, Is.GreaterThan(0));
            Assert.That(library.Items.Select(i => i.Id).Distinct().Count(), Is.EqualTo(library.Items.Length));
            foreach (var item in library.Items)
            {
                Assert.That(item.Prefab, Is.Not.Null, item.Id);
                Assert.That(item.Category, Is.Not.Empty, item.Id);
                Assert.That(item.Height, Is.GreaterThan(0).And.LessThan(float.PositiveInfinity), item.Id);
                Assert.That(item.Size.x, Is.GreaterThan(0).And.LessThan(float.PositiveInfinity), item.Id);
                var renderers = item.Prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, item.Id);
                Assert.That(item.Prefab.GetComponentsInChildren<MonoBehaviour>(true).All(b => b), Is.True, item.Id + " has a missing script after the assembly split");
                Assert.That(renderers.Any(r => r is SkinnedMeshRenderer s ? s.sharedMesh && s.sharedMesh.vertexCount > 0 :
                    r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0), Is.True, item.Id + " has no visible source geometry");
                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterials, Is.Not.Empty, item.Id + "/" + renderer.name);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, item.Id + "/" + renderer.name);
                        Assert.That(material.shader, Is.Not.Null, item.Id + "/" + material.name);
                        Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), item.Id);
                        // shader.isSupported is deliberately not used: GameCI can use a null graphics device.
                    }
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        Assert.That(skin.sharedMesh, Is.Not.Null, item.Id);
                        Assert.That(skin.sharedMesh.vertexCount, Is.GreaterThan(0), item.Id);
                        Assert.That(skin.bones, Is.Not.Empty, item.Id);
                        Assert.That(skin.bones.All(b => b), Is.True, item.Id + " has a missing bone reference");
                    }
                    else if (renderer is MeshRenderer)
                    {
                        var mesh = renderer.GetComponent<MeshFilter>();
                        Assert.That(mesh, Is.Not.Null, item.Id);
                        Assert.That(mesh.sharedMesh, Is.Not.Null, item.Id);
                        // Some source rigs include an empty auxiliary MeshRenderer; the prefab-level geometry check covers visibility.
                    }
                }
            }
        }

        [TestCase("bird")]
        [TestCase("dolphin")]
        [TestCase("fish")]
        [TestCase("shark")]
        [TestCase("animal")]
        [TestCase("polar-bear")]
        [TestCase("wolf")]
        [TestCase("dinosaur")]
        [TestCase("pterosaur")]
        [TestCase("boat")]
        [TestCase("spectator")]
        [TestCase("reef-shark")]
        public void FlyingAndSwimmingContentHasPlayableLoopingSourceAnimation(string category)
        {
            var library = Resources.Load<ContentLibrary>("ContentLibrary");
            Assert.That(library, Is.Not.Null);
            foreach (var item in library.Category(category))
            {
                var animations = item.Prefab.GetComponentsInChildren<Animation>(true);
                Assert.That(animations, Is.Not.Empty, item.Id);
                Assert.That(animations.Any(a => a.clip && a.clip.legacy && a.clip.length > 0 && a.playAutomatically && a.wrapMode == WrapMode.Loop), Is.True, item.Id);
            }
        }

        [Test]
        public void NearGroundDetailKeepsItsLicensedTextureReferences()
        {
            var library = Resources.Load<ContentLibrary>("ContentLibrary");
            Assert.That(library.GroundDiffuse, Is.Not.Null);
            Assert.That(library.GroundNormal, Is.Not.Null);
            var normal = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(library.GroundNormal)) as TextureImporter;
            Assert.That(normal.textureType, Is.EqualTo(TextureImporterType.NormalMap));
        }

        [Test]
        public void RacerContentIncludesAuthoredWheelPivots()
        {
            var library = Resources.Load<ContentLibrary>("ContentLibrary");
            Assert.That(library, Is.Not.Null);
            Assert.That(library.Category("car").Any(item => item.Prefab.GetComponentsInChildren<SourcePropMotion>(true)
                .Any(m => m.Wheels.Length >= 4 && m.Wheels.All(w => w))), Is.True);
        }

        [Test]
        public void ArtistOverrideSurvivesGeneratedRebuildAndRefreshesCachedCategories()
        {
            var library = ScriptableObject.CreateInstance<ContentLibrary>();
            var source = new GameObject("generated test content");
            var authored = new GameObject("authored test content");
            try
            {
                var generated = new ContentItem { Id = "hero", Category = "bird", Prefab = source };
                library.SetItems(new[] { generated });
                Assert.That(library.Category("bird").Single().Prefab, Is.SameAs(source));
                library.AuthoredItems = new[] {
                    new ContentItem { Id = "hero", Category = "bird", Prefab = authored },
                    new ContentItem { Id = "extra", Category = "tree", Prefab = authored }
                };
                library.SetItems(new[] { generated }); // The actual builder's cache invalidation path.
                Assert.That(library.Category("bird").Single().Prefab, Is.SameAs(authored));
                Assert.That(library.Category("tree").Single().Id, Is.EqualTo("extra"));
                library.SetItems(new[] { generated });
                Assert.That(library.AuthoredItems.Length, Is.EqualTo(2));
                Assert.That(library.Category("bird").Single().Prefab, Is.SameAs(authored));
            }
            finally
            {
                Object.DestroyImmediate(library);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(authored);
            }
        }

        [Test]
        public void UnfinishedArtistOverrideDoesNotHideWorkingGeneratedContent()
        {
            var library = ScriptableObject.CreateInstance<ContentLibrary>();
            var source = new GameObject("generated test content");
            try
            {
                library.AuthoredItems = new[] { new ContentItem { Id = "hero", Category = "bird" }, null };
                library.SetItems(new[] { new ContentItem { Id = "hero", Category = "bird", Prefab = source } });
                Assert.That(library.Category("bird").Single().Prefab, Is.SameAs(source));
            }
            finally
            {
                Object.DestroyImmediate(library);
                Object.DestroyImmediate(source);
            }
        }
    }
}
