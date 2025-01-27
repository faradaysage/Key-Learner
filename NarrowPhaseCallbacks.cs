using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using System;

namespace KeyLearner
{
    public struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        // Initializes simulation-related data if necessary.
        public void Initialize(Simulation simulation)
        {
            // No specific initialization is required for basic callbacks.
        }

        // Determines whether contact generation between two collidables is allowed.
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            // Allow all contact generation.
            return true;
        }

        // Determines whether contact generation for specific child indices is allowed (used for compound shapes).
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            // Allow all contact generation for child indices.
            return true;
        }

        // Configures the material properties for a contact manifold.
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            // Define material properties like friction and restitution.
            pairMaterial.FrictionCoefficient = 1.0f; // Standard friction
            pairMaterial.MaximumRecoveryVelocity = 2.0f; // Recovery velocity cap
            pairMaterial.SpringSettings = new SpringSettings(30, 1); // Spring behavior settings
            return true; // Allow the contact manifold.
        }

        // Configures the contact manifold for specific child indices (used for compound shapes).
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            // Adjust contact points if needed (leave unmodified for default behavior).
            return true; // Allow the contact manifold.
        }

        // Cleanup any resources or state when no longer needed.
        public void Dispose()
        {
            // No specific cleanup is required for basic callbacks.
        }
    }
}
