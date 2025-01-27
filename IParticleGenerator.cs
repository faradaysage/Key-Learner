using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLearner
{
    public interface IParticleGenerator
    {
        /// <summary>
        /// Determines whether the generator has expired and can be removed.
        /// </summary>
        bool IsExpired { get; }

        /// <summary>
        /// Updates the state of the generator and its particles.
        /// </summary>
        void Update(GameTime gameTime);
    }
}
