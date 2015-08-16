namespace Perst.Impl
{
    using System;
    using Perst;

    public class DefaultAllocator : Persistent, CustomAllocator 
    { 
        public DefaultAllocator(Storage storage) 
            : base(storage)
        {
        }
    
        protected DefaultAllocator() {}

        public long SegmentBase
        {
            get 
            {
                return 0;
            }
        }


        public long SegmentSize
        {
            get
            {
                return 1L << StorageImpl.dbLargeDatabaseOffsetBits;
            }
        }
        

        public long Allocate(long size) 
        { 
            return ((StorageImpl)Storage).allocate(size, 0);
        }

        public long Reallocate(long pos, long oldSize, long newSize) 
        {
            StorageImpl db = (StorageImpl)Storage;
            if (((newSize + StorageImpl.dbAllocationQuantum - 1) & ~(StorageImpl.dbAllocationQuantum-1))
                > ((oldSize + StorageImpl.dbAllocationQuantum - 1) & ~(StorageImpl.dbAllocationQuantum-1)))
            { 
                long newPos = db.allocate(newSize, 0);
                db.cloneBitmap(pos, oldSize);
                db.free(pos, oldSize);
                pos = newPos;
            }
            return pos;
        }

        public void Free(long pos, long size) 
        { 
            ((StorageImpl)Storage).cloneBitmap(pos, size);
        }
        
        public void Commit() {}
    }      
}
