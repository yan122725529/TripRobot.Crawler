namespace Perst.Impl        
{
    using System;
    using Perst;
    using System.Diagnostics;
	
    class PagePool
    {
        internal LRU    lru;
        internal Page   freePages;
        internal Page[] hashTable;
        internal int    poolSize;
        internal IFile  file;
        internal long   lruLimit;
        internal bool   autoExtended;
		 
        internal int nDirtyPages;
        internal Page[] dirtyPages;
		
        internal bool flushing;
		
        internal PagePool(IFile f)
        {        
            file = f;
        }

        const int INFINITE_POOL_INITIAL_SIZE = 8;

        internal PagePool(IFile f, int poolSize, long lruLimit)
        {
            if (poolSize == 0) { 
                autoExtended = true;
                poolSize = INFINITE_POOL_INITIAL_SIZE;
            } 
            this.poolSize = poolSize; 
            this.lruLimit = lruLimit;
            file = f;
            reset();
        }
		
        internal virtual Page find(long addr, int state)
        {
            Debug.Assert((addr & (Page.pageSize - 1)) == 0);
            Page pg;
            int pageNo = (int)((ulong)addr >> Page.pageSizeLog);
            int hashCode = pageNo % poolSize;
			
            lock(this)
            {
                int nCollisions = 0;
                for (pg = hashTable[hashCode]; pg != null; pg = pg.collisionChain)
                {
                    if (pg.offs == addr)
                    {
                        if (pg.accessCount++ == 0)
                        {
                            pg.unlink();
                        }
                        break;
                    }
                    nCollisions += 1;
                }
                if (pg == null)
                {
                    pg = freePages;
                    if (pg != null)
                    {
                        if (pg.data == null) 
                        {
                            pg.data = new byte[Page.pageSize];
                        }
                        freePages = (Page) pg.next;
                    }
                    else if (autoExtended) 
                    { 
                        if (pageNo >= poolSize) {
                            int newPoolSize = pageNo >= poolSize*2 ? pageNo+1 : poolSize*2;
                            Page[] newHashTable = new Page[newPoolSize];
                            Array.Copy(hashTable, 0, newHashTable, 0, hashTable.Length);
                            hashTable = newHashTable;
                            poolSize = newPoolSize;
                        }
                        pg = new Page();
                        pg.data = new byte[Page.pageSize];
                        hashCode = pageNo;
                    }
                    else
                    {
                        Debug.Assert(lru.prev != lru, "unfixed page available");
                        pg = (Page) lru.prev;
                        pg.unlink();
                        lock(pg)
                        {
                            if ((pg.state & Page.psDirty) != 0)
                            {
                                pg.state = 0;
                                file.Write(pg.offs, pg.data);
                                if (!flushing)
                                {
                                    dirtyPages[pg.writeQueueIndex] = dirtyPages[--nDirtyPages];
                                    dirtyPages[pg.writeQueueIndex].writeQueueIndex = pg.writeQueueIndex;
                                }
                            }
                        }
                        int h = (int) (pg.offs >> Page.pageSizeLog) % poolSize;
                        Page curr = hashTable[h], prev = null;
                        while (curr != pg)
                        {
                            prev = curr;
                            curr = curr.collisionChain;
                        }
                        if (prev == null)
                        {
                            hashTable[h] = pg.collisionChain;
                        }
                        else
                        {
                            prev.collisionChain = pg.collisionChain;
                        }
                    }
                    pg.accessCount = 1;
                    pg.offs = addr;
                    pg.state = Page.psRaw;
                    pg.collisionChain = hashTable[hashCode];
                    hashTable[hashCode] = pg;
                }
                if ((pg.state & Page.psDirty) == 0 && (state & Page.psDirty) != 0)
                {
                    Debug.Assert(!flushing);
                    if (nDirtyPages >= dirtyPages.Length) {                     
                        Page[] newDirtyPages = new Page[nDirtyPages*2];
                        Array.Copy(dirtyPages, 0, newDirtyPages, 0, dirtyPages.Length);
                        dirtyPages = newDirtyPages;
                    }
                    dirtyPages[nDirtyPages] = pg;
                    pg.writeQueueIndex = nDirtyPages++;
                    pg.state |= Page.psDirty;
                }
                if ((pg.state & Page.psRaw) != 0)
                {
                    // Console.WriteLine("Read page {0}", pg.offs);
                    if (file.Read(pg.offs, pg.data) < Page.pageSize)
                    {
                        for (int i = 0; i < Page.pageSize; i++)
                        {
                            pg.data[i] = 0;
                        }
                    }
                    pg.state &= ~ Page.psRaw;
                }
            }
            return pg;
        }
		
		
        internal void  copy(long dst, long src, long size)
        {
            int dstOffs = (int) dst & (Page.pageSize - 1);
            int srcOffs = (int) src & (Page.pageSize - 1);
            dst -= dstOffs;
            src -= srcOffs;
            Page dstPage = find(dst, Page.psDirty);
            Page srcPage = find(src, 0);
            do 
            {
                if (dstOffs == Page.pageSize)
                {
                    unfix(dstPage);
                    dst += Page.pageSize;
                    dstPage = find(dst, Page.psDirty);
                    dstOffs = 0;
                }
                if (srcOffs == Page.pageSize)
                {
                    unfix(srcPage);
                    src += Page.pageSize;
                    srcPage = find(src, 0);
                    srcOffs = 0;
                }
                long len = size;
                if (len > Page.pageSize - srcOffs)
                {
                    len = Page.pageSize - srcOffs;
                }
                if (len > Page.pageSize - dstOffs)
                {
                    len = Page.pageSize - dstOffs;
                }
                Array.Copy(srcPage.data, srcOffs, dstPage.data, dstOffs, (int) len);
                srcOffs = (int) (srcOffs + len);
                dstOffs = (int) (dstOffs + len);
                size -= len;
            }
            while (size != 0);
            unfix(dstPage);
            unfix(srcPage);
        }
		
        internal void write(long dstPos, byte[] src) 
        {
            Debug.Assert((dstPos & (Page.pageSize-1)) == 0);
            Debug.Assert((src.Length & (Page.pageSize-1)) == 0);
            for (int i = 0; i < src.Length;) 
            { 
                Page pg = find(dstPos, Page.psDirty);
                byte[] dst = pg.data;
                for (int j = 0; j < Page.pageSize; j++) 
                { 
                    dst[j] = src[i++];
                }
                unfix(pg);
                dstPos += Page.pageSize;
            }
        }

        void reset()
        {
            hashTable = new Page[poolSize];
            dirtyPages = new Page[poolSize];
            nDirtyPages = 0;
            lru = new LRU();
            freePages = null;
            if (!autoExtended) { 
                for (int i = poolSize; --i >= 0; )
                {
                    Page pg = new Page();
                    pg.next = freePages;
                    freePages = pg;
                }
            }
        }
		
        internal virtual void clear() 
        { 
            Debug.Assert(nDirtyPages == 0);
            reset();
        }

        internal virtual void close()
        {
            lock(this)
            {
                file.Close();
                hashTable = null;
                dirtyPages = null;
                lru = null;
                freePages = null;
            }
        }
		
        internal virtual void unfix(Page pg)
        {
            lock(this)
            {
                Debug.Assert(pg.accessCount > 0);
                if (--pg.accessCount == 0)
                {
                    if (pg.offs <= lruLimit) 
                    { 
                        lru.link(pg);
                    }
                    else 
                    { 
                        lru.prev.link(pg);
                    }
                }
            }
        }
		
        internal virtual void  modify(Page pg)
        {
            lock(this)
            {
                Debug.Assert(pg.accessCount > 0);
                if ((pg.state & Page.psDirty) == 0)
                {
                    Debug.Assert(!flushing);
                    pg.state |= Page.psDirty;
                    if (nDirtyPages >= dirtyPages.Length) {                     
                        Page[] newDirtyPages = new Page[nDirtyPages*2];
                        Array.Copy(dirtyPages, 0, newDirtyPages, 0, dirtyPages.Length);
                        dirtyPages = newDirtyPages;
                    }
                    dirtyPages[nDirtyPages] = pg;
                    pg.writeQueueIndex = nDirtyPages++;
                }
            }
        }
		
        internal Page getPage(long addr)
        {
            return find(addr, 0);
        }
		
        internal Page putPage(long addr)
        {
            return find(addr, Page.psDirty);
        }
		
        internal byte[] get(long pos)
        {
            Debug.Assert(pos != 0);
            int offs = (int) pos & (Page.pageSize - 1);
            Page pg = find(pos - offs, 0);
            int size = ObjectHeader.getSize(pg.data, offs);
            Debug.Assert(size >= ObjectHeader.Sizeof);
            byte[] obj = new byte[size];
            int dst = 0;
            while (size > Page.pageSize - offs)
            {
                Array.Copy(pg.data, offs, obj, dst, Page.pageSize - offs);
                unfix(pg);
                size -= Page.pageSize - offs;
                pos += Page.pageSize - offs;
                dst += Page.pageSize - offs;
                pg = find(pos, 0);
                offs = 0;
            }
            Array.Copy(pg.data, offs, obj, dst, size);
            unfix(pg);
            return obj;
        }
		
        internal void  put(long pos, byte[] obj)
        {
            put(pos, obj, obj.Length);
        }
		
        internal void  put(long pos, byte[] obj, int size)
        {
            int offs = (int) pos & (Page.pageSize - 1);
            Page pg = find(pos - offs, Page.psDirty);
            int src = 0;
            while (size > Page.pageSize - offs)
            {
                Array.Copy(obj, src, pg.data, offs, Page.pageSize - offs);
                unfix(pg);
                size -= Page.pageSize - offs;
                pos += Page.pageSize - offs;
                src += Page.pageSize - offs;
                pg = find(pos, Page.psDirty);
                offs = 0;
            }
            byte[] dst = pg.data;
            while (--size >= 0) dst[offs++] = obj[src++];
//            Array.Copy(obj, src, pg.data, offs, size);
            unfix(pg);
        }
		
#if COMPACT_NET_FRAMEWORK
        class PageComparator : System.Collections.IComparer 
        {
            public int Compare(object o1, object o2) 
            {
                long delta = ((Page)o1).offs - ((Page)o2).offs;
                return delta < 0 ? -1 : delta == 0 ? 0 : 1;
            }
        }
        static PageComparator pageComparator = new PageComparator();
#endif


        internal virtual void flush()
        {
            lock(this)
            {
                flushing = true;
#if COMPACT_NET_FRAMEWORK
                Array.Sort(dirtyPages, 0, nDirtyPages, pageComparator);
#else
                Array.Sort(dirtyPages, 0, nDirtyPages);
#endif
            }
            for (int i = 0; i < nDirtyPages; i++)
            {
                Page pg = dirtyPages[i];
                lock(pg)
                {
                    if ((pg.state & Page.psDirty) != 0)
                    {
                        file.Write(pg.offs, pg.data);
                        pg.state &= ~ Page.psDirty;
                    }
                }
            }
            file.Sync();
            nDirtyPages = 0;
            flushing = false;
        }
    }

    class InfinitePagePool : PagePool
    {
        byte[][] pages;
        int[]    modifiedPages;
        int      nPages;
    
        const int INFINITE_POOL_INITIAL_SIZE = 8;
        const int FLUSH_BUFFER_SIZE = 256;

        internal InfinitePagePool(IFile file) : base(file)
        { 
            nPages = (int)((file.Length + Page.pageSize - 1) >> Page.pageSizeLog);
            int allocated = nPages < INFINITE_POOL_INITIAL_SIZE ? INFINITE_POOL_INITIAL_SIZE : nPages;
            pages = new byte[allocated][];
            modifiedPages = new int[(allocated+31) >> 5];
            for (int i = 0; i < nPages; i++) 
            { 
                pages[i] = new byte[Page.pageSize];
                file.Read((long)i << Page.pageSizeLog, pages[i]);
            }
        }

        internal override Page find(long addr, int state) 
        {     
            lock(this)
            {
                int pageNo = (int)(addr >> Page.pageSizeLog);
                if (pageNo >= pages.Length) 
                { 
                    int allocated = pages.Length*2 > pageNo ? pages.Length*2 : pageNo+1;
                    byte[][] newPages = new byte[allocated][];
                    int[] newModifiedPages = new int[(allocated+31) >> 5];
                    Array.Copy(pages, 0, newPages, 0, nPages);
                    Array.Copy(modifiedPages, 0, newModifiedPages, 0, (nPages+31) >> 5);
                    pages = newPages;
                    modifiedPages = newModifiedPages;
                }
                if (pageNo >= nPages) 
                { 
                    nPages = pageNo + 1;
                }
                byte[] body = pages[pageNo];
                if (body == null) 
                { 
                    pages[pageNo] = body = new byte[Page.pageSize];
                } 
                if ((state & Page.psDirty) != 0) 
                { 
                    nDirtyPages += 1;
                    modifiedPages[pageNo >> 5] |= 1 << (pageNo & 31);
                }
                return new Page(addr, body);
            }
        }

        internal override void close() 
        {
            base.close();
            pages = null;
            modifiedPages = null;       
            nDirtyPages = 0;
        }

        internal override void unfix(Page pg) 
        { 
        }

        internal override void modify(Page pg) 
        { 
            lock(this)
            {
                int pageNo = (int)(pg.offs >> Page.pageSizeLog);
                modifiedPages[pageNo >> 5] |= 1 << (pageNo & 31);
                nDirtyPages += 1;
            }
        }

        internal override void flush() 
        { 
            lock(this)
            {
                byte[][] pages = this.pages;
#if SILVERLIGHT 
                if (nDirtyPages*2 >= nPages && file is OSFile)
                {
                    byte[] buf = new byte[FLUSH_BUFFER_SIZE << Page.pageSizeLog];
                    for (int i = 0, n = nPages; i < n; i += FLUSH_BUFFER_SIZE) 
                    {                            
                         int chunk = i + FLUSH_BUFFER_SIZE < n ? FLUSH_BUFFER_SIZE : n - i;
                         for (int j = 0; j < chunk; j++) 
                         {
                             if (pages[i+j] != null)
                             {
                                 Array.Copy(pages[i+j], 0, buf, j << Page.pageSizeLog, Page.pageSize);
                             }
                             else
                             {
                                 Array.Clear(buf, j << Page.pageSizeLog, Page.pageSize);
                             }
                         }
                         ((OSFile)file).Write((long)i << Page.pageSizeLog, buf, 0, chunk << Page.pageSizeLog);
                         
                    }
                    Array.Clear(modifiedPages, 0, (nPages + 31) >> 5);
                } 
                else
#endif
                {
                    int[] modifiedPages = this.modifiedPages;
                    for (int i = 0, n = nPages; i < n; i++) 
                    { 
                        if ((modifiedPages[i >> 5] & (1 << (i & 31))) != 0) 
                        { 
                            file.Write((long)i << Page.pageSizeLog, pages[i]);
                            modifiedPages[i >> 5] &= ~(1 << (i & 31));
                        }
                    }
                }           
                nDirtyPages = 0;
                file.Sync();
            }
        }
    }
}

