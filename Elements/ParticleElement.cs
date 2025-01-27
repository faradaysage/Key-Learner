using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLearner.Elements
{
    public class ParticleElement
    {
        public BodyHandle BodyHandle { get; set; }
        public float Lifetime { get; set; }
        public float Radius { get; set; }
        public float Alpha { get; set; }
        public Microsoft.Xna.Framework.Color Color { get; set; }

    }
}
