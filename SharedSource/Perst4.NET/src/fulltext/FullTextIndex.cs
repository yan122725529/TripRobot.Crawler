using System;
using System.IO;
using Perst;

namespace Perst.FullText
{       
    /// <summary> Full text search index.
    /// This index split document text in words, perform stemming of the words and build inverse index.
    /// Full text index is able to execute search queries with logical operators (AND/OR/NOT) and 
    /// strict match. Returned results are ordered by rank, which includes inverse document frequency (IDF),
    /// frequency of word in the document, occurrence kind and nearness of query keywords in the document text.
    /// </summary>
    public interface FullTextIndex:IPersistent, IResource
    {
        /// <summary> Add document to the index</summary>
        /// <param name="obj">document to be added
        /// </param>
        void  Add(FullTextSearchable obj);
        
        /// <summary> Add document to the index</summary>
        /// <param name="obj">document to be added
        /// </param>
        /// <param name="text">document text to be indexed
        /// </param>
        /// <param name="language">language of the text
        /// </param>
        void  Add(object obj, TextReader text, string language);
        
        /// <summary> Delete document from the index</summary>
        /// <param name="obj">document to be deleted
        /// </param>
        void  Delete(object obj);
        
        /// <summary>
        /// Remove all elements from full text index
        /// </summary>
        void Clear();

        /// <summary> Locate all documents containing words started with specified prefix
        /// </summary> 
        /// <param name="prefix">word prefix
        /// </param>
        /// <param name="maxResults">maximal amount of selected documents
        /// </param>
        /// <param name="timeLimit">limit for query execution time
        /// </param>
        /// <param name="sort"> whether it is necessary to sort result by rank
        /// </param>
        /// <returns> result of query execution ordered by rank or null in case of empty or incorrect query
        /// </returns>
        FullTextSearchResult SearchPrefix(string prefix, int maxResults, int timeLimit, bool sort);

        /// <summary> Parse and execute full text search query</summary>
        /// <param name="query">text of the query
        /// </param>
        /// <param name="language">language if the query
        /// </param>
        /// <param name="maxResults">maximal amount of selected documents
        /// </param>
        /// <param name="timeLimit">limit for query execution time
        /// </param>
        /// <returns> result of query execution ordered by rank or null in case of empty or incorrect query
        /// </returns>
        FullTextSearchResult Search(string query, string language, int maxResults, int timeLimit);
        
        /// <summary> Execute full text search query</summary>
        /// <param name="query">prepared query
        /// </param>
        /// <param name="maxResults">maximal amount of selected documents
        /// </param>
        /// <param name="timeLimit">limit for query execution time
        /// </param>
        /// <returns> result of query execution ordered by rank or null in case of empty or incorrect query
        /// </returns>
        FullTextSearchResult Search(FullTextQuery query, int maxResults, int timeLimit);
        
        /// <summary> Get total number of different words in all documents</summary>
        int NumberOfWords { get; }
        
        /// <summary> Get total number of indexed documents</summary>
        int NumberOfDocuments { get; }
        
        /// <summary> Get full text search helper</summary>
        FullTextSearchHelper Helper { get; }

        /// <summary> 
        /// Get enumerator through full text index keywords started with specified prefix
        /// </summary> 
        /// <param name="prefix">keyword prefix (user empty string to get list of all keywords)</param>
        /// <returns>enumerator through list of all keywords with specified prefix</returns>
#if NET_FRAMEWORK_20
        System.Collections.Generic.IEnumerable<Keyword> GetKeywords(string prefix);
#else
        System.Collections.IEnumerable GetKeywords(string prefix);
#endif
    }

    /// <summary> 
    /// Description of full text index keyword
    /// </summary> 
    public interface Keyword 
    { 
        /**
         * Normal form of the keyword
         */
        string NormalForm
        {
            get;
        }

        /**
         * Number of keyword occurrences (number of documents containing this keyword)
         */
        long NumberOfOccurrences
        {
            get;
        }
    }
}