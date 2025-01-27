using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace KeyLearner
{
    public class RainbowColorGenerator
    {
        private readonly float _cycleDuration; // Total duration for a complete rainbow cycle

        private static readonly Color[] _rainbowColors = {
            new Color(255, 0, 0),    // Red
            new Color(255, 127, 0),  // Orange
            new Color(255, 255, 0),  // Yellow
            new Color(0, 255, 0),    // Green
            new Color(0, 0, 255),    // Blue
            new Color(75, 0, 130),   // Indigo
            new Color(148, 0, 211)   // Violet
        };

        public static Color[] RainbowColors => _rainbowColors;


        public RainbowColorGenerator(float cycleDuration = 7.0f)
        {
            _cycleDuration = cycleDuration;
        }

        /// <summary>
        /// Generates a solid rainbow color based on the current time.
        /// </summary>
        /// <param name="time">Elapsed time in seconds.</param>
        /// <returns>A solid rainbow color.</returns>
        public Color GenerateRainbowSolid(float time)
        {
            // Calculate the active color index based on elapsed time
            float timePerColor = _cycleDuration / _rainbowColors.Length;
            int colorIndex = (int)(time / timePerColor) % _rainbowColors.Length;
            return _rainbowColors[colorIndex];
        }


        public Color GenerateRainbowBlended(float elapsedTime)
        {
            float cycleTime = elapsedTime % _cycleDuration;
            float normalizedTime = cycleTime / (_cycleDuration / _rainbowColors.Length);

            int firstColorIndex = (int)normalizedTime % _rainbowColors.Length;
            int nextColorIndex = (firstColorIndex + 1) % _rainbowColors.Length;

            float blendFactor = normalizedTime - (int)normalizedTime;

            Color firstColor = _rainbowColors[firstColorIndex];
            Color nextColor = _rainbowColors[nextColorIndex];

            // Linear interpolation (LERP) between the two colors
            return new Color(
                (int)(firstColor.R + (nextColor.R - firstColor.R) * blendFactor),
                (int)(firstColor.G + (nextColor.G - firstColor.G) * blendFactor),
                (int)(firstColor.B + (nextColor.B - firstColor.B) * blendFactor),
                255); // Keep alpha constant
        }

    }
}