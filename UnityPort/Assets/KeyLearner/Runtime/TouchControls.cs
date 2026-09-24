using System;
using UnityEngine;

namespace KeyLearner.Unity
{
    /// <summary>Direct movement controls; no virtual key events or software keyboard.</summary>
    public static class TouchControls
    {
        static readonly Rect SteeringArea = new Rect(45, 530, 320, 255);
        static readonly Rect BoostArea = new Rect(1120, 655, 260, 115);
        static readonly Rect SignalArea = new Rect(815, 655, 270, 115);
        static readonly Rect ViewArea = new Rect(1120, 510, 260, 115);
        static readonly Vector2 Center = new Vector2(205, 660);
        public static Vector2 Logical(Vector2 screen)
        {
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            return (new Vector2(screen.x, Screen.height-screen.y) - new Vector2((Screen.width-1440*scale)/2, (Screen.height-900*scale)/2))/scale;
        }
        static bool Held(out Vector2 point)
        {
            point = default;
            if (Input.touchCount > 1) return false;
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) return false;
                point = Logical(t.position);
                return true;
            }
            if (!Input.GetMouseButton(0)) return false;
            point = Logical(Input.mousePosition);
            return true;
        }
        public static Vector2 Steering
        {
            get
            {
                if (!Held(out var point) || !SteeringArea.Contains(point)) return Vector2.zero;
                var value = Vector2.ClampMagnitude(new Vector2(point.x-Center.x, Center.y-point.y)/105, 1);
                return value.magnitude < .16f ? Vector2.zero : value;
            }
        }
        public static bool Boost => Held(out var point) && BoostArea.Contains(point);
        public static bool Owns(Vector2 point) => SteeringArea.Contains(point) || BoostArea.Contains(point) || SignalArea.Contains(point) || ViewArea.Contains(point);
        public static void Draw(string signalName, Action signal, Action changeView = null)
        {
            Ui.Panel(SteeringArea, new Color(.035f,.075f,.12f,.9f));
            Ui.Ball(Center, 87, Style.Panel);
            var direction = Steering;
            Ui.Ball(Center + new Vector2(direction.x,-direction.y)*65, 32, Style.Mint);
            Ui.Label(new Rect(55,540,300,42), "Drag to steer", 27, Color.white);
            Ui.Panel(BoostArea, Boost ? Style.Mint : Style.Panel);
            Ui.Label(BoostArea,"Hold to boost",30,Boost ? Style.Navy : Color.white);
            Ui.Button(SignalArea, signalName, signal, Input.touchCount <= 1);
            if (changeView != null) Ui.Button(ViewArea, "Change view", changeView, Input.touchCount <= 1);
        }
    }
}
