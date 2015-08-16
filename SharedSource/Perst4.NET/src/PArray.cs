namespace Perst
{
    using System;
	
    /// <summary>
    /// Common interface for all PArrays
    /// </summary> 
    public interface GenericPArray 
    {
        /// <summary> Get number of the array elements
        /// </summary>
        /// <returns>the number of related objects
        /// 
        /// </returns>
        int Size();

        /// <summary> Get OID of array element.
        /// </summary>
        /// <param name="i">index of the object in the relation
        /// </param>
        /// <returns>OID of the object (0 if array contains <b>null</b> reference)
        /// </returns>
        int GetOid(int i);

        /// <summary>
        /// Set owner objeet for this PArray. Owner is persistent object contaning this PArray.
        /// This method is mostly used by storage itself, but can also used explicityl by programmer if
        /// Parray component of one persistent object is assigned to component of another persistent object
        /// </summary>
        /// <param name="owner">Link owner</param>
        void SetOwner(object owner);
    }

    /// <summary>Dynamically extended array of reference to persistent objects.
    /// It is inteded to be used in classes using virtual properties to 
    /// access components of persistent objects. You can not use standard
    /// C# array here, instead you should use PArray class.
    /// PArray is created by Storage.createArray method
    /// </summary>
#if USE_GENERICS
    public interface PArray<T> : GenericPArray, Link<T> where T:class
#else
    public interface PArray : GenericPArray, Link
#endif
    {
    }
}