using System;
using UnityEngine;
using KeyLearner.Studio;
namespace KeyLearner.Unity
{
    // Shared approved parent palettes, separated from any minigame presentation.
    public static class ThemeColors
    {
        static readonly Color32[][] Palettes ={
            new Color32[]{new Color32(137,244,208,255),new Color32(181,154,255,255),new Color32(255,179,198,255),new Color32(255,222,143,255)},
            new Color32[]{new Color32(74,231,215,255),new Color32(90,173,255,255),new Color32(181,244,196,255),new Color32(254,231,161,255)},
            new Color32[]{new Color32(255,155,119,255),new Color32(246,104,161,255),new Color32(255,214,135,255),new Color32(166,144,250,255)},
            new Color32[]{new Color32(243,143,221,255),new Color32(128,205,255,255),new Color32(251,229,147,255),new Color32(171,240,212,255)},
            new Color32[]{new Color32(255,38,50,255),new Color32(255,215,0,255),new Color32(25,100,255,255),new Color32(255,215,0,255)},
            new Color32[]{new Color32(255,255,255,255),new Color32(210,210,210,255),new Color32(255,255,255,255),new Color32(160,160,160,255)}};
        public static Color At(Mood theme, int index) => Palettes[Mathf.Clamp((int)theme, 0, 5)][Math.Abs(index) % 4];
    }
}
