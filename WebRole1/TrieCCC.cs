using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Tnode
    {
        public char value { get; set; }
        public Dictionary<char, Tnode> children { get; set; }
        public bool isWord;
    }

    public class TrieStuff
    {

        public Tnode root { get; set; }


        public TrieStuff()
        {
            root = new Tnode() { value = ' ' };

        }


        /// <summary>
        /// This function adds new characters from page titles into the Trie structure
        /// </summary>
        /// <param name="line">Reads each line of word</param>
        public void addToTrie(string line)
        {
            Tnode current = root;
            Tnode tmp = null;

            foreach (char ch in line)
            {
                if (current.children == null)
                {
                    current.children = new Dictionary<char, Tnode>();
                }

                if (!current.children.ContainsKey(ch))
                {
                    tmp = new Tnode() { value = ch };
                    current.children.Add(ch, tmp);
                }

                current = current.children[ch];
            }

            current.isWord = true;
        }

        /// <summary>
        /// Searches Trie structure to see if there is matching word
        /// </summary>
        /// <param name="word">String that client enters</param>
        /// <returns>List of matching words</returns>
        public List<string> searchPrefix(string word)
        {
            List<string> results = new List<string>();
            Tnode current = root;
            string prefix = String.Empty;

            foreach (char c in word)
            {
                if (current.children.ContainsKey(c))
                {
                    prefix += c;
                    current = current.children[c];
                }
                else
                {
                    break;
                }
            }

            if (current.isWord && results.Count < 10)
            {
                results.Add(prefix);
            }

            traverseTrie(prefix, current, results);

            return results;
        }

        /// <summary>
        /// Recursive function to traverse through every Trie node
        /// </summary>
        /// <param name="word">Prefix or string that client inputs</param>
        /// <param name="root">Node</param>
        /// <param name="results">List of matching words</param>
        public void traverseTrie(string word, Tnode root, List<string> results)
        {
            if (root.children == null)
            {
                return;
            }

            foreach (Tnode node in root.children.Values)
            {
                string temp = word + node.value;
                if (node.isWord)
                {
                    if (results.Count >= 10)
                    {
                        break;
                    }
                    else
                    {
                        results.Add(temp);
                    }
                }
                traverseTrie(temp, node, results);
            }
        }



    } //Closes the TrieStuff class
}