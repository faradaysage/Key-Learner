using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>Bounded flight for a real animated source bird; owned by its streamed landscape chapter.</summary>
    public sealed class SceneryFlight : MonoBehaviour
    {
        GameServices services;
        Vector3 center;
        float radius, phase, speed, yaw;
        Animation[] animations;
        bool wasGentle;

        public void Configure(GameServices gameServices, Vector3 orbitCenter, float orbitRadius, float initialPhase, float angularSpeed, float sourceYaw)
        {
            services = gameServices;
            center = orbitCenter;
            radius = orbitRadius;
            phase = initialPhase;
            speed = angularSpeed;
            yaw = sourceYaw;
            animations = GetComponentsInChildren<Animation>();
            foreach (var animation in animations)
            {
                if (!animation.clip)
                    continue;
                animation.Play(animation.clip.name);
                animation[animation.clip.name].normalizedTime = Mathf.Repeat(initialPhase, 1);
            }
            ApplyAnimationSpeed(services.Settings.GentleMotion);
            PositionBird();
        }

        void Update()
        {
            if (services == null)
                return;
            bool gentle = services.Settings.GentleMotion;
            if (gentle != wasGentle)
                ApplyAnimationSpeed(gentle);
            phase += Time.deltaTime * speed * (gentle ? .38f : 1);
            PositionBird();
        }

        void ApplyAnimationSpeed(bool gentle)
        {
            wasGentle = gentle;
            foreach (var animation in animations)
                if (animation.clip)
                    animation[animation.clip.name].speed = gentle ? .6f : 1;
        }

        void PositionBird()
        {
            transform.localPosition = center + new Vector3(Mathf.Sin(phase) * radius, Mathf.Sin(phase * .73f) * 3, Mathf.Cos(phase) * radius * .62f);
            var tangent = new Vector3(Mathf.Cos(phase) * radius, Mathf.Cos(phase * .73f) * 2.19f, -Mathf.Sin(phase) * radius * .62f);
            transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0, yaw, 0);
        }
    }
}
