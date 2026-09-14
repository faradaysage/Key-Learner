using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>A brief presentation offset. Restored before camera logic and cleared on every focus transition.</summary>
    public sealed class CameraFeedback
    {
        Camera camera;
        Vector3 offset;
        float remaining, duration, amplitude, phase;
        public void Pulse(float strength, float seconds = .4f)
        {
            amplitude = Mathf.Max(amplitude, strength);
            duration = remaining = Mathf.Clamp(seconds, .05f, .7f);
            phase = 0;
        }
        public void Restore()
        {
            if (camera)
                camera.transform.position -= offset;
            offset = Vector3.zero;
        }
        public void Apply(Camera target, float dt, bool gentle)
        {
            Restore();
            camera = target;
            if (gentle)
            {
                Clear();
                return;
            }
            remaining = Mathf.Max(0, remaining - dt);
            if (remaining <= 0)
            {
                amplitude = 0;
                return;
            }
            phase += dt;
            float fade = remaining / duration;
            offset = (camera.transform.right * Mathf.Sin(phase * 73) + camera.transform.up * Mathf.Sin(phase * 57) * .45f) * amplitude * fade * fade;
            camera.transform.position += offset;
        }
        public void Clear()
        {
            Restore();
            remaining = amplitude = 0;
        }
    }
}
