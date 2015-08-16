using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst
{
    ///<summary>
    /// Interface for ordered collection (sequence). 
    /// The user can access elements by their integer index (position in
    /// the list), and search for elements in the list.
    /// </summary>
#if USE_GENERICS
    public interface IPersistentList<T> : IPersistent, IResource, ITable<T>, IList<T> where T:class
#else
    public interface IPersistentList : IPersistent, IResource, ITable, IList
#endif
    {   
#if !USE_GENERICS
        /// <summary> Remove all objects from the index
        /// </summary>
        new void Clear();
#endif

        /// <summary> Get relation members as array of objects
        /// </summary>
        /// <returns>created array</returns>
#if USE_GENERICS
        T[] ToArray();
#else
        object[] ToArray();
#endif

        /// <summary> Get relation members as array with specifed element type
        /// </summary>
        /// <param name="elemType">element type of created array</param>
        /// <returns>created array</returns>
        Array ToArray(Type elemType);

        /// <summary>
        /// Get bidirectional enumerator started with specified current position
        /// </summary>
        /// <param name="start">position of the first element. 
        /// After creation of this enumerator <see cref="P:System.IO.IEnumerator.Current"/> property points to the list 
        /// element with index <b>start</b> (if any), following <see cref="M:System.IO.IEnumerator.MoveNext"/> moves 
        /// enumerator current position to the element with index <b>star+1</b> and 
        /// <see cref="M:Perst.IBidirectionalEnumerator.MovePrevious"/> - to the element with index <b>start-1</b>.
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