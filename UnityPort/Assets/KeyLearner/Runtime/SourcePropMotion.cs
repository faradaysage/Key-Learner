using System;
using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>Moves authored movable mesh parts around their original pivots.</summary>
    public sealed class SourcePropMotion : MonoBehaviour
    {
        public Transform[] Wheels = Array.Empty<Transform>();
        public Transform[] WindmillBlades = Array.Empty<Transform>();
        public float MotionScale = 1;
        public float WindmillDegreesPerSecond = 22;
        public float WheelRadius = .3f;
        Quaternion[] wheelRest, bladeRest;
        Vector3 previousPosition;
        float wheelAngle, bladeAngle;
        void Awake()
        {
            wheelRest = new Quaternion[Wheels.Length];
            bladeRest = new Quaternion[WindmillBlades.Length];
            for (int i = 0; i < Wheels.Length; i++)
                if (Wheels[i])
                    wheelRest[i] = Wheels[i].localRotation;
            for (int i = 0; i < WindmillBlades.Length; i++)
                if (WindmillBlades[i])
                    bladeRest[i] = WindmillBlades[i].localRotation;
            previousPosition = transform.position;
        }
        void OnEnable()
        {
            previousPosition = transform.position;
        }
        void LateUpdate()
        {
            float distance = Vector3.Dot(transform.position - previousPosition, transform.forward);
            previousPosition = transform.position;
            float worldRadius = Mathf.Max(.01f, WheelRadius * Mathf.Abs(transform.lossyScale.y));
            // Teleports/scene resumes reset placement without spinning through a huge distance.
            if (Mathf.Abs(distance) < worldRadius * 80)
                wheelAngle = Mathf.Repeat(wheelAngle + distance / worldRadius * Mathf.Rad2Deg, 360);
            bladeAngle = Mathf.Repeat(bladeAngle + Time.deltaTime * WindmillDegreesPerSecond * Mathf.Clamp01(MotionScale), 360);
            for (int i = 0; i < Wheels.Length; i++)
                if (Wheels[i])
                    Wheels[i].localRotation = wheelRest[i] * Quaternion.AngleAxis(wheelAngle, Vector3.right);
            for (int i = 0; i < WindmillBlades.Length; i++)
                if (WindmillBlades[i])
                    WindmillBlades[i].localRotation = bladeRest[i] * Quaternion.AngleAxis(bladeAngle, Vector3.up);
        }
    }
}
