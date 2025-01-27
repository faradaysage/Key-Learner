using KeyLearner.Core.Interfaces;
using KeyLearner.Core.Models;
using System;

namespace KeyLearner.Core.Models
{
    public class ModeMetadata
    {
        public string Name { get; set; }
        public GameLevel Level { get; set; }
        public string Description { get; set; }
        public Type ModeType { get; set; }
    }
}