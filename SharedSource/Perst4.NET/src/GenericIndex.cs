namespace Perst
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;

    public enum IterationOrder 
    {
        AscentOrder, 
        DescentOrder
    };
    
#if USE_GENERICS
    /// <summary> Interface of unparemtrized object index.
    /// Index is used to provide fast access to the object by key. 
    /// Object in the index are stored ordered by key value. 
    /// It is possible to select object using exact value of the key or 
    /// select set of objects which key belongs to the specified interval 
    /// (each boundary can be specified or unspecified and can be inclusive or exclusive)
    /// Key should be of scalar, String, DateTime or peristent object type.
    /// </summary>
    public interface GenericIndex : IPersistent, IResource, IEnumerable
    {
        /// <summary>
        /// Get type of index key
        /// </summary>
        /// <returns>type of index key</returns>
        Type KeyType {get;}

        /// <summary>
        /// Get enumerable collection of objects in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>enumerable collection</returns>
        ///
        IEnumerable Range(Key from, Key till, IterationOrder order);

        /// <summary>
        /// Get enumerable ascent ordered collection of objects in the index which key starts with specified prefix. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="prefix">String key prefix</param>
        /// <returns>enumerable collection</returns>
        ///
        IEnumerable StartsWith(string prefix);

        /// <summary>
        /// Get enumerable ascent or descent ordered collection of objects in the index which key starts with specified prefix. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="prefix">String key prefix</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>enumerable collection</returns>
        ///
        IEnumerable StartsWith(string prefix, IterationOrder order);

        /// <summary>
        /// Check if index is unique
        /// </summary>
        bool IsUnique { get; }
    }

    /// <summary> Interface of object index with arbitrary key type
    /// Index is used to provide fast access to the object by key. 
    /// Object in the index are stored ordered by key value. 
    /// It is possible to select object using exact value of the key or 
    /// select set of objects which key belongs to the specified interval 
    /// (each boundary can be specified or unspecified and can be inclusive or exclusive)
    /// Key should be of scalar, String, DateTime or peristent object type.
    /// </summary>
    public interface GenericKeyIndex<V> : ITable<V>, GenericIndex 
#else
    /// <summary> Interface of object index.
    /// Index is used to provide fast access to the object by key. 
    /// Object in the index are stored ordered by key value. 
    /// It is possible to select object using exact value of the key or 
    /// select set of objects which key belongs to the specified interval 
    /// (each boundary can be specified or unspecified and can be inclusive or exclusive)
    /// Key should be of scalar, String, DateTime or peristent object type.
    /// </summary>
    public interface GenericIndex : IPersistent, IResource, ITable
#endif
    {
        /// <summary> Get object by key (exact match)     
        /// </summary>
        /// <param name="key">wrapper of the specified key. It should match with type of the index and should be inclusive.
        /// </param>
        /// <returns>object with this value of the key or <b>null</b> if key nmot found
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE) exception if there are more than 
        /// one objects in the index with specified value of the key.
        /// 
        /// </exception>
#if USE_GENERICS
        V Get(Key key);
#else
        object Get(Key key);
#endif

        /// <summary> Get objects which key value belongs to the specified range.
        /// Either from boundary, either till boundary either both of them can be <b>null</b>.
        /// In last case the method returns all objects from the index.
        /// </summary>
        /// <param name="from">low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive. 
        /// </param>
        /// <param name="till">high boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive. 
        /// </param>
        /// <returns>array of objects which keys belongs to the specified interval, ordered by key value
        /// 
        /// </returns>
#if USE_GENERICS
        V[] Get(Key from, Key till);
#else
        object[] Get(Key from, Key till);
#endif

        /// <summary> Get objects which key starts with specifid prefix.
        /// </summary>
        /// <param name="prefix">String key prefix</param>
        /// <returns>array of objects which key starts with specifid prefix, ordered by key value 
        /// </returns>
#if USE_GENERICS
        V[] GetPrefix(string prefix);
#else
        object[] GetPrefix(string prefix);
#endif

        /// <summary> 
        /// Locate all objects which key is prefix of specified word.
        /// </summary>
        /// <param name="word">string which prefixes are located in index</param>
        /// <returns>array of objects which key is prefix of specified word, ordered by key value
        /// </returns>
#if USE_GENERICS
        V[] PrefixSearch(string word);
#else
        object[] PrefixSearch(string word);
#endif

        /// <summary> Get number of objects in the index
        /// </summary>
        /// <returns>number of objects in the index
        /// </returns>
        int Size();

        /// <summary> Get all objects in the index as array orderd by index key
        /// </summary>
        /// <returns>array of objects in the index ordered by key value
        /// </returns>
