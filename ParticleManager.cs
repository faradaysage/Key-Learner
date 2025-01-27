using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using BepuUtilities;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Vector3 = System.Numerics.Vector3;
using System;
using System.Linq;
using KeyLearner.Elements;

namespace KeyLearner
{
    public class ParticleManager
    {
        private readonly Texture2D _particleTexture;
        private readonly List<TextElement> _textElements = new();
        private readonly List<ParticleElement> _particles = new();
        private readonly BufferPool _bufferPool;
        private readonly Shapes _shapes;
        private readonly Simulation _simulation;
        private readonly List<ParticleGenerator> _generators = new();
        private readonly float _maxParticleRadius = 0.5f;
        private readonly float _particleMass = 0.1f;
        private readonly float _maxLifetime = 5.0f; // Maximum lifetime in seconds
        private readonly float _maxAlpha = 0.8f;
        public IEnumerable<ParticleGenerator> Generators => _generators;

        private readonly RainbowColorGenerator _rainbowGenerator = new(7.0f); // Default 7-second cycle

        private readonly Random _random = new Random();

        public ParticleManager(Texture2D particleTexture)
        {
            _particleTexture = particleTexture;

            // Initialize the buffer pool
            _bufferPool = new BufferPool();

            _shapes = new Shapes(_bufferPool, 4096); // 4096 is an example initial capacity

            // Create the simulation with required parameters
            _simulation = Simulation.Create(
                _bufferPool,
                new NarrowPhaseCallbacks(),
                new PoseIntegratorCallbacks(new System.Numerics.Vector3(0, -9.81f, 0)), // Gravity
                new SolveDescription(4, 1), // Multithreading and velocity substeps
                default(DefaultTimestepper), // Use DefaultTimestepper as a struct
                null, // Default allocation sizes
                _shapes); // Shape handler
        }

        /// <summary>
        /// Adds a particle generator to the manager.
        /// </summary>
        public void AddGenerator(ParticleGenerator generator)
        {
            _generators.Add(generator);
        }

        public BodyHandle CreateTextParticle(Vector3 position, Vector3 velocity)
        {
            var box = new Box(1f, 1f, 1f); // Adjust size as needed
            var inertia = box.ComputeInertia(1f); // Adjust mass as needed

            var bodyDescription = BodyDescription.CreateDynamic(
                new RigidPose(position),
                new BodyVelocity(velocity),
                inertia,
                new CollidableDescription(_shapes.Add(box), 0.1f), // Adjust collision properties
                new BodyActivityDescription(0.01f)
            );

            var handle = _simulation.Bodies.Add(bodyDescription);
            return handle;
        }

        public void AddTextElement(TextElement textElement)
        {
            _textElements.Add(textElement);
        }

        /// <summary>
        /// Adds a new particle to the simulation.
        /// </summary>
        /// <param name="gameTime">Game time elapsed since the last update.</param>
        /// <param name="position">Initial position of the particle.</param>
        /// <param name="velocity">Initial velocity of the particle.</param>
        public void AddParticle(GameTime gameTime, Vector3 position, Vector3 velocity, float? lifetime = null, float? alpha = null)
        {
            var sphere = new Sphere(_maxParticleRadius);
            var inertia = sphere.ComputeInertia(_particleMass);
            var collidableDescription = new CollidableDescription(
                _simulation.Shapes.Add(sphere),
                0.1f); // Specify collision properties

            var bodyDescription = BodyDescription.CreateDynamic(
                new RigidPose(position),
                new BodyVelocity(velocity),
                inertia,
                collidableDescription,
                new BodyActivityDescription(0.01f)
            );

            var particle = new ParticleElement
            {
                BodyHandle = _simulation.Bodies.Add(bodyDescription),
                Lifetime = lifetime ?? _maxLifetime, // Use provided lifetime or default
                Radius = _random.NextFloat(_maxParticleRadius / 2f, _maxParticleRadius),
                Alpha = alpha ?? _maxAlpha, // Use provided alpha or default
                Color = GenerateColor(ColorScheme.RainbowBlended, (float)gameTime.TotalGameTime.TotalSeconds)
            };
            _particles.Add(particle);
        }


