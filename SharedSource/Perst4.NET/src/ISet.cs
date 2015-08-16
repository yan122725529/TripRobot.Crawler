using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst
{
    ///<summary>
    /// Interface of objects set
    /// </summary>
#if USE_GENERICS
    public interface ISet<T> : IPersistent, IResource, ITable<T> where T:class
#else
    public interface ISet : IPersistent, IResource, ITable
#endif
    {
#if !USE_GENERICS
        /// <summary>
        /// Check if set contains specified element
        /// </summary>
        /// <param name="o">checked element</param>
        /// <returns><b>true</b> if elementis in set</returns>
        bool Contains(object o);
#endif

        /// <summary>
        /// Check if the set contains all members from specified collection
        /// </summary>
        /// <param name="c">collection specifying members</param>
        /// <returns><b>true</b> if all members of enumerator are present in the set</returns>
#if USE_GENERICS
        bool ContainsAll(ICollection<T> c);
#else
        bool ContainsAll(ICollection c);
#endif

#if !USE_GENERICS
        /// <summary>
        /// Add new element to the set
        /// </summary>
        /// <param name="o">element to be added</param>
        void Add(object o);
#endif

        /// <summary>
        /// Add all elements from specified collection to the set
        /// </summary>
        /// <param name="c">collection specifying members</param>
        /// <returns><b>true</b> if at least one element was added to the set,
        /// <b>false</b> if now new elements were added</returns>
#if USE_GENERICS
        bool AddAll(ICollection<T> c);
#else
        bool AddAll(ICollection c);
#endif

#if !USE_GENERICS
        /// <summary> 
        /// Remove element from the set
        /// </summary>
        /// <param name="o">removed element</param>
        /// <returns><b>true</b> if element was successfully removed,
        /// <b>false</b> if there is not such element in the set</returns>
        bool Remove(object o);
#endif
    
        /// <summary>
        /// Remove from the set all members from the specified enumerator
        /// </summary>
        /// <param name="c">collection specifying members</param>
        /// <returns></returns>
#if USE_GENERICS
        bool RemoveAll(ICollection<T> c);
#else
        bool RemoveAll(ICollection c);
#endif

        /// <summary>
        /// Copy all set members to an array
        /// </summary>
        /// <returns>array of object with set members</returns>
#if USE_GENERICS
        T[] ToArray();
#else
        object[] ToArray();
#endif
        
        /// <summary>
        /// Copy all set members to an array of specified type
        /// </summary>
        /// <param name="elemType">type of array element</param>
        /// <returns>array of specified type with members of the set</returns>
        Array ToArray(Type elemType);

        /// <summary>
        /// Perform join of two sorted set. This method can be used to incrementally
        /// join two or more inverse lists (represented using IPersistentSet collections).
        /// For example, assume that we need to implement own primitive full text search engine
        /// (please notice that Perst has builtin full text search engine).
        /// So we have inverse index keyword-&gt;list of documents with occurrences of this keyword.
        /// Given full text query (set of keywords) we need to locate all documents
        /// which contains all specified keywords. It can be done in this way:
        /// <code>
        /// class FullTextSearchEngine : PersistentResource {
        ///     Index&lt;ISet&lt;Document&gt;&gt; inverseIndex;
        ///     public FullTextSearchEngine(Storage storage) : base (storage) { 
        ///         inverseIndex = storage.CreateIndex&lt;string,ISet&lt;Document&gt;&gt;CreateIndex(true);
        ///     }
        ///     public IEnumerable&lt;Document&gt; Search(ICollection&lt;string&gt; keywords) {
        ///         IEnumerable&lt;Document&lt; result = null;
        ///         foreach (string keyword in keywords) {
        ///             ISet&lt;Document&gt; occurrences = inverseIndex.Get(keyword); 
        ///             if (occurrences == null) {
        ///                 return null;
        ///             }
        ///             result = occurrences.Join(result);
        ///         }
        ///         return result;
        ///     }
        /// }
        /// </code>
        /// </summary>
        /// 
        /// <param name="with">set of object ordered by OID. Usually it is iterator of ISet class.
        /// This parameter may be null, in this case iterator of the target persistent set is returned.</param>
        /// <returns>iterator through result of two sets join. Join is performed incrementally so join
        /// of two large sets will not consume a lot of memory and first results of such join
        /// can be obtains fast enough.</returns>
        ////
#if USE_GENERICS
       IEnumerable<T> Join(IEnumerable<T> with);
#else
       IEnumerable Join(IEnumerable with);
#endif
    }
}