#if USE_GENERICS
        V[] ToArray();
#else
        object[] ToArray();
#endif

        /// <summary> Get all objects in the index as array of specified type ordered by index key
        /// </summary>
        /// <param name="elemType">type of array element</param>
        /// <returns>array of objects in the index ordered by key value
        /// </returns>
        Array ToArray(Type elemType);

        /// <summary>
        /// Get enumerator for traversing objects in ascent order belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <returns>selection enumerator</returns>
        ///
#if USE_GENERICS
        IEnumerator<V> GetEnumerator(Key from, Key till);
#else
        IEnumerator GetEnumerator(Key from, Key till);
#endif

        /// <summary>
        /// Get object with the largest value of the key
        /// </summary>
        /// <returns>Last object in the index or null if index is empty</returns>
#if USE_GENERICS
        V Last {get;}
#else
        object Last { get; }
#endif
                
        /// <summary>
        /// Get object with the smallest value of the key
        /// </summary>
        /// <returns>First object in the index or null if index is empty</returns>
#if USE_GENERICS
        V First {get;}
#else
        object First { get; }
#endif
                

        /// <summary>
        /// Get enumerator for traversing objects in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>selection enumerator</returns>
        ///
#if USE_GENERICS
        IEnumerator<V> GetEnumerator(Key from, Key till, IterationOrder order);
#else
        IEnumerator GetEnumerator(Key from, Key till, IterationOrder order);
#endif

        /// <summary>
        /// Get enumerator for traversing objects in ascent order which key starts with specified prefix. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="prefix">String key prefix</param>
        /// <returns>selection enumerator</returns>
        ///
#if USE_GENERICS
        IEnumerator<V> GetEnumerator(string prefix);
#else
        IEnumerator GetEnumerator(string prefix);
#endif

        /// <summary>
        /// Get enumerable collection of objects in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>enumerable collection</returns>
        ///
#if USE_GENERICS
        new IEnumerable<V> Range(Key from, Key till, IterationOrder order);
#else
        IEnumerable Range(Key from, Key till, IterationOrder order);
#endif

        /// <summary>
        /// Get enumerable ascent ordered collection of objects in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <returns>enumerable collection</returns>
        ///
#if USE_GENERICS
        IEnumerable<V> Range(Key from, Key till);
#else
        IEnumerable Range(Key from, Key till);
#endif

        /// <summary>
        /// Get enumerable collection of objects in descending order
        /// </summary>
        /// <returns>enumerable collection</returns>
        ///
#if USE_GENERICS
        IEnumerable<V> Reverse();
#else
        IEnumerable Reverse();
#endif
        
        /// <summary>
        /// Get enumerable ascent ordered collection of objects in the index which key starts with specified prefix. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="prefix">String key prefix</param>
        /// <returns>enumerable collection</returns>
        ///
#if USE_GENERICS
        new IEnumerable<V> StartsWith(string prefix);
#else
        IEnumerable StartsWith(string prefix);
#endif

        /// <summary>
        /// Get enumerable ascent or descent ordered collection of objects in the index which key starts with specified prefix. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="prefix">String key prefix</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>enumerable collection</returns>
        ///
#if USE_GENERICS
        new IEnumerable<V> StartsWith(string prefix, IterationOrder order);
#else
        IEnumerable StartsWith(string prefix, IterationOrder order);
#endif

        /// <summary>
        /// Get enumerator for traversing all entries in the index 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <returns>entry enumerator</returns>
        ///
        IDictionaryEnumerator GetDictionaryEnumerator();
        
        /// <summary>
        /// Get enumerator for traversing entries in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <param name="order"><b>AscentOrder</b> or <b>DescentOrder</b></param>
        /// <returns>selection enumerator</returns>
        ///
        IDictionaryEnumerator GetDictionaryEnumerator(Key from, Key till, IterationOrder order);

#if !USE_GENERICS
        /// <summary>
        /// Get type of index key
        /// </summary>
        /// <returns>type of index key</returns>
        Type KeyType {get;}
#endif

#if USE_GENERICS
    }
    /// <summary> Interface of object index with arbitrary key type
    /// Index is used to provide fast access to the object by key. 
    /// Object in the index are stored ordered by key value. 
    /// It is possible to select object using exact value of the key or 
    /// select set of objects which key belongs to the specified interval 
    /// (each boundary can be specified or unspecified and can be inclusive or exclusive)
    /// Key should be of scalar, String, DateTime or peristent object type.
    /// </summary>
    public interface GenericIndex<K,V> : GenericKeyIndex<V> where V:class
    {
#endif
        /// <summary> Access element by key
        /// </summary>
#if USE_GENERICS
        V this[K key] 
#else
        object this[object key] 
#endif
        {
            get;
            set;
        }       

        /// <summary> Get objects which key value belongs to the specified range.
        /// </summary>
#if USE_GENERICS
        V[] this[K from, K till] 
#else
        object[] this[object from, object till] 
#endif
        {
            get;
        }       

        /// <summary> Get object by key (exact match)     
        /// </summary>
        /// <param name="key">specified key value. It should match with type of the index and should be inclusive.
        /// </param>
        /// <returns>object with this value of the key or <b>null</b> if key nmot found
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE) exception if there are more than 
        /// one objects in the index with specified value of the key.
        /// 
        /// </exception>
