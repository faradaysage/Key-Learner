using BepuPhysics;
using BepuUtilities;
using System.Numerics;

namespace KeyLearner
{
    public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        private Vector3 _gravity;
        private Vector3Wide _gravityWide;

        /// <summary>
        /// Initializes the pose integrator callbacks with a gravity vector.
        /// </summary>
        /// <param name="gravity">The gravity vector to apply to all bodies.</param>
        public PoseIntegratorCallbacks(Vector3 gravity)
        {
            _gravity = gravity;
            _gravityWide = default;
        }

        /// <summary>
        /// Whether the simulation allows substeps for unconstrained bodies. Set to false for simplicity.
        /// </summary>
        public bool AllowSubstepsForUnconstrainedBodies => false;

        /// <summary>
        /// Whether kinematic bodies should have their velocities integrated.
        /// </summary>
        public bool IntegrateVelocityForKinematics => false;

        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

        /// <summary>
        /// Prepares the gravity vector for integration.
        /// </summary>
        /// <param name="dt">The time step.</param>
        public void PrepareForIntegration(float dt)
        {
            // Broadcast the gravity vector into a wide vector format.
            _gravityWide = Vector3Wide.Broadcast(_gravity);
        }

        /// <summary>
        /// Applies gravity and updates the velocity for the given set of bodies.
        /// </summary>
        /// <param name="bodyIndices">Indices of the bodies being integrated.</param>
        /// <param name="position">Position of the bodies (unused).</param>
        /// <param name="orientation">Orientation of the bodies (unused).</param>
        /// <param name="localInertia">Local inertia of the bodies.</param>
        /// <param name="integrationMask">Integration mask.</param>
        /// <param name="workerIndex">Index of the worker thread.</param>
        /// <param name="dt">Time step size.</param>
        /// <param name="velocity">Velocity of the bodies, to be updated.</param>
        public void IntegrateVelocity(
            Vector<int> bodyIndices,
            Vector3Wide position,
            QuaternionWide orientation,
            BodyInertiaWide localInertia,
            Vector<int> integrationMask,
            int workerIndex,
            Vector<float> dt,
            ref BodyVelocityWide velocity)
        {
            // Apply gravity to the linear velocity.
            Vector3Wide.Scale(_gravityWide, dt, out var gravityDelta);
            Vector3Wide.Add(velocity.Linear, gravityDelta, out velocity.Linear);
        }

        /// <summary>
        /// Initializes the simulation. No additional setup is required.
        /// </summary>
        /// <param name="simulation">The simulation instance.</param>
        public void Initialize(Simulation simulation)
        {
            // No initialization is required for this implementation.
        }
    }
}
