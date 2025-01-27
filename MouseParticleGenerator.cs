using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLearner
{
    public class MouseParticleGenerator : BaseParticleGenerator
    {
        private readonly Random _random = new();
        private readonly int _particlesPerSecond; // Maximum particles generated per second
        private Vector2 _lastMousePosition;

        // Configurable properties
        public int ParticleMultiplier { get; set; } = 2; // Multiplier for particles when the mouse button is pressed
        public float VelocityMultiplier { get; set; } = 3.0f; // Multiplier for velocity when the mouse button is pressed

        public MouseParticleGenerator(ParticleManager particleManager, TimeSpan duration, int particlesPerSecond)
            : base(particleManager, duration)
        {
            _particlesPerSecond = particlesPerSecond;
        }

        public override void Update(GameTime gameTime)
        {
            var mouseState = Mouse.GetState();
            var mousePosition = new Vector2(mouseState.X, mouseState.Y);
            bool isMouseButtonPressed = mouseState.LeftButton == ButtonState.Pressed;

            // Emit particles when the mouse is moved or button is pressed
            if (_lastMousePosition != mousePosition || isMouseButtonPressed)
            {
                _timer = _duration; // Reset the timer
                _lastMousePosition = mousePosition;

                // Calculate multipliers based on mouse button state
                int particleMultiplier = isMouseButtonPressed ? ParticleMultiplier : 1;
                float velocityMultiplier = isMouseButtonPressed ? VelocityMultiplier : 1.0f;

                float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
                int particlesToGenerate = (int)(_particlesPerSecond * elapsedSeconds) * particleMultiplier;

                for (int i = 0; i < particlesToGenerate; i++)
                {
                    // Generate random position and velocity
                    var position = new System.Numerics.Vector3(mousePosition.X, mousePosition.Y, 0);
                    var velocity = new System.Numerics.Vector3(
                        _random.NextFloat(-150f, 150f) * velocityMultiplier,
                        _random.NextFloat(-150f, 150f) * velocityMultiplier,
                        _random.NextFloat(-50f, 50f) * velocityMultiplier);

                    ParticleManager.AddParticle(gameTime, position, velocity);
                }
            }

            // Decrease timer
            if (!isMouseButtonPressed)
            {
                _timer -= gameTime.ElapsedGameTime;
            }
        }
    }