using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using KeyLearner.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using KeyLearner.Elements;

namespace KeyLearner
{
    public class SpriteManager
    {
        private SpriteFont _defaultFont;
        private readonly List<TextElement> _textElements = new();
        private ContentManager _contentManager;
        private GraphicsDevice _graphicsDevice;
        private Random _random = new Random();
        public SpriteManager(ContentManager contentManager, GraphicsDevice graphicsDevice)
        {
            _contentManager = contentManager;
            _graphicsDevice = graphicsDevice;
        }
        public void ClearAllTextElements()
        {
            _textElements.Clear();
        }

        public void LoadContent()
        {
            _defaultFont = _contentManager.Load<SpriteFont>("Fonts/DefaultFont");
        }
        public void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            DrawText(spriteBatch, null, text, position, color);
        }

        public void DrawText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color)
        {
            if (_graphicsDevice == null)
                throw new InvalidOperationException("GraphicsDevice is not initialized.");

            spriteBatch.DrawString(font ?? _defaultFont, text, position, color);
        }

        public void DrawText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, bool hasOutline = false, float outlineThickness = 0f, ColorScheme outlineColorScheme = ColorScheme.Solid, Color? solidOutlineColor = null, GameTime gameTime = null)
        {
            if (_graphicsDevice == null)
                throw new InvalidOperationException("GraphicsDevice is not initialized.");

            if (hasOutline && outlineThickness > 0)
            {
                // Generate the outline color dynamically if required
                Color outlineColor = solidOutlineColor ?? GenerateOutlineColor(outlineColorScheme, gameTime);

                // Render the outline by offsetting the text in all directions
                foreach (var offset in GetOutlineOffsets(outlineThickness))
                {
                    spriteBatch.DrawString(
                        font ?? _defaultFont,
                        text,
                        position + offset,
                        outlineColor);
                }
            }

            // Draw the main text
            spriteBatch.DrawString(font ?? _defaultFont, text, position, color);
        }

        // Generate a dynamic outline color
        private Color GenerateOutlineColor(ColorScheme scheme, GameTime gameTime)
        {
            switch (scheme)
            {
                case ColorScheme.Solid:
                    return Color.Black; // Default to black if solid color isn't specified
                case ColorScheme.RainbowBlended:
                case ColorScheme.RainbowSolid:
                    return RainbowColor((float)gameTime.TotalGameTime.TotalSeconds); // Existing rainbow logic
                case ColorScheme.BlackAndWhite:
                    return gameTime.TotalGameTime.TotalSeconds % 2 < 1 ? Color.Black : Color.White;
                case ColorScheme.PrimaryColors:
                    return PrimaryColor((float)gameTime.TotalGameTime.TotalSeconds); // Existing primary colors logic
                default:
                    return Color.Black;
            }
        }
        private Color RainbowColor(float timeInSeconds)
        {
            // Define the colors of the rainbow
            var colors = RainbowColorGenerator.RainbowColors;

            // Calculate the index based on the time
            int index = (int)(timeInSeconds % colors.Length);

            // For RainbowBlended, blend between adjacent colors
            float blendFactor = timeInSeconds % 1; // Fractional part of time determines blend
            Color currentColor = colors[index];
            Color nextColor = colors[(index + 1) % colors.Length];

            return Color.Lerp(currentColor, nextColor, blendFactor);
        }

        private Color PrimaryColor(float timeInSeconds)
        {
            // Define primary colors
            var primaryColors = new[]
            {
                    Color.Red,
                    Color.Blue,
                    Color.Yellow
                };

            // Calculate the index based on the time
            int index = (int)(timeInSeconds % primaryColors.Length);

            return primaryColors[index];
        }

        // Helper to calculate outline offsets
        private IEnumerable<Vector2> GetOutlineOffsets(float thickness)
        {
            return new[]
            {
                    new Vector2(-thickness, -thickness),
                    new Vector2(0, -thickness),
                    new Vector2(thickness, -thickness),
                    new Vector2(-thickness, 0),
                    new Vector2(thickness, 0),
                    new Vector2(-thickness, thickness),
                    new Vector2(0, thickness),
                    new Vector2(thickness, thickness)
                };
        }

        public Vector2 MeasureString(SpriteFont font, string text)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            return font.MeasureString(text);
        }

        public TextElement AddText(
            string text,
            Vector2 position,
            Color color,
            TimeSpan duration,
            GameTime gameTime,
            SpriteFont font,
            ParticleManager particleManager, // Add reference to ParticleManager
            bool hasOutline = false,
            float outlineThickness = 0f,
            ColorScheme outlineColorScheme = ColorScheme.Solid,
            Color? solidOutlineColor = null,
            bool isWord = false // Flag for word
        )
        {
            var expirationTime = gameTime.TotalGameTime + duration;

            var textElement = new TextElement(text)
            {
                Position = position,
                Destination = position, // Initially, no movement
                Color = color,
                ExpirationTime = expirationTime,
                TotalDuration = duration,
                Velocity = isWord ? new Vector2(0, 0) : new Vector2(_random.NextFloat(-50, 50), _random.NextFloat(-50, 50)),
                IsPinned = true,
                Font = font,
                HasOutline = hasOutline,
                OutlineThickness = outlineThickness,
                OutlineColorScheme = outlineColorScheme,
                SolidOutlineColor = solidOutlineColor,
                IsWord = isWord
            };

            _textElements.Add(textElement);

            // Register in the particle system
            var position3D = new System.Numerics.Vector3(position.X, position.Y, isWord ? 100 : 0); // Words start zoomed out
            var velocity3D = new System.Numerics.Vector3(textElement.Velocity.X, textElement.Velocity.Y, isWord ? -50 : 0); // Words zoom in
            textElement.BodyHandle = particleManager.CreateTextParticle(position3D, velocity3D);
            particleManager.AddTextElement(textElement);

            return textElement;
        }

        public void Update(GameTime gameTime)
        {
            // Update all text elements
            for (int i = _textElements.Count - 1; i >= 0; i--)
            {
                var element = _textElements[i];

                // Remove expired elements
                if (gameTime.TotalGameTime > element.ExpirationTime)
                {
                    _textElements.RemoveAt(i);
                    continue;
                }

                // Animate position (move towards Destination)
                if (element.Position != element.Destination)
                {
                    var direction = Vector2.Normalize(element.Destination - element.Position);
                    var distance = Vector2.Distance(element.Position, element.Destination);
                    var step = Math.Min(distance, 5f); // Adjust speed (5 pixels per frame)
                    element.Position += direction * step;

                    if (distance < 1f)
                    {
                        element.Position = element.Destination; // Snap to destination
                    }
                }

                // Gradually fade out in the last 25% of lifespan
                var remainingTime = element.ExpirationTime - gameTime.TotalGameTime;
                var totalTime = element.ExpirationTime - (gameTime.TotalGameTime - TimeSpan.FromSeconds(5));
                if (remainingTime < totalTime * 0.25)
                {
                    var alpha = (byte)(255 * (remainingTime.TotalSeconds / (totalTime.TotalSeconds * 0.25)));
                    element.Color = new Color(element.Color.R, element.Color.G, element.Color.B, alpha);
                }
            }
        }

        // New: Update an existing text element
        public void UpdateTextElement(TextElement element)
        {
            var existingElement = _textElements.FirstOrDefault(te => te == element);
            if (existingElement != null)
            {
                existingElement.Position = element.Position;
                existingElement.Destination = element.Destination;
                existingElement.Rotation = element.Rotation;
                existingElement.Scale = element.Scale;
                existingElement.Color = element.Color;
            }
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            foreach (var textElement in _textElements)
            {
                // Adjust color based on alpha
                var colorWithAlpha = textElement.Color * (textElement.GetAlpha(gameTime) / 255f);

                // Draw text with or without outline
                DrawText(
                    spriteBatch,
                    textElement.Font,
                    textElement.Text,
                    textElement.Position,
                    colorWithAlpha,
                    textElement.HasOutline,
                    textElement.OutlineThickness,
                    textElement.OutlineColorScheme,
                    textElement.SolidOutlineColor,
                    gameTime);
            }
        }

        public void ClearScreen(Color color)
        {
            _graphicsDevice.Clear(color);
        }
    }
}
