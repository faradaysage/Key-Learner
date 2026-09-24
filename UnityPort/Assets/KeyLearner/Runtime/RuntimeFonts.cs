using System;
using UnityEngine;
using KeyLearner.Unity.Platform;

namespace KeyLearner.Unity
{
    public static class RuntimeFonts
    {
        public static Font Load(string name)
        {
            bool baked = AndroidContent.Enabled && (name == "Fredoka-Play" || name == "BalooBhai2-Play");
            var font = Resources.Load<Font>((baked ? "AndroidContent/Fonts/" : "Fonts/") + name);
            if (baked && (!font || font.dynamic))
                throw new InvalidOperationException("Android requires the baked font atlas: " + name);
            return font;
        }
    }
}
