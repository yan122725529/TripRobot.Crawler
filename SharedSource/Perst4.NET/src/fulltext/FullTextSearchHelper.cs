using System;
using System.Text;
using System.Collections;
using System.IO;
using Perst;

namespace Perst.FullText
{
    
    /// <summary> Helper class for full text search.
    /// This class provides functionality for parsing and stemming query
    /// and tuning document rank calculation
    /// </summary>
#if !SILVERLIGHT
    [Serializable]
#endif
    public class FullTextSearchHelper:Persistent
    {
        /// <summary> Perform stemming of the word</summary>
        /// <param name="word">word to be stemmed
        /// </param>
        /// <param name="language">language of the word (null if unknown)
        /// </param>
        /// <returns> normal forms of the word (some words belongs to more than one part of the speech, so there
        /// are can be more than one normal form)
        /// </returns>
        public virtual string[] GetNormalForms(string word, string language)
        {
            return new string[]{word};
        }
        
        /// <summary> Split text of the documents into tokens</summary>
        /// <param name="reader">stream with document text
        /// </param>
        /// <returns> array of occurrences of words in thedocument
        /// </returns>
        public virtual Occurrence[] ParseText(TextReader reader)
        {
            int pos = 0;
            ArrayList list = new ArrayList();
            int ch = reader.Read();
            
            while (ch > 0)
            {
                if (Char.IsLetter((char) ch) || Char.IsDigit((char) ch))
                {
                    StringBuilder buf = new StringBuilder();
                    int wordPos = pos;
                    do 
                    {
                        pos += 1;
                        buf.Append((char) ch);
                        ch = reader.Read();
                    }
                    while (ch > 0 && (Char.IsLetter((char) ch) || Char.IsDigit((char) ch)));
                    string word = buf.ToString().ToLower();
                    if (!IsStopWord(word))
                    {
                        list.Add(new Occurrence(word, wordPos, 0));
                    }
                }
                else
                {
                    pos += 1;
                    ch = reader.Read();
                }
            }
            return (Occurrence[])list.ToArray(typeof(Occurrence));
        }
        
        protected internal string AND;
        protected internal string OR;
        protected internal string NOT;
        
        [NonSerialized]
        protected internal Hashtable stopList;
        
        protected internal virtual void FillStopList()
        {
            stopList = new Hashtable();
            stopList["a"] = true;
            stopList["the"] = true;
            stopList["at"] = true;
            stopList["on"] = true;
            stopList["of"] = true;
            stopList["to"] = true;
            stopList["an"] = true;
        }
        
        public override void OnLoad()
        {
            FillStopList();
        }
        
        /// <summary> Check if word is stop word and should bw not included in index</summary>
        /// <param name="word">lowercased word
        /// </param>
        /// <returns> true if word is in stop list, false otherwize
        /// </returns>
        public virtual bool IsStopWord(string word)
        {
            return stopList.ContainsKey(word);
        }
        
        /*
        * Full text search helper constructor
        */
        public FullTextSearchHelper(Storage storage):base(storage)
        {
            AND = "AND";
            OR = "OR";
            NOT = "NOT";
            FillStopList();
        }
        
        protected internal FullTextSearchHelper()
        {
        }
        
        protected internal class QueryScanner
        {
            private FullTextSearchHelper helper;

            internal string query;
            internal int pos;
            internal bool inQuotes;
            internal bool unget;
            internal string word;
            internal int wordPos;
            internal int token;
            internal string language;
            
            internal QueryScanner(FullTextSearchHelper helper, string query, string language)
            {
                this.helper = helper;
                this.query = query;
                this.language = language;
            }
            
            internal const int TKN_EOQ = 0;
            internal const int TKN_WORD = 1;
            internal const int TKN_AND = 2;
            internal const int TKN_OR = 3;
            internal const int TKN_NOT = 4;
            internal const int TKN_LPAR = 5;
            internal const int TKN_RPAR = 6;
            
            internal virtual int scan()
            {
                if (unget)
                {
                    unget = false;
                    return token;
                }
                int len = query.Length;
                int p = pos;
                string q = query;
                while (p < len)
                {
                    char ch = q[p];
                    if (ch == '"')
                    {
                        inQuotes = !inQuotes;
                        p += 1;
                    }
                    else if (ch == '(')
                    {
                        pos = p + 1;
                        return token = TKN_LPAR;
                    }
                    else if (ch == ')')
                    {
                        pos = p + 1;
                        return token = TKN_RPAR;
                    }
                    else if (Char.IsLetter(ch) || Char.IsDigit(ch))
                    {
                        wordPos = p;
                        while (++p < len && (Char.IsLetter(q[p]) || Char.IsDigit(q[p])))
                            ;
                        string word = q.Substring(wordPos, (p) - (wordPos));
                        pos = p;
                        if (word.Equals(helper.AND))
                        {
                            return token = TKN_AND;
                        }
                        else if (word.Equals(helper.OR))
                        {
                            return token = TKN_OR;
                        }
                        else if (word.Equals(helper.NOT))
                        {
                            return token = TKN_NOT;
                        }
                        else
                        {
                            word = word.ToLower();
                            if (!helper.IsStopWord(word))
                            {
                                if (!inQuotes)
                                {
                                    // just get the first normal form and ignore all other alternatives 
                                    word = helper.GetNormalForms(word, language)[0];
                                }
                                this.word = word;
                                return token = TKN_WORD;
                            }
                        }
                    }
                    else
                    {
                        p += 1;
                    }
                }
                pos = p;
                return token = TKN_EOQ;
            }
        }
        
        protected internal virtual FullTextQuery disjunction(QueryScanner scanner)
        {
            FullTextQuery left = conjunction(scanner);
            if (scanner.token == QueryScanner.TKN_OR)
            {
                FullTextQuery right = disjunction(scanner);
                if (left != null && right != null)
                {
                    return new FullTextQueryBinaryOp(FullTextQuery.Operator.Or, left, right);
                }
                else if (right != null)
                {
                    return right;
                }
            }
            return left;
        }
        
        protected internal virtual FullTextQuery conjunction(QueryScanner scanner)
        {
            FullTextQuery left = term(scanner);
            if (scanner.token == QueryScanner.TKN_WORD || scanner.token == QueryScanner.TKN_AND)
            {
                if (scanner.token == QueryScanner.TKN_WORD)
                {
                    scanner.unget = true;
                }
                FullTextQuery.Operator cop = scanner.inQuotes ? FullTextQuery.Operator.Near : FullTextQuery.Operator.And;
                FullTextQuery right = disjunction(scanner);
                if (left != null && right != null)
                {
                    return new FullTextQueryBinaryOp(cop, left, right);
                }
                else if (right != null)
                {
                    return right;
                }
            }
            return left;
        }
        
        protected internal virtual FullTextQuery term(QueryScanner scanner)
        {
            FullTextQuery q = null;
            switch (scanner.scan())
            {
                
                case QueryScanner.TKN_NOT: 
                    q = term(scanner);
                    return (q != null) ? new FullTextQueryUnaryOp(FullTextQuery.Operator.Not, q) : null;
                
                case QueryScanner.TKN_LPAR: 
                    q = disjunction(scanner);
                    break;
                
                case QueryScanner.TKN_WORD: 
                    q = new FullTextQueryMatchOp(scanner.inQuotes ? FullTextQuery.Operator.StrictMatch : FullTextQuery.Operator.Match, scanner.word, scanner.wordPos);
                    break;
                
                case QueryScanner.TKN_EOQ: 
                    return null;

                default:
                    break;
            }
            scanner.scan();
            return q;
        }
        
        public virtual FullTextQuery ParseQuery(string query, string language)
        {
            return disjunction(new QueryScanner(this, query, language));
        }
        
        internal static readonly float[] OCCURRENCE_KIND_WEIGHTS = new float[0];
        
        /// <summary> Get occurrence kind weight. Occurrence kinds can be: in-title, in-header, emphased,...
        /// It is up to the document scanner implementation how to enumerate occurence kinds.
        /// These is only one limitation - number of difference kinds should not exceed 8.
        /// </summary>
        /// <returns> array with weights of each occurrence kind
        /// </returns>
        public virtual float[] OccurrenceKindWeights
        {
            get
            {
                return OCCURRENCE_KIND_WEIGHTS;
            }
        }
        
        /// <summary> Get weight of nearness criteria in document rank.
        /// Document rank is calculated as (keywordRank*(1 + nearness*nearnessWeight))
        /// </summary>
        /// <returns> weight of nearness criteria
        /// </returns>
        public virtual float NearnessWeight
        {
            get
            {
                return 10.0f;
            }
        }
        
        /// <summary> Get penalty of inverse word order in the text.
        /// Assume that document text contains phrase "ah oh ugh".
        /// And query "ugh ah" is executed. The distance between "ugh" and "ah"
        /// in the document text is 6. But as far as them are in difference order than in query, 
        /// this distance will be multiplied on "swap penalty", so if swap penalty is 10, then distance
        /// between these two word is considered to be 60.
        /// </summary>
        /// <returns> swap penalty
        /// </returns>
        public virtual int WordSwapPenalty
        {
            get
            {
                return 10;
            }
        }
    }
}