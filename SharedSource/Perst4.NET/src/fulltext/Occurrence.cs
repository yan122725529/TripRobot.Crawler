using System;

namespace Perst.FullText
{
    
    /// <summary> Occurrence of word in the document</summary>
    public class Occurrence : IComparable
    {
        /// <summary> Word (lowercased)</summary>
        public string word;
        /// <summary> Position of word in document text (0 based)</summary>
        public int position;
        /// <summary> Word occurrence kind. 
        /// It is up to the document scanner implementation how to enumerate occurence kinds.
        /// These is only one limitation - number of difference kinds should not exceed 8.
        /// </summary>
        public int kind;
        
        /// <summary> Occurrence constructor</summary>
        /// <param name="word">lowercased word 
        /// </param>
        /// <param name="position">offset of word from the beginning of document text
        /// </param>
        /// <param name="kind">word occurrence kind (should be less than 8)
        /// </param>
        public Occurrence(string word, int position, int kind)
        {
            this.word = word;
            this.position = position;
            this.kind = kind;
        }
        
        public int CompareTo(object o)
        {
            Occurrence occ = (Occurrence) o;
            int diff = String.CompareOrdinal(word, occ.word);
            if (diff == 0)
            {
                diff = position - occ.position;
            }
            return diff;
        }
    }
}