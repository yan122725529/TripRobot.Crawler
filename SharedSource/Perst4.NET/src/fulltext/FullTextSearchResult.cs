using System;
using System.Collections;

namespace Perst.FullText
{
    
    /// <summary> Full text search result</summary>
    public class FullTextSearchResult
    {
        /// <summary> Estimation of total number of documents in the index matching this query.
        /// Full text search query result is usually limited by number of returned documents
        /// and query execution time. So there are can be more documents in the index matching this query than 
        /// actually returned. This field provides estimation for total number of documents matching the query.
        /// </summary>
        public int Estimation;
        
        /// <summary> Full text search result hits</summary>
        public FullTextSearchHit[] Hits;
        
        private class OidComparer : IComparer  
        {
            int IComparer.Compare(object x, object y)  
            {
                return ((FullTextSearchHit)x).oid - ((FullTextSearchHit)y).oid;
            }
        }

        /// <summary> Merge results of two searches
        /// </summary>
        /// <param name="another">Full text search result to merge with this result</param>
        /// <returns>Result set containing documents present in both result sets</returns>
        public FullTextSearchResult Merge(FullTextSearchResult another) 
        {
            if (Hits.Length == 0 || another.Hits.Length == 0) 
            {
                return new FullTextSearchResult(new FullTextSearchHit[0], 0);
            }
            FullTextSearchHit[] joinHits = new FullTextSearchHit[Hits.Length + another.Hits.Length];
            Array.Copy(Hits, 0, joinHits, 0, Hits.Length);
            Array.Copy(another.Hits, 0, joinHits, Hits.Length, another.Hits.Length);
            Array.Sort(joinHits, new OidComparer());
            int n = 0;
            for (int i = 1; i < joinHits.Length; i++) 
            { 
                if (joinHits[i-1].oid == joinHits[i].oid) 
                {      
                    joinHits[i].Rank += joinHits[i-1].Rank;
                    joinHits[n++] = joinHits[i];
                    i += 1;
                } 
            }
            FullTextSearchHit[] mergeHits = new FullTextSearchHit[n];
            Array.Copy(joinHits, 0, mergeHits, 0, n);
            Array.Sort(joinHits);
            return new FullTextSearchResult(joinHits, Math.Min(Estimation*n/Hits.Length, another.Estimation*n/another.Hits.Length));
        }

        public FullTextSearchResult(FullTextSearchHit[] hits, int estimation)
        {
            Hits = hits;
            Estimation = estimation;
        }
    }

    /// <summary> Class representing full text search result hit (document + rank)</summary>
    public class FullTextSearchHit : IComparable
    {
        /// <summary> Get document matching full text query </summary>
        public object Document
        {
            get
            {
                return storage.GetObjectByOID(oid);
            }
        }
        
        /// <summary> Rank of the document for this query</summary>
        public float Rank;
        
        internal int oid;
        internal Storage storage;
        
        public int CompareTo(object o)
        {
            float oRank = ((FullTextSearchHit) o).Rank;
            return Rank > oRank?-1:(Rank < oRank?1:0);
        }
        
        /// <summary> Constructor of the full text search result hit</summary>
        public FullTextSearchHit(Storage storage, int oid, float rank)
        {
            this.storage = storage;
            this.oid = oid;
            Rank = rank;
        }
    }
}