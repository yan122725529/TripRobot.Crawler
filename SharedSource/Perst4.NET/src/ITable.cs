namespace Perst
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;

    /// <summary> Interface of selectable collection.
    /// Selectable collections allows to selct its memebers using JSQL query
    /// </summary>
#if USE_GENERICS
    public interface ITable<T> : ICollection<T>,IEnumerable
    {
        new IEnumerator<T> GetEnumerator();

        /// <summary> Select members of the collection using search predicate
        /// </summary>
        /// <param name="predicate">JSQL condition
        /// </param>
        /// <returns> iterator through members of the collection matching search condition
        /// </returns>
        IEnumerable<T> Select(string predicate);
#else
    public interface ITable : ICollection
    {
        /// <summary> Select members of the collection using search predicate
        /// </summary>
        /// <param name="cls">class of index members
        /// </param>
        /// <param name="predicate">JSQL condition
        /// </param>
        /// <returns> iterator through members of the collection matching search condition
        /// </returns>
        IEnumerable Select(Type cls, string predicate);

        /// <summary> Remove all objects from the index
        /// </summary>
        void  Clear();
#endif
        /// <summary>
        /// Remove all objects from the index and deallocate them.
        /// This method is equivalent to th following peace of code:
        /// { foreach (IPersistent o in this) o.Deallocate(); Clear(); }
        /// Please notice that this method doesn't check if there are some other references to the deallocated objects.
        /// If deallocated object is included in some other index or is referenced from some other objects, then after deallocation
        /// there will be dangling references and dereferencing them can cause unpredictable behavior of the program.
        /// </summary>
        void DeallocateMembers();
    }
}