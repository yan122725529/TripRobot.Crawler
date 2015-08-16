using System;

namespace Perst
{
    /// <summary>
    /// Custom allocator interface. Custom allocator can be used for more efficiently 
    /// allocate space using application specific semantic of the object. For example,
    /// application can place all BLOBs (images, texts, video,...) in separate file, located at separate disk
    /// and keep file with the rest of the data (metadata describing this BLOBs) relatively small, improving
    /// speed of search operations. Such separation of BLOBs and their descriptors can be achieved
    /// using custom allocator in conjunction with multifile. First segment is used for allocation of normal
    /// (non-BLOB) objects. It's size can be set practically unlimited: 0x1000 0000 0000 0000.
    /// And second segment should be used by custom allocator to allocate BLOBs. So BLOBs offsets are started
    /// from 0x1000000000000000 and BLOB content will be stored in separate file which in turn can be located 
    /// at separate disk.
    /// </summary>summary>
    public interface CustomAllocator :  IPersistent 
    { 
        /// <summary> 
        /// Get allocator segment base address
        /// </summary> 
        long SegmentBase
        {
            get;
        }

        /// <summary> 
        /// Get allocator segment size
        /// </summary>     
        long SegmentSize
        {
            get;
        }

        /// <summary>
        /// Allocate object
        /// </summary>
        /// <param name="size">allocated object size</param>
        /// <returns>position of the object in daatbase file.
        /// It should not overlap with space covered by main database allocation bitmap</returns>
        long Allocate(long size);

        /// <summary>
        /// Reallocate object previously allocated by this allocator.
        /// This method should try to extend or shrink this object in its current location
        /// and if it is not possible, allocate new space for the object and free its old location.
        /// </summary>
        /// <param name="pos">old position of the object</param>
        /// <param name="oldSize">old size of the object</param>
        /// <param name="newSize">new size of the object</param>
        /// <returns>new position of the object (it can be equal to old position)</returns>
        long Reallocate(long pos, long oldSize, long newSize);

        /// <summary>
        /// Deallocate object previously allocated by this allocator.
        /// Space used by this object can not be reused until transaction commit (when commit method is called for this
        /// allocator)
        /// </summary>
        /// <param name="pos">position of the object</param>
        ///<param name="size">size of allocated object</param>
        void Free(long pos, long size);

        /// <summary>
        /// Make it possible to reused space of all previously deallocated shadow objects. 
        /// </summary>
        void Commit();
    }
}
