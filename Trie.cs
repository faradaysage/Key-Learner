using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLearner
{
    public class Trie
    {
        private readonly TrieNode _root = new();

        public void Insert(string word)
        {
            var current = _root;
            foreach (var c in word)
            {
                if (!current.Children.ContainsKey(c))
                {
                    current.Children[c] = new TrieNode();
                }
                current = current.Children[c];
            }
            current.IsWord = true;
        }

        public bool Search(string word)
        {
            var current = _root;
            foreach (var c in word)
            {
                if (!current.Children.ContainsKey(c)) return false;
                current = current.Children[c];
            }
            return current.IsWord;
        }

        public IEnumerable<string> GetMatchingWords(string prefix)
        {
            var current = _root;
            foreach (var c in prefix)
            {
                if (!current.Children.ContainsKey(c)) return Enumerable.Empty<string>();
                current = current.Children[c];
            }

            return GetWordsFromNode(current, prefix);
        }

        private IEnumerable<string> GetWordsFromNode(TrieNode node, string prefix)
        {
            var results = new List<string>();
            if (node.IsWord) results.Add(prefix);

            foreach (var kvp in node.Children)
            {
                results.AddRange(GetWordsFromNode(kvp.Value, prefix + kvp.Key));
            }
            return results;
        }
    }
}
