using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLearner
{
    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new();
        public bool IsWord { get; set; }
    }

    

}
