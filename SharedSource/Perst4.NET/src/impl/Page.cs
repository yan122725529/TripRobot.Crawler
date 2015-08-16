namespace Perst.Impl
{
    using System;
    using Perst;
	
    class Page:LRU, IComparable
    {
        internal Page collisionChain;
        internal int accessCount;
        internal int writeQueueIndex;
        internal int state;
        internal long offs;
        internal byte[] data;
		
        internal const int psDirty = 0x01; // page has been modified
        internal const int psRaw   = 0x02; // page is loaded from the disk
        internal const int psWait  = 0x04; // other thread(s) wait load operation completion
		
        internal const int pageSizeLog = 12;
        internal const int pageSize = 1 << pageSizeLog;
		
        public virtual int CompareTo(Object o)
        {
            long po = ((Page) o).offs;
            return offs < po ? -1 : offs == po ? 0 : 1;
        }

        internal Page() 
        { 
        }

        internal Page(long offs, byte[] data) 
        {
            this.offs = offs;
            this.data = data;
        }
    }
}