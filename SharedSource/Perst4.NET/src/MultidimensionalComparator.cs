namespace Perst
{
    public enum CompareResult
    {
        LEFT_UNDEFINED,
        LT,
        EQ,
        GT,
        RIGHT_UNDEFINED,
        NE,
    };

    /// <summary> 
    /// Base class for multidimensional persistent comparator used in multidimensional index
    /// </summary>
#if USE_GENERICS
    public abstract class MultidimensionalComparator<T> : Persistent where T:class
#else
    public abstract class MultidimensionalComparator : Persistent
#endif
    {
        /// <summary>
        /// Compare  i-th component of two objects
        /// </summary>
        /// <param name="m1">first object</param>
        /// <param name="m2">second object</param>
        /// <param name="i">component index</param>
        /// <returns>LEFT_UNDEFINED if value of i-th component of m1 is null and 
        /// value  of i-th component of m2 is not null, 
        /// RIGHT_UNDEFINED if value of i-th component of m1 is not null and 
        /// value  of i-th component of m2 is null, 
        /// EQ if both values are null,
        /// otherwise LT, EQ or GT depending on result of their comparison
        /// </returns>
#if USE_GENERICS
        public abstract CompareResult Compare(T m1, T m2, int i);
#else
        public abstract CompareResult Compare(object m1, object m2, int i);
#endif

        /// <summary>
        /// Get number of dimensions
        /// </summary>
        /// <returns>number of dimensions
        /// </returns>
        public abstract int NumberOfDimensions
        {
            get;
        }

        /// <summary>
        /// Create clone of the specified object contining copy of the specified field
        /// </summary>
        /// <param name="obj">original object</param>
        /// <param name="i">component index</param>
        /// <returns>clone of the object
        /// </returns>
#if USE_GENERICS
        public abstract T CloneField(T obj, int i);
#else
        public abstract object CloneField(object obj, int i);
#endif

        protected MultidimensionalComparator(Storage storage)
            : base(storage)
        {
        }

        protected MultidimensionalComparator()
        {
        }
    }
}