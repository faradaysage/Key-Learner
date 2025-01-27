using BepuPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace KeyLearner.Elements
{
    public class TextElement : ParticleElement
    {
        public string Text { get; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; } // Velocity for physics
        public TimeSpan ExpirationTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public bool IsPinned { get; set; } // Determines if the text is affected by physics
        public SpriteFont Font { get; set; } // Font specific to this text element

        public Vector2 Destination { get; set; } // Target position for animations
        public float Rotation { get; set; } = 0f; // Rotation angle in degrees
        public float Scale { get; set; } = 1f; // Scaling factor
                                               // New: Word association
        public string ParentWord { get; set; } = string.Empty;


        public bool HasOutline { get; set; } = false; // Indicates if the text has an outline
        public float OutlineThickness { get; set; } = 0f; // Thickness of the outline
        public ColorScheme OutlineColorScheme { get; set; } = ColorScheme.Solid; // Color scheme for the outline
        public Color? SolidOutlineColor { get; set; } = null; // Color for solid outlines (if applicable)

        public bool IsWord { get; set; } = false; // Flag for word vs letter

        public TextElement(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));

            Text = text;
        }
        public byte GetAlpha(GameTime gameTime)
        {
            var remainingTime = ExpirationTime - gameTime.TotalGameTime;

            if (remainingTime <= TotalDuration * 0.25)
            {
                double fadeRatio = remainingTime.TotalMilliseconds / (TotalDuration.TotalMilliseconds * 0.25);
                return (byte)(255 * fadeRatio);
            }

            return 255;
        }
    }


}
