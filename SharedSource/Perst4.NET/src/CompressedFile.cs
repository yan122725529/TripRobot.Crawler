namespace Perst
{
    using System.IO;
    using System.Collections;
    using Perst.Impl;
    using System.Diagnostics;
    using System.Text;
    using System;
#if !WP7
    using System.IO.Compression;
#endif

    ///
    /// <summary>
    /// Compressed read-write database file. 
    /// To work with compressed database file you should pass instance of this class in <code>Storage.Open</code> method
    /// </summary>
    ///
    public class CompressedFile : OSFile 
    { 
        public override void Write(long pageAddr, byte[] buf) 
        {
            long pageOffs = 0;
            if (pageAddr != 0) 
            { 
                Debug.Assert(buf.Length == Page.pageSize);
                Debug.Assert((pageAddr & (Page.pageSize-1)) == 0);
                long pagePos = pageMap.get(pageAddr);
                bool firstUpdate = false;
                if (pagePos == 0) 
                { 
                    long bp = (pageAddr >> (Page.pageSizeLog - 3));
                    byte[] posBuf = new byte[8];
                    indexFile.Read(bp, posBuf);
                    pagePos = Bytes.unpack8(posBuf, 0);
                    firstUpdate = true;
                }
                int pageSize = ((int)pagePos & (Page.pageSize-1)) + 1;
                MemoryStream ms = new MemoryStream();
                GZipStream stream = new GZipStream(ms, CompressionMode.Compress, true);
                stream.Write(buf, 0, buf.Length);
#if WINRT_NET_FRAMEWORK
                stream.Dispose();
#else
                stream.Close();
#endif
                if (ms.Length < Page.pageSize)
                {
                    buf = ms.ToArray();
                } 
                else 
                {
                    byte[] copy = new byte[buf.Length];
                    Array.Copy(buf, 0, copy, 0, buf.Length);
                }
                crypt(buf, buf.Length);
                int newPageBitSize = (buf.Length + ALLOCATION_QUANTUM - 1) >> ALLOCATION_QUANTUM_LOG;
                int oldPageBitSize = (pageSize + ALLOCATION_QUANTUM - 1) >> ALLOCATION_QUANTUM_LOG;

                if (firstUpdate || newPageBitSize != oldPageBitSize) 
                { 
                    if (!firstUpdate) 
                    { 
                        Bitmap.free(bitmap, pagePos >> (Page.pageSizeLog + ALLOCATION_QUANTUM_LOG), 
                                    oldPageBitSize);   
                    }
                    pageOffs = allocate(newPageBitSize);
                } 
                else 
                {
                    pageOffs = pagePos >> Page.pageSizeLog;
                }
                pageMap.put(pageAddr, (pageOffs << Page.pageSizeLog) | (buf.Length-1), pagePos);
            }
            base.Write(pageOffs, buf);
        }
    
        public override int Read(long pageAddr, byte[] buf) 
        {
            if (pageAddr != 0) 
            {  
                int offs;
                Debug.Assert((pageAddr & (Page.pageSize-1)) == 0);
                long pagePos = 0;
                if (pageMap != null) 
                { 
                    pagePos = pageMap.get(pageAddr);
                }
                if (pagePos == 0) 
                { 
                    long bp = (pageAddr >> (Page.pageSizeLog - 3));
                    byte[] posBuf = new byte[8];
                    indexFile.Read(bp, posBuf);
                    pagePos = Bytes.unpack8(posBuf, 0);
                    if (pagePos == 0) 
                    {
                        return 0;
                    }                        
                }                
                int size = ((int)pagePos & (Page.pageSize-1)) + 1;
                byte[] compressedBuf = size < Page.pageSize ? new byte[size] : buf;
                int rc = base.Read(pagePos >> Page.pageSizeLog, compressedBuf);
                if (rc != size) 
                { 
                    throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR);
                }
                crypt(compressedBuf, size);
                if (size != Page.pageSize)
                {
                    MemoryStream stream = new MemoryStream(compressedBuf, 0, size);
                    GZipStream zipStream = new GZipStream(stream, CompressionMode.Decompress);
                    for (offs = 0; offs < buf.Length; offs += rc)
                    { 
                        rc = zipStream.Read(buf, offs, buf.Length - offs);
                        if (rc <= 0)
                        {
                            throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR);
                        }
                    } 
#if WINRT_NET_FRAMEWORK
                    zipStream.Dispose();
#else
                    zipStream.Close();           
#endif
                    return offs;
                }
                return size;
            } 
            else 
            { 
                return base.Read(0, buf);
            }
        }
    
        public override void Sync() 
        {
            base.Sync();
            if (pageMap.size() != 0)
            { 
                byte[] buf = new byte[8];
                foreach (PageMap.Entry e in pageMap) 
                {       
                    Bytes.pack8(buf, 0, e.newPos);
                    long bp = (e.addr >> (Page.pageSizeLog - 3));
                    indexFile.Write(bp, buf);
                    if (e.oldPos != 0) 
                    { 
                        Bitmap.free(bitmap, e.oldPos >> (Page.pageSizeLog + ALLOCATION_QUANTUM_LOG), 
                                    ((e.oldPos & (Page.pageSize-1)) + ALLOCATION_QUANTUM) >> ALLOCATION_QUANTUM_LOG);
                    }
                }
                indexFile.Sync();
                pageMap.clear();
            }
        }
    
        public override void Close() 
        {
            base.Close();
            indexFile.Close();
        }
    
        ///
        /// <summary>
        /// Constructor of compressed file with default parameter values
        /// </summary>
        /// <param name="dataFilePath">path to the data file</param> 
        ///
        public CompressedFile(string dataFilePath) 
        : this(dataFilePath, null)
        {
        } 
    
        ///
        /// <summary>
        /// Constructor of compressed file with default parameter values
        /// </summary>
        /// <param name="dataFilePath">path to the data file</param> 
        /// <param name="cipherKey">cipher key (if null, then no encryption is performed)</param> 
        ///
        public CompressedFile(string dataFilePath, string cipherKey) 
        : this(dataFilePath, cipherKey, dataFilePath + ".map", new FileParameters(false, false, false, 8*1024*1024))
        {
        } 
    
        ///
        /// <summary>
        /// Constructor of compressed file
        /// </summary>
        /// <param name="dataFilePath">path to the data file</param> 
        /// <param name="cipherKey">cipher key (if null, then no encryption is performed)</param> 
        /// <param name="indexFilePath">path to the index file</param> 
        /// <param name="parameters">file parameters</param> 
        ///
        public CompressedFile(string dataFilePath,        
                              string cipherKey,
                              string indexFilePath, 
                              FileParameters parameters)
        : base(dataFilePath, parameters)
        {
            indexFile = new OSFile(indexFilePath, parameters);
                
            if (cipherKey != null) 
            {
                setKey(Encoding.Unicode.GetBytes(cipherKey)); 
            }
            if (!parameters.readOnly)
            {
                bitmapExtensionQuantum = (int)(parameters.fileExtensionQuantum >> (ALLOCATION_QUANTUM_LOG + 3));
                bitmap = new byte[(int)(base.Length >> (ALLOCATION_QUANTUM_LOG + 3)) + bitmapExtensionQuantum];
                bitmapPos = bitmapStart = Page.pageSize >> (ALLOCATION_QUANTUM_LOG + 3);
    
                pageMap = new PageMap();            
    
                byte[] buf = new byte[8];  
                for (long indexPos = 0, indexSize = indexFile.Length; indexPos < indexSize; indexPos += 8) 
                {
                    indexFile.Read(indexPos, buf);
                    long pagePos = Bytes.unpack8(buf, 0);
                    long pageBitOffs = pagePos >> (Page.pageSizeLog + ALLOCATION_QUANTUM_LOG);
                    long pageBitSize = ((pagePos & (Page.pageSize - 1)) + ALLOCATION_QUANTUM) >> ALLOCATION_QUANTUM_LOG;
                    Bitmap.reserve(bitmap, pageBitOffs, pageBitSize);
                }
            }
        }
    
        private long allocate(int bitSize)
        {
            long pos = Bitmap.allocate(bitmap, bitmapPos, bitmap.Length, bitSize);
            if (pos < 0) 
            { 
                pos = Bitmap.allocate(bitmap, bitmapStart, Bitmap.locateHoleEnd(bitmap, bitmapPos), bitSize);
                if (pos < 0) 
                { 
                    byte[] newBitmap = new byte[bitmap.Length + bitmapExtensionQuantum];
                    Array.Copy(bitmap, 0, newBitmap, 0, bitmap.Length);
                    pos = Bitmap.allocate(newBitmap, Bitmap.locateBitmapEnd(newBitmap, bitmap.Length), newBitmap.Length, bitSize);
                    Debug.Assert(pos >= 0);
                    bitmap = newBitmap;       
                }     
            }
            bitmapPos = (int)((pos + bitSize) >> 3);
            return pos << ALLOCATION_QUANTUM_LOG;
        }
    
        class PageMap : IEnumerable
        { 
            const float LOAD_FACTOR = 0.75f;
    
            static int[] primeNumbers = {
                1361,           /* 6 */
                2729,           /* 7 */
                5471,           /* 8 */
                10949,          /* 9 */
                21911,          /* 10 */
                43853,          /* 11 */
                87719,          /* 12 */
                175447,         /* 13 */
                350899,         /* 14 */
                701819,         /* 15 */
                1403641,        /* 16 */
                2807303,        /* 17 */
                5614657,        /* 18 */
                11229331,       /* 19 */
                22458671,       /* 20 */
                44917381,       /* 21 */
                89834777,       /* 22 */
                179669557,      /* 23 */
                359339171,      /* 24 */
                718678369,      /* 25 */
                1437356741,     /* 26 */
                2147483647      /* 27 (largest signed int prime) */
            };
    
    
            Entry[] table;
            int count;
            int tableSizePrime;
            int tableSize;
            int threshold;
    
            public PageMap() 
            {
                tableSizePrime = 0;
                tableSize = primeNumbers[tableSizePrime];
                threshold = (int)(tableSize * LOAD_FACTOR);
                table = new Entry[tableSize];
            }
    
            public void put(long addr, long newPos, long oldPos) 
            { 
                Entry[] tab = table;
                int index = (int)((addr >> Page.pageSizeLog) % tableSize);
                for (Entry e = tab[index]; e != null; e = e.next) {
                    if (e.addr == addr) {
                        e.newPos = newPos;
                        return;
                    }
                }
                if (count >= threshold) {
                    // Rehash the table if the threshold is exceeded
                    rehash();
                    tab = table;
                    index = (int)((addr >> Page.pageSizeLog) % tableSize);
                } 
    
                // Creates the new entry.
                tab[index] = new Entry(addr, newPos, oldPos, tab[index]);
                count += 1;
            }
        
            public long get(long addr) 
            {
                int index = (int)((addr >> Page.pageSizeLog) % tableSize);
                for (Entry e = table[index]; e != null; e = e.next) {
                    if (e.addr == addr) {
                        return e.newPos;
                    }
                }
                return 0;
            }
    
            public void clear() 
            {
                Entry[] tab = table;
                int size = tableSize;
                for (int i = 0; i < size; i++) { 
                    tab[i] = null;
                }
                count = 0;
            }
    
            void rehash() 
            {
                int oldCapacity = tableSize;
                int newCapacity = tableSize = primeNumbers[++tableSizePrime];
                Entry[] oldMap = table;
                Entry[] newMap = new Entry[newCapacity];
    
                threshold = (int)(newCapacity * LOAD_FACTOR);
                table = newMap;
                tableSize = newCapacity;
    
                for (int i = 0; i < oldCapacity; i++) {
                    for (Entry old = oldMap[i]; old != null; ) {
                        Entry e = old;
                        old = old.next;
                        int index = (int)((e.addr >> Page.pageSizeLog) % newCapacity);
                        e.next = newMap[index];
                        newMap[index] = e;
                    }
                }
            }
    
            public IEnumerator GetEnumerator() 
            { 
                return new PageMapIterator(this);
            }
    
            public int size() 
            { 
                return count;
            }
    
            class PageMapIterator : IEnumerator  
            {
                public PageMapIterator(PageMap map)
                {
                    this.map = map;
                }

                public object Current 
                {
                    get
                    {  
                        return curr;
                    }
                }
                
                public bool MoveNext() 
                {
                    if (curr != null) {         
                        curr = curr.next;
                    }
                    while (curr == null && i < map.tableSize) { 
                        curr = map.table[i++];
                    }
                    return curr != null;
                }
    
                public void Reset()
                {
                    curr = null;
                    i = 0;
                }

                PageMap map;
                Entry curr;
                int i;
            }
    
            public class Entry 
            { 
                public Entry next;
                public long  addr;
                public long  newPos;
                public long  oldPos;
            
                public Entry(long addr, long newPos, long oldPos, Entry chain) 
                { 
                    next = chain;
                    this.addr = addr;
                    this.newPos = newPos;
                    this.oldPos = oldPos;
                }
            }
        }

        public class Bitmap
        {
            static byte[] firstHoleSize = 
            {
                8,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,
                5,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,
                6,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,
                5,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,
                7,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,
                5,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,
                6,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,
                5,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0,4,0,1,0,2,0,1,0,3,0,1,0,2,0,1,0
            };
        
            static byte[] lastHoleSize = 
            {
                8,7,6,6,5,5,5,5,4,4,4,4,4,4,4,4,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,
                2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,
                1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,
                1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,
                0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
                0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
            };
        
            static byte[] maxHoleSize = 
            {
                8,7,6,6,5,5,5,5,4,4,4,4,4,4,4,4,4,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,
                5,4,3,3,2,2,2,2,3,2,2,2,2,2,2,2,4,3,2,2,2,2,2,2,3,2,2,2,2,2,2,2,
                6,5,4,4,3,3,3,3,3,2,2,2,2,2,2,2,4,3,2,2,2,1,1,1,3,2,1,1,2,1,1,1,
                5,4,3,3,2,2,2,2,3,2,1,1,2,1,1,1,4,3,2,2,2,1,1,1,3,2,1,1,2,1,1,1,
                7,6,5,5,4,4,4,4,3,3,3,3,3,3,3,3,4,3,2,2,2,2,2,2,3,2,2,2,2,2,2,2,
                5,4,3,3,2,2,2,2,3,2,1,1,2,1,1,1,4,3,2,2,2,1,1,1,3,2,1,1,2,1,1,1,
                6,5,4,4,3,3,3,3,3,2,2,2,2,2,2,2,4,3,2,2,2,1,1,1,3,2,1,1,2,1,1,1,
                5,4,3,3,2,2,2,2,3,2,1,1,2,1,1,1,4,3,2,2,2,1,1,1,3,2,1,1,2,1,1,0
            };
        
            static byte[] maxHoleOffset = 
            {
                0,1,2,2,3,3,3,3,4,4,4,4,4,4,4,4,0,1,5,5,5,5,5,5,0,5,5,5,5,5,5,5,
                0,1,2,2,0,3,3,3,0,1,6,6,0,6,6,6,0,1,2,2,0,6,6,6,0,1,6,6,0,6,6,6,
                0,1,2,2,3,3,3,3,0,1,4,4,0,4,4,4,0,1,2,2,0,1,0,3,0,1,0,2,0,1,0,5,
                0,1,2,2,0,3,3,3,0,1,0,2,0,1,0,4,0,1,2,2,0,1,0,3,0,1,0,2,0,1,0,7,
                0,1,2,2,3,3,3,3,0,4,4,4,4,4,4,4,0,1,2,2,0,5,5,5,0,1,5,5,0,5,5,5,
                0,1,2,2,0,3,3,3,0,1,0,2,0,1,0,4,0,1,2,2,0,1,0,3,0,1,0,2,0,1,0,6,
                0,1,2,2,3,3,3,3,0,1,4,4,0,4,4,4,0,1,2,2,0,1,0,3,0,1,0,2,0,1,0,5,
                0,1,2,2,0,3,3,3,0,1,0,2,0,1,0,4,0,1,2,2,0,1,0,3,0,1,0,2,0,1,0,0
            };
        
            public static long allocate(byte[] bitmap, int begin, int end, long objBitSize) 
            {
                long holeBitSize = 0;
                for (int i = begin; i < end; i++) { 
                    int mask = bitmap[i] & 0xFF; 
                    if (holeBitSize + firstHoleSize[mask] >= objBitSize) { 
                        bitmap[i] |= (byte)((1 << (int)(objBitSize - holeBitSize)) - 1); 
                        long pos = (long)i*8 - holeBitSize;
                        if (holeBitSize != 0) { 
                            while ((holeBitSize -= 8) > 0) { 
                                bitmap[--i] = (byte)0xFF;
                            }
                            bitmap[i-1] |= (byte)~((1 << -(int)holeBitSize) - 1);
                        }
                        return pos;
                    } else if (Bitmap.maxHoleSize[mask] >= objBitSize) {
                        int holeBitOffset = maxHoleOffset[mask]; 
                        bitmap[i] |= (byte)(((1<<(int)objBitSize) - 1) << holeBitOffset);
                        return (long)i*8 + holeBitOffset;
                    } else {
                        if (lastHoleSize[mask] == 8) { 
                            holeBitSize += 8;
                        } else { 
                            holeBitSize = lastHoleSize[mask];
                        }
                    }
                }
                return -1;
            }
        
            public static int locateBitmapEnd(byte[] bitmap, int offs) 
            { 
                while (offs != 0 && bitmap[--offs] == 0);
                return offs;
            }
        
            public static int locateHoleEnd(byte[] bitmap, int offs) 
            { 
                while (offs < bitmap.Length && bitmap[offs++] == 0);
                return offs;
            }
        
            public static void free(byte[] bitmap, long objBitPos, long objBitSize) 
            { 
                int bitOffs = (int)objBitPos & 7;
                int offs = (int)(objBitPos >> 3);
        
                if (objBitSize > 8 - bitOffs) { 
                    objBitSize -= 8 - bitOffs;
                    bitmap[offs++] &= (byte)((1 << bitOffs) - 1);
                    while ((objBitSize -= 8) > 0) { 
                        bitmap[offs++] = (byte)0;
                    }
                    bitmap[offs] &= (byte)~((1 << ((int)objBitSize + 8)) - 1);
                } else { 
                    bitmap[offs] &= (byte)~(((1 << (int)objBitSize) - 1) << bitOffs); 
                }
            }
        
            public static void reserve(byte[] bitmap, long objBitPos, long objBitSize) 
            { 
                while (--objBitSize >= 0) { 
                    bitmap[(int)(objBitPos >> 3)] |= (byte)(1 << (int)(objBitPos & 7));
                    objBitPos += 1;
                }
            }
        } 

        private void setKey(byte[] key)
        {
            byte[] state = new byte[256];
            for (int counter = 0; counter < 256; ++counter) 
            { 
                state[counter] = (byte)counter;
            }
            int index1 = 0;
            int index2 = 0;
            int length = key.Length;
            for (int counter = 0; counter < 256; ++counter) 
            {
                index2 = (key[index1] + state[counter] + index2) & 0xff;
                byte temp = state[counter];
                state[counter] = state[index2];
                state[index2] = temp;
                index1 = (index1 + 1) % length;
            }
            pattern = new byte[Page.pageSize];
            int x = 0;
            int y = 0;
            for (int i = 0; i < Page.pageSize; i++) {
                x = (x + 1) & 0xff;
                y = (y + state[x]) & 0xff;
                byte temp = state[x];
                state[x] = state[y];
                state[y] = temp;
                pattern[i] = state[(state[x] + state[y]) & 0xff];
            }
        }

        private void crypt(byte[] buf, int len)
        {
            if (pattern != null)
            {
                for (int i = 0; i < len; i++) 
                {
                    buf[i] ^= pattern[i];
                }
            }
        }

        public override bool IsEncrypted
        {
            get
            {
                return pattern != null;
            }
        }

        const int ALLOCATION_QUANTUM_LOG = 9;
        const int ALLOCATION_QUANTUM = 1 << ALLOCATION_QUANTUM_LOG;
            
        byte[]   bitmap;
        int      bitmapPos;
        int      bitmapStart;
        int      bitmapExtensionQuantum;
        
        PageMap  pageMap;
        OSFile   indexFile;    
   
        byte[]   pattern;
    }
}