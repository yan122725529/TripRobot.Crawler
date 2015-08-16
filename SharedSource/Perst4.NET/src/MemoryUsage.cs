using System;

namespace Perst
{
    /// <summary>
    /// Information about memory usage for the correspondent class. 
    /// Instances of this class are created by Storage.getMemoryDump method.
    /// Size of internal database structures (object index,* memory allocation bitmap) is associated with 
    /// <see cref="Perst.Storage"/> class. Size of class descriptors  - with <see cref="System.Type"/> class.
    /// </summary>
    public class MemoryUsage
    {
        /// <summary>
        /// Class of persistent object or Storage for database internal data
        /// </summary>
        public Type type;

        /// <summary>
        /// Number of reachable instance of the particular class in the database.
        /// </summary>
        public int nInstances;
    
        /// <summary>
        /// Total size of all reachable instances
        /// </summary>
        public long totalSize;

        /// <summary>
        /// Real allocated size of all instances. Database allocates space for th objects using quantums,
        /// for example object wilth size 25 bytes will use 32 bytes in the storage.
        /// In item associated with Storage class this field contains size of all allocated
        /// space in the database (marked as used in bitmap) 
        /// </summary>
        public long allocatedSize;

        /// <summary>
        /// MemoryUsage constructor
        /// </summary>
        public MemoryUsage(Type type)
        {
            this.type = type;
        }
    }
}
