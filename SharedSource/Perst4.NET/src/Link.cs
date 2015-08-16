namespace Perst
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#else
    using System.Collections;
#endif
	
    /// <summary>
    /// Common interface for all links
    /// </summary>
    public interface GenericLink {
        /// <summary> Get number of the linked objects 
        /// </summary>
        /// <returns>the number of related objects
        /// 
        /// </returns>
        int Size();

        /// <summary> Get related object by index without loading it.
        /// Returned object can be used only to get it OID or to compare with other objects using
        /// <b>Equals</b> method
        /// </summary>
        /// <param name="i">index of the object in the relation
        /// </param>
        /// <returns>stub representing referenced object
        /// 
        /// </returns>
        object GetRaw(int i);

        /// <summary>
        /// Set owner object for this link. Owner is persistent object contaning this link.
        /// This method is mostly used by storage itself, but can also used explicityl by programmer if
        /// link component of one persistent object is assigned to component of another persistent object
        /// </summary>
        /// <param name="owner">Link owner</param>
        void SetOwner(object owner);

        /// <summary>
        /// Replace all direct references to linked objects with stubs. 
        /// This method is needed tyo avoid memory exhaustion in case when 
        /// there is a large numebr of objectys in databasse, mutually
        /// refefencing each other (each object can directly or indirectly 
        /// be accessed from other objects).
        /// </summary>
        void Unpin();     
     
       /// <summary>
       /// Replace references to elements with direct references.
       /// It will impove spped of manipulations with links, but it can cause
       /// recursive loading in memory large number of objects and as a result - memory
       /// overflow, because garabge collector will not be able to collect them
       /// </summary>
       void Pin();     
    }

    /// <summary> Interface for one-to-many relation. There are two types of relations:
    /// embedded (when references to the relarted obejcts are stored in lreation
    /// owner obejct itself) and stanalone (when relation is separate object, which contains
    /// the reference to the relation owner and relation members). Both kinds of relations
    /// implements Link interface. Embedded relation is created by Storage.createLink method
    /// and standalone relation is represented by Relation persistent class created by
    /// Storage.createRelation method.
    /// </summary>
#if USE_GENERICS
    public interface Link<T> : IList<T>, ITable<T>, GenericLink where T:class
#else
    public interface Link : ITable, GenericLink
#endif
    {
        /// <summary>Number of the linked objects 
        /// </summary>
        int Length {
             get;
             set;
        }        
        
#if !USE_GENERICS
        /// <summary> Access element by index
        /// </summary>
        object this[int i]
        {
             get;
             set;
        }       
#endif

		/// <summary> Get related object by index
        /// </summary>
        /// <param name="i">index of the object in the relation
        /// </param>
        /// <returns>referenced object
        /// 
        /// </returns>
#if USE_GENERICS
        T Get(int i);
#else
        object Get(int i);
#endif

        /// <summary> Replace i-th element of the relation
        /// </summary>
        /// <param name="i">index in the relartion
        /// </param>
        /// <param name="obj">object to be included in the relation     
        /// 
        /// </param>
#if USE_GENERICS
        void  Set(int i, T obj);
#else
        void  Set(int i, object obj);
#endif

        /// <summary> Remove object with specified index from the relation
        /// </summary>
        /// <param name="i">index in the relartion
        /// 
        /// </param>
        void  Remove(int i);

#if !USE_GENERICS
        /// <summary> Remove object from the relation
        /// </summary>
        /// <param name="obj">object to be removed
        /// </param>
        /// <returns><b>true</b> if member was successfully removed or <b>false</b> if member is not found</returns>
        bool Remove(object obj);
#endif

#if !USE_GENERICS
        /// <summary> Insert new object in the relation
        /// </summary>
        /// <param name="i">insert poistion, should be in [0,size()]
        /// </param>
        /// <param name="obj">object inserted in the relation
        /// 
        /// </param>
        void  Insert(int i, object obj);
#endif

#if !USE_GENERICS
        /// <summary> Add new object to the relation
        /// </summary>
        /// <param name="obj">object inserted in the relation
        /// 
        /// </param>
        void  Add(object obj);
#endif

        /// <summary> Add all elements of the array to the relation
        /// </summary>
        /// <param name="arr">array of obects which should be added to the relation
        /// 
        /// </param>
#if USE_GENERICS
        void  AddAll(T[] arr);
#else
        void  AddAll(object[] arr);
#endif

        /// <summary> Add specified elements of the array to the relation
        /// </summary>
        /// <param name="arr">array of obects which should be added to the relation
        /// </param>
        /// <param name="from">index of the first element in the array to be added to the relation
        /// </param>
        /// <param name="length">number of elements in the array to be added in the relation
        /// 
        /// </param>
#if USE_GENERICS
        void  AddAll(T[] arr, int from, int length);
#else
        void  AddAll(object[] arr, int from, int length);
#endif

        /// <summary> Add all object members of the other relation to this relation
        /// </summary>
        /// <param name="link">another relation
        /// 
        /// </param>
#if USE_GENERICS
        void  AddAll(Link<T> link);
#else
        void  AddAll(Link link);
#endif

        /// <summary> Get relation members as array of objects
        /// </summary>
        /// <returns>created array</returns>
#if USE_GENERICS
        T[] ToArray();
#else
        object[] ToArray();
#endif

        /// <summary> 
        /// Return array with relation members. Members are not loaded and 
        /// size of the array can be greater than actual number of members. 
        /// </summary>
        /// <returns>array of object with relation members used in implementation of Link class
        /// </returns>
        Array ToRawArray(); 


        /// <summary> Get relation members as array with specifed element type
        /// </summary>
        /// <param name="elemType">element type of created array</param>
        /// <returns>created array</returns>
        Array ToArray(Type elemType);

#if !USE_GENERICS
        /// <summary> Checks if relation contains specified object
        /// </summary>
        /// <param name="obj">specified object
        /// 
        /// </param>
        bool Contains(object obj);
#endif

        /// <summary>Check if i-th element of Link is the same as specified obj
        /// </summary>
        /// <param name="i"> element index</param>
        /// <param name="obj">specified object</param>
        /// <returns><b>true</b> if i-th element of Link reference the same object as "obj"</returns>
#if USE_GENERICS
        bool ContainsElement(int i, T obj);
#else
        bool ContainsElement(int i, object obj);
#endif

#if !USE_GENERICS
        /// <summary> Get index of the specified object in the relation
        /// </summary>
        /// <param name="obj">specified object
        /// </param>
        /// <returns>zero based index of the object or -1 if object is not in the relation
        /// 
        /// </returns>
        int IndexOf(object obj);
#endif

        /// <summary>
        /// Get bidirectional enumerator started with specified current position
        /// </summary>
        /// <param name="start">position of the first element. 
        /// After creation of this enumerator <b>IEnumerator.Current</b> property points to the list 
        /// element with index <b>start</b> (if any), following <b>IEnumerator.MoveNext</b> moves 
        /// enumerator current position to the element with index <b>star+1</b> and 
        /// <b>IEnumerator.MovePrevious</b> - to the element with index <b>start-1</b>.
        /// Standard enumerator is equivalent to <b>start == -1</b>.
        /// </param>
        /// <returns>iterator through the list elements starting from element with specified position</returns>
#if USE_GENERICS
        IBidirectionalEnumerator<T> GetEnumerator(int start);
#else
        IBidirectionalEnumerator GetEnumerator(int start);
#endif
    }
}