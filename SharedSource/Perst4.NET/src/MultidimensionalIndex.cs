namespace Perst
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;

    /// <summary>
    /// Interface of multidimensional index.
    /// The main idea of this index is to provide efficient search of object using multiple search criteria, for example
    /// "select from StockOptions where Symbol between 'AAB' and 'ABC' and Price > 100 and Volume between 1000 and 10000".
    /// Each component of the object is represented as separate dimension in the index.
    /// </summary>
#if USE_GENERICS
    public interface MultidimensionalIndex<T> :  ITable<T>, IPersistent, IResource where T:class
#else
    public interface MultidimensionalIndex : ITable, IPersistent, IResource
#endif
    {
        /// <summary>
        /// Get comparator used in this index
        /// </summary>
        /// <returns>comparator used to compare objects in this index</returns>
        ///
#if USE_GENERICS
        MultidimensionalComparator<T> Comparator { get; }
#else
        MultidimensionalComparator Comparator
        {
            get;
        }
#endif

#if !USE_GENERICS
        /// <summary> Remove object from the index
        /// </summary>
        /// <param name="obj">object removed from the index. Object should contain indexed field. 
        /// </param>
        /// <returns><b>true</b> if member was successfully removed or <b>false</b> if member is not found</returns>
        bool Remove(object obj);

        /// <summary> 
        /// Check if index contains specified object
        /// </summary>
        /// <param name="obj">object to be searched in the index. Object should contain indexed field. 
        /// </param>
        /// <returns><b>true</b> if object is present in the index, <b>false</b> otherwise
        /// </returns>
        bool Contains(object obj);

        /// <summary> Add object to the index. 
        /// </summary>
        /// <param name="obj">object to be inserted in index. 
        /// </param>
        void Add(object obj);
#endif

        /// <summary>
        /// Get iterator through objects matching specified pattern object.
        /// All fields which are part of multidimensional index and which values in pattern object is not null
        /// are used as filter for index members.
        /// </summary>
        /// <param name="pattern">object which is used as search pattern, non null values of components of 
        /// this object forms search condition</param>
        /// <returns>iterator through index members which field values are equal to correspondent non-null fields of pattern object</returns>
#if USE_GENERICS
        IEnumerator<T> GetEnumerator(T pattern);
#else
        IEnumerator GetEnumerator(object pattern);
#endif

        /// <summary>
        /// Get iterator through objects which field values belongs to the range specified by correspondent
        /// fields of low and high objects.
        /// </summary>
        /// <param name="low">pattern object specifying inclusive low boundary for field values. If there is no low boundary for some particular field
        /// it should be set to null. For scalar types (like int) you can use instead minimal possible value, 
        /// like int.MinValue. If low is null, then low boundary is not specified for all fields.</param>
        /// <param name="high">pattern object specifying inclusive high boundary for field values. 
        /// If there is no high boundary for some particular field it should be set to null. 
        /// For scalar types (like int) you can use instead maximal possible value, like int.MaxValue.
        /// If high is null, then high boundary is not specified for all fields.</param>
        /// <returns>iterator through index members which field values are belongs to the range specified by 
        /// correspondent fields of low and high objects</returns>
#if USE_GENERICS
        IEnumerator<T> GetEnumerator(T low, T high);
#else
        IEnumerator GetEnumerator(object low, object high);
#endif

        /// <summary>
        /// Get enumerable for collection of objects which field values belongs to the range specified by correspondent
        /// fields of low and high objects.
        /// </summary>
        /// <param name="low">pattern object specifying inclusive low boundary for field values. If there is no low boundary for some particular field
        /// it should be set to null. For scalar types (like int) you can use instead minimal possible value, 
        /// like int.MinValue. If low is null, then low boundary is not specified for all fields.</param>
        /// <param name="high">pattern object specifying inclusive high boundary for field values. 
        /// If there is no high boundary for some particular field it should be set to null. 
        /// For scalar types (like int) you can use instead maximal possible value, like int.MaxValue.
        /// If high is null, then high boundary is not specified for all fields.</param>
        /// <returns>iterator through index members which field values are belongs to the range specified by 
        /// correspondent fields of low and high objects</returns>
#if USE_GENERICS
        IEnumerable<T> Range(T low, T high);
#else
        IEnumerable Range(object low, object high);
#endif

        /// <summary>
        /// Get array of index members matching specified pattern object.
        /// All fields which are part of multidimensional index and which values in pattern object is not null
        /// are used as filter for index members.
        /// </summary>
        /// <param name="pattern">object which is used as search pattern, non null values of components of this object 
        /// forms search condition</param>
        /// <returns>array of index members which field values are equal to correspondent non-null fields of pattern object</returns>
#if USE_GENERICS
        T[] QueryByExample(T pattern);
#else
        object[] QueryByExample(object pattern);
#endif

        /// <summary>
        /// Get array of index members which field values belongs to the range specified by correspondent
        /// fields of low and high objects.
        /// </summary>
        /// <param name="low">pattern object specifying inclusive low boundary for field values. If there is no low boundary for some particular field
        /// it should be set to null. For scalar types (like int) you can use instead minimal possible value, 
        /// like int.MinValue. If low is null, then low boundary is not specified for all fields.</param>
        /// <param name="high">pattern object specifying inclusive high boundary for field values. 
        /// If there is no high boundary for some particular field it should be set to null. 
        /// For scalar types (like int) you can use instead maximal possible value, like int.MaxValue.
        /// If high is null, then high boundary is not specified for all fields.</param>
        /// <returns>array of index members which field values are belongs to the range specified by correspondent fields
        /// of low and high objects</returns>
#if USE_GENERICS
        T[] QueryByExample(T low, T high);
#else
        object[] QueryByExample(object low, object high);
#endif


        /// <summary>
        /// Optimize index to make search more efficient.
        /// This operation cause complete reconstruction of the index and so may take a long time.
        /// Also please notice that this method doesn't build the ideally balanced tree - it just reinserts
        /// elements in the tree in random order
        /// </summary>
        void Optimize();

        /// <summary>
        /// Height of the tree. Height of the tree can be used by application
        /// to determine when tree structure is no optimal and tree should be reconstructed 
        /// using optimize method.
        /// </summary>
        int  Height {get;}
    }
}
