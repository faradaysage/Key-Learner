using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLearner
{
    public abstract class BaseParticleGenerator : IParticleGenerator
    {
        private ParticleManager _particleManager;
        private TimeSpan _lifespan; // Total duration of particle generation
        private TimeSpan _timer;    // Time remaining for particle generation

        protected ParticleManager ParticleManager => _particleManager;

        protected BaseParticleGenerator(ParticleManager particleManager, TimeSpan lifespan)
        {
            _particleManager = particleManager;
            _lifespan = lifespan;
            _timer = lifespan;
        }

        public bool IsExpired => _timer <= TimeSpan.Zero;

        /// <summary>
        /// Subclasses should override this to define specific particle generation logic.
        /// </summary>
        public abstract void Update(GameTime gameTime);
    }
}
