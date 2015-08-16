namespace Perst
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
	
    /// <summary> Interface of object spatial index.
    /// Spatial index is used to allow fast selection of spatial objects belonging to the specified rectangle.
    /// Spatial index is implemented using Guttman R-Tree with quadratic split algorithm.
    /// </summary>
#if USE_GENERICS
    public interface SpatialIndexR2<T> : IPersistent, IResource, ITable<T> where T:class
#else
    public interface SpatialIndexR2 : IPersistent, IResource, ITable
#endif
    {
        /// <summary>
        /// Find all objects located in the selected rectangle
        /// </summary>
        /// <param name="r">selected rectangle
        /// </param>
        /// <returns>array of objects which enveloping rectangle intersects with specified rectangle
        /// </returns>             
#if USE_GENERICS
        T[] Get(RectangleR2 r);
#else
        object[] Get(RectangleR2 r);
#endif
    
        /// <summary>
        /// Put new object in the index. 
        /// </summary>
        /// <param name="r">enveloping rectangle for the object
        /// </param>
        /// <param name="obj"> object associated with this rectangle. Object can be not yet persistent, in this case
        /// its forced to become persistent by assigning OID to it.
        /// </param>
#if USE_GENERICS
        void Put(RectangleR2 r, T obj);
#else
        void Put(RectangleR2 r, object obj);
#endif

        /// <summary>
        /// Remove object with specified enveloping rectangle from the tree.
        /// </summary>
        /// <param name="r">enveloping rectangle for the object
        /// </param>
        /// <param name="obj">object removed from the index
        /// </param>
        /// <exception  cref="Perst.StorageError">StorageError(StorageError.KEY_NOT_FOUND) exception 
        /// if there is no such key in the index
        /// </exception>
#if USE_GENERICS
        void Remove(RectangleR2 r, T obj);
#else
        void Remove(RectangleR2 r, object obj);
#endif

        /// <summary>
        /// Get number of objects in the index
        /// </summary>
        /// <returns>number of objects in the index
        /// </returns>
        int  Size();
    
        /// <summary>
        /// Get wrapping rectangle 
        /// </summary>
        /// <returns>Minimal rectangle containing all rectangles in the index     
        /// If index is empty <i>empty rectangle</i> (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue)
        /// is returned.
        /// </returns>
        RectangleR2 WrappingRectangle 
        {
            get;
        }

        /// <summary>
        /// Get enumerator for objects located in the selected rectangle
        /// </summary>
        /// <param name="r">Selected rectangle</param>
        /// <returns>enumerable collection for objects which enveloping rectangle overlaps with specified rectangle
        /// </returns>
#if USE_GENERICS
        IEnumerable<T> Overlaps(RectangleR2 r);
#else
        IEnumerable Overlaps(RectangleR2 r);
#endif

        /// <summary>
        /// Get dictionary enumerator for objects located in the selected rectangle
        /// </summary>
        /// <param name="r">Selected rectangle</param>
        /// <returns>dictionary enumerator for objects which enveloping rectangle overlaps with specified rectangle
        /// </returns>
        IDictionaryEnumerator GetDictionaryEnumerator(RectangleR2 r);

        /// <summary>
        /// Get dictionary enumerator for all objects in the index
        /// </summary>
        /// <returns>dictionary enumerator for all objects in the index
        /// </returns>
        IDictionaryEnumerator GetDictionaryEnumerator();

        ///<summary>
        /// Get iterator through all neighbors of the specified point in the order of increasing distance 
        /// from the specified point to the wrapper rectangle of the object
        /// </summary>
        /// <param name="x">x coordinate of the point</param>
        /// <param name="y">y coordinate of the point</param>
        /// <returns>iterator through all objects in the index in the order of increasing distance from the specified point
        /// </returns>
#if USE_GENERICS
        IEnumerable<T> Neighbors(double x, double y);
#else
        IEnumerable Neighbors(double x, double y);
#endif
    }
}

