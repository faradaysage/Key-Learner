using KeyLearner;
using Microsoft.Xna.Framework;
using System;
using System.Numerics;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

public class ParticleGenerator
{
    private readonly Random _random = new();
    private TimeSpan _timer; // Time remaining for particle generation
    private readonly TimeSpan _duration; // Total duration of particle generation after a mouse move
    private readonly int _particlesPerSecond; // Maximum particles generated per second
    private Vector2 _lastMousePosition;

    // Configurable properties
    public int ParticleMultiplier { get; set; } = 2; // Multiplier for particles when mouse button is pressed
    public float VelocityMultiplier { get; set; } = 3.0f; // Multiplier for velocity when mouse button is pressed

    /// <summary>
    /// Creates a new particle generator.
    /// </summary>
    /// <param name="duration">Duration to emit particles after the mouse stops moving.</param>
    /// <param name="particlesPerSecond">Maximum particles generated per second.</param>
    public ParticleGenerator(TimeSpan duration, int particlesPerSecond)
    {
        _duration = duration;
        _particlesPerSecond = particlesPerSecond;
    }


    /// <summary>
    /// Updates the particle generator, creating particles and adding them to the ParticleManager.
    /// </summary>
    /// <param name="gameTime">Game time elapsed since the last update.</param>
    /// <param name="mousePosition">Current mouse position in screen space.</param>
    /// <param name="particleManager">The particle manager to add new particles to.</param>

    public void Update(GameTime gameTime)
    {
        // Check if the left mouse button is pressed
        var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        bool isMouseButtonPressed = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

        // Emit particles when the mouse is moved
        if (_lastMousePosition != mousePosition)
        {
            _timer = _duration; // Reset the timer
            _lastMousePosition = mousePosition;
        }

        // Calculate multipliers based on mouse button state
        int particleMultiplier = isMouseButtonPressed ? ParticleMultiplier : 1;
        float velocityMultiplier = isMouseButtonPressed ? VelocityMultiplier : 1.0f;

        // Emit particles regardless of movement if the mouse button is pressed
        if (isMouseButtonPressed || _timer > TimeSpan.Zero)
        {
            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Calculate the number of particles to generate
            int particlesToGenerate = (int)(_particlesPerSecond * elapsedSeconds) * particleMultiplier;

            for (int i = 0; i < particlesToGenerate; i++)
            {
                // Generate random position and velocity
                var position = new Vector3(mousePosition.X, mousePosition.Y, 0); // Map 2D mouse position to 3D space
                var velocity = new Vector3(
                    _random.NextFloat(-150f, 150f) * velocityMultiplier,
                    _random.NextFloat(-150f, 150f) * velocityMultiplier,
                    _random.NextFloat(-50f, 50f) * velocityMultiplier); // Add slight Z-axis variation for depth

                // Add the new particle to the simulation
                particleManager.AddParticle(gameTime, position, velocity);
            }

            // Decrease the timer
            if (!isMouseButtonPressed) // Only decrease the timer if the button is not pressed
            {
                _timer -= gameTime.ElapsedGameTime;
            }
        }
    }
}
