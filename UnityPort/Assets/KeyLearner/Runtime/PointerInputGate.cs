using KeyLearner.Studio;
using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>Native touch and emulated mouse share one gameplay action path.</summary>
    public sealed class PointerInputGate
    {
        readonly PointerGestureSafety safety = new PointerGestureSafety();
        public void Reset() => safety.Reset(Time.unscaledTimeAsDouble);
        public bool TryGet(out Vector2 bottomLeft, out bool right)
        {
            bottomLeft = default;
            right = false;
            int active = 0;
            bool began = false;
            Vector2 position = default;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    continue;
                active++;
                if (touch.phase == TouchPhase.Began)
                {
                    began = true;
                    position = touch.position;
                }
            }
            double now = Time.unscaledTimeAsDouble;
            if (safety.ObserveTouches(now, active, Input.touchCount > 0, began))
            {
                bottomLeft = position;
                return true;
            }
            if (!safety.AllowsMouse(now))
                return false;
            right = Input.GetMouseButtonDown(1);
            if (!right && !Input.GetMouseButtonDown(0))
                return false;
            bottomLeft = Input.mousePosition;
            return true;
        }
    }
}