        private Microsoft.Xna.Framework.Color GenerateColor(ColorScheme scheme, float elapsedTime)
        {
            switch (scheme)
            {
                case ColorScheme.BlackAndWhite:
                    return _random.Next(2) == 0
                        ? Microsoft.Xna.Framework.Color.Black
                        : Microsoft.Xna.Framework.Color.White;

                case ColorScheme.PrimaryColors:
                    return new Microsoft.Xna.Framework.Color(
                        _random.Next(3) == 0 ? 255 : 0,
                        _random.Next(3) == 1 ? 255 : 0,
                        _random.Next(3) == 2 ? 255 : 0,
                        255);

                case ColorScheme.PastelColors:
                    return new Microsoft.Xna.Framework.Color(
                        128 + _random.Next(128),
                        128 + _random.Next(128),
                        128 + _random.Next(128),
                        255);

                case ColorScheme.Monochrome:
                    int grayValue = _random.Next(256);
                    return new Microsoft.Xna.Framework.Color(
                        grayValue, grayValue, grayValue, 255);

                case ColorScheme.RainbowSolid:
                    return _rainbowGenerator.GenerateRainbowSolid(elapsedTime);

                case ColorScheme.RainbowBlended:
                    return _rainbowGenerator.GenerateRainbowBlended(elapsedTime);

                case ColorScheme.Random:
                default:
                    return new Microsoft.Xna.Framework.Color(
                        _random.Next(256),
                        _random.Next(256),
                        _random.Next(256),
                        255);
            }
        }


        /// <summary>
        /// Updates the particle manager and removes expired particles.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            var listCopy = _generators.ToList(); // iterate over a copy so we can remove elements from the original
            foreach (var generator in listCopy)
            {
                generator.Update(gameTime, this);

                // Track expired generators for removal
                if (generator.IsExpired)
                {
                    _generators.Remove(expired);
                }
            }


            // Ensure the timestep is positive, set a minimum value if required
            float timestep = Math.Max((float)gameTime.ElapsedGameTime.TotalSeconds, 0.0001f);

            // Step the simulation forward
            _simulation.Timestep(timestep);

            var listCopy = _particles.ToList(); // iterate over a copy so we can remove elements from the original

            foreach (var particle in listCopy)
            {
                if (_simulation.Bodies.BodyExists(particle.BodyHandle))
                {
                    // Decrease lifetime and remove expired particles
                    particle.Lifetime -= timestep;

                    if (particle.Lifetime <= 0)
                    {
                        _simulation.Bodies.Remove(particle.BodyHandle);
                        _particles.Remove(particle);
                    }
                }
            }

            // Update positions of TextElements associated with physics bodies
            foreach (var textElement in _textElements)
            {
                if (_simulation.Bodies.BodyExists(textElement.BodyHandle))
                {
                    var bodyReference = _simulation.Bodies.GetBodyReference(textElement.BodyHandle);
                    textElement.Position = new Microsoft.Xna.Framework.Vector2(
                        (float)bodyReference.Pose.Position.X,
                        (float)bodyReference.Pose.Position.Y);
                }
            }
        }



        /// <summary>
        /// Draws all active particles.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            foreach (var particle in _particles)
            {
                var bodyReference = _simulation.Bodies.GetBodyReference(particle.BodyHandle);
                var position = bodyReference.Pose.Position;

                // Fade alpha based on remaining lifespan
                float alpha = (particle.Lifetime / _maxLifetime) * particle.Alpha;
                var color = particle.Color * alpha;

                // Adjust radius for fading effect
                float radius = (particle.Lifetime / _maxLifetime) * particle.Radius;

                spriteBatch.Draw(
                    _particleTexture,
                    new Microsoft.Xna.Framework.Vector2((float)position.X, (float)position.Y),
                    null,
                    color,
                    0f,
                    new Microsoft.Xna.Framework.Vector2(_particleTexture.Width / 2, _particleTexture.Height / 2),
                    radius, // Scale factor for visibility
                    SpriteEffects.None,
                    0f);
            }
        }


        /// <summary>
        /// Disposes of simulation resources.
        /// </summary>
        public void Dispose()
        {
            _simulation.Dispose();
            _bufferPool.Clear();
        }
    }
}
