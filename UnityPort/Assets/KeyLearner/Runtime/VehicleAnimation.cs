using System.Collections.Generic;
using UnityEngine;
namespace KeyLearner.Unity
{
    public sealed class VehicleAnimation
    {
        readonly List<(Transform Wheel, Quaternion Neutral, bool Front)> wheels = new List<(Transform, Quaternion, bool)>();
        float rotation, steering;
        public float Steering => steering;
        public int FrontWheels => wheels.FindAll(w => w.Front).Count;
        public VehicleAnimation(GameObject vehicle)
        {
            // This controller owns steering and roll; prevent prefab LateUpdate from overwriting it.
            foreach (var motion in vehicle.GetComponentsInChildren<SourcePropMotion>()) motion.enabled = false;
            foreach (var t in vehicle.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("wheel-")) wheels.Add((t, t.localRotation, t.name.Contains("front")));
        }
        public void Tick(float dt, float turn, float speed, bool gentle)
        {
            steering = Mathf.Lerp(steering, turn * 30, 1 - Mathf.Exp(-dt * 9));
            rotation = (rotation + speed * dt * (gentle ? 35 : 70)) % 360;
            foreach (var w in wheels)
                if (w.Wheel) w.Wheel.localRotation = w.Neutral * Quaternion.Euler(0, w.Front ? steering : 0, 0) * Quaternion.Euler(rotation, 0, 0);
        }
    }
}