#if USE_GENERICS
        V Get(K key);
#else
        object Get(object key);
#endif


        /// <summary> Get objects which key value belongs to the specified inclusive range.
        /// Either from boundary, either till boundary either both of them can be <b>null</b>.
        /// In last case the method returns all objects from the index.
        /// </summary>
        /// <param name="from">Inclusive low boundary. If <b>null</b> then low boundary is not specified.
        /// </param>
        /// <param name="till">Inclusive high boundary. If <b>null</b> then high boundary is not specified.
        /// </param>
        /// <returns>array of objects which keys belongs to the specified interval, ordered by key value
        /// 
        /// </returns>
#if USE_GENERICS
        V[] Get(K from, K till);
#else
        object[] Get(object from, object till);
#endif

        /// <summary>
        /// Get enumerator for traversing objects in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>selection enumerator</returns>
        ///
#if USE_GENERICS
        IEnumerator<V> GetEnumerator(K from, K till, IterationOrder order);
#else
        IEnumerator GetEnumerator(object from, object till, IterationOrder order);
#endif

        /// <summary>
        /// Get enumerator for traversing objects in ascent order belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Low boundary. If <b>null</b> then low boundary is not specified.
        /// Low boundary can be inclusive or exclusive.</param>
        /// <param name="till">High boundary. If <b>null</b> then high boundary is not specified.
        /// High boundary can be inclusive or exclusive.</param>
        /// <returns>selection enumerator</returns>
        ///
#if USE_GENERICS
        IEnumerator<V> GetEnumerator(K from, K till);
#else
        IEnumerator GetEnumerator(object from, object till);
#endif

        /// <summary>
        /// Get enumerable collection of objects in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Inclusive low boundary. If <b>null</b> then low boundary is not specified.</param>
        /// <param name="till">Inclusive high boundary. If <b>null</b> then high boundary is not specified.</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>enumerable collection</returns>
        ///
#if USE_GENERICS
        IEnumerable<V> Range(K from, K till, IterationOrder order);
#else
        IEnumerable Range(object from, object till, IterationOrder order);
#endif

        /// <summary>
        /// Get enumerable ascent ordered collection of objects in the index with key belonging to the specified range. 
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="from">Inclusive low boundary. If <b>null</b> then low boundary is not specified.</param>
        /// <param name="till">Inclusive high boundary. If <b>null</b> then high boundary is not specified.</param>
        /// <returns>enumerable collection</returns>
        ///
#if USE_GENERICS
        IEnumerable<V> Range(K from, K till);
#else
        IEnumerable Range(object from, object till);
#endif

        /// <summmary>
        /// Get element at specified position. This methid is efficient only for random access indices
        /// </summmary>
        /// <param name="i">position of element in the index</param>
        /// <returns>object at sepcified position</returns>
        /// <exception cref="System.IndexOutOfRangeException">System.IndexOutOfRangeException if position is less than 0 or greater or equal than index size</exception> 
#if USE_GENERICS
        V GetAt(int i);
#else
        object GetAt(int i);
#endif
                
        /// <summary>
        /// Get position of the first element with specified key. This method is efficient only for random access indices
        /// </summary>
        /// <param name="key">located key</param>
        /// <returns>position of the first element with this key or -1 if no such element is found</returns>
        int IndexOf(Key key);


        /// <summary>
        /// Get dictionary enumerator of objects in the index starting with specified position.
        /// This methid is efficient only for random access indices
        /// You should not update/remove or add members to the index during iteration
        /// </summary>
        /// <param name="start">Start position in the index. First <b>pos</b> elements will be skipped.</param>
        /// <param name="order"><b>IterationOrder.AscentOrder</b> or <b>IterationOrder.DescentOrder</b></param>
        /// <returns>dictionary enumerator</returns>
        ///
        IDictionaryEnumerator GetDictionaryEnumerator(int start, IterationOrder order);

#if !USE_GENERICS
        /// <summary>
        /// Check if index is unique
        /// </summary>
        bool IsUnique { get; }
#endif
    }
}
