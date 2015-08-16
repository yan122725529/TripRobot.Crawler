namespace Perst.Impl
{
    using System;
    using Perst;
    using System.Collections;
    using System.Diagnostics;

    public class BitmapCustomAllocator : Persistent, CustomAllocator 
    { 
        protected int  quantum;
        protected int  quantumBits;
        protected long baseAddr;
        protected long limit;
#if USE_GENERICS
        protected Link<BitmapPage> pages;
#else
        protected Link pages;
#endif
        protected int  extensionPages;

        [NonSerialized()] 
        int  currPage;
        [NonSerialized()] 
        int  currOffs;
        [NonSerialized()] 
        ArrayList reserved = new ArrayList();

        const int BITMAP_PAGE_SIZE = Page.pageSize - ObjectHeader.Sizeof - 4;
        const int BITMAP_PAGE_BITS = BITMAP_PAGE_SIZE*8;
    
        public override void OnLoad()
        {
            reserved = new ArrayList();
        }

        protected class BitmapPage : Persistent 
        { 
            public byte[] data;
        }

        public BitmapCustomAllocator(Storage storage, int quantum, long baseAddr, long extension, long limit) 
            : base(storage)
        {
            this.quantum = quantum;
            this.baseAddr = baseAddr;
            this.limit = limit;
            int bits = 0;
            for (int q = quantum; q != 1; q >>= 1) 
            { 
                bits += 1;
            }
            quantumBits = bits;
            Debug.Assert((1 << bits) == quantum);
            extensionPages = (int)((extension + ((long)BITMAP_PAGE_BITS << quantumBits) - 1) / ((long)BITMAP_PAGE_BITS << quantumBits));
#if USE_GENERICS
            pages = storage.CreateLink<BitmapPage>();
#else
            pages = storage.CreateLink();
#endif
        }
    
        protected BitmapCustomAllocator() {}

        public long Allocate(long size) 
        { 
            size = (size + quantum-1) & ~(quantum-1);
            long objBitSize = size >> quantumBits;
            long pos;    
            long holeBitSize = 0;
            int  firstPage = currPage;
            int  lastPage = pages.Count;
            int  offs = currOffs;
            long lastHoleSize = 0;

            while (true) 
            { 
                for (int i = firstPage; i < lastPage; i++) 
                {
                    BitmapPage pg = (BitmapPage)pages[i];
                    while (offs < BITMAP_PAGE_SIZE) 
                    { 
                        int mask = pg.data[offs] & 0xFF; 
                        if (holeBitSize + StorageImpl.firstHoleSize[mask] >= objBitSize) 
                        { 
                            pos = baseAddr + ((((long)i*BITMAP_PAGE_SIZE + offs)*8 - holeBitSize) << quantumBits);
                            long nextPos = wasReserved(pos, size);
                            if (nextPos != 0) 
                            {
                                long quantNo = ((nextPos - baseAddr) >> quantumBits);
                                i = (int)(quantNo / BITMAP_PAGE_BITS);
                                pg = (BitmapPage)pages[i];
                                offs = (int)(quantNo + 7 - (long)i*BITMAP_PAGE_BITS) >> 3;
                                holeBitSize = 0;
                                continue;
                            }       
                            currPage = i;
                            currOffs = offs;
                            pg.data[offs] |= (byte)((1 << (int)(objBitSize - holeBitSize)) - 1); 
                            pg.Modify();
                            if (holeBitSize != 0) 
                            { 
                                if (holeBitSize > offs*8) 
                                { 
                                    memset(pg, 0, 0xFF, offs);
                                    holeBitSize -= offs*8;
                                    pg = (BitmapPage)pages[--i];
                                    offs = BITMAP_PAGE_SIZE;
                                }
                                while (holeBitSize > BITMAP_PAGE_BITS) 
                                { 
                                    memset(pg, 0, 0xFF, BITMAP_PAGE_SIZE);
                                    holeBitSize -= BITMAP_PAGE_BITS;
                                    pg = (BitmapPage)pages[--i];
                                }
                                while ((holeBitSize -= 8) > 0) 
                                { 
                                    pg.data[--offs] = (byte)0xFF; 
                                }
                                pg.data[offs-1] |= (byte)~((1 << -(int)holeBitSize) - 1);
                                pg.Modify();
                            }
                            return pos;
                        } 
                        else if (StorageImpl.maxHoleSize[mask] >= objBitSize) 
                        { 
                            int holeBitOffset = StorageImpl.maxHoleOffset[mask];
                            pos = baseAddr + ((((long)i*BITMAP_PAGE_SIZE + offs)*8 + holeBitOffset) << quantumBits);
                            long nextPos = wasReserved(pos, size);
                            if (nextPos != 0) 
                            {
                                long quantNo = ((nextPos - baseAddr) >> quantumBits);
                                i = (int)(quantNo / BITMAP_PAGE_BITS);
                                pg = (BitmapPage)pages[i];
                                offs = (int)(quantNo + 7 - (long)i*BITMAP_PAGE_BITS) >> 3;
                                holeBitSize = 0;
                                continue;
                            }       
                            currPage = i;
                            currOffs = offs;
                            pg.data[offs] |= (byte)(((1<<(int)objBitSize) - 1) << holeBitOffset);
                            pg.Modify();
                            return pos;
                        }
                        offs += 1;
                        if (StorageImpl.lastHoleSize[mask] == 8) 
                        { 
                            holeBitSize += 8;
                        } 
                        else 
                        { 
                            holeBitSize = StorageImpl.lastHoleSize[mask];
                        }
                    }
                    offs = 0;
                }
                if (firstPage == 0) 
                {
                    firstPage = pages.Count;
                    int nPages = (int)((size + BITMAP_PAGE_BITS*quantum - 1) / (BITMAP_PAGE_BITS*quantum));
                    lastPage = firstPage + (nPages > extensionPages ? nPages : extensionPages);
                    if ((long)lastPage*BITMAP_PAGE_BITS*quantum > limit) 
                    {
                        throw new StorageError(StorageError.ErrorCode.NOT_ENOUGH_SPACE);
                    }
                    pages.Length = lastPage;
                    for (int i = firstPage; i < lastPage; i++) 
                    { 
                        BitmapPage pg = new BitmapPage();
                        pg.data = new byte[BITMAP_PAGE_SIZE];
                        pages[i] = pg;
                    }
                    holeBitSize = lastHoleSize;
                } 
                else 
                {
                    lastHoleSize = holeBitSize;
                    holeBitSize = 0;
                    lastPage = firstPage + 1;
                    firstPage = 0;
                }
            }
        }


        public long Reallocate(long pos, long oldSize, long newSize) 
        {
            if (((newSize + quantum - 1) & ~(quantum-1)) > ((oldSize + quantum - 1) & ~(quantum-1))) 
            { 
                long newPos = Allocate(newSize);
                free0(pos, oldSize);
                pos = newPos;
            }
            return pos;
        }

        public void Free(long pos, long size) 
        { 
            reserve(pos, size);
            free0(pos, size);
        }


        class Location : IComparable 
        { 
            public long pos;
            public long size;
        
            public Location(long pos, long size) 
            { 
                this.pos = pos;
                this.size = size;
            }

            public int CompareTo(Object o) 
            { 
                Location loc = (Location)o;
                return pos + size <= loc.pos ? -1 : loc.pos + loc.size <= pos ? 1 : 0;
            }
        }

#if COMPACT_NET_FRAMEWORK
        class LocationComparer : IComparer 
        {
            public int Compare(object a, object b)
            {
                return ((Location)a).CompareTo(b);
            }
        }
        readonly LocationComparer comparer = new LocationComparer();
#endif

        private long wasReserved(long pos, long size) 
        { 
            Location loc = new Location(pos, size);
#if COMPACT_NET_FRAMEWORK
            int i = reserved.BinarySearch(0, reserved.Count, loc, comparer);
#else
            int i = reserved.BinarySearch(loc);
#endif
            if (i >= 0) 
            { 
                Location r = (Location)reserved[i];
                return Math.Max(pos + size, r.pos + r.size);
            }
            return 0;
        }

        private void reserve(long pos, long size) 
        { 
            Location loc = new Location(pos, (size + quantum - 1) & ~(quantum - 1));
#if COMPACT_NET_FRAMEWORK
            int i = reserved.BinarySearch(0, reserved.Count, loc, comparer);
#else
            int i = reserved.BinarySearch(loc);
#endif
            reserved.Insert(~i, loc);
        }
            
        private void free0(long pos, long size) 
        { 
            long quantNo = (pos - baseAddr) >> quantumBits;
            long objBitSize = (size+quantum-1) >> quantumBits;
            int  pageId = (int)(quantNo / BITMAP_PAGE_BITS);
            int  offs = (int)(quantNo - (long)pageId*BITMAP_PAGE_BITS) >> 3;
            BitmapPage pg = (BitmapPage)pages[pageId];
            int  bitOffs = (int)quantNo & 7;
        
            if (objBitSize > 8 - bitOffs) 
            { 
                objBitSize -= 8 - bitOffs;
                pg.data[offs++] &= (byte)((1 << bitOffs) - 1);
                while (objBitSize + offs*8 > BITMAP_PAGE_BITS) 
                { 
                    memset(pg, offs, 0, BITMAP_PAGE_SIZE - offs);
                    pg = (BitmapPage)pages[++pageId];
                    objBitSize -= (BITMAP_PAGE_SIZE - offs)*8;
                    offs = 0;
                }
                while ((objBitSize -= 8) > 0) 
                { 
                    pg.data[offs++] = (byte)0;
                }
                pg.data[offs] &= (byte)~((1 << ((int)objBitSize + 8)) - 1);
            } 
            else 
            { 
                pg.data[offs] &= (byte)~(((1 << (int)objBitSize) - 1) << bitOffs); 
            }
            pg.Modify();
        }

        static void memset(BitmapPage pg, int offs, int pattern, int len) 
        { 
            byte[] arr = pg.data;
            byte pat = (byte)pattern;
            while (--len >= 0) 
            { 
                arr[offs++] = pat;
            }
            pg.Modify();
        }


        public void Commit() 
        {
            reserved.Clear();
        }

        public long SegmentBase
        {
            get 
            {
                return baseAddr;
            }
        }

        public long SegmentSize
        {
            get
            {
                return limit;
            }
        }
    }
}
