using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KeyLearner.Unity.Tests
{
    public sealed class GameCleanupTests
    {
        [Test]
        public void DinosaurExitToleratesCameraDestroyedBeforeSuite()
        {
            var owner = new GameObject("Shutdown camera");
            var camera = owner.AddComponent<Camera>();
            var game = new DinosaurGame();
            typeof(Minigame).GetField("S", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(game, new GameServices { Camera = camera });
            Object.DestroyImmediate(owner);
            Assert.DoesNotThrow(() => game.Exit());
        }
    }
}
