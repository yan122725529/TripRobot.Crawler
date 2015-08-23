namespace Perst.Impl
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Diagnostics;
    using System.Text;
    using System.IO;
    using Perst;
    using Perst.FullText;

#if USE_GENERICS
    using System.Collections.Generic;
#endif

    public class StorageImpl : Storage
    {
        public const int DEFAULT_PAGE_POOL_SIZE = 4 * 1024 * 1024;

#if (COMPACT_NET_FRAMEWORK || SILVERLIGHT) && !WINRT_NET_FRAMEWORK
        static StorageImpl()
        {
            assemblies = new ArrayList();
        }
        public StorageImpl(Assembly callingAssembly)
        {
            fileParameters = new FileParameters(false, false, false, 1024*1024);
            if (!assemblies.Contains(callingAssembly))
            {
                assemblies.Add(callingAssembly);
            }

            if (!assemblies.Contains(Assembly.GetExecutingAssembly()))
            {
                assemblies.Add(Assembly.GetExecutingAssembly());
            }
            Assembly mscorlib = typeof(string).Assembly;
            if (!assemblies.Contains(mscorlib))
            {
                assemblies.Add(mscorlib);
            }
/*
#if __MonoCS__  || (NET_FRAMEWORK_20 && !COMPACT_NET_FRAMEWORK && !SILVERLIGHT)
            Assembly mscorlib = Assembly.GetAssembly(typeof(System.String));
            if (!assemblies.Contains(mscorlib))
            {
                assemblies.Add(mscorlib);
            }
#endif
*/
        }
#else
        public StorageImpl()
        {
            fileParameters = new FileParameters(false, false, false, 1024*1024);
        }
#endif

        public object Root
        {
            get
            {
                lock (this)
                {
                    if (!opened)
                    {
                        throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                    }
                    int rootOid = header.root[1 - currIndex].rootObject;
                    return (rootOid == 0) ? null : lookupObject(rootOid, null);
                }
            }

            set
            {
                lock (this)
                {
                    if (!opened)
                    {
                        throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                    }
                    if (value == null)
                    {
                        header.root[1 - currIndex].rootObject = 0;
                    }
                    else
                    {
                        if (!IsPersistent(value))
                        {
                            storeObject0(value, false);
                        }
                        header.root[1 - currIndex].rootObject = GetOid(value);
                    }
                    modified = true;
                }
            }

        }

        /// <summary> Initialial database index size - increasing it reduce number of inde reallocation but increase
        /// initial database size. Should be set before openning connection.
        /// </summary>
        const int dbDefaultInitIndexSize = 1024;

        /// <summary> Initial capacity of object hash
        /// </summary>
#if COMPACT_NET_FRAMEWORK
        const int dbDefaultObjectCacheInitSize = 113;
#else
        const int dbDefaultObjectCacheInitSize = 1319;
#endif
        /// <summary> Database extension quantum. Memory is allocate by scanning bitmap. If there is no
        /// large enough hole, then database is extended by the value of dbDefaultExtensionQuantum
        /// This parameter should not be smaller than dbFirstUserId
        /// </summary>
        static long dbDefaultExtensionQuantum = 1024 * 1024;

        const long dbDefaultPagePoolLruLimit = 1L << 60;

#if COMPACT_NET_FRAMEWORK
        internal const int dbDatabaseOidBits = 20; // up to 1 million of objects
#else
        internal const int dbDatabaseOidBits = 31; // up to 2 milliards of objects
#endif
        internal const int dbDatabaseOffsetBits = 32; // up to 4 gigabyte
        internal const int dbLargeDatabaseOffsetBits = 40; // up to 1 terabyte
        internal const int dbMaxObjectOid = (int)((1U << dbDatabaseOidBits) - 1);

        internal const int dbAllocationQuantumBits = 5;
        internal const int dbAllocationQuantum = 1 << dbAllocationQuantumBits;

        const int dbBitmapSegmentBits = Page.pageSizeLog + 3 + dbAllocationQuantumBits;
        const int dbBitmapSegmentSize = 1 << dbBitmapSegmentBits;
        const int dbBitmapPages = 1 << (dbDatabaseOffsetBits - dbBitmapSegmentBits);
        const int dbLargeBitmapPages = 1 << (dbLargeDatabaseOffsetBits - dbBitmapSegmentBits);
        const int dbHandlesPerPageBits = Page.pageSizeLog - 3;
        const int dbHandlesPerPage = 1 << dbHandlesPerPageBits;
        const int dbDirtyPageBitmapSize = 1 << (dbDatabaseOidBits - dbHandlesPerPageBits - 3);

        const int dbAllocRecursionLimit = 10;

        const int dbInvalidId = 0;
        const int dbBitmapId = 1;
        const int dbFirstUserId = dbBitmapId + dbBitmapPages;

        internal const int dbPageObjectFlag = 1;
        internal const int dbModifiedFlag = 2;
        internal const int dbFreeHandleFlag = 4;
        internal const int dbFlagsMask = 7;
        internal const int dbFlagsBits = 3;

        /// <summary>
        /// Current version of database format. 0 means that database is not initilized.
        /// Used to provide backward compatibility of Perst releases.
        /// </summary>
        const byte dbDatabaseFormatVersion = (byte)3;

        public int PerstVersion
        {
            get
            {
                return 443;
            }
        }

        public int DatabaseFormatVersion
        {
            get
            {
                return header.databaseFormatVersion;
            }
        }

        int getBitmapPageId(int i)
        {
            return i < dbBitmapPages ? dbBitmapId + i : header.root[1 - currIndex].bitmapExtent + i - bitmapExtentBase;
        }

        internal long getPos(int oid)
        {
            lock (objectCache)
            {
                if (oid == 0 || oid >= currIndexSize)
                {
                    throw new StorageError(StorageError.ErrorCode.INVALID_OID);
                }
                if (multiclientSupport && !IsInsideThreadTransaction)
                {
                    throw new StorageError(StorageError.ErrorCode.NOT_IN_TRANSACTION);
                }
                Page pg = pool.getPage(header.root[1 - currIndex].index + ((long)(oid >> dbHandlesPerPageBits) << Page.pageSizeLog));
                long pos = Bytes.unpack8(pg.data, (oid & (dbHandlesPerPage - 1)) << 3);
                pool.unfix(pg);
                return pos;
            }
        }

        internal void setPos(int oid, long pos)
        {
            lock (objectCache)
            {
                dirtyPagesMap[oid >> (dbHandlesPerPageBits + 5)] |= 1 << ((oid >> dbHandlesPerPageBits) & 31);
                Page pg = pool.putPage(header.root[1 - currIndex].index + ((long)(oid >> dbHandlesPerPageBits) << Page.pageSizeLog));
                Bytes.pack8(pg.data, (oid & (dbHandlesPerPage - 1)) << 3, pos);
                pool.unfix(pg);
            }
        }

        internal byte[] get(int oid)
        {
            long pos = getPos(oid);
            if ((pos & (dbFreeHandleFlag | dbPageObjectFlag)) != 0)
            {
                throw new StorageError(StorageError.ErrorCode.INVALID_OID);
            }
            return pool.get(pos & ~dbFlagsMask);
        }

        internal Page getPage(int oid)
        {
            long pos = getPos(oid);
            if ((pos & (dbFreeHandleFlag | dbPageObjectFlag)) != dbPageObjectFlag)
            {
                throw new StorageError(StorageError.ErrorCode.DELETED_OBJECT);
            }
            return pool.getPage(pos & ~dbFlagsMask);
        }

        internal Page putPage(int oid)
        {
            lock (objectCache)
            {
                long pos = getPos(oid);
                if ((pos & (dbFreeHandleFlag | dbPageObjectFlag)) != dbPageObjectFlag)
                {
                    throw new StorageError(StorageError.ErrorCode.DELETED_OBJECT);
                }
                if ((pos & dbModifiedFlag) == 0)
                {
                    dirtyPagesMap[oid >> (dbHandlesPerPageBits + 5)] |= 1 << ((oid >> dbHandlesPerPageBits) & 31);
                    allocate(Page.pageSize, oid);
                    cloneBitmap(pos & ~dbFlagsMask, Page.pageSize);
                    pos = getPos(oid);
                }
                modified = true;
                return pool.putPage(pos & ~dbFlagsMask);
            }
        }


        internal int allocatePage()
        {
            int oid = allocateId();
            setPos(oid, allocate(Page.pageSize, 0) | dbPageObjectFlag | dbModifiedFlag);
            return oid;
        }

        public void deallocateObject(object obj)
        {
            lock (this)
            {
                lock (objectCache)
                {
                    if (GetOid(obj) == 0)
                    {
                        return;
                    }
                    if (useSerializableTransactions)
                    {
                        ThreadTransactionContext ctx = TransactionContext;
                        if (ctx.nested != 0)
                        { // serializable transaction
                            ctx.deleted.Add(obj);
                            return;
                        }
                    }
                    deallocateObject0(obj);
                }
            }
        }


        private void deallocateObject0(object obj)
        {
            if (listener != null)
            {
                listener.OnObjectDelete(obj);
            }
            int oid = GetOid(obj);
            long pos = getPos(oid);
            objectCache.remove(oid);
            int offs = (int)pos & (Page.pageSize - 1);
            if ((offs & (dbFreeHandleFlag | dbPageObjectFlag)) != 0)
            {
                throw new StorageError(StorageError.ErrorCode.DELETED_OBJECT);
            }
            freeId(oid);
            if (pos != 0) 
            { 
                Page pg = pool.getPage(pos - offs);
                offs &= ~dbFlagsMask;
                int size = ObjectHeader.getSize(pg.data, offs);
                pool.unfix(pg);
                CustomAllocator allocator = (customAllocatorMap != null)
                    ? getCustomAllocator(obj.GetType()) : null;
                if (allocator != null)
                {
                    allocator.Free(pos & ~dbFlagsMask, size);
                }
                else
                {
                    if ((pos & dbModifiedFlag) != 0)
                    {
                        free(pos & ~dbFlagsMask, size);
                    }
                    else
                    {
                        cloneBitmap(pos, size);
                    }
                }
            }
            UnassignOid(obj);
        }

        internal void freePage(int oid)
        {
            long pos = getPos(oid);
            Debug.Assert((pos & (dbFreeHandleFlag | dbPageObjectFlag)) == dbPageObjectFlag);
            if ((pos & dbModifiedFlag) != 0)
            {
                free(pos & ~dbFlagsMask, Page.pageSize);
            }
            else
            {
                cloneBitmap(pos & ~dbFlagsMask, Page.pageSize);
            }
            freeId(oid);
        }

        virtual protected bool isDirty()
        {
            return header.dirty;
        }


        internal virtual void setDirty()
        {
            modified = true;
            if (!header.dirty)
            {
                header.dirty = true;
                Page pg = pool.putPage(0);
                header.pack(pg.data);
                pool.flush();
                pool.unfix(pg);
            }
        }

        internal int allocateId()
        {
            lock (objectCache)
            {
                int oid;
                int curr = 1 - currIndex;
                setDirty();
                if (reuseOid && (oid = header.root[curr].freeList) != 0)
                {
                    header.root[curr].freeList = (int)(getPos(oid) >> dbFlagsBits);
                    Debug.Assert(header.root[curr].freeList >= 0);
                    dirtyPagesMap[oid >> (dbHandlesPerPageBits + 5)]
                        |= 1 << ((oid >> dbHandlesPerPageBits) & 31);
                    return oid;
                }

                if (currIndexSize >= dbMaxObjectOid)
                {
                    throw new StorageError(StorageError.ErrorCode.TOO_MUCH_OBJECTS);
                }
                if (currIndexSize >= header.root[curr].indexSize)
                {
                    int oldIndexSize = header.root[curr].indexSize;
                    int newIndexSize = oldIndexSize * 2;
                    if (newIndexSize < oldIndexSize)
                    {
                        newIndexSize = int.MaxValue & ~(dbHandlesPerPage - 1);
                        if (newIndexSize <= oldIndexSize)
                        {
                            throw new StorageError(StorageError.ErrorCode.NOT_ENOUGH_SPACE);
                        }
                    }
                    long newIndex = allocate(newIndexSize * 8L, 0);
                    if (currIndexSize >= header.root[curr].indexSize)
                    {
                        long oldIndex = header.root[curr].index;
                        pool.copy(newIndex, oldIndex, currIndexSize * 8L);
                        header.root[curr].index = newIndex;
                        header.root[curr].indexSize = newIndexSize;
                        free(oldIndex, oldIndexSize * 8L);
                    }
                    else
                    {
                        // index was already reallocated
                        free(newIndex, newIndexSize * 8L);
                    }
                }
                oid = currIndexSize;
                header.root[curr].indexUsed = ++currIndexSize;
                return oid;
            }
        }

        internal void freeId(int oid)
        {
            lock (objectCache)
            {
                setPos(oid, ((long)(header.root[1 - currIndex].freeList) << dbFlagsBits) | dbFreeHandleFlag);
                header.root[1 - currIndex].freeList = oid;
            }
        }

        internal static byte[] firstHoleSize = new byte[] { 8, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 6, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 7, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 6, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0 };
        internal static byte[] lastHoleSize = new byte[] { 8, 7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        internal static byte[] maxHoleSize = new byte[] { 8, 7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 5, 4, 3, 3, 2, 2, 2, 2, 3, 2, 2, 2, 2, 2, 2, 2, 4, 3, 2, 2, 2, 2, 2, 2, 3, 2, 2, 2, 2, 2, 2, 2, 6, 5, 4, 4, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 4, 3, 2, 2, 2, 1, 1, 1, 3, 2, 1, 1, 2, 1, 1, 1, 5, 4, 3, 3, 2, 2, 2, 2, 3, 2, 1, 1, 2, 1, 1, 1, 4, 3, 2, 2, 2, 1, 1, 1, 3, 2, 1, 1, 2, 1, 1, 1, 7, 6, 5, 5, 4, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 4, 3, 2, 2, 2, 2, 2, 2, 3, 2, 2, 2, 2, 2, 2, 2, 5, 4, 3, 3, 2, 2, 2, 2, 3, 2, 1, 1, 2, 1, 1, 1, 4, 3, 2, 2, 2, 1, 1, 1, 3, 2, 1, 1, 2, 1, 1, 1, 6, 5, 4, 4, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 4, 3, 2, 2, 2, 1, 1, 1, 3, 2, 1, 1, 2, 1, 1, 1, 5, 4, 3, 3, 2, 2, 2, 2, 3, 2, 1, 1, 2, 1, 1, 1, 4, 3, 2, 2, 2, 1, 1, 1, 3, 2, 1, 1, 2, 1, 1, 0 };
        internal static byte[] maxHoleOffset = new byte[] { 0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 0, 1, 5, 5, 5, 5, 5, 5, 0, 5, 5, 5, 5, 5, 5, 5, 0, 1, 2, 2, 0, 3, 3, 3, 0, 1, 6, 6, 0, 6, 6, 6, 0, 1, 2, 2, 0, 6, 6, 6, 0, 1, 6, 6, 0, 6, 6, 6, 0, 1, 2, 2, 3, 3, 3, 3, 0, 1, 4, 4, 0, 4, 4, 4, 0, 1, 2, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 5, 0, 1, 2, 2, 0, 3, 3, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 2, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 7, 0, 1, 2, 2, 3, 3, 3, 3, 0, 4, 4, 4, 4, 4, 4, 4, 0, 1, 2, 2, 0, 5, 5, 5, 0, 1, 5, 5, 0, 5, 5, 5, 0, 1, 2, 2, 0, 3, 3, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 2, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 6, 0, 1, 2, 2, 3, 3, 3, 3, 0, 1, 4, 4, 0, 4, 4, 4, 0, 1, 2, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 5, 0, 1, 2, 2, 0, 3, 3, 3, 0, 1, 0, 2, 0, 1, 0, 4, 0, 1, 2, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0, 0 };

        internal const int pageBits = Page.pageSize * 8;
        internal const int inc = Page.pageSize / dbAllocationQuantum / 8;

        internal static void memset(Page pg, int offs, int pattern, int len)
        {
            byte[] arr = pg.data;
            byte pat = (byte)pattern;
            while (--len >= 0)
            {
                arr[offs++] = pat;
            }
        }

        public long UsedSize
        {
            get
            {
                return usedSize;
            }
        }

        public long DatabaseSize
        {
            get
            {
                return header.root[1 - currIndex].size;
            }
        }

        public int MaxOid
        {
            get
            {
                return currIndexSize;
            }
        }

        internal void extend(long size)
        {
            if (size > header.root[1 - currIndex].size)
            {
                header.root[1 - currIndex].size = size;
            }
        }

        class Location
        {
            internal long pos;
            internal long size;
            internal Location next;
        }

        internal bool wasReserved(long pos, long size)
        {
            for (Location location = reservedChain; location != null; location = location.next)
            {
                if ((pos >= location.pos && pos - location.pos < location.size) || (pos <= location.pos && location.pos - pos < size))
                {
                    return true;
                }
            }
            return false;
        }

        internal void reserveLocation(long pos, long size)
        {
            Location location = new Location();
            location.pos = pos;
            location.size = size;
            location.next = reservedChain;
            reservedChain = location;
            reservedChainLength += 1;
        }

        internal void commitLocation()
        {
            reservedChain = reservedChain.next;
            reservedChainLength -= 1;
        }

        Page putBitmapPage(int i)
        {
            return putPage(getBitmapPageId(i));
        }

        Page getBitmapPage(int i)
        {
            return getPage(getBitmapPageId(i));
        }


        internal long allocate(long size, int oid)
        {
            lock (objectCache)
            {
                setDirty();
                size = (size + dbAllocationQuantum - 1) & ~(dbAllocationQuantum - 1);
                Debug.Assert(size != 0);
                allocatedDelta += size;
                if (allocatedDelta > gcThreshold)
                {
                    gc0();
                }
                int objBitSize = (int)(size >> dbAllocationQuantumBits);
                Debug.Assert(objBitSize == (size >> dbAllocationQuantumBits));
                long pos;
                int holeBitSize = 0;
                int allocBitSize = objBitSize;
                int alignment = (int)size & (Page.pageSize - 1);
                int offs, firstPage, lastPage, i, j;
                int holeBeforeFreePage = 0;
                int freeBitmapPage = 0;
                int curr = 1 - currIndex;
                Page pg;

                lastPage = header.root[curr].bitmapEnd - dbBitmapId;
                usedSize += size;

                if (alignment == 0)
                {
                    if ((reservedChainLength & 1) != 0)
                    {
                        Debug.Assert(size == Page.pageSize);
                        allocBitSize <<= 1;
                    }
                    if (reservedChainLength > dbAllocRecursionLimit)
                    {
                        firstPage = lastPage-1;
                        offs = 0;
                    }
                    else
                    {
                        firstPage = currPBitmapPage;
                        offs = (currPBitmapOffs + inc - 1) & ~(inc - 1);
                    }
                }
                else
                {
                    firstPage = currRBitmapPage;
                    offs = currRBitmapOffs;
                }

                while (true)
                {
                    if (alignment == 0)
                    {
                        // allocate page object
                        for (i = firstPage; i < lastPage; i++)
                        {
                            int spaceNeeded = allocBitSize - holeBitSize < pageBits
                                ? allocBitSize - holeBitSize : pageBits;
                            if (bitmapPageAvailableSpace[i] <= spaceNeeded)
                            {
                                holeBitSize = 0;
                                offs = 0;
                                continue;
                            }
                            pg = getBitmapPage(i);
                            int startOffs = offs;
                            while (offs < Page.pageSize)
                            {
                                if (pg.data[offs++] != 0)
                                {
                                    offs = (offs + inc - 1) & ~(inc - 1);
                                    holeBitSize = 0;
                                }
                                else if ((holeBitSize += 8) == allocBitSize)
                                {
                                    pos = (((long)i * Page.pageSize + offs) * 8 - holeBitSize)
                                        << dbAllocationQuantumBits;
                                    if (wasReserved(pos, size))
                                    {
                                        startOffs = offs = (offs + inc - 1) & ~(inc - 1);
                                        holeBitSize = 0;
                                        continue;
                                    }
                                    reserveLocation(pos, size);
                                    extend(pos + size);
                                    if (oid != 0)
                                    {
                                        long prev = getPos(oid);
                                        uint marker = (uint)prev & dbFlagsMask;
                                        pool.copy(pos, prev - marker, size);
                                        setPos(oid, pos | marker | dbModifiedFlag);
                                    }
                                    pool.unfix(pg);
                                    int holeBytes = holeBitSize >> 3;
                                    if (allocBitSize != objBitSize)
                                    {
                                        Debug.Assert(holeBytes == inc*2);
                                        holeBytes = inc;
                                        if (inc > offs)
                                        {
                                            i -= 1;
                                            offs = Page.pageSize;
                                        }
                                        else
                                        {
                                            offs -= inc;
                                        }
                                    }
                                    currPBitmapPage = i;
                                    currPBitmapOffs = offs;
                                    pg = putBitmapPage(i);

                                    if (holeBytes > offs)
                                    {
                                        memset(pg, 0, 0xFF, offs);
                                        holeBytes -= offs;
                                        pool.unfix(pg);
                                        pg = putBitmapPage(--i);
                                        offs = Page.pageSize;
                                    }
                                    while (holeBytes > Page.pageSize)
                                    {
                                        memset(pg, 0, 0xFF, Page.pageSize);
                                        holeBytes -= Page.pageSize;
                                        bitmapPageAvailableSpace[i] = 0;
                                        pool.unfix(pg);
                                        pg = putBitmapPage(--i);
                                    }
                                    memset(pg, offs - holeBytes, 0xFF, holeBytes);
                                    commitLocation();
                                    pool.unfix(pg);
                                    return pos;
                                }
                            }
                            if (startOffs == 0 && holeBitSize == 0
                                && spaceNeeded < bitmapPageAvailableSpace[i])
                            {
                                bitmapPageAvailableSpace[i] = spaceNeeded;
                            }
                            offs = 0;
                            pool.unfix(pg);
                        }
                    }
                    else
                    {
                        for (i = firstPage; i < lastPage; i++)
                        {
                            int spaceNeeded = objBitSize - holeBitSize < pageBits
                                ? objBitSize - holeBitSize : pageBits;
                            if (bitmapPageAvailableSpace[i] <= spaceNeeded)
                            {
                                holeBitSize = 0;
                                offs = 0;
                                continue;
                            }
                            pg = getBitmapPage(i);
                            int startOffs = offs;
                            while (offs < Page.pageSize)
                            {
                                int mask = pg.data[offs] & 0xFF;
                                if (holeBitSize + firstHoleSize[mask] >= objBitSize)
                                {
                                    pos = (((long)i * Page.pageSize + offs) * 8
                                        - holeBitSize) << dbAllocationQuantumBits;
                                    if (wasReserved(pos, size))
                                    {
                                        startOffs = offs += 1;
                                        holeBitSize = 0;
                                        continue;
                                    }
                                    reserveLocation(pos, size);
                                    currRBitmapPage = i;
                                    currRBitmapOffs = offs;
                                    extend(pos + size);
                                    if (oid != 0)
                                    {
                                        long prev = getPos(oid);
                                        uint marker = (uint)prev & dbFlagsMask;
                                        pool.copy(pos, prev - marker, size);
                                        setPos(oid, pos | marker | dbModifiedFlag);
                                    }
                                    pool.unfix(pg);
                                    pg = putBitmapPage(i);
                                    pg.data[offs] |= (byte)((1 << (objBitSize - holeBitSize)) - 1);
                                    if (holeBitSize != 0)
                                    {
                                        if (holeBitSize > offs * 8)
                                        {
                                            memset(pg, 0, 0xFF, offs);
                                            holeBitSize -= offs * 8;
                                            pool.unfix(pg);
                                            pg = putBitmapPage(--i);
                                            offs = Page.pageSize;
                                        }
                                        while (holeBitSize > pageBits)
                                        {
                                            memset(pg, 0, 0xFF, Page.pageSize);
                                            holeBitSize -= pageBits;
                                            bitmapPageAvailableSpace[i] = 0;
                                            pool.unfix(pg);
                                            pg = putBitmapPage(--i);
                                        }
                                        while ((holeBitSize -= 8) > 0)
                                        {
                                            pg.data[--offs] = (byte)0xFF;
                                        }
                                        pg.data[offs - 1] |= (byte)~((1 << -holeBitSize) - 1);
                                    }
                                    pool.unfix(pg);
                                    commitLocation();
                                    return pos;
                                }
                                else if (maxHoleSize[mask] >= objBitSize)
                                {
                                    int holeBitOffset = maxHoleOffset[mask];
                                    pos = (((long)i * Page.pageSize + offs) * 8 + holeBitOffset) << dbAllocationQuantumBits;
                                    if (wasReserved(pos, size))
                                    {
                                        startOffs = offs += 1;
                                        holeBitSize = 0;
                                        continue;
                                    }
                                    reserveLocation(pos, size);
                                    currRBitmapPage = i;
                                    currRBitmapOffs = offs;
                                    extend(pos + size);
                                    if (oid != 0)
                                    {
                                        long prev = getPos(oid);
                                        uint marker = (uint)prev & dbFlagsMask;
                                        pool.copy(pos, prev - marker, size);
                                        setPos(oid, pos | marker | dbModifiedFlag);
                                    }
                                    pool.unfix(pg);
                                    pg = putBitmapPage(i);
                                    pg.data[offs] |= (byte)(((1 << objBitSize) - 1) << holeBitOffset);
                                    pool.unfix(pg);
                                    commitLocation();
                                    return pos;
                                }
                                offs += 1;
                                if (lastHoleSize[mask] == 8)
                                {
                                    holeBitSize += 8;
                                }
                                else
                                {
                                    holeBitSize = lastHoleSize[mask];
                                }
                            }
                            if (startOffs == 0 && holeBitSize == 0
                                && spaceNeeded < bitmapPageAvailableSpace[i])
                            {
                                bitmapPageAvailableSpace[i] = spaceNeeded;
                            }
                            offs = 0;
                            pool.unfix(pg);
                        }
                    }
                    if (firstPage == 0 || reservedChainLength > dbAllocRecursionLimit)
                    {
                        if (freeBitmapPage > i)
                        {
                            i = freeBitmapPage;
                            holeBitSize = holeBeforeFreePage;
                        }
                        objBitSize -= holeBitSize;
                        // number of bits reserved for the object and aligned on page boundary
                        int skip = (objBitSize + Page.pageSize / dbAllocationQuantum - 1)
                            & ~(Page.pageSize / dbAllocationQuantum - 1);
                        // page aligned position after allocated object
                        pos = ((long)i << dbBitmapSegmentBits) + ((long)skip << dbAllocationQuantumBits);

                        long extension = (size > extensionQuantum) ? size : extensionQuantum;
                        int oldIndexSize = 0;
                        long oldIndex = 0;
                        int morePages = (int)((extension + Page.pageSize * (dbAllocationQuantum * 8 - 1) - 1)
                            / (Page.pageSize * (dbAllocationQuantum * 8 - 1)));
                        if (i + morePages > dbLargeBitmapPages)
                        {
                            throw new StorageError(StorageError.ErrorCode.NOT_ENOUGH_SPACE);
                        }
                        if (i <= dbBitmapPages && i + morePages > dbBitmapPages)
                        {
                            // We are out of space mapped by memory default allocation bitmap
                            oldIndexSize = header.root[curr].indexSize;
                            if (oldIndexSize <= currIndexSize + dbLargeBitmapPages - dbBitmapPages)
                            {
                                int newIndexSize = oldIndexSize;
                                oldIndex = header.root[curr].index;
                                do
                                {
                                    newIndexSize <<= 1;
                                    if (newIndexSize < 0)
                                    {
                                        newIndexSize = int.MaxValue & ~(dbHandlesPerPage - 1);
                                        if (newIndexSize < currIndexSize + dbLargeBitmapPages - dbBitmapPages)
                                        {
                                            throw new StorageError(StorageError.ErrorCode.NOT_ENOUGH_SPACE);
                                        }
                                        break;
                                    }
                                } while (newIndexSize <= currIndexSize + dbLargeBitmapPages - dbBitmapPages);

                                if (size + newIndexSize * 8L > extensionQuantum)
                                {
                                    extension = size + newIndexSize * 8L;
                                    morePages = (int)((extension + Page.pageSize * (dbAllocationQuantum * 8 - 1) - 1)
                                        / (Page.pageSize * (dbAllocationQuantum * 8 - 1)));
                                }
                                extend(pos + (long)morePages * Page.pageSize + newIndexSize * 8L);
                                long newIndex = pos + (long)morePages * Page.pageSize;
                                fillBitmap(pos + (skip >> 3) + (long)morePages * (Page.pageSize / dbAllocationQuantum / 8),
                                    newIndexSize >> dbAllocationQuantumBits);
                                pool.copy(newIndex, oldIndex, oldIndexSize * 8L);
                                header.root[curr].index = newIndex;
                                header.root[curr].indexSize = newIndexSize;
                            }
                            int[] newBitmapPageAvailableSpace = new int[dbLargeBitmapPages];
                            Array.Copy(bitmapPageAvailableSpace, 0, newBitmapPageAvailableSpace, 0, dbBitmapPages);
                            for (j = dbBitmapPages; j < dbLargeBitmapPages; j++)
                            {
                                newBitmapPageAvailableSpace[j] = int.MaxValue;
                            }
                            bitmapPageAvailableSpace = newBitmapPageAvailableSpace;

                            for (j = 0; j < dbLargeBitmapPages - dbBitmapPages; j++)
                            {
                                setPos(currIndexSize + j, dbFreeHandleFlag);
                            }

                            header.root[curr].bitmapExtent = currIndexSize;
                            header.root[curr].indexUsed = currIndexSize += dbLargeBitmapPages - dbBitmapPages;
                        }
                        extend(pos + (long)morePages * Page.pageSize);
                        long adr = pos;
                        int len = objBitSize >> 3;
                        // fill bitmap pages used for allocation of object space with 0xFF
                        while (len >= Page.pageSize)
                        {
                            pg = pool.putPage(adr);
                            memset(pg, 0, 0xFF, Page.pageSize);
                            pool.unfix(pg);
                            adr += Page.pageSize;
                            len -= Page.pageSize;
                        }
                        // fill part of last page responsible for allocation of object space
                        pg = pool.putPage(adr);
                        memset(pg, 0, 0xFF, len);
                        pg.data[len] = (byte)((1 << (objBitSize & 7)) - 1);
                        pool.unfix(pg);

                        // mark in bitmap newly allocated object
                        fillBitmap(pos + (skip >> 3), morePages * (Page.pageSize / dbAllocationQuantum / 8));

                        j = i;
                        while (--morePages >= 0)
                        {
                            setPos(getBitmapPageId(j++), pos | dbPageObjectFlag | dbModifiedFlag);
                            pos += Page.pageSize;
                        }
                        header.root[curr].bitmapEnd = j + dbBitmapId;
                        j = i + objBitSize / pageBits;
                        if (alignment != 0)
                        {
                            currRBitmapPage = j;
                            currRBitmapOffs = 0;
                        }
                        else
                        {
                            currPBitmapPage = j;
                            currPBitmapOffs = 0;
                        }
                        while (j > i)
                        {
                            bitmapPageAvailableSpace[--j] = 0;
                        }

                        pos = ((long)i * Page.pageSize * 8 - holeBitSize) << dbAllocationQuantumBits;
                        if (oid != 0)
                        {
                            long prev = getPos(oid);
                            uint marker = (uint)prev & dbFlagsMask;
                            pool.copy(pos, prev - marker, size);
                            setPos(oid, pos | marker | dbModifiedFlag);
                        }

                        if (holeBitSize != 0)
                        {
                            reserveLocation(pos, size);
                            while (holeBitSize > pageBits)
                            {
                                holeBitSize -= pageBits;
                                pg = putBitmapPage(--i);
                                memset(pg, 0, 0xFF, Page.pageSize);
                                bitmapPageAvailableSpace[i] = 0;
                                pool.unfix(pg);
                            }
                            pg = putBitmapPage(--i);
                            offs = Page.pageSize;
                            while ((holeBitSize -= 8) > 0)
                            {
                                pg.data[--offs] = (byte)0xFF;
                            }
                            pg.data[offs - 1] |= (byte)~((1 << -holeBitSize) - 1);
                            pool.unfix(pg);
                            commitLocation();
                        }
                        if (oldIndex != 0)
                        {
                            free(oldIndex, oldIndexSize * 8L);
                        }
                        return pos;
                    }
                    if (gcThreshold != Int64.MaxValue && !gcDone)
                    {
                        allocatedDelta -= size;
                        usedSize -= size;
                        gc0();
                        currRBitmapPage = currPBitmapPage = 0;
                        currRBitmapOffs = currPBitmapOffs = 0;
                        return allocate(size, oid);
                    }
                    freeBitmapPage = i;
                    holeBeforeFreePage = holeBitSize;
                    holeBitSize = 0;
                    lastPage = firstPage + 1;
                    firstPage = 0;
                    offs = 0;
                }
            }
        }

        void fillBitmap(long adr, int len)
        {
            while (true)
            {
                int off = (int)adr & (Page.pageSize - 1);
                Page pg = pool.putPage(adr - off);
                if (Page.pageSize - off >= len)
                {
                    memset(pg, off, 0xFF, len);
                    pool.unfix(pg);
                    break;
                }
                else
                {
                    memset(pg, off, 0xFF, Page.pageSize - off);
                    pool.unfix(pg);
                    adr += Page.pageSize - off;
                    len -= Page.pageSize - off;
                }
            }
        }


        internal void free(long pos, long size)
        {
            lock (objectCache)
            {
                Debug.Assert(pos != 0 && (pos & (dbAllocationQuantum - 1)) == 0);
                long quantNo = pos >> dbAllocationQuantumBits;
                int objBitSize = (int)((size + dbAllocationQuantum - 1) >> dbAllocationQuantumBits);
                int pageId = (int)(quantNo >> (Page.pageSizeLog + 3));
                int offs = (int)(quantNo & (Page.pageSize * 8 - 1)) >> 3;
                Page pg = putBitmapPage(pageId);
                int bitOffs = (int)quantNo & 7;

                allocatedDelta -= (long)objBitSize << dbAllocationQuantumBits;
                usedSize -= (long)objBitSize << dbAllocationQuantumBits;

                if ((pos & (Page.pageSize - 1)) == 0 && size >= Page.pageSize)
                {
                    if (pageId == currPBitmapPage && offs < currPBitmapOffs)
                    {
                        currPBitmapOffs = offs;
                    }
                }
                if (pageId == currRBitmapPage && offs < currRBitmapOffs)
                {
                    currRBitmapOffs = offs;
                }
                bitmapPageAvailableSpace[pageId] = System.Int32.MaxValue;

                if (objBitSize > 8 - bitOffs)
                {
                    objBitSize -= 8 - bitOffs;
                    pg.data[offs++] &= (byte)((1 << bitOffs) - 1);
                    while (objBitSize + offs * 8 > Page.pageSize * 8)
                    {
                        memset(pg, offs, 0, Page.pageSize - offs);
                        pool.unfix(pg);
                        pg = putBitmapPage(++pageId);
                        bitmapPageAvailableSpace[pageId] = System.Int32.MaxValue;
                        objBitSize -= (Page.pageSize - offs) * 8;
                        offs = 0;
                    }
                    while ((objBitSize -= 8) > 0)
                    {
                        pg.data[offs++] = (byte)0;
                    }
                    pg.data[offs] &= (byte)(~((1 << (objBitSize + 8)) - 1));
                }
                else
                {
                    pg.data[offs] &= (byte)(~(((1 << objBitSize) - 1) << bitOffs));
                }
                pool.unfix(pg);
            }
        }

        class CloneNode
        {
            internal long pos;
            internal CloneNode next;

            internal CloneNode(long pos, CloneNode list)
            {
                this.pos = pos;
                this.next = list;
            }
        }


        internal void cloneBitmap(long pos, long size)
        {
            lock (objectCache)
            {
                if (insideCloneBitmap)
                {
                    Debug.Assert(size == Page.pageSize);
                    cloneList = new CloneNode(pos, cloneList);
                }
                else
                {
                    insideCloneBitmap = true;
                    while (true)
                    {
                        long quantNo = pos >> dbAllocationQuantumBits;
                        int objBitSize = (int)((size + dbAllocationQuantum - 1) >> dbAllocationQuantumBits);
                        int pageId = (int)(quantNo >> (Page.pageSizeLog + 3));
                        int offs = (int)(quantNo & (Page.pageSize * 8 - 1)) >> 3;
                        int bitOffs = (int)quantNo & 7;
                        int oid = getBitmapPageId(pageId);
                        pos = getPos(oid);
                        if ((pos & dbModifiedFlag) == 0)
                        {
                            dirtyPagesMap[oid >> (dbHandlesPerPageBits + 5)]
                                |= 1 << ((oid >> dbHandlesPerPageBits) & 31);
                            allocate(Page.pageSize, oid);
                            cloneBitmap(pos & ~dbFlagsMask, Page.pageSize);
                        }

                        if (objBitSize > 8 - bitOffs)
                        {
                            objBitSize -= 8 - bitOffs;
                            offs += 1;
                            while (objBitSize + offs * 8 > Page.pageSize * 8)
                            {
                                oid = getBitmapPageId(++pageId);
                                pos = getPos(oid);
                                if ((pos & dbModifiedFlag) == 0)
                                {
                                    dirtyPagesMap[oid >> (dbHandlesPerPageBits + 5)]
                                        |= 1 << ((oid >> dbHandlesPerPageBits) & 31);
                                    allocate(Page.pageSize, oid);
                                    cloneBitmap(pos & ~dbFlagsMask, Page.pageSize);
                                }
                                objBitSize -= (Page.pageSize - offs) * 8;
                                offs = 0;
                            }
                        }
                        if (cloneList == null)
                        {
                            break;
                        }
                        pos = cloneList.pos;
                        size = Page.pageSize;
                        cloneList = cloneList.next;
                    }
                    insideCloneBitmap = false;
                }
            }
        }

        public void Open(String filePath)
        {
            Open(filePath, DEFAULT_PAGE_POOL_SIZE);
        }

        public void Open(IFile file)
        {
            Open(file, DEFAULT_PAGE_POOL_SIZE);
        }

        public void Open(String filePath, long pagePoolSize)
        {
            IFile file = filePath.StartsWith("@")
                ? (IFile)new MultiFile(filePath.Substring(1), fileParameters)
                : (IFile)new OSFile(filePath, fileParameters);
            try
            {
                Open(file, pagePoolSize);
            }
            catch (StorageError ex)
            {
                file.Close();
                throw ex;
            }
#if SILVERLIGHT && !WINRT_NET_FRAMEWORK
            catch (System.IO.IsolatedStorage.IsolatedStorageException ex)
            {
                file.Close();
                throw ex;
            }
#endif
        }

        public void ClearObjectCache()
        {
            objectCache.clear();
        }

        protected virtual OidHashTable createObjectCache(string kind, long pagePoolSize, int objectCacheSize)
        {
            switch (kind)
            {
                case "strong":
                   return new StrongHashTable(this, objectCacheSize);
                case "weak":
                   return new WeakHashTable(this, objectCacheSize);
                case "pinned":
                   return new PinWeakHashTable(this, objectCacheSize);
                case "lru":
                   return new LruObjectCache(this, objectCacheSize);
                default:
                   return pagePoolSize == 0
                       ? (OidHashTable)new StrongHashTable(this, objectCacheSize)
                       : (OidHashTable)new LruObjectCache(this, objectCacheSize);
            }
        }


        public void Open(String filePath, long pagePoolSize, String cipherKey)
        {
            Rc4File file = new Rc4File(filePath, fileParameters, cipherKey);
            try
            {
                Open(file, pagePoolSize);
            }
            catch (StorageError ex)
            {
                file.Close();
                throw ex;
            }
        }

        protected void initialize(IFile file, long pagePoolSize)
        {
            this.file = file;
            if (fileParameters.lockFile && !multiclientSupport)
            {
                file.Lock(fileParameters.readOnly);
            }
            dirtyPagesMap = new int[dbDirtyPageBitmapSize / 4 + 1];
            gcThreshold = Int64.MaxValue;
            backgroundGcMonitor = new object();
            backgroundGcStartMonitor = new object();
            gcGo = false;
#if !WINRT_NET_FRAMEWORK
            gcThread = null;
#endif
            gcActive = false;
            gcDone = false;
            allocatedDelta = 0;

            resolvedTypes = new Hashtable();
            recursiveLoadingPolicy = new Hashtable();
            recursiveLoadingPolicyDefined = false;

            reservedChain = null;
            reservedChainLength = 0;
            cloneList = null;
            insideCloneBitmap = false;

            nNestedTransactions = 0;
            nBlockedTransactions = 0;
            nCommittedTransactions = 0;
            scheduledCommitTime = Int64.MaxValue;
#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
            transactionMonitor = new CNetMonitor();
#else
            transactionMonitor = new object();
#endif
            transactionLock = new PersistentResource();

            modified = false;

            objectCache = createObjectCache(cacheKind, pagePoolSize, objectCacheInitSize);

            objMap = new ObjectMap(objectCacheInitSize);

            classDescMap = new Hashtable();
            descList = null;

#if SUPPORT_RAW_TYPE
            objectFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#endif
            header = new Header();
            pool = pagePoolSize == 0 && !multiclientSupport
                ? new InfinitePagePool(file)
                : new PagePool(file, (int)(pagePoolSize / Page.pageSize), pagePoolLruLimit);
        }

        public virtual void Open(IFile file, long pagePoolSize)
        {
            lock (this)
            {
                Page pg;
                int i;

                if (opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_ALREADY_OPENED);
                }
                initialize(file, pagePoolSize);

                if (multiclientSupport)
                {
                    BeginThreadTransaction(fileParameters.readOnly ? TransactionMode.ReadOnly : TransactionMode.ReadWrite);
                }
                scheduledCommitTime = Int64.MaxValue;

                byte[] buf = new byte[Header.Sizeof];
                int rc = file.Read(0, buf);
                StorageError.ErrorCode corruptionError = file.IsEncrypted ? StorageError.ErrorCode.WRONG_CIPHER_KEY : StorageError.ErrorCode.DATABASE_CORRUPTED;
                if (rc > 0 && rc < Header.Sizeof)
                {
                    throw new StorageError(corruptionError);
                }
                header.unpack(buf);
                if (header.curr < 0 || header.curr > 1)
                {
                    throw new StorageError(corruptionError);
                }
                transactionId = header.transactionId;
                if (header.databaseFormatVersion == 0) // database not initialized
                {
                    if (fileParameters.readOnly)
                    {
                        throw new StorageError(StorageError.ErrorCode.READ_ONLY_DATABASE);
                    }

                    int indexSize = initIndexSize;
                    if (indexSize < dbFirstUserId)
                    {
                        indexSize = dbFirstUserId;
                    }
                    indexSize = (indexSize + dbHandlesPerPage - 1) & ~(dbHandlesPerPage - 1);

                    bitmapExtentBase = dbBitmapPages;

                    header.curr = currIndex = 0;
                    long used = Page.pageSize;
                    header.root[0].index = used;
                    header.root[0].indexSize = indexSize;
                    header.root[0].indexUsed = dbFirstUserId;
                    header.root[0].freeList = 0;
                    used += indexSize * 8L;
                    header.root[1].index = used;
                    header.root[1].indexSize = indexSize;
                    header.root[1].indexUsed = dbFirstUserId;
                    header.root[1].freeList = 0;
                    used += indexSize * 8L;

                    header.root[0].shadowIndex = header.root[1].index;
                    header.root[1].shadowIndex = header.root[0].index;
                    header.root[0].shadowIndexSize = indexSize;
                    header.root[1].shadowIndexSize = indexSize;

                    int bitmapPages = (int)((used + Page.pageSize * (dbAllocationQuantum * 8 - 1) - 1) / (Page.pageSize * (dbAllocationQuantum * 8 - 1)));
                    long bitmapSize = (long)bitmapPages * Page.pageSize;
                    int usedBitmapSize = (int)((used + bitmapSize) >> (dbAllocationQuantumBits + 3));

                    for (i = 0; i < bitmapPages; i++)
                    {
                        pg = pool.putPage(used + (long)i * Page.pageSize);
                        byte[] bitmap = pg.data;
                        int n = usedBitmapSize > Page.pageSize ? Page.pageSize : usedBitmapSize;
                        for (int j = 0; j < n; j++)
                        {
                            bitmap[j] = (byte)0xFF;
                        }
                        pool.unfix(pg);
                    }

                    int bitmapIndexSize = ((dbBitmapId + dbBitmapPages) * 8 + Page.pageSize - 1) & ~(Page.pageSize - 1);
                    byte[] index = new byte[bitmapIndexSize];
                    Bytes.pack8(index, dbInvalidId * 8, dbFreeHandleFlag);
                    for (i = 0; i < bitmapPages; i++)
                    {
                        Bytes.pack8(index, (dbBitmapId + i) * 8, used | dbPageObjectFlag);
                        used += Page.pageSize;
                    }
                    header.root[0].bitmapEnd = dbBitmapId + i;
                    header.root[1].bitmapEnd = dbBitmapId + i;
                    while (i < dbBitmapPages)
                    {
                        Bytes.pack8(index, (dbBitmapId + i) * 8, dbFreeHandleFlag);
                        i += 1;
                    }
                    header.root[0].size = used;
                    header.root[1].size = used;
                    usedSize = used;
                    committedIndexSize = currIndexSize = dbFirstUserId;

                    pool.write(header.root[1].index, index);
                    pool.write(header.root[0].index, index);

                    modified = true;
                    header.dirty = true;
                    header.root[0].size = header.root[1].size;
                    pg = pool.putPage(0);
                    header.pack(pg.data);
                    pool.flush();
                    pool.modify(pg);
                    header.databaseFormatVersion = dbDatabaseFormatVersion;
                    header.pack(pg.data);
                    pool.unfix(pg);
                    pool.flush();
                }
                else
                {
                    int curr = header.curr;
                    currIndex = curr;
                    if (header.root[curr].indexSize != header.root[curr].shadowIndexSize)
                    {
                        throw new StorageError(corruptionError);
                    }
                    bitmapExtentBase = (header.databaseFormatVersion < 2) ? 0 : dbBitmapPages;

                    if (isDirty())
                    {
                        if (listener != null)
                        {
                            listener.DatabaseCorrupted();
                        }
#if !WINRT_NET_FRAMEWORK
                        System.Console.WriteLine("Database was not normally closed: start recovery");
#endif
                        header.root[1 - curr].size = header.root[curr].size;
                        header.root[1 - curr].indexUsed = header.root[curr].indexUsed;
                        header.root[1 - curr].freeList = header.root[curr].freeList;
                        header.root[1 - curr].index = header.root[curr].shadowIndex;
                        header.root[1 - curr].indexSize = header.root[curr].shadowIndexSize;
                        header.root[1 - curr].shadowIndex = header.root[curr].index;
                        header.root[1 - curr].shadowIndexSize = header.root[curr].indexSize;
                        header.root[1 - curr].bitmapEnd = header.root[curr].bitmapEnd;
                        header.root[1 - curr].rootObject = header.root[curr].rootObject;
                        header.root[1 - curr].classDescList = header.root[curr].classDescList;
                        header.root[1 - curr].bitmapExtent = header.root[curr].bitmapExtent;

                        modified = true;
                        pg = pool.putPage(0);
                        header.pack(pg.data);
                        pool.unfix(pg);

                        pool.copy(header.root[1 - curr].index,
                            header.root[curr].index,
                            (header.root[curr].indexUsed * 8L + Page.pageSize - 1) & ~(Page.pageSize - 1));
                        if (listener != null)
                        {
                            listener.RecoveryCompleted();
                        }
#if !WINRT_NET_FRAMEWORK
                        System.Console.WriteLine("Recovery completed");
#endif
                    }
                    currIndexSize = header.root[1 - curr].indexUsed;
                    committedIndexSize = currIndexSize;
                    usedSize = header.root[curr].size;
                }
                int nBitmapPages = header.root[1 - currIndex].bitmapExtent == 0 ? dbBitmapPages : dbLargeBitmapPages;
                bitmapPageAvailableSpace = new int[nBitmapPages];
                for (i = 0; i < bitmapPageAvailableSpace.Length; i++)
                {
                    bitmapPageAvailableSpace[i] = int.MaxValue;
                }
                currRBitmapPage = currPBitmapPage = 0;
                currRBitmapOffs = currPBitmapOffs = 0;

                opened = true;
                reloadScheme();

                if (multiclientSupport)
                {
                    EndThreadTransaction();
                }
                else
                {
                    Commit(); // commit scheme changes
                }
            }
        }

        public bool IsOpened()
        {
            return opened;
        }

        internal static void checkIfFinal(ClassDescriptor desc)
        {
            System.Type cls = desc.cls;
            for (ClassDescriptor next = desc.next; next != null; next = next.next)
            {
                next.Load();
#if WINRT_NET_FRAMEWORK
                if (cls.GetTypeInfo().IsAssignableFrom(next.cls.GetTypeInfo()))
                {
                    desc.hasSubclasses = true;
                }
                else if (next.cls.GetTypeInfo().IsAssignableFrom(cls.GetTypeInfo()))
                {
                    next.hasSubclasses = true;
                }
#else
                if (cls.IsAssignableFrom(next.cls))
                {
                    desc.hasSubclasses = true;
                }
                else if (next.cls.IsAssignableFrom(cls))
                {
                    next.hasSubclasses = true;
                }

#endif
            }
        }


        internal void reloadScheme()
        {
            classDescMap.Clear();
            customAllocatorMap = null;
            customAllocatorList = null;
            defaultAllocator = new DefaultAllocator(this);
            int descListOid = header.root[1 - currIndex].classDescList;
            classDescMap[typeof(ClassDescriptor)] = new ClassDescriptor(this, typeof(ClassDescriptor));
            classDescMap[typeof(ClassDescriptor.FieldDescriptor)] = new ClassDescriptor(this, typeof(ClassDescriptor.FieldDescriptor));
            if (descListOid != 0)
            {
                ClassDescriptor desc;
                descList = findClassDescriptor(descListOid);
                for (desc = descList; desc != null; desc = desc.next)
                {
                    desc.Load();
                }
                for (desc = descList; desc != null; desc = desc.next)
                {
                    if (findClassDescriptor(desc.cls) == desc)
                    {
                        desc.resolve();
                    }
                    if (desc.allocator != null)
                    {
                        if (customAllocatorMap == null)
                        {
                            customAllocatorMap = new Hashtable();
                            customAllocatorList = new ArrayList();
                        }
                        CustomAllocator allocator = desc.allocator;
                        allocator.Load();
                        customAllocatorMap[desc.cls] = allocator;
                        customAllocatorList.Add(allocator);
                        reserveLocation(allocator.SegmentBase, allocator.SegmentSize);
                        reservedChainLength = 0;
                    }
                    checkIfFinal(desc);
                }
            }
            else
            {
                descList = null;
            }
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
            if (runtimeCodeGeneration == RuntimeCodeGeneration.Asynchronous)
            {
                codeGenerationThread = new Thread(new ThreadStart(generateSerializers));
                codeGenerationThread.Priority = ThreadPriority.BelowNormal;
                codeGenerationThread.IsBackground = true;
                codeGenerationThread.Start();
            }
            else if (runtimeCodeGeneration == RuntimeCodeGeneration.Synchronous)
            {
                generateSerializers();
            }
#endif
        }


        internal void generateSerializers()
        {
            for (ClassDescriptor desc = descList; desc != null; desc = desc.next)
            {
                desc.generateSerializer();
            }
        }

        internal void registerClassDescriptor(ClassDescriptor desc)
        {
            classDescMap[desc.cls] = desc;
            desc.next = descList;
            descList = desc;
            checkIfFinal(desc);
            storeObject0(desc, false);
            header.root[1 - currIndex].classDescList = desc.Oid;
            modified = true;
        }


        internal ClassDescriptor findClassDescriptor(Type cls)
        {
            return (ClassDescriptor)classDescMap[cls];
        }

        internal ClassDescriptor getClassDescriptor(Type cls)
        {
            ClassDescriptor desc = findClassDescriptor(cls);
            if (desc == null)
            {
                desc = new ClassDescriptor(this, cls);
                desc.generateSerializer();
                registerClassDescriptor(desc);
            }
            return desc;
        }


        public void Commit()
        {
            if (useSerializableTransactions && TransactionContext.nested != 0)
            {
                // Commit should not be used in serializable transaction mode
                throw new StorageError(StorageError.ErrorCode.INVALID_OPERATION, "commit");
            }
            lock (backgroundGcMonitor)
            {
                lock (this)
                {
                    if (!opened)
                    {
                        throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                    }
                    if (!modified)
                    {
                        return;
                    }
                    objectCache.flush();

                    if (customAllocatorList != null)
                    {
                        foreach (CustomAllocator alloc in customAllocatorList)
                        {
                            if (alloc.IsModified())
                            {
                                alloc.Store();
                            }
                            alloc.Commit();
                        }
                    }
                    commit0();
                    modified = false;
                }
            }
        }

        private void commit0()
        {
            int curr = currIndex;
            int i, j, n;
            int[] map = dirtyPagesMap;
            int oldIndexSize = header.root[curr].indexSize;
            int newIndexSize = header.root[1 - curr].indexSize;
            int nPages = committedIndexSize >> dbHandlesPerPageBits;
            Page pg;
            if (newIndexSize > oldIndexSize)
            {
                cloneBitmap(header.root[curr].index, oldIndexSize * 8L);
                long newIndex;
                while (true)
                {
                    newIndex = allocate(newIndexSize * 8L, 0);
                    if (newIndexSize == header.root[1 - curr].indexSize)
                    {
                        break;
                    }
                    free(newIndex, newIndexSize * 8L);
                    newIndexSize = header.root[1 - curr].indexSize;
                }
                header.root[1 - curr].shadowIndex = newIndex;
                header.root[1 - curr].shadowIndexSize = newIndexSize;
                free(header.root[curr].index, oldIndexSize * 8L);
            }
            long currSize = header.root[1 - curr].size;
            for (i = 0; i < nPages; i++)
            {
                if ((map[i >> 5] & (1 << (i & 31))) != 0)
                {
                    Page srcIndex = pool.getPage(header.root[1 - curr].index + (long)i * Page.pageSize);
                    Page dstIndex = pool.getPage(header.root[curr].index + (long)i * Page.pageSize);
                    for (j = 0; j < Page.pageSize; j += 8)
                    {
                        long pos = Bytes.unpack8(dstIndex.data, j);
                        if (Bytes.unpack8(srcIndex.data, j) != pos && pos < currSize)
                        {
                            if ((pos & dbFreeHandleFlag) == 0)
                            {
                                if ((pos & dbPageObjectFlag) != 0)
                                {
                                    free(pos & ~dbFlagsMask, Page.pageSize);
                                }
                                else if (pos != 0)
                                {
                                    int offs = (int)pos & (Page.pageSize - 1);
                                    pg = pool.getPage(pos - offs);
                                    free(pos, ObjectHeader.getSize(pg.data, offs));
                                    pool.unfix(pg);
                                }
                            }
                        }
                    }
                    pool.unfix(srcIndex);
                    pool.unfix(dstIndex);
                }
            }
            n = committedIndexSize & (dbHandlesPerPage - 1);
            if (n != 0 && (map[i >> 5] & (1 << (i & 31))) != 0)
            {
                Page srcIndex = pool.getPage(header.root[1 - curr].index + (long)i * Page.pageSize);
                Page dstIndex = pool.getPage(header.root[curr].index + (long)i * Page.pageSize);
                j = 0;
                do
                {
                    long pos = Bytes.unpack8(dstIndex.data, j);
                    if (Bytes.unpack8(srcIndex.data, j) != pos && pos < currSize)
                    {
                        if ((pos & dbFreeHandleFlag) == 0)
                        {
                            if ((pos & dbPageObjectFlag) != 0)
                            {
                                free(pos & ~dbFlagsMask, Page.pageSize);
                            }
                            else if (pos != 0)
                            {
                                int offs = (int)pos & (Page.pageSize - 1);
                                pg = pool.getPage(pos - offs);
                                free(pos, ObjectHeader.getSize(pg.data, offs));
                                pool.unfix(pg);
                            }
                        }
                    }
                    j += 8;
                }
                while (--n != 0);

                pool.unfix(srcIndex);
                pool.unfix(dstIndex);
            }
            for (i = 0; i <= nPages; i++)
            {
                if ((map[i >> 5] & (1 << (i & 31))) != 0)
                {
                    pg = pool.putPage(header.root[1 - curr].index + (long)i * Page.pageSize);
                    for (j = 0; j < Page.pageSize; j += 8)
                    {
                        Bytes.pack8(pg.data, j, Bytes.unpack8(pg.data, j) & ~dbModifiedFlag);
                    }
                    pool.unfix(pg);
                }
            }
            if (currIndexSize > committedIndexSize)
            {
                long page = (header.root[1 - curr].index + committedIndexSize * 8L) & ~(Page.pageSize - 1);
                long end = (header.root[1 - curr].index + Page.pageSize - 1 + currIndexSize * 8L) & ~(Page.pageSize - 1);
                while (page < end)
                {
                    pg = pool.putPage(page);
                    for (j = 0; j < Page.pageSize; j += 8)
                    {
                        Bytes.pack8(pg.data, j, Bytes.unpack8(pg.data, j) & ~dbModifiedFlag);
                    }
                    pool.unfix(pg);
                    page += Page.pageSize;
                }
            }
            header.root[1 - curr].usedSize = usedSize;
            pg = pool.putPage(0);
            header.pack(pg.data);
            pool.flush();
            pool.modify(pg);
            Debug.Assert(header.transactionId == transactionId);
            header.transactionId = ++transactionId;
            header.curr = curr ^= 1;
            header.dirty = true;
            header.pack(pg.data);
            pool.unfix(pg);
            pool.flush();

            header.root[1 - curr].size = header.root[curr].size;
            header.root[1 - curr].indexUsed = currIndexSize;
            header.root[1 - curr].freeList = header.root[curr].freeList;
            header.root[1 - curr].bitmapEnd = header.root[curr].bitmapEnd;
            header.root[1 - curr].rootObject = header.root[curr].rootObject;
            header.root[1 - curr].classDescList = header.root[curr].classDescList;
            header.root[1 - curr].bitmapExtent = header.root[curr].bitmapExtent;

            if (currIndexSize == 0 || newIndexSize != oldIndexSize)
            {
                if (currIndexSize == 0)
                {
                    currIndexSize = header.root[1 - curr].indexUsed;
                }
                header.root[1 - curr].index = header.root[curr].shadowIndex;
                header.root[1 - curr].indexSize = header.root[curr].shadowIndexSize;
                header.root[1 - curr].shadowIndex = header.root[curr].index;
                header.root[1 - curr].shadowIndexSize = header.root[curr].indexSize;
                pool.copy(header.root[1 - curr].index, header.root[curr].index, currIndexSize * 8L);
                i = (currIndexSize + dbHandlesPerPage * 32 - 1) >> (dbHandlesPerPageBits + 5);
                while (--i >= 0)
                {
                    map[i] = 0;
                }
            }
            else
            {
                for (i = 0; i < nPages; i++)
                {
                    if ((map[i >> 5] & (1 << (i & 31))) != 0)
                    {
                        map[i >> 5] -= (1 << (i & 31));
                        pool.copy(header.root[1 - curr].index + (long)i * Page.pageSize,
                            header.root[curr].index + (long)i * Page.pageSize,
                            Page.pageSize);
                    }
                }
                if (currIndexSize > i * dbHandlesPerPage && ((map[i >> 5] & (1 << (i & 31))) != 0 || currIndexSize != committedIndexSize))
                {
                    pool.copy(header.root[1 - curr].index + (long)i * Page.pageSize,
                        header.root[curr].index + (long)i * Page.pageSize,
                        8L * currIndexSize - (long)i * Page.pageSize);
                    j = i >> 5;
                    n = (currIndexSize + dbHandlesPerPage * 32 - 1) >> (dbHandlesPerPageBits + 5);
                    while (j < n)
                    {
                        map[j++] = 0;
                    }
                }
            }
            gcDone = false;
            currIndex = curr;
            committedIndexSize = currIndexSize;

            if (multiclientSupport)
            {
                pool.flush();
                pg = pool.putPage(0);
                header.dirty = false;
                header.pack(pg.data);
                pool.unfix(pg);
                pool.flush();
            }
            if (listener != null)
            {
                listener.OnTransactionCommit();
            }
        }


        public void Rollback()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                if (useSerializableTransactions && TransactionContext.nested != 0)
                {
                    // Rollback should not be used in serializable transaction mode
                    throw new StorageError(StorageError.ErrorCode.INVALID_OPERATION, "rollback");
                }
                objectCache.invalidate();
                lock (objectCache)
                {
                    if (!modified)
                    {
                        return;
                    }
                    rollback0();
                    modified = false;
                    if (reloadObjectsOnRollback)
                    {
                        objectCache.reload();
                    }
                    else
                    {
                        objectCache.clear();
                    }
                }
            }
        }

        private void rollback0()
        {
            int curr = currIndex;
            int[] map = dirtyPagesMap;
            if (header.root[1 - curr].index != header.root[curr].shadowIndex)
            {
                pool.copy(header.root[curr].shadowIndex, header.root[curr].index, 8L * committedIndexSize);
            }
            else
            {
                int nPages = (committedIndexSize + dbHandlesPerPage - 1) >> dbHandlesPerPageBits;
                for (int i = 0; i < nPages; i++)
                {
                    if ((map[i >> 5] & (1 << (i & 31))) != 0)
                    {
                        pool.copy(header.root[curr].shadowIndex + (long)i * Page.pageSize,
                            header.root[curr].index + (long)i * Page.pageSize,
                            Page.pageSize);
                    }
                }
            }
            for (int j = (currIndexSize + dbHandlesPerPage * 32 - 1) >> (dbHandlesPerPageBits + 5); --j >= 0; map[j] = 0)
                ;
            header.root[1 - curr].index = header.root[curr].shadowIndex;
            header.root[1 - curr].indexSize = header.root[curr].shadowIndexSize;
            header.root[1 - curr].indexUsed = committedIndexSize;
            header.root[1 - curr].freeList = header.root[curr].freeList;
            header.root[1 - curr].bitmapEnd = header.root[curr].bitmapEnd;
            header.root[1 - curr].size = header.root[curr].size;
            header.root[1 - curr].rootObject = header.root[curr].rootObject;
            header.root[1 - curr].classDescList = header.root[curr].classDescList;
            header.root[1 - curr].bitmapExtent = header.root[curr].bitmapExtent;
            header.dirty = true;
            usedSize = header.root[curr].size;
            currIndexSize = committedIndexSize;

            currRBitmapPage = currPBitmapPage = 0;
            currRBitmapOffs = currPBitmapOffs = 0;

            reloadScheme();
            if (listener != null)
            {
                listener.OnTransactionRollback();
            }
        }


        private void memset(byte[] arr, int off, int len, byte val)
        {
            while (--len >= 0)
            {
                arr[off++] = val;
            }
        }

#if COMPACT_NET_FRAMEWORK || WP7 || WINRT_NET_FRAMEWORK
        class PositionComparer : System.Collections.IComparer
        {
            public int Compare(object o1, object o2)
            {
                long i1 = (long)o1;
                long i2 = (long)o2;
                return i1 < i2 ? -1 : i1 == i2 ? 0 : 1;
            }
        }
#else
        public object CreateClass(Type type)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                lock (objectCache)
                {
                    Type wrapper = getWrapper(type);
                    object obj = wrapper.Assembly.CreateInstance(wrapper.Name);
                    int oid = allocateId();
                    AssignOid(obj, oid, false);
                    setPos(oid, 0);
                    objectCache.put(oid, obj);
                    Modify(obj);
                    return obj;
                }
            }
        }

        internal Type getWrapper(Type original)
        {
            Type wrapper = (Type)wrapperHash[original];
            if (wrapper == null)
            {
                wrapper = CILGenerator.Instance.CreateWrapper(original);
                wrapperHash[original] = wrapper;
            }
            return wrapper;
        }
#endif
        public int MakePersistent(object obj)
        {
            if (obj == null)
            {
                return 0;
            }
            int oid = GetOid(obj);
            if (oid != 0)
            {
                return oid;
            }
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                lock (objectCache)
                {
                    oid = allocateId();
                    AssignOid(obj, oid, false);
                    setPos(oid, 0);
                    objectCache.put(oid, obj);
                    Modify(obj);
                    return oid;
                }
            }
        }

        public void Backup(string filePath, string cipherKey)
        {
            Backup(new IFileStream((cipherKey != null)
                ? (IFile)new Rc4File(filePath, fileParameters, cipherKey)
                : (IFile)new OSFile(filePath, fileParameters)));
        }

        public void Backup(System.IO.Stream stream)
        {
            // lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                lock (this)
                {
                    objectCache.flush();
                }
                int curr = 1 - currIndex;
                int nObjects = header.root[curr].indexUsed;
                long indexOffs = header.root[curr].index;
                int i, j, k;
                int nUsedIndexPages = (nObjects + dbHandlesPerPage - 1) / dbHandlesPerPage;
                int nIndexPages = (int)((header.root[curr].indexSize + dbHandlesPerPage - 1) / dbHandlesPerPage);
                long totalRecordsSize = 0;
                long nPagedObjects = 0;
                int bitmapExtent = header.root[curr].bitmapExtent;
                long[] index = new long[nObjects];
                int[] oids = new int[nObjects];

                if (bitmapExtent == 0)
                {
                    bitmapExtent = int.MaxValue;
                }
                for (i = 0, j = 0; i < nUsedIndexPages; i++)
                {
                    Page pg = pool.getPage(indexOffs + (long)i * Page.pageSize);
                    for (k = 0; k < dbHandlesPerPage && j < nObjects; k++, j++)
                    {
                        long pos = Bytes.unpack8(pg.data, k * 8);
                        index[j] = pos;
                        oids[j] = j;
                        if ((pos & dbFreeHandleFlag) == 0)
                        {
                            if ((pos & dbPageObjectFlag) != 0)
                            {
                                nPagedObjects += 1;
                            }
                            else if (pos != 0)
                            {
                                int offs = (int)pos & (Page.pageSize - 1);
                                Page op = pool.getPage(pos - offs);
                                int size = ObjectHeader.getSize(op.data, offs & ~dbFlagsMask);
                                size = (size + dbAllocationQuantum - 1) & ~(dbAllocationQuantum - 1);
                                totalRecordsSize += size;
                                pool.unfix(op);
                            }
                        }
                    }
                    pool.unfix(pg);

                }
                Header newHeader = new Header();
                newHeader.curr = 0;
                newHeader.dirty = false;
                newHeader.databaseFormatVersion = header.databaseFormatVersion;
                long newFileSize = (long)(nPagedObjects + nIndexPages * 2 + 1) * Page.pageSize + totalRecordsSize;
                newFileSize = (newFileSize + Page.pageSize - 1) & ~(Page.pageSize - 1);
                newHeader.root = new RootPage[2];
                newHeader.root[0] = new RootPage();
                newHeader.root[1] = new RootPage();
                newHeader.root[0].size = newHeader.root[1].size = newFileSize;
                newHeader.root[0].index = newHeader.root[1].shadowIndex = Page.pageSize;
                newHeader.root[0].shadowIndex = newHeader.root[1].index = Page.pageSize + (long)nIndexPages * Page.pageSize;
                newHeader.root[0].shadowIndexSize = newHeader.root[0].indexSize =
                    newHeader.root[1].shadowIndexSize = newHeader.root[1].indexSize = nIndexPages * dbHandlesPerPage;
                newHeader.root[0].indexUsed = newHeader.root[1].indexUsed = nObjects;
                newHeader.root[0].freeList = newHeader.root[1].freeList = header.root[curr].freeList;
                newHeader.root[0].bitmapEnd = newHeader.root[1].bitmapEnd = header.root[curr].bitmapEnd;

                newHeader.root[0].rootObject = newHeader.root[1].rootObject = header.root[curr].rootObject;
                newHeader.root[0].classDescList = newHeader.root[1].classDescList = header.root[curr].classDescList;
                newHeader.root[0].bitmapExtent = newHeader.root[1].bitmapExtent = bitmapExtent;

                byte[] page = new byte[Page.pageSize];
                newHeader.pack(page);
                stream.Write(page, 0, Page.pageSize);

                long pageOffs = (long)(nIndexPages * 2 + 1) * Page.pageSize;
                long recOffs = (long)(nPagedObjects + nIndexPages * 2 + 1) * Page.pageSize;
#if COMPACT_NET_FRAMEWORK
                Array.Sort(index, oids, 0, nObjects, new PositionComparer());
#else
#if WINRT_NET_FRAMEWORK
                SortedDictionary<long,int> dict = new SortedDictionary<long,int>();
                for (i = 0; i < nObjects; i++) {
                    dict.Add(index[i], oids[i]);
                }
                dict.Keys.CopyTo(index, 0);
                dict.Values.CopyTo(oids, 0);
#else
                Array.Sort(index, oids);
#endif
#endif
                byte[] newIndex = new byte[nIndexPages * dbHandlesPerPage * 8];
                for (i = 0; i < nObjects; i++)
                {
                    long pos = index[i];
                    int oid = oids[i];
                    if (pos != 0 && (pos & dbFreeHandleFlag) == 0)
                    {
                        if ((pos & dbPageObjectFlag) != 0)
                        {
                            Bytes.pack8(newIndex, oid * 8, pageOffs | dbPageObjectFlag);
                            pageOffs += Page.pageSize;
                        }
                        else
                        {
                            Bytes.pack8(newIndex, oid * 8, recOffs);
                            int offs = (int)pos & (Page.pageSize - 1);
                            Page op = pool.getPage(pos - offs);
                            int size = ObjectHeader.getSize(op.data, offs & ~dbFlagsMask);
                            size = (size + dbAllocationQuantum - 1) & ~(dbAllocationQuantum - 1);
                            recOffs += size;
                            pool.unfix(op);
                        }
                    }
                    else
                    {
                        Bytes.pack8(newIndex, oid * 8, pos);
                    }
                }
                stream.Write(newIndex, 0, newIndex.Length);
                stream.Write(newIndex, 0, newIndex.Length);

                for (i = 0; i < nObjects; i++)
                {
                    long pos = index[i];
                    if (((int)pos & (dbFreeHandleFlag | dbPageObjectFlag)) == dbPageObjectFlag)
                    {
                        if (oids[i] < dbBitmapId + dbBitmapPages
                            || (oids[i] >= bitmapExtent && oids[i] < bitmapExtent + dbLargeBitmapPages - dbBitmapPages))
                        {
                            int pageId = oids[i] < dbBitmapId + dbBitmapPages
                                ? oids[i] - dbBitmapId : oids[i] - bitmapExtent + bitmapExtentBase;
                            long mappedSpace = (long)pageId * Page.pageSize * 8 * dbAllocationQuantum;
                            if (mappedSpace >= newFileSize)
                            {
                                memset(page, 0, Page.pageSize, (byte)0);
                            }
                            else if (mappedSpace + Page.pageSize * 8 * dbAllocationQuantum <= newFileSize)
                            {
                                memset(page, 0, Page.pageSize, (byte)0xFF);
                            }
                            else
                            {
                                int nBits = (int)((newFileSize - mappedSpace) >> dbAllocationQuantumBits);
                                memset(page, 0, nBits >> 3, (byte)0xFF);
                                page[nBits >> 3] = (byte)((1 << (nBits & 7)) - 1);
                                memset(page, (nBits >> 3) + 1, Page.pageSize - (nBits >> 3) - 1, (byte)0);
                            }
                            stream.Write(page, 0, Page.pageSize);
                        }
                        else
                        {
                            Page pg = pool.getPage(pos & ~dbFlagsMask);
                            stream.Write(pg.data, 0, Page.pageSize);
                            pool.unfix(pg);
                        }
                    }
                }
                for (i = 0; i < nObjects; i++)
                {
                    long pos = index[i];
                    if (pos != 0 && ((int)pos & (dbFreeHandleFlag | dbPageObjectFlag)) == 0)
                    {
                        pos &= ~dbFlagsMask;
                        int offs = (int)pos & (Page.pageSize - 1);
                        Page pg = pool.getPage(pos - offs);
                        int size = ObjectHeader.getSize(pg.data, offs);
                        size = (size + dbAllocationQuantum - 1) & ~(dbAllocationQuantum - 1);

                        while (true)
                        {
                            if (Page.pageSize - offs >= size)
                            {
                                stream.Write(pg.data, offs, size);
                                break;
                            }
                            stream.Write(pg.data, offs, Page.pageSize - offs);
                            size -= Page.pageSize - offs;
                            pos += Page.pageSize - offs;
                            offs = 0;
                            pool.unfix(pg);
                            pg = pool.getPage(pos);
                        }
                        pool.unfix(pg);
                    }
                }
                if (recOffs != newFileSize)
                {
                    Debug.Assert(newFileSize - recOffs < Page.pageSize);
                    int align = (int)(newFileSize - recOffs);
                    memset(page, 0, align, (byte)0);
                    stream.Write(page, 0, align);
                }
            }
        }

        public Bitmap CreateBitmap(IEnumerator e)
        {
            return new Bitmap(this, e);
        }

#if USE_GENERICS
        public Query<T> CreateQuery<T>()
        {
            return new QueryImpl<T>(this);
        }

        public Index<K,V> CreateIndex<K,V>(bool unique) where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Index<K,V> index = alternativeBtree
                    ? (Index<K,V>)new AltBtree<K,V>(unique)
                    : (Index<K,V>)new Btree<K,V>(unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public CompoundIndex<V> CreateIndex<V>(Type[] types, bool unique) where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
#if COMPACT_NET_FRAMEWORK
                if (alternativeBtree)
                {
                    throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE);
                }
                CompoundIndex<V> index = new BtreeCompoundIndex<V>(types,unique);
#else
                CompoundIndex<V> index = alternativeBtree
                    ? (CompoundIndex<V>)new AltBtreeCompoundIndex<V>(types, unique)
                    : (CompoundIndex<V>)new BtreeCompoundIndex<V>(types,unique);
#endif
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public MultidimensionalIndex<V> CreateMultidimensionalIndex<V>(MultidimensionalComparator<V> comparator) where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new KDTree<V>(this, comparator);
            }
        }

        public MultidimensionalIndex<V> CreateMultidimensionalIndex<V>(string[] fieldNames, bool treateZeroAsUndefinedValue) where V : class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new KDTree<V>(this, fieldNames, treateZeroAsUndefinedValue);
            }
        }

        public Index<K,V> CreateThickIndex<K,V>() where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new ThickIndex<K,V>(this);
            }
        }

        public BitIndex<T> CreateBitIndex<T>() where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                BitIndex<T> index = new BitIndexImpl<T>();
                index.AssignOid(this, 0, false);
                return index;
            }
        }


        public SpatialIndex<T> CreateSpatialIndex<T>() where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Rtree<T> index = new Rtree<T>();
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public SpatialIndexR2<T> CreateSpatialIndexR2<T>() where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                RtreeR2<T> index = new RtreeR2<T>();
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public SpatialIndexRn<T> CreateSpatialIndexRn<T>() where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                RtreeRn<T> index = new RtreeRn<T>();
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public SortedCollection<K,V> CreateSortedCollection<K,V>(PersistentComparator<K,V> comparator, bool unique) where V:class
        {
            if (!opened)
            {
                throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
            }
            return new Ttree<K,V>(this, comparator, unique);
        }

        public SortedCollection<K,V> CreateSortedCollection<K,V>(bool unique) where V:class,IComparable<K>,IComparable<V>
        {
            if (!opened)
            {
                throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
            }
            return new Ttree<K,V>(this, new DefaultPersistentComparator<K,V>(), unique);
        }

        public Perst.ISet<T> CreateSet<T>() where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Perst.ISet<T> s = alternativeBtree
                    ? (Perst.ISet<T>)new AltPersistentSet<T>(true)
                    : (Perst.ISet<T>)new PersistentSet<T>(true);
                s.AssignOid(this, 0, false);
                return s;
            }
        }

        public Perst.ISet<T> CreateBag<T>() where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Perst.ISet<T> s = alternativeBtree
                    ? (Perst.ISet<T>)new AltPersistentSet<T>(false)
                    : (Perst.ISet<T>)new PersistentSet<T>(false);
                s.AssignOid(this, 0, false);
                return s;
            }
        }

        public Perst.ISet<T> CreateScalableSet<T>() where T:class
        {
            return CreateScalableSet<T>(8);
        }

        public Perst.ISet<T> CreateScalableSet<T>(int initialSize) where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new ScalableSet<T>(this, initialSize);
            }
        }

        public IPersistentList<T> CreateList<T>() where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new PersistentListImpl<T>(this);
            }
        }

        public IPersistentList<T> CreateScalableList<T>() where T:class
        {
            return CreateScalableList<T>(8);
        }

        public IPersistentList<T> CreateScalableList<T>(int initialSize) where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new ScalableList<T>(this, initialSize);
            }
        }

        public IPersistentMap<K,V> CreateHash<K,V>() where V:class
        {
            return CreateHash<K,V>(101, 2);
        }

        public IPersistentMap<K,V> CreateHash<K,V>(int pageSize, int loadFactor) where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new PersistentHashImpl<K,V>(this, pageSize, loadFactor);
            }
        }

        public IPersistentMap<K,V> CreateMap<K,V>() where K:IComparable where V:class
        {
            return CreateMap<K,V>(4);
        }

        public IPersistentMap<K,V> CreateMap<K,V>(int initialSize) where K:IComparable where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new PersistentMapImpl<K,V>(this, initialSize);
            }
        }

        public FieldIndex<K,V> CreateFieldIndex<K,V>(String fieldName, bool unique) where V:class
        {
            return CreateFieldIndex<K,V>(fieldName, unique, false, false);
        }

        public FieldIndex<K,V> CreateFieldIndex<K,V>(String fieldName, bool unique, bool caseInsensitive) where V:class
        {
            return CreateFieldIndex<K,V>(fieldName, unique, caseInsensitive, false);
        }

        public FieldIndex<K,V> CreateFieldIndex<K,V>(String fieldName, bool unique, bool caseInsensitive, bool thick) where V:class
        {

            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                FieldIndex<K,V> index = thick
                    ? caseInsensitive
                        ? (FieldIndex<K,V>)new ThickCaseInsensitiveFieldIndex<K,V>(this, fieldName)
                        : (FieldIndex<K,V>)new ThickFieldIndex<K,V>(this, fieldName)
                    : caseInsensitive
                        ? alternativeBtree
                            ? (FieldIndex<K,V>)new AltBtreeCaseInsensitiveFieldIndex<K,V>(fieldName, unique)
                            : (FieldIndex<K,V>)new BtreeCaseInsensitiveFieldIndex<K,V>(fieldName, unique)
                        : alternativeBtree
                            ? (FieldIndex<K,V>)new AltBtreeFieldIndex<K,V>(fieldName, unique)
                            : (FieldIndex<K,V>)new BtreeFieldIndex<K,V>(fieldName, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public MultiFieldIndex<T> CreateFieldIndex<T>(string[] fieldNames, bool unique) where T:class
        {
            return CreateFieldIndex<T>(fieldNames, unique, false);
        }

        public MultiFieldIndex<T> CreateFieldIndex<T>(string[] fieldNames, bool unique, bool caseInsensitive) where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
#if COMPACT_NET_FRAMEWORK
                if (alternativeBtree)
                {
                    throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE);
                }
                MultiFieldIndex<T> index = caseInsensitive
                    ? new BtreeCaseInsensitiveMultiFieldIndex<T>(fieldNames, unique);
                    : new BtreeMultiFieldIndex<T>(fieldNames, unique);
#else
                MultiFieldIndex<T> index = caseInsensitive
                    ? alternativeBtree
                        ? (MultiFieldIndex<T>)new AltBtreeCaseInsensitiveMultiFieldIndex<T>(fieldNames, unique)
                        : (MultiFieldIndex<T>)new BtreeCaseInsensitiveMultiFieldIndex<T>(fieldNames, unique)
                    : alternativeBtree
                        ? (MultiFieldIndex<T>)new AltBtreeMultiFieldIndex<T>(fieldNames, unique)
                        : (MultiFieldIndex<T>)new BtreeMultiFieldIndex<T>(fieldNames, unique);
#endif
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public RegexIndex<T> CreateRegexIndex<T>(string fieldName, bool caseInsensitive, int nGrams) where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new RegexIndexImpl<T>(this, fieldName, caseInsensitive, nGrams);
            }
        }

        public RegexIndex<T> CreateRegexIndex<T>(string fieldName) where T:class
        {
            return CreateRegexIndex<T>(fieldName, true, 3);
        }

        public Index<K,V> CreateRandomAccessIndex<K,V>(bool unique) where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Index<K,V> index = new RndBtree<K,V>(unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

#if !COMPACT_NET_FRAMEWORK
        public CompoundIndex<V> CreateRandomAccessIndex<V>(Type[] types, bool unique) where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                CompoundIndex<V> index = new RndBtreeCompoundIndex<V>(types, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }
#endif

        public FieldIndex<K,V> CreateRandomAccessFieldIndex<K,V>(String fieldName, bool unique) where V:class
        {
            return CreateRandomAccessFieldIndex<K,V>(fieldName, unique, false);
        }

        public FieldIndex<K,V> CreateRandomAccessFieldIndex<K,V>(String fieldName, bool unique, bool caseInsensitive) where V:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                FieldIndex<K,V> index = caseInsensitive
                   ? (FieldIndex<K,V>)new RndBtreeCaseInsensitiveFieldIndex<K,V>(fieldName, unique)
                   : (FieldIndex<K,V>)new RndBtreeFieldIndex<K,V>(fieldName, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

#if !COMPACT_NET_FRAMEWORK
        public MultiFieldIndex<T> CreateRandomAccessFieldIndex<T>(string[] fieldNames, bool unique) where T:class
        {
            return CreateRandomAccessFieldIndex<T>(fieldNames, unique, false);
        }

        public MultiFieldIndex<T> CreateRandomAccessFieldIndex<T>(string[] fieldNames, bool unique, bool caseInsensitive) where T:class
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                MultiFieldIndex<T> index = caseInsensitive
                    ? (MultiFieldIndex<T>)new RndBtreeCaseInsensitiveMultiFieldIndex<T>(fieldNames, unique)
                    : (MultiFieldIndex<T>)new RndBtreeMultiFieldIndex<T>(fieldNames, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }
#endif

        public Link<T> CreateLink<T>() where T:class
        {
            return CreateLink<T>(8);
        }

        public Link<T> CreateLink<T>(int initialSize) where T:class
        {
            return new LinkImpl<T>(this, initialSize);
        }

        internal Link<T> ConstructLink<T>(object[] arr, object owner) where T:class
        {
            return new LinkImpl<T>(this, arr, owner);
        }

        public PArray<T> CreateArray<T>() where T:class
        {
            return CreateArray<T>(8);
        }

        public PArray<T> CreateArray<T>(int initialSize) where T:class
        {
            return new PArrayImpl<T>(this, initialSize);
        }

        internal PArray<T> ConstructArray<T>(int[] arr, object owner) where T:class
        {
            return new PArrayImpl<T>(this, arr, owner);
        }

        public Relation<M,O> CreateRelation<M,O>(O owner) where M:class where O:class
        {
            return new RelationImpl<M,O>(this, owner);
        }

        public TimeSeries<T> CreateTimeSeries<T>(int blockSize, long maxBlockTimeInterval) where T:TimeSeriesTick
        {
            return new TimeSeriesImpl<T>(this, blockSize, maxBlockTimeInterval);
        }

        public PatriciaTrie<T> CreatePatriciaTrie<T>() where T:class
        {
            return new PTrie<T>();
        }

        public Perst.ISet<object> CreateSet()
        {
             return CreateSet<object>();
        }

        public Perst.ISet<object> CreateBag()
        {
             return CreateBag<object>();
        }

        public Link<object> CreateLink()
        {
            return CreateLink<object>(8);
        }

        public Link<object> CreateLink(int initialSize)
        {
            return CreateLink<object>(initialSize);
        }

        public PArray<object> CreateArray()
        {
            return CreateArray<object>(8);
        }

        public PArray<object> CreateArray(int initialSize)
        {
            return CreateArray<object>(initialSize);
        }
#else
        public Query CreateQuery()
        {
            return new QueryImpl(this);
        }

        public Index CreateIndex(Type keyType, bool unique)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Index index = alternativeBtree
                    ? (Index)new AltBtree(keyType, unique)
                    : (Index)new Btree(keyType, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public CompoundIndex CreateIndex(Type[] keyTypes, bool unique)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
#if COMPACT_NET_FRAMEWORK
                if (alternativeBtree)
                {
                    throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE);
                }
                CompoundIndex index = new BtreeCompoundIndex(keyTypes, unique);
#else
                CompoundIndex index = alternativeBtree
                    ? (CompoundIndex)new AltBtreeCompoundIndex(keyTypes, unique)
                    : (CompoundIndex)new BtreeCompoundIndex(keyTypes, unique);
#endif
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public MultidimensionalIndex CreateMultidimensionalIndex(MultidimensionalComparator comparator)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new KDTree(this, comparator);
            }

        }

        public MultidimensionalIndex CreateMultidimensionalIndex(Type cls, string[] fieldNames, bool treateZeroAsUndefinedValue)
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new KDTree(this, cls, fieldNames, treateZeroAsUndefinedValue);
            }
        }


        public Index CreateThickIndex(Type keyType)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new ThickIndex(this, keyType);
            }
        }

        public BitIndex CreateBitIndex()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                BitIndex index = new BitIndexImpl();
                index.AssignOid(this, 0, false);
                return index;
            }
        }


        public SpatialIndex CreateSpatialIndex()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Rtree index = new Rtree();
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public SpatialIndexR2 CreateSpatialIndexR2()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                RtreeR2 index = new RtreeR2();
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public SpatialIndexRn CreateSpatialIndexRn()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                RtreeRn index = new RtreeRn();
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public SortedCollection CreateSortedCollection(PersistentComparator comparator, bool unique)
        {
            if (!opened)
            {
                throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
            }
            return new Ttree(this, comparator, unique);
        }

        public SortedCollection CreateSortedCollection(bool unique)
        {
            if (!opened)
            {
                throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
            }
            return new Ttree(this, new DefaultPersistentComparator(), unique);
        }

        public ISet CreateSet()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                ISet s = alternativeBtree
                    ? (ISet)new AltPersistentSet(true)
                    : (ISet)new PersistentSet(true);
                s.AssignOid(this, 0, false);
                return s;
            }
        }

        public ISet CreateBag()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                ISet s = alternativeBtree
                    ? (ISet)new AltPersistentSet(false)
                    : (ISet)new PersistentSet(false);
                s.AssignOid(this, 0, false);
                return s;
            }
        }

        public ISet CreateScalableSet()
        {
            return CreateScalableSet(8);
        }

        public ISet CreateScalableSet(int initialSize)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new ScalableSet(this, initialSize);
            }
        }

        public IPersistentMap CreateHash()
        {
            return CreateHash(101, 2);
        }

        public IPersistentMap CreateHash(int pageSize, int loadFactor)
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new PersistentHashImpl(this, pageSize, loadFactor);
            }
        }

        public IPersistentMap CreateMap(Type keyType)
        {
            return CreateMap(keyType, 4);
        }

        public IPersistentMap CreateMap(Type keyType, int initialSize)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new PersistentMapImpl(this, keyType, initialSize);
            }
        }

        public IPersistentList CreateList()
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new PersistentListImpl(this);
            }
        }

        public IPersistentList CreateScalableList()
        {
            return CreateScalableList(8);
        }

        public IPersistentList CreateScalableList(int initialSize)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new ScalableList(this, initialSize);
            }
        }

        public FieldIndex CreateFieldIndex(Type type, String fieldName, bool unique)
        {
            return CreateFieldIndex(type, fieldName, unique, false, false);
        }

        public FieldIndex CreateFieldIndex(Type type, String fieldName, bool unique, bool caseInsensitive)
        {
            return CreateFieldIndex(type, fieldName, unique, caseInsensitive, false);
        }

        public FieldIndex CreateFieldIndex(Type type, String fieldName, bool unique, bool caseInsensitive, bool thick)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                FieldIndex index = thick
                    ? caseInsensitive
                        ? (FieldIndex)new ThickCaseInsensitiveFieldIndex(this, type, fieldName)
                        : (FieldIndex)new ThickFieldIndex(this, type, fieldName)
                    : caseInsensitive
                        ? alternativeBtree
                            ? (FieldIndex)new AltBtreeCaseInsensitiveFieldIndex(type, fieldName, unique)
                            : (FieldIndex)new BtreeCaseInsensitiveFieldIndex(type, fieldName, unique)
                        : alternativeBtree
                            ? (FieldIndex)new AltBtreeFieldIndex(type, fieldName, unique)
                            : (FieldIndex)new BtreeFieldIndex(type, fieldName, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public MultiFieldIndex CreateFieldIndex(Type type, String[] fieldNames, bool unique)
        {
            return CreateFieldIndex(type, fieldNames, unique, false);
        }

        public MultiFieldIndex CreateFieldIndex(System.Type type, String[] fieldNames, bool unique, bool caseInsensitive)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
#if COMPACT_NET_FRAMEWORK
                if (alternativeBtree)
                {
                    throw new  StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE);
                }
                MultiFieldIndex index = caseInsensitive
                    ? (MultiFieldIndex)new BtreeCaseInsensitiveMultiFieldIndex(type, fieldNames, unique)
                    : (MultiFieldIndex)new BtreeMultiFieldIndex(type, fieldNames, unique);
#else
                MultiFieldIndex index = caseInsensitive
                    ? alternativeBtree
                        ? (MultiFieldIndex)new AltBtreeCaseInsensitiveMultiFieldIndex(type, fieldNames, unique)
                        : (MultiFieldIndex)new BtreeCaseInsensitiveMultiFieldIndex(type, fieldNames, unique)
                    : alternativeBtree
                        ? (MultiFieldIndex)new AltBtreeMultiFieldIndex(type, fieldNames, unique)
                        : (MultiFieldIndex)new BtreeMultiFieldIndex(type, fieldNames, unique);
#endif
                index.AssignOid(this, 0, false);
                return index;
            }
        }

        public RegexIndex CreateRegexIndex(Type type, string fieldName, bool caseInsensitive, int nGrams)
        {
            lock(this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                return new RegexIndexImpl(this, type, fieldName, caseInsensitive, nGrams);
            }
        }

        public RegexIndex CreateRegexIndex(Type type, string fieldName)
        {
            return CreateRegexIndex(type, fieldName, true, 3);
        }


        public Index CreateRandomAccessIndex(Type keyType, bool unique)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                Index index = new RndBtree(keyType, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

#if !COMPACT_NET_FRAMEWORK
        public CompoundIndex CreateRandomAccessIndex(Type[] keyTypes, bool unique)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                CompoundIndex index = new RndBtreeCompoundIndex(keyTypes, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }
#endif

        public FieldIndex CreateRandomAccessFieldIndex(Type type, String fieldName, bool unique)
        {
            return CreateRandomAccessFieldIndex(type, fieldName, unique, false);
        }

        public FieldIndex CreateRandomAccessFieldIndex(Type type, String fieldName, bool unique, bool caseInsensitive)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                FieldIndex index = caseInsensitive
                    ? (FieldIndex)new RndBtreeCaseInsensitiveFieldIndex(type, fieldName, unique)
                    : (FieldIndex)new RndBtreeFieldIndex(type, fieldName, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }

#if !COMPACT_NET_FRAMEWORK
        public MultiFieldIndex CreateRandomAccessFieldIndex(System.Type type, String[] fieldNames, bool unique)
        {
            return CreateRandomAccessFieldIndex(type, fieldNames, unique, false);
        }

        public MultiFieldIndex CreateRandomAccessFieldIndex(System.Type type, String[] fieldNames, bool unique, bool caseInsensitive)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                MultiFieldIndex index = caseInsensitive
                    ? (MultiFieldIndex)new RndBtreeCaseInsensitiveMultiFieldIndex(type, fieldNames, unique)
                    : (MultiFieldIndex)new RndBtreeMultiFieldIndex(type, fieldNames, unique);
                index.AssignOid(this, 0, false);
                return index;
            }
        }
#endif

        public Link CreateLink()
        {
            return CreateLink(8);
        }

        public Link CreateLink(int initialSize)
        {
            return new LinkImpl(this, initialSize);
        }

        public PArray CreateArray()
        {
            return CreateArray(8);
        }

        public PArray CreateArray(int initialSize)
        {
            return new PArrayImpl(this, initialSize);
        }

        public Relation CreateRelation(object owner)
        {
            return new RelationImpl(this, owner);
        }

        public TimeSeries CreateTimeSeries(Type blockClass, long maxBlockTimeInterval)
        {
            return new TimeSeriesImpl(this, blockClass, maxBlockTimeInterval);
        }

        public PatriciaTrie CreatePatriciaTrie()
        {
            return new PTrie();
        }
#endif
        public Blob CreateBlob()
        {
            return new BlobImpl(this, Page.pageSize - ObjectHeader.Sizeof - 16);
        }

        public FullTextIndex CreateFullTextIndex(FullTextSearchHelper helper)
        {
            return new FullTextIndexImpl(this, helper);
        }

        public FullTextIndex CreateFullTextIndex()
        {
            return CreateFullTextIndex(new FullTextSearchHelper(this));
        }

        public void ExportXML(System.IO.StreamWriter writer)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                int rootOid = header.root[1 - currIndex].rootObject;
                if (rootOid != 0)
                {
                    XMLExporter xmlExporter = new XMLExporter(this, writer);
                    xmlExporter.exportDatabase(rootOid);
                }
            }
        }

        public void ImportXML(System.IO.TextReader reader)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                XMLImporter xmlImporter = new XMLImporter(this, reader);
                xmlImporter.importDatabase();
            }
        }

        internal long getGCPos(int oid)
        {
            Page pg = pool.getPage(header.root[currIndex].index
                + ((long)(oid >> dbHandlesPerPageBits) << Page.pageSizeLog));
            long pos = Bytes.unpack8(pg.data, (oid & (dbHandlesPerPage - 1)) << 3);
            pool.unfix(pg);
            return pos;
        }

        internal void markOid(int oid)
        {
            if (oid != 0)
            {
                long pos = getGCPos(oid);
                if (pos == 0 || (pos & (dbFreeHandleFlag | dbPageObjectFlag)) != 0)
                {
                    throw new StorageError(StorageError.ErrorCode.INVALID_OID);
                }
                if (pos < header.root[currIndex].size)
                {
                    int bit = (int)((ulong)pos >> dbAllocationQuantumBits);
                    if ((blackBitmap[(uint)bit >> 5] & (1 << (bit & 31))) == 0)
                    {
                        greyBitmap[(uint)bit >> 5] |= 1 << (bit & 31);
                    }
                }
            }
        }

        internal Page getGCPage(int oid)
        {
            return pool.getPage(getGCPos(oid) & ~dbFlagsMask);
        }

        public void SetGcThreshold(long maxAllocatedDelta)
        {
            gcThreshold = maxAllocatedDelta;
        }

        public int Gc()
        {
            lock (this)
            {
                return gc0();
            }
        }

        internal Btree createBtreeStub(byte[] data, int offs)
        {
#if USE_GENERICS
            return new Btree<int,object>(data, ObjectHeader.Sizeof + offs);
#else
            return new Btree(data, ObjectHeader.Sizeof + offs);
#endif
        }


        private void mark()
        {
            // Console.WriteLine("Start GC, allocatedDelta=" + allocatedDelta + ", header[" + currIndex + "].size=" + header.root[currIndex].size + ", gcTreshold=" + gcThreshold);
            int bitmapSize = (int)((ulong)header.root[currIndex].size >> (dbAllocationQuantumBits + 5)) + 1;
            bool existsNotMarkedObjects;
            long pos;
            int i, j;

            if (listener != null)
            {
                listener.GcStarted();
            }

            greyBitmap = new int[bitmapSize];
            blackBitmap = new int[bitmapSize];
            int rootOid = header.root[currIndex].rootObject;
            if (rootOid != 0)
            {
                markOid(rootOid);
                do
                {
                    existsNotMarkedObjects = false;
                    for (i = 0; i < bitmapSize; i++)
                    {
                        if (greyBitmap[i] != 0)
                        {
                            existsNotMarkedObjects = true;
                            for (j = 0; j < 32; j++)
                            {
                                if ((greyBitmap[i] & (1 << j)) != 0)
                                {
                                    pos = (((long)i << 5) + j) << dbAllocationQuantumBits;
                                    greyBitmap[i] &= ~(1 << j);
                                    blackBitmap[i] |= 1 << j;
                                    int offs = (int)pos & (Page.pageSize - 1);
                                    Page pg = pool.getPage(pos - offs);
                                    int typeOid = ObjectHeader.getType(pg.data, offs);
                                    if (typeOid != 0)
                                    {
                                        ClassDescriptor desc = (ClassDescriptor)lookupObject(typeOid, typeof(ClassDescriptor));
#if WINRT_NET_FRAMEWORK
                                        if (typeof(Btree).GetTypeInfo().IsAssignableFrom(desc.cls.GetTypeInfo()))
#else
                                        if (typeof(Btree).IsAssignableFrom(desc.cls))
#endif
                                        {
                                            Btree btree = createBtreeStub(pg.data, offs);
                                            btree.AssignOid(this, 0, false);
                                            btree.markTree();
                                        }
                                        else if (desc.hasReferences)
                                        {
                                            pool.unfix(pg); // avoid recursiving pinning of large number of pages
                                            markObject(pool.get(pos), ObjectHeader.Sizeof, desc);
                                            continue;
                                        }
                                    }
                                    pool.unfix(pg);
                                }
                            }
                        }
                    }
                } while (existsNotMarkedObjects);
            }
        }


        private int sweep()
        {
            int nDeallocated = 0;
            long pos;
            gcDone = true;
            for (int i = dbFirstUserId, j = committedIndexSize; i < j; i++)
            {
                pos = getGCPos(i);
                if (pos != 0 && ((int)pos & (dbPageObjectFlag | dbFreeHandleFlag)) == 0)
                {
                    int bit = (int)((ulong)pos >> dbAllocationQuantumBits);
                    if ((blackBitmap[(uint)bit >> 5] & (1 << (bit & 31))) == 0)
                    {
                        // object is not accessible
                        if (getPos(i) != pos)
                        {
                            int offs = (int)pos & (Page.pageSize - 1);
                            Page pg = pool.getPage(pos - offs);
                            int typeOid = ObjectHeader.getType(pg.data, offs);
                            if (typeOid != 0)
                            {
                                ClassDescriptor desc = findClassDescriptor(typeOid);
                                nDeallocated += 1;
                                if (desc != null
#if WINRT_NET_FRAMEWORK
                                    && (typeof(Btree).GetTypeInfo().IsAssignableFrom(desc.cls.GetTypeInfo())))
#else
                                    && (typeof(Btree).IsAssignableFrom(desc.cls)))
#endif
                                    {
                                    Btree btree = createBtreeStub(pg.data, offs);
                                    pool.unfix(pg);
                                    btree.AssignOid(this, i, false);
                                    btree.Deallocate();
                                }
                                else
                                {
                                    int size = ObjectHeader.getSize(pg.data, offs);
                                    pool.unfix(pg);
                                    freeId(i);
                                    objectCache.remove(i);
                                    cloneBitmap(pos, size);
                                }
                                if (listener != null)
                                {
                                    listener.DeallocateObject(desc.cls, i);
                                }
                            }
                            else
                            {
                                pool.unfix(pg);
                            }
                        }
                    }
                }
            }

            greyBitmap = null;
            blackBitmap = null;
            allocatedDelta = 0;
            gcActive = false;

            if (listener != null)
            {
                listener.GcCompleted(nDeallocated);
            }
            return nDeallocated;
        }

#if !COMPACT_NET_FRAMEWORK
        void doGc()
        {
            lock (backgroundGcMonitor)
            {
                if (!opened)
                {
                    return;
                }
                mark();
                lock (this)
                {
                    lock (objectCache)
                    {
                        sweep();
                    }
                }
            }
        }

        public void backgroundGcThread()
        {
            while (true)
            {
                lock (backgroundGcStartMonitor)
                {
                    while (!gcGo && opened)
                    {
                        Monitor.Wait(backgroundGcStartMonitor);
                    }
                    if (!opened)
                    {
                        return;
                    }
                    gcGo = false;
                }
                doGc();
            }
        }

        private void activateGc()
        {
            lock (backgroundGcStartMonitor)
            {
                gcGo = true;
                Monitor.Pulse(backgroundGcStartMonitor);
            }
        }
#endif

        private int gc0()
        {
            lock (objectCache)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                if (gcDone || gcActive)
                {
                    return 0;
                }
                gcActive = true;
#if !COMPACT_NET_FRAMEWORK
                if (backgroundGc)
                {
#if WINRT_NET_FRAMEWORK
                    System.Threading.Tasks.Parallel.Invoke(new Action(doGc));
#else
                    if (gcThread == null)
                    {
                        gcThread = new Thread(new ThreadStart(backgroundGcThread));
                        gcThread.Start();
                    }
                    activateGc();
#endif
                    return 0;
                }
#endif
                // System.out.println("Start GC, allocatedDelta=" + allocatedDelta + ", header[" + currIndex + "].size=" + header.root[currIndex].size + ", gcTreshold=" + gcThreshold);

                mark();
                return sweep();
            }
        }


        public Hashtable GetMemoryDump()
        {
            lock (this)
            {
                lock (objectCache)
                {
                    if (!opened)
                    {
                        throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                    }
                    int bitmapSize = (int)(header.root[currIndex].size >> (dbAllocationQuantumBits + 5)) + 1;
                    bool existsNotMarkedObjects;
                    long pos;
                    int i, j;

                    // mark
                    greyBitmap = new int[bitmapSize];
                    blackBitmap = new int[bitmapSize];
                    int rootOid = header.root[currIndex].rootObject;
                    Hashtable map = new Hashtable();

                    if (rootOid != 0)
                    {
                        MemoryUsage indexUsage = new MemoryUsage(typeof(GenericIndex));
                        MemoryUsage classUsage = new MemoryUsage(typeof(Type));

                        markOid(rootOid);
                        do
                        {
                            existsNotMarkedObjects = false;
                            for (i = 0; i < bitmapSize; i++)
                            {
                                if (greyBitmap[i] != 0)
                                {
                                    existsNotMarkedObjects = true;
                                    for (j = 0; j < 32; j++)
                                    {
                                        if ((greyBitmap[i] & (1 << j)) != 0)
                                        {
                                            pos = (((long)i << 5) + j) << dbAllocationQuantumBits;
                                            greyBitmap[i] &= ~(1 << j);
                                            blackBitmap[i] |= 1 << j;
                                            int offs = (int)pos & (Page.pageSize - 1);
                                            Page pg = pool.getPage(pos - offs);
                                            int typeOid = ObjectHeader.getType(pg.data, offs);
                                            int objSize = ObjectHeader.getSize(pg.data, offs);
                                            int alignedSize = (objSize + dbAllocationQuantum - 1) & ~(dbAllocationQuantum - 1);
                                            if (typeOid != 0)
                                            {
                                                markOid(typeOid);
                                                ClassDescriptor desc = findClassDescriptor(typeOid);
#if WINRT_NET_FRAMEWORK
                                                if (typeof(Btree).GetTypeInfo().IsAssignableFrom(desc.cls.GetTypeInfo()))
#else
                                                if (typeof(Btree).IsAssignableFrom(desc.cls))
#endif
                                                {
                                                    Btree btree = createBtreeStub(pg.data, offs);
                                                    btree.AssignOid(this, 0, false);
                                                    int nPages = btree.markTree();
                                                    indexUsage.nInstances += 1;
                                                    indexUsage.totalSize += (long)nPages * Page.pageSize + objSize;
                                                    indexUsage.allocatedSize += (long)nPages * Page.pageSize + alignedSize;
                                                }
                                                else
                                                {
                                                    MemoryUsage usage = (MemoryUsage)map[desc.cls];
                                                    if (usage == null)
                                                    {
                                                        usage = new MemoryUsage(desc.cls);
                                                        map[desc.cls] = usage;
                                                    }
                                                    usage.nInstances += 1;
                                                    usage.totalSize += objSize;
                                                    usage.allocatedSize += alignedSize;

                                                    if (desc.hasReferences)
                                                    {
                                                        markObject(pool.get(pos), ObjectHeader.Sizeof, desc);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                classUsage.nInstances += 1;
                                                classUsage.totalSize += objSize;
                                                classUsage.allocatedSize += alignedSize;
                                            }
                                            pool.unfix(pg);
                                        }
                                    }
                                }
                            }
                        } while (existsNotMarkedObjects);

                        if (indexUsage.nInstances != 0)
                        {
                            map[typeof(GenericIndex)] = indexUsage;
                        }
                        if (classUsage.nInstances != 0)
                        {
                            map[typeof(Type)] = classUsage;
                        }
                        MemoryUsage system = new MemoryUsage(typeof(Storage));
                        system.totalSize += header.root[0].indexSize * 8L;
                        system.totalSize += header.root[1].indexSize * 8L;
                        system.totalSize += (long)(header.root[currIndex].bitmapEnd - dbBitmapId) * Page.pageSize;
                        system.totalSize += Page.pageSize; // root page

                        if (header.root[currIndex].bitmapExtent != 0)
                        {
                            system.allocatedSize = getBitmapUsedSpace(dbBitmapId, dbBitmapId + dbBitmapPages)
                                + getBitmapUsedSpace(header.root[currIndex].bitmapExtent + dbBitmapPages - bitmapExtentBase,
                                header.root[currIndex].bitmapExtent + header.root[currIndex].bitmapEnd - dbBitmapId - bitmapExtentBase);
                        }
                        else
                        {
                            system.allocatedSize = getBitmapUsedSpace(dbBitmapId, header.root[currIndex].bitmapEnd);
                        }
                        system.nInstances = header.root[currIndex].indexSize;
                        map[typeof(Storage)] = system;
                    }
                    return map;
                }
            }
        }

        long getBitmapUsedSpace(int from, int till)
        {
            long allocated = 0;
            while (from < till)
            {
                Page pg = getGCPage(from);
                for (int j = 0; j < Page.pageSize; j++)
                {
                    int mask = pg.data[j] & 0xFF;
                    while (mask != 0)
                    {
                        if ((mask & 1) != 0)
                        {
                            allocated += dbAllocationQuantum;
                        }
                        mask >>= 1;
                    }
                }
                pool.unfix(pg);
                from += 1;
            }
            return allocated;
        }

        internal int markObjectReference(byte[] obj, int offs)
        {
            int oid = Bytes.unpack4(obj, offs);
            offs += 4;
            if (oid < 0)
            {
                int tid = -1 - oid;
                switch ((ClassDescriptor.FieldType)tid)
                {
                    case ClassDescriptor.FieldType.tpString:
                    case ClassDescriptor.FieldType.tpType:
                    {
                        offs = Bytes.skipString(obj, offs);
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfByte:
                    {
                        int len = Bytes.unpack4(obj, offs);
                        offs += len + 4;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfObject:
                    {
                        int len = Bytes.unpack4(obj, offs);
                        offs += 4;
                        for (int i = 0; i < len; i++)
                        {
                            offs = markObjectReference(obj, offs);
                        }
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfRaw:
                    {
                        int len = Bytes.unpack4(obj, offs);
                        offs += 8;
                        for (int i = 0; i < len; i++)
                        {
                             offs = markObjectReference(obj, offs);
                        }
                        break;
                    }
                    case ClassDescriptor.FieldType.tpCustom:
                    {
                        MemoryReader reader = new MemoryReader(this, obj, offs, null, false, true);
                        serializer.Unpack(reader);
                        offs = reader.Position;
                        break;
                    }
                    default:
                    {
                        if (tid >= (int)ClassDescriptor.FieldType.tpValueTypeBias)
                        {
                            int typeOid = -(int)ClassDescriptor.FieldType.tpValueTypeBias - oid;
                            ClassDescriptor desc = findClassDescriptor(typeOid);
                            if (desc.isCollection)
                            {
                                int len = Bytes.unpack4(obj, offs);
                                offs += 4;
                                for (int i = 0; i < len; i++)
                                {
                                    offs = markObjectReference(obj, offs);
                                }
                            }
                            else if (desc.isDictionary)
                            {
                                int len = Bytes.unpack4(obj, offs);
                                offs += 4;
                                for (int i = 0; i < len; i++)
                                {
                                    offs = markObjectReference(obj, offs);
                                    offs = markObjectReference(obj, offs);
                                }
                            }
                            else if (desc.hasReferences)
                            {
                                offs = markObject(obj, offs, desc);
                            }
                        }
                        else
                        {
                            offs += ClassDescriptor.Sizeof[tid];
                        }
                        break;
                    }
                }
            }
            else
            {
                markOid(oid);
            }
            return offs;
        }

        internal int markObject(byte[] obj, int offs, ClassDescriptor desc)
        {
            ClassDescriptor.FieldDescriptor[] all = desc.allFields;

            for (int i = 0, n = all.Length; i < n; i++)
            {
                ClassDescriptor.FieldDescriptor fd = all[i];
                switch (fd.type)
                {
                    case ClassDescriptor.FieldType.tpBoolean:
                    case ClassDescriptor.FieldType.tpByte:
                    case ClassDescriptor.FieldType.tpSByte:
                        offs += 1;
                        continue;
                    case ClassDescriptor.FieldType.tpChar:
                    case ClassDescriptor.FieldType.tpShort:
                    case ClassDescriptor.FieldType.tpUShort:
                        offs += 2;
                        continue;
                    case ClassDescriptor.FieldType.tpInt:
                    case ClassDescriptor.FieldType.tpUInt:
                    case ClassDescriptor.FieldType.tpEnum:
                    case ClassDescriptor.FieldType.tpFloat:
                        offs += 4;
                        continue;
                    case ClassDescriptor.FieldType.tpLong:
                    case ClassDescriptor.FieldType.tpULong:
                    case ClassDescriptor.FieldType.tpDouble:
                    case ClassDescriptor.FieldType.tpDate:
                        offs += 8;
                        continue;
                    case ClassDescriptor.FieldType.tpDecimal:
                    case ClassDescriptor.FieldType.tpGuid:
                        offs += 16;
                        continue;

#if NET_FRAMEWORK_20
                    case ClassDescriptor.FieldType.tpNullableBoolean:
                    case ClassDescriptor.FieldType.tpNullableByte:
                    case ClassDescriptor.FieldType.tpNullableSByte:
                        if (obj[offs++] != 0)
                        {
                            offs += 1;
                        }
                        continue;
                    case ClassDescriptor.FieldType.tpNullableChar:
                    case ClassDescriptor.FieldType.tpNullableShort:
                    case ClassDescriptor.FieldType.tpNullableUShort:
                        if (obj[offs++] != 0)
                        {
                            offs += 2;
                        }
                        continue;
                    case ClassDescriptor.FieldType.tpNullableInt:
                    case ClassDescriptor.FieldType.tpNullableUInt:
                    case ClassDescriptor.FieldType.tpNullableEnum:
                    case ClassDescriptor.FieldType.tpNullableFloat:
                        if (obj[offs++] != 0)
                        {
                            offs += 4;
                        }
                        continue;
                    case ClassDescriptor.FieldType.tpNullableLong:
                    case ClassDescriptor.FieldType.tpNullableULong:
                    case ClassDescriptor.FieldType.tpNullableDouble:
                    case ClassDescriptor.FieldType.tpNullableDate:
                        if (obj[offs++] != 0)
                        {
                            offs += 8;
                        }
                        continue;
                    case ClassDescriptor.FieldType.tpNullableDecimal:
                    case ClassDescriptor.FieldType.tpNullableGuid:
                        if (obj[offs++] != 0)
                        {
                            offs += 16;
                        }
                        continue;
                    case ClassDescriptor.FieldType.tpNullableValue:
                        if (obj[offs++] != 0)
                        {
                            offs = markObject(obj, offs, fd.valueDesc);
                        }
                        continue;
#endif

                    case ClassDescriptor.FieldType.tpString:
                    case ClassDescriptor.FieldType.tpType:
                        {
                            offs = Bytes.skipString(obj, offs);
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpObject:
                    case ClassDescriptor.FieldType.tpOid:
                        offs = markObjectReference(obj, offs);
                        continue;
                    case ClassDescriptor.FieldType.tpValue:
                        offs = markObject(obj, offs, fd.valueDesc);
                        continue;
#if SUPPORT_RAW_TYPE
                    case ClassDescriptor.FieldType.tpRaw:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            if (len > 0)
                            {
                                offs += len;
                            }
                            else if (len == -2 - (int)ClassDescriptor.FieldType.tpObject)
                            {
                                markOid(Bytes.unpack4(obj, offs));
                                offs += 4;
                            }
                            else if (len < -1)
                            {
                                offs += ClassDescriptor.Sizeof[-2 - len];
                            }
                            continue;
                        }
#endif
                    case ClassDescriptor.FieldType.tpCustom:
                        {
                            MemoryReader reader = new MemoryReader(this, obj, offs, null, false, true);
                            serializer.Unpack(reader);
                            offs = reader.Position;
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfByte:
                    case ClassDescriptor.FieldType.tpArrayOfSByte:
                    case ClassDescriptor.FieldType.tpArrayOfBoolean:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            if (len > 0)
                            {
                                offs += len;
                            }
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfShort:
                    case ClassDescriptor.FieldType.tpArrayOfUShort:
                    case ClassDescriptor.FieldType.tpArrayOfChar:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            if (len > 0)
                            {
                                offs += len * 2;
                            }
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfInt:
                    case ClassDescriptor.FieldType.tpArrayOfUInt:
                    case ClassDescriptor.FieldType.tpArrayOfEnum:
                    case ClassDescriptor.FieldType.tpArrayOfFloat:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            if (len > 0)
                            {
                                offs += len * 4;
                            }
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfLong:
                    case ClassDescriptor.FieldType.tpArrayOfULong:
                    case ClassDescriptor.FieldType.tpArrayOfDouble:
                    case ClassDescriptor.FieldType.tpArrayOfDate:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            if (len > 0)
                            {
                                offs += len * 8;
                            }
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfString:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            while (--len >= 0)
                            {
                                offs = Bytes.skipString(obj, offs);
                            }
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfObject:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            while (--len >= 0) {
                                offs = markObjectReference(obj, offs);
                            }
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfOid:
                    case ClassDescriptor.FieldType.tpLink:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            while (--len >= 0)
                            {
                                markOid(Bytes.unpack4(obj, offs));
                                offs += 4;
                            }
                            continue;
                        }
                    case ClassDescriptor.FieldType.tpArrayOfValue:
                        {
                            int len = Bytes.unpack4(obj, offs);
                            offs += 4;
                            ClassDescriptor valueDesc = fd.valueDesc;
                            while (--len >= 0)
                            {
                                offs = markObject(obj, offs, valueDesc);
                            }
                            continue;
                        }
                }
            }
            return offs;
        }


        internal class ThreadTransactionContext
        {
            internal int nested;
            internal ArrayList locked;
            internal ArrayList deleted;
            internal ArrayList modified;

            internal ThreadTransactionContext()
            {
                locked = new ArrayList();
                deleted = new ArrayList();
                modified = new ArrayList();
            }
        }

        internal static ThreadTransactionContext TransactionContext
        {
            get
            {
#if COMPACT_NET_FRAMEWORK
                ThreadTransactionContext ctx = (ThreadTransactionContext)Thread.GetData(transactionContext);
                if (ctx == null)
                {
                    ctx = new ThreadTransactionContext();
                    Thread.SetData(transactionContext, ctx);
                }
#elif WP7
                ThreadTransactionContext ctx;
                int selfId = Thread.CurrentThread.ManagedThreadId;
                lock (transactionContext)
                {
                     if (!transactionContext.TryGetValue(selfId, out ctx))
                     {
                         transactionContext[selfId] = ctx = new ThreadTransactionContext();
                     }
                }
#else
                ThreadTransactionContext ctx = transactionContext;
                if (ctx == null)
                {
                    transactionContext = ctx = new ThreadTransactionContext();
                }
#endif
                return ctx;
            }
        }

        public void EndThreadTransaction()
        {
            EndThreadTransaction(Int32.MaxValue);
        }

        public bool IsInsideThreadTransaction
        {
            get
            {
                return TransactionContext.nested != 0 || nNestedTransactions != 0;
            }
        }

        public void BeginSerializableTransaction()
        {
            if (multiclientSupport)
            {
                BeginThreadTransaction(TransactionMode.ReadWrite);
            }
            else
            {
                BeginThreadTransaction(TransactionMode.Serializable);
            }
        }

        public void CommitSerializableTransaction()
        {
            if (!IsInsideThreadTransaction)
            {
                throw new StorageError(StorageError.ErrorCode.NOT_IN_TRANSACTION);
            }
            EndThreadTransaction(Int32.MaxValue);
        }

        public void RollbackSerializableTransaction()
        {
            if (!IsInsideThreadTransaction)
            {
                throw new StorageError(StorageError.ErrorCode.NOT_IN_TRANSACTION);
            }
            RollbackThreadTransaction();
        }

#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
        public void RegisterAssembly(System.Reflection.Assembly assembly)
        {
            assemblies.Add(assembly);
        }

        public void BeginThreadTransaction(TransactionMode mode)
        {
            if (mode == TransactionMode.Serializable)
            {
                if (multiclientSupport)
                {
                    throw new ArgumentException("Illegal transaction mode");
                }
                useSerializableTransactions = true;
                TransactionContext.nested += 1;;
            }
            else
            {
                if (multiclientSupport)
                {
                    if (mode == TransactionMode.Exclusive)
                    {
                        transactionLock.ExclusiveLock();
                    }
                    else
                    {
                        transactionLock.SharedLock();
                    }
                    transactionMonitor.Enter();
                    try {
                        if (nNestedTransactions++ == 0)
                        {
                            file.Lock(mode == TransactionMode.ReadOnly);
                            byte[] buf = new byte[Header.Sizeof];
                            int rc = file.Read(0, buf);
                            if (rc > 0 && rc < Header.Sizeof)
                            {
                                throw new StorageError(StorageError.ErrorCode.DATABASE_CORRUPTED);
                            }
                            header.unpack(buf);
                            int curr = header.curr;
                            currIndex = curr;
                            currIndexSize = header.root[1-curr].indexUsed;
                            committedIndexSize = currIndexSize;
                            usedSize = header.root[curr].size;

                            if (header.transactionId != transactionId)
                            {
                                if (bitmapPageAvailableSpace != null)
                                {
                                    for (int i = 0; i < bitmapPageAvailableSpace.Length; i++)
                                    {
                                        bitmapPageAvailableSpace[i] = int.MaxValue;
                                    }
                                }
                                objectCache.clear();
                                pool.clear();
                                transactionId = header.transactionId;
                            }
                        }
                    } finally {
                        transactionMonitor.Exit();
                    }
                }
                else
                {
                    transactionMonitor.Enter();
                    try {
                        if (scheduledCommitTime != Int64.MaxValue)
                        {
                            nBlockedTransactions += 1;
                            while (DateTime.Now.Ticks >= scheduledCommitTime)
                            {
                                transactionMonitor.Wait();
                            }
                            nBlockedTransactions -= 1;
                        }
                        nNestedTransactions += 1;
                    } finally {
                        transactionMonitor.Exit();
                    }
                    if (mode == TransactionMode.Exclusive)
                    {
                        transactionLock.ExclusiveLock();
                    }
                    else
                    {
                        transactionLock.SharedLock();
                    }
                }
            }
        }

        public void EndThreadTransaction(int maxDelay)
        {
            if (multiclientSupport)
            {
                if (maxDelay != int.MaxValue)
                {
                    throw new ArgumentException("Delay is not supported for global transactions");
                }
                transactionMonitor.Enter();
                try {
                    transactionLock.Unlock();
                    if (nNestedTransactions != 0) { // may be everything is already aborted
                        if (nNestedTransactions == 1) {
                            Commit();
                            pool.flush();
                            file.Unlock();
                        }
                        nNestedTransactions -= 1;
                    }
                } finally {
                    transactionMonitor.Exit();
                }
                return;
            }
            ThreadTransactionContext ctx = TransactionContext;
            if (ctx.nested != 0)
            { // serializable transaction
                if (--ctx.nested == 0)
                {
                    lock (backgroundGcMonitor)
                    {
                        lock (this)
                        {
                            lock (objectCache)
                            {
                                foreach (object obj in ctx.modified)
                                {
                                    Store(obj);
                                }
                                foreach (object obj in ctx.deleted)
                                {
                                    deallocateObject0(obj);
                                }
                                if (ctx.modified.Count + ctx.deleted.Count != 0)
                                {
                                    commit0();
                                }
                            }
                        }
                    }
                    foreach (IResource res in ctx.locked)
                    {
                        res.Reset();
                    }
                    ctx.modified.Clear();
                    ctx.deleted.Clear();
                    ctx.locked.Clear();
                }
            }
            else
            { // exclusive or cooperative transaction
                transactionMonitor.Enter();
                try {
                    transactionLock.Unlock();
                    if (nNestedTransactions != 0)
                    { // may be everything is already aborted
                        if (--nNestedTransactions == 0)
                        {
                            nCommittedTransactions += 1;
                            Commit();
                            scheduledCommitTime = Int64.MaxValue;
                            if (nBlockedTransactions != 0)
                            {
                                transactionMonitor.PulseAll();
                            }
                        }
                        else
                        {
                            if (maxDelay != Int32.MaxValue)
                            {
                                long nextCommit = DateTime.Now.Ticks + maxDelay;
                                if (nextCommit < scheduledCommitTime)
                                {
                                    scheduledCommitTime = nextCommit;
                                }
                                if (maxDelay == 0)
                                {
                                    int n = nCommittedTransactions;
                                    nBlockedTransactions += 1;
                                    do
                                    {
                                        transactionMonitor.Wait();
                                    } while (nCommittedTransactions == n);
                                    nBlockedTransactions -= 1;
                                }
                            }
                        }
                    }
                } finally {
                    transactionMonitor.Exit();
                }
            }
        }


        public void RollbackThreadTransaction()
        {
            if (multiclientSupport)
            {
                transactionMonitor.Enter();
                try {
                    transactionLock.Reset();
                    Rollback();
                    file.Unlock();
                    nNestedTransactions = 0;
                } finally {
                   transactionMonitor.Exit();
                }
                return;
            }
            ThreadTransactionContext ctx = TransactionContext;
            if (ctx.nested != 0)
            { // serializable transaction
                lock (this)
                {
                    lock (objectCache)
                    {
                        foreach (object obj in ctx.modified)
                        {
                            int oid = GetOid(obj);
                            Debug.Assert(oid != 0);
                            Invalidate(obj);
                            if (getPos(oid) == 0)
                            {
                                freeId(oid);
                                objectCache.remove(oid);
                            }
                            else
                            {
                                loadStub(oid, obj, obj.GetType());
                                objectCache.clearDirty(obj);
                            }
                        }
                    }
                }
                foreach (IResource res in ctx.locked)
                {
                    res.Reset();
                }
                ctx.nested = 0;
                ctx.modified.Clear();
                ctx.locked.Clear();
                ctx.deleted.Clear();
                if (listener != null)
                {
                    listener.OnTransactionRollback();
                }
            }
            else
            {
                transactionMonitor.Enter();
                try {
                    transactionLock.Reset();
                    nNestedTransactions = 0;
                    if (nBlockedTransactions != 0)
                    {
                        transactionMonitor.PulseAll();
                    }
                    Rollback();
                } finally {
                   transactionMonitor.Exit();
                }
            }
        }


#else
        public virtual void BeginThreadTransaction(TransactionMode mode)
        {
            if (mode == TransactionMode.Serializable)
            {
                useSerializableTransactions = true;
                TransactionContext.nested += 1;
            }
            else
            {
                if (multiclientSupport)
                {
                    if (mode == TransactionMode.Exclusive)
                    {
                        transactionLock.ExclusiveLock();
                    }
                    else
                    {
                        transactionLock.SharedLock();
                    }
                    lock (transactionMonitor)
                    {
                        if (nNestedTransactions++ == 0)
                        {
                            file.Lock(mode == TransactionMode.ReadOnly);
                            byte[] buf = new byte[Header.Sizeof];
                            int rc = file.Read(0, buf);
                            if (rc > 0 && rc < Header.Sizeof)
                            {
                                throw new StorageError(StorageError.ErrorCode.DATABASE_CORRUPTED);
                            }
                            header.unpack(buf);
                            int curr = header.curr;
                            currIndex = curr;
                            currIndexSize = header.root[1-curr].indexUsed;
                            committedIndexSize = currIndexSize;
                            usedSize = header.root[curr].size;

                            if (header.transactionId != transactionId)
                            {
                                if (bitmapPageAvailableSpace != null)
                                {
                                    for (int i = 0; i < bitmapPageAvailableSpace.Length; i++)
                                    {
                                        bitmapPageAvailableSpace[i] = int.MaxValue;
                                    }
                                }
                                objectCache.clear();
                                pool.clear();
                                transactionId = header.transactionId;
                            }
                        }
                    }
                }
                else
                {
                    lock (transactionMonitor)
                    {
                        if (scheduledCommitTime != Int64.MaxValue)
                        {
                            nBlockedTransactions += 1;
                            while (DateTime.Now.Ticks >= scheduledCommitTime)
                            {
                                Monitor.Wait(transactionMonitor);
                            }
                            nBlockedTransactions -= 1;
                        }
                        nNestedTransactions += 1;
                    }
                    if (mode == TransactionMode.Exclusive)
                    {
                        transactionLock.ExclusiveLock();
                    }
                    else
                    {
                        transactionLock.SharedLock();
                    }
                }
            }
        }


        public virtual void EndThreadTransaction(int maxDelay)
        {
            if (multiclientSupport)
            {
                if (maxDelay != int.MaxValue)
                {
                    throw new ArgumentException("Delay is not supported for global transactions");
                }
                lock (transactionMonitor)
                {
                    transactionLock.Unlock();
                    if (nNestedTransactions != 0) { // may be everything is already aborted
                        if (nNestedTransactions == 1) {
                            Commit();
                            pool.flush();
                            file.Unlock();
                        }
                        nNestedTransactions -= 1;
                    }
                }
                return;
            }
            ThreadTransactionContext ctx = TransactionContext;
            if (ctx.nested != 0)
            { // serializable transaction
                if (--ctx.nested == 0)
                {
                    lock (backgroundGcMonitor)
                    {
                        lock (this)
                        {
                            lock (objectCache)
                            {
                                foreach (object obj in ctx.modified)
                                {
                                    Store(obj);
                                }
                                foreach (object obj in ctx.deleted)
                                {
                                    deallocateObject0(obj);
                                }
                                if (ctx.modified.Count + ctx.deleted.Count != 0)
                                {
                                    commit0();
                                }
                            }
                        }
                    }
                    foreach (IResource res in ctx.locked)
                    {
                        res.Reset();
                    }
                    ctx.modified.Clear();
                    ctx.deleted.Clear();
                    ctx.locked.Clear();
                }
            }
            else
            { // exclusive or cooperative transaction
                lock (transactionMonitor)
                {
                    transactionLock.Unlock();
                    if (nNestedTransactions != 0)
                    { // may be everything is already aborted
                        if (--nNestedTransactions == 0)
                        {
                            nCommittedTransactions += 1;
                            Commit();
                            scheduledCommitTime = Int64.MaxValue;
                            if (nBlockedTransactions != 0)
                            {
                                Monitor.PulseAll(transactionMonitor);
                            }
                        }
                        else
                        {
                            if (maxDelay != Int32.MaxValue)
                            {
                                long nextCommit = DateTime.Now.Ticks + maxDelay;
                                if (nextCommit < scheduledCommitTime)
                                {
                                    scheduledCommitTime = nextCommit;
                                }
                                if (maxDelay == 0)
                                {
                                    int n = nCommittedTransactions;
                                    nBlockedTransactions += 1;
                                    do
                                    {
                                        Monitor.Wait(transactionMonitor);
                                    } while (nCommittedTransactions == n);
                                    nBlockedTransactions -= 1;
                                }
                            }
                        }
                    }
                }
            }
        }


        public void RollbackThreadTransaction()
        {
            if (multiclientSupport)
            {
                lock (transactionMonitor)
                {
                    transactionLock.Reset();
                    Rollback();
                    file.Unlock();
                    nNestedTransactions = 0;
                }
                return;
            }
            ThreadTransactionContext ctx = TransactionContext;
            if (ctx.nested != 0)
            { // serializable transaction
                lock (this)
                {
                    lock (objectCache)
                    {
                        foreach (object obj in ctx.modified)
                        {
                            int oid = GetOid(obj);
                            Invalidate(obj);
                            if (getPos(oid) == 0)
                            {
                                freeId(oid);
                                objectCache.remove(oid);
                            }
                            else
                            {
                                loadStub(oid, obj, obj.GetType());
                                objectCache.clearDirty(obj);
                            }
                        }
                    }
                }
                foreach (IResource res in ctx.locked)
                {
                    res.Reset();
                }
                ctx.nested = 0;
                ctx.modified.Clear();
                ctx.locked.Clear();
                ctx.deleted.Clear();
                if (listener != null)
                {
                    listener.OnTransactionRollback();
                }
            }
            else
            {
                lock (transactionMonitor)
                {
                    transactionLock.Reset();
                    nNestedTransactions = 0;
                    if (nBlockedTransactions != 0)
                    {
                        Monitor.PulseAll(transactionMonitor);
                    }
                    Rollback();
                }
            }
        }


#endif

        public virtual void Close()
        {
            lock (backgroundGcMonitor)
            {
                Commit();
                opened = false;
            }
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
            if (codeGenerationThread != null)
            {
                codeGenerationThread.Abort();
                codeGenerationThread.Join();
                codeGenerationThread = null;
            }
            if (gcThread != null)
            {
                activateGc();
                gcThread.Join();
            }
#endif
            if (isDirty())
            {
                Page pg = pool.putPage(0);
                header.pack(pg.data);
                pool.flush();
                pool.modify(pg);
                header.dirty = false;
                header.pack(pg.data);
                pool.unfix(pg);
                pool.flush();
            }
            pool.close();
            pool = null;
            objectCache = null;
            classDescMap = null;
            resolvedTypes = null;
            bitmapPageAvailableSpace = null;
            dirtyPagesMap = null;
            descList = null;
        }

        private bool getBooleanValue(object val)
        {
            if (val is bool)
            {
                return (bool)val;
            }
            else if (val is string)
            {
                return bool.Parse((string)val);
            }
            throw new StorageError(StorageError.ErrorCode.BAD_PROPERTY_VALUE);
        }

        private long getIntegerValue(object val)
        {
            if (val is int)
            {
                return (int)val;
            }
            else if (val is long)
            {
                return (long)val;
            }
            else if (val is string)
            {
                return long.Parse((string)val);
            }
            else
            {
                throw new StorageError(StorageError.ErrorCode.BAD_PROPERTY_VALUE);
            }
        }

        private RuntimeCodeGeneration parseCodeGeneration(object val)
        {
            if (val is string)
            {
                string str = (string)val;
                if (str.StartsWith("sync"))
                {
                    return RuntimeCodeGeneration.Synchronous;
                }
                else if (str.StartsWith("async"))
                {
                    return RuntimeCodeGeneration.Asynchronous;
                }
            }
            return getBooleanValue(val)
                ? RuntimeCodeGeneration.Asynchronous : RuntimeCodeGeneration.Disabled;
        }

        public void SetProperties(Hashtable props)
        {
            object val;
#if SILVERLIGHT
            foreach (System.Collections.Generic.KeyValuePair<object,object> e in props)
            {
                properties.Add(e.Key, e.Value);
            }
#else
            foreach (DictionaryEntry e in props)
            {
                properties.Add(e.Key, e.Value);
            }
#endif
            if ((val = props["perst.serialize.transient.objects"]) != null)
            {
                ClassDescriptor.serializeNonPersistentObjects = getBooleanValue(val);
            }
            if ((val = props["perst.object.cache.init.size"]) != null)
            {
                objectCacheInitSize = (int)getIntegerValue(val);
            }
            if ((val = props["perst.object.cache.kind"]) != null)
            {
                cacheKind = (string)val;
            }
            if ((val = props["perst.object.index.init.size"]) != null)
            {
                initIndexSize = (int)getIntegerValue(val);
            }
            if ((val = props["perst.extension.quantum"]) != null)
            {
                extensionQuantum = getIntegerValue(val);
            }
            if ((val = props["perst.gc.threshold"]) != null)
            {
                gcThreshold = getIntegerValue(val);
            }
            if ((val = props["perst.code.generation"]) != null)
            {
                runtimeCodeGeneration = parseCodeGeneration(val);
            }
            if ((val = props["perst.file.readonly"]) != null)
            {
                fileParameters.readOnly = getBooleanValue(val);
            }
            if ((val = props["perst.file.truncate"]) != null)
            {
                fileParameters.truncate = getBooleanValue(val);
            }
            if ((val = props["perst.lock.file"]) != null)
            {
                fileParameters.lockFile = getBooleanValue(val);
            }
            if ((val = props["perst.file.noflush"]) != null)
            {
                fileParameters.noFlush = getBooleanValue(val);
                if (opened)
                {
                    file.NoFlush = fileParameters.noFlush;
                }
            }
            if ((val = props["perst.isolated.storage.init.quota"]) != null)
            {
                fileParameters.initialQuota = getIntegerValue(val);
            }
            if ((val = props["perst.isolated.storage.quota.increase.quantum"]) != null)
            {
                fileParameters.quotaIncreaseQuantum = getIntegerValue(val);
            }
            if ((val = props["perst.isolated.storage.quota.increase.percent"]) != null)
            {
                fileParameters.quotaIncreasePercent = (int)getIntegerValue(val);
            }
            if ((val = props["perst.file.extension.quantum"]) != null)
            {
                fileParameters.fileExtensionQuantum = getIntegerValue(val);
            }
            if ((val = props["perst.file.extension.percent"]) != null)
            {
                fileParameters.fileExtensionPercent = (int)getIntegerValue(val);
            }
            else if ((val = props["perst.file.buffer.size"]) != null)
            {
                fileParameters.fileBufferSize = (int)getIntegerValue(val);
            }
            if ((val = props["perst.alternative.btree"]) != null)
            {
                alternativeBtree = getBooleanValue(val);
            }
            if ((val = props["perst.background.gc"]) != null)
            {
                backgroundGc = getBooleanValue(val);
            }
            if ((val = props["perst.string.encoding"]) != null)
            {
                encoding = Encoding.GetEncoding((string)val);
            }
            if ((val = props["perst.replication.ack"]) != null)
            {
                replicationAck = getBooleanValue(val);
            }
            if ((val = props["perst.replication.receive.timeout"]) != null)
            {
                replicationReceiveTimeout = (int)getIntegerValue(val);
            }
            if ((val = props["perst.concurrent.iterator"]) != null)
            {
                concurrentIterator = getBooleanValue(val);
            }
            if ((val = props["perst.slave.connection.timeout"]) != null)
            {
                slaveConnectionTimeout = (int)getIntegerValue(val);
            }
            if ((val = props["perst.page.pool.lru.limit"]) != null)
            {
                pagePoolLruLimit = getIntegerValue(val);
            }
            if ((val = props["perst.multiclient.support"]) != null)
            {
                multiclientSupport = getBooleanValue(val);
            }
            if ((val = props["perst.reload.objects.on.rollback"]) != null)
            {
                reloadObjectsOnRollback = getBooleanValue(val);
            }
            if ((val = props["perst.serialize.system.collections"]) != null)
            {
                serializeSystemCollections = getBooleanValue(val);
            }
            if ((val = props["perst.reuse.oid"]) != null)
            {
                reuseOid = getBooleanValue(val);
            }
            if ((val = props["perst.ignore.missed.classes"]) != null)
            {
               ignoreMissedClasses = getBooleanValue(val);
            }
            if (multiclientSupport && backgroundGc)
            {
                throw new ArgumentException("In mutliclient access mode bachround GC is not supported");
            }
        }

        public void SetProperty(string name, object val)
        {
            properties[name] = val;

            if (name.Equals("perst.serialize.transient.objects"))
            {
                ClassDescriptor.serializeNonPersistentObjects = getBooleanValue(val);
            }
            else if (name.Equals("perst.object.cache.init.size"))
            {
                objectCacheInitSize = (int)getIntegerValue(val);
            }
            else if (name.Equals("perst.object.cache.kind"))
            {
                cacheKind = (string)val;
            }
            else if (name.Equals("perst.object.index.init.size"))
            {
                initIndexSize = (int)getIntegerValue(val);
            }
            else if (name.Equals("perst.extension.quantum"))
            {
                extensionQuantum = getIntegerValue(val);
            }
            else if (name.Equals("perst.gc.threshold"))
            {
                gcThreshold = getIntegerValue(val);
            }
            else if (name.Equals("perst.code.generation"))
            {
                runtimeCodeGeneration = parseCodeGeneration(val);
            }
            else if (name.Equals("perst.file.readonly"))
            {
                fileParameters.readOnly = getBooleanValue(val);
            }
            else if (name.Equals("perst.file.truncate"))
            {
                fileParameters.truncate = getBooleanValue(val);
            }
            else if (name.Equals("perst.lock.file"))
            {
                fileParameters.lockFile = getBooleanValue(val);
            }
            else if (name.Equals("perst.file.buffer.size"))
            {
                fileParameters.fileBufferSize = (int)getIntegerValue(val);
            }
            else if (name.Equals("perst.file.noflush"))
            {
                fileParameters.noFlush = getBooleanValue(val);
                if (opened)
                {
                    file.NoFlush = fileParameters.noFlush;
                }
            }
            else if (name.Equals("perst.isolated.storage.init.quota"))
            {
                fileParameters.initialQuota = getIntegerValue(val);
            }
            else if (name.Equals("perst.isolated.storage.quota.increase.quantum"))
            {
                fileParameters.quotaIncreaseQuantum = getIntegerValue(val);
            }
            else if (name.Equals("perst.isolated.storage.quota.increase.percent"))
            {
                fileParameters.quotaIncreasePercent = (int)getIntegerValue(val);
            }
            else if (name.Equals("perst.file.extension.quantum"))
            {
                fileParameters.fileExtensionQuantum = getIntegerValue(val);
            }
            else if (name.Equals("perst.file.extension.percent"))
            {
                fileParameters.fileExtensionPercent = (int)getIntegerValue(val);
            }
            else if (name.Equals("perst.alternative.btree"))
            {
                alternativeBtree = getBooleanValue(val);
            }
            else if (name.Equals("perst.background.gc"))
            {
                backgroundGc = getBooleanValue(val);
            }
            else if (name.Equals("perst.string.encoding"))
            {
                encoding = Encoding.GetEncoding((string)val);
            }
            else if (name.Equals("perst.replication.ack"))
            {
                replicationAck = getBooleanValue(val);
            }
            else if (name.Equals("perst.replication.receive.timeout"))
            {
                replicationReceiveTimeout = (int)getIntegerValue(val);
            }
            else if (name.Equals("perst.concurrent.iterator"))
            {
                concurrentIterator = getBooleanValue(val);
            }
            else if (name.Equals("perst.slave.connection.timeout"))
            {
                slaveConnectionTimeout = (int)getIntegerValue(val);
            }
            else if (name.Equals("perst.page.pool.lru.limit"))
            {
                pagePoolLruLimit = getIntegerValue(val);
            }
            else if (name.Equals("perst.serialize.system.collections"))
            {
                serializeSystemCollections = getBooleanValue(val);
            }
            else if (name.Equals("perst.multiclient.support"))
            {
                multiclientSupport = getBooleanValue(val);
            }
            else if (name.Equals("perst.reload.objects.on.rollback"))
            {
                reloadObjectsOnRollback = getBooleanValue(val);
            }
            else if (name.Equals("perst.reuse.oid"))
            {
                reuseOid = getBooleanValue(val);
            }
            else if (name.Equals("perst.ignore.missed.classes"))
            {
               ignoreMissedClasses = getBooleanValue(val);
            }

            if (multiclientSupport && backgroundGc)
            {
                throw new ArgumentException("In mutliclient access mode bachround GC is not supported");
            }
        }

        public object GetProperty(string name)
        {
            return properties[name];
        }

        public Hashtable GetProperties()
        {
            return properties;
        }


        public StorageListener Listener
        {
            set
            {
                listener = value;
            }
            get
            {
                return listener;
            }
        }

        public object GetObjectByOID(int oid)
        {
            lock (this)
            {
                return oid == 0 ? null : lookupObject(oid, null);
            }
        }



        public void modifyObject(object obj)
        {
            lock (this)
            {
                lock (objectCache)
                {
                    if (!IsModified(obj))
                    {
                        modified = true;
                        if (useSerializableTransactions)
                        {
                            ThreadTransactionContext ctx = TransactionContext;
                            if (ctx.nested != 0)
                            { // serializable transaction
                                ctx.modified.Add(obj);
                                return;
                            }
                        }
                        objectCache.setDirty(obj);
                    }
                }
            }
        }

        public bool lockObject(object obj)
        {
            if (useSerializableTransactions)
            {
                ThreadTransactionContext ctx = TransactionContext;
                if (ctx.nested != 0)
                { // serializable transaction
                    ArrayList locked = ctx.locked;
                    for (int i = locked.Count; --i >= 0; )
                    {
#if !COMPACT_NET_FRAMEWORK
                        if (object.ReferenceEquals(obj, locked[i]))
#else
                        if (obj == locked[i])
#endif
                        {
                            return false;
                        }
                    }
                    locked.Add(obj);
                }
            }
            return true;
        }

        public void storeObject(object obj)
        {
            lock (this)
            {
                if (!opened)
                {
                    throw new StorageError(StorageError.ErrorCode.STORAGE_NOT_OPENED);
                }
                if (useSerializableTransactions && TransactionContext.nested != 0)
                {
                    // Store should not be used in serializable transaction mode
                    throw new StorageError(StorageError.ErrorCode.INVALID_OPERATION, "store object");
                }
                lock (objectCache)
                {
                    storeObject0(obj, false);
                }
            }
        }

        public void storeFinalizedObject(object obj)
        {
            if (opened)
            {
                lock (objectCache)
                {
                    if (GetOid(obj) != 0)
                    {
                        storeObject0(obj, true);
                    }
                }
            }
        }

        CustomAllocator getCustomAllocator(Type cls)
        {
            Object a = customAllocatorMap[cls];
            if (a != null)
            {
                return a == defaultAllocator ? null : (CustomAllocator)a;
            }
#if WINRT_NET_FRAMEWORK
            Type superclass = cls.GetTypeInfo().BaseType;
#else
            Type superclass = cls.BaseType;
#endif
            if (superclass != null)
            {
                CustomAllocator alloc = getCustomAllocator(superclass);
                if (alloc != null)
                {
                    customAllocatorMap[cls] = alloc;
                    return alloc;
                }
            }
#if WINRT_NET_FRAMEWORK
            foreach (Type i in cls.GetTypeInfo().ImplementedInterfaces)
#else
            foreach (Type i in cls.GetInterfaces())
#endif
            {

                CustomAllocator alloc = getCustomAllocator(i);
                if (alloc != null)
                {
                    customAllocatorMap[cls] = alloc;
                    return alloc;
                }
            }
            customAllocatorMap[cls] = defaultAllocator;
            return null;
        }

        void storeObject0(object obj, bool finalized)
        {
            if (obj is IStoreable)
            {
                ((IStoreable)obj).OnStore();
            }
            if (listener != null)
            {
                listener.OnObjectStore(obj);
            }
            int oid = GetOid(obj);
            bool newObject = false;
            if (oid == 0)
            {
                oid = allocateId();
                if (!finalized)
                {
                    objectCache.put(oid, obj);
                }
                AssignOid(obj, oid, false);
                newObject = true;
            }
            else if (IsModified(obj))
            {
                objectCache.clearDirty(obj);
            }
            byte[] data = packObject(obj, finalized);
            long pos;
            int newSize = ObjectHeader.getSize(data, 0);
            CustomAllocator allocator = (customAllocatorMap != null) ? getCustomAllocator(obj.GetType()) : null;
            if (newObject || (pos = getPos(oid)) == 0)
            {
                pos = allocator != null ? allocator.Allocate(newSize) : allocate(newSize, 0);
                setPos(oid, pos | dbModifiedFlag);
            }
            else
            {
                int offs = (int)pos & (Page.pageSize - 1);
                if ((offs & (dbFreeHandleFlag | dbPageObjectFlag)) != 0)
                {
                    throw new StorageError(StorageError.ErrorCode.DELETED_OBJECT);
                }
                Page pg = pool.getPage(pos - offs);
                int size = ObjectHeader.getSize(pg.data, offs & ~dbFlagsMask);
                pool.unfix(pg);
                if ((pos & dbModifiedFlag) == 0)
                {
                    if (allocator != null)
                    {
                        allocator.Free(pos & ~dbFlagsMask, size);
                        pos = allocator.Allocate(newSize);
                    }
                    else
                    {
                        cloneBitmap(pos & ~dbFlagsMask, size);
                        pos = allocate(newSize, 0);
                    }
                    setPos(oid, pos | dbModifiedFlag);
                }
                else
                {
                    pos &= ~dbFlagsMask;
                    if (newSize != size)
                    {
                        if (allocator != null)
                        {
                            long newPos = allocator.Reallocate(pos, size, newSize);
                            if (newPos != pos)
                            {
                                pos = newPos;
                                setPos(oid, pos | dbModifiedFlag);
                            }
                            else if (newSize < size)
                            {
                                ObjectHeader.setSize(data, 0, size);
                            }
                        }
                        else
                        {

                            if (((newSize + dbAllocationQuantum - 1) & ~(dbAllocationQuantum - 1))
                                > ((size + dbAllocationQuantum - 1) & ~(dbAllocationQuantum - 1)))
                            {
                                long newPos = allocate(newSize, 0);
                                cloneBitmap(pos, size);
                                free(pos, size);
                                pos = newPos;
                                setPos(oid, pos | dbModifiedFlag);
                            }
                            else if (newSize < size)
                            {
                                ObjectHeader.setSize(data, 0, size);
                            }
                        }
                    }
                }
            }
            modified = true;
            pool.put(pos, data, newSize);
        }

        public void loadObject(object obj)
        {
            lock (this)
            {
                if (IsRaw(obj))
                {
                    loadStub(GetOid(obj), obj, obj.GetType());
                }
            }
        }

        internal object lookupObject(int oid, System.Type cls)
        {
            object obj = objectCache.get(oid);
            if (obj == null || IsRaw(obj))
            {
                obj = loadStub(oid, obj, cls);
            }
            if (listener != null)
            {
                listener.OnObjectLookup(obj);
            }
            return obj;
        }

        internal int swizzle(ByteBuffer buf, int offs, object obj)
        {
            if (obj is IPersistent || obj == null)
            {
                offs = buf.packI4(offs, swizzle(obj, buf.finalized));
            }
            else
            {
                Type t = obj.GetType();
#if WINRT_NET_FRAMEWORK
                if (t.GetTypeInfo().IsPrimitive)
#else
                if (t.IsPrimitive)
#endif
                {
                    if (t == typeof(int))
                    {
                        buf.extend(offs + 8);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpInt);
                        Bytes.pack4(buf.arr, offs + 4, (int)obj);
                        offs += 8;
                    }
                    else if (t == typeof(long))
                    {
                        buf.extend(offs + 12);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpLong);
                        Bytes.pack8(buf.arr, offs + 4, (long)obj);
                        offs += 12;
                    }
                    else if (t == typeof(bool))
                    {
                        buf.extend(offs + 5);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpBoolean);
                        buf.arr[offs + 4] = (byte)((bool)obj ? 1 : 0);
                        offs += 5;
                    }
                    else if (t == typeof(char))
                    {
                        buf.extend(offs + 6);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpChar);
                        Bytes.pack2(buf.arr, offs + 4, (short)(char)obj);
                        offs += 6;
                    }
                    else if (t == typeof(byte))
                    {
                        buf.extend(offs + 5);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpByte);
                        buf.arr[offs + 4] = (byte)obj;
                        offs += 5;
                    }
                    else if (t == typeof(sbyte))
                    {
                        buf.extend(offs + 5);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpSByte);
                        buf.arr[offs + 4] = (byte)(sbyte)obj;
                        offs += 5;
                    }
                    else if (t == typeof(short))
                    {
                        buf.extend(offs + 6);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpShort);
                        Bytes.pack2(buf.arr, offs + 4, (short)obj);
                        offs += 6;
                    }
                    else if (t == typeof(ushort))
                    {
                        buf.extend(offs + 6);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpUShort);
                        Bytes.pack2(buf.arr, offs + 4, (short)(ushort)obj);
                        offs += 6;
                    }
                    else if (t == typeof(uint))
                    {
                        buf.extend(offs + 8);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpUInt);
                        Bytes.pack4(buf.arr, offs + 4, (int)(uint)obj);
                        offs += 8;
                    }
                    else if (t == typeof(ulong))
                    {
                        buf.extend(offs + 12);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpULong);
                        Bytes.pack8(buf.arr, offs + 4, (long)(ulong)obj);
                        offs += 12;
                    }
                    else if (t == typeof(float))
                    {
                        buf.extend(offs + 8);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpFloat);
                        Bytes.packF4(buf.arr, offs + 4, (float)obj);
                        offs += 8;
                    }
                    else if (t == typeof(double))
                    {
                        buf.extend(offs + 12);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpDouble);
                        Bytes.packF8(buf.arr, offs + 4, (double)obj);
                        offs += 12;
                    }
                    else
                    {
                        throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_TYPE);
                    }
                }
                else if (t == typeof(DateTime))
                {
                    buf.extend(offs + 12);
                    Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpDate);
                    Bytes.packDate(buf.arr, offs + 4, (DateTime)obj);
                    offs += 12;
                }
                else if (t == typeof(Guid))
                {
                    buf.extend(offs + 20);
                    Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpGuid);
                    Bytes.packGuid(buf.arr, offs + 4, (Guid)obj);
                    offs += 20;
                }
                else if (t == typeof(Decimal))
                {
                    buf.extend(offs + 20);
                    Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpDecimal);
                    Bytes.packDecimal(buf.arr, offs + 4, (decimal)obj);
                    offs += 20;
                }
                else if (t == typeof(string))
                {
                    offs = buf.packI4(offs, -1 - (int)ClassDescriptor.FieldType.tpString);
                    offs = buf.packString(offs, (string)obj);
                }
                else if (t == typeof(Type))
                {
                    offs = buf.packI4(offs, -1 - (int)ClassDescriptor.FieldType.tpType);
                    offs = buf.packString(offs, ClassDescriptor.getTypeName((Type)obj));
                }
                else if (obj is IList && (!serializeSystemCollections || t.Namespace.StartsWith("System.Collections.")))
                {
                    ClassDescriptor valueDesc = getClassDescriptor(obj.GetType());
                    offs = buf.packI4(offs, -(int)ClassDescriptor.FieldType.tpValueTypeBias - valueDesc.Oid);
                    IList list = (IList)obj;
                    offs = buf.packI4(offs, list.Count);
                    foreach (object elem in list)
                    {
                        offs = swizzle(buf, offs, elem);
                    }
                }
                else if (obj is IDictionary && (!serializeSystemCollections || t.Namespace.StartsWith("System.Collections.")))
                {
                    ClassDescriptor valueDesc = getClassDescriptor(obj.GetType());
                    offs = buf.packI4(offs, -(int)ClassDescriptor.FieldType.tpValueTypeBias - valueDesc.Oid);
                    IDictionary d = (IDictionary)obj;
                    IDictionaryEnumerator e = d.GetEnumerator();
                    offs = buf.packI4(offs, d.Count);
                    while (e.MoveNext())
                    {
                        offs = swizzle(buf, offs, e.Key);
                        offs = swizzle(buf, offs, e.Value);
                    }
                }
#if WINRT_NET_FRAMEWORK
                else if (t.GetTypeInfo().IsValueType)
#else
                else if (t.IsValueType)
#endif
                {
                    ClassDescriptor valueDesc = getClassDescriptor(obj.GetType());
                    offs = buf.packI4(offs, -(int)ClassDescriptor.FieldType.tpValueTypeBias - valueDesc.Oid);
                    offs = packObject(obj, valueDesc, offs, buf);
                }
                else if (t.IsArray)
                {
                    Type elemType = t.GetElementType();
                    if (elemType == typeof(byte))
                    {
                        byte[] arr = (byte[])obj;
                        int len = arr.Length;
                        buf.extend(offs + len + 8);
                        Bytes.pack4(buf.arr, offs, -1 - (int)ClassDescriptor.FieldType.tpArrayOfByte);
                        Bytes.pack4(buf.arr, offs + 4, len);
                        Array.Copy(arr, 0, buf.arr, offs + 8, len);
                        offs += 8 + len;
                    }
                    else if (elemType == typeof(object))
                    {
                        offs = buf.packI4(offs, -1 - (int)ClassDescriptor.FieldType.tpArrayOfObject);
                        object[] arr = (object[])obj;
                        int len = arr.Length;
                        offs = buf.packI4(offs, len);
                        for (int i = 0; i < len; i++)
                        {
                            offs = swizzle(buf, offs, arr[i]);
                        }
                    }
                    else
                    {
                        Array arr = (Array)obj;
                        offs = buf.packI4(offs, -1 - (int)ClassDescriptor.FieldType.tpArrayOfRaw);
                        int len = arr.Length;
                        offs = buf.packI4(offs, len);
                        ClassDescriptor desc = getClassDescriptor(elemType);
                        offs = buf.packI4(offs, desc.Oid);
                        for (int i = 0; i < len; i++)
                        {
                            offs = swizzle(buf, offs, arr.GetValue(i));
                        }
                    }
                }
                else if (serializer != null && serializer.IsEmbedded(obj))
                {
                    buf.packI4(offs, -1 - (int)ClassDescriptor.FieldType.tpCustom);
                    serializer.Pack(obj, buf.GetWriter());
                    offs = buf.used;
                }
                else
                {
                    offs = buf.packI4(offs, swizzle(obj, buf.finalized));
                }
            }
            return offs;
        }


        internal int swizzle(object obj, bool finalized)
        {
            int oid = 0;
            if (obj != null)
            {
                if (!IsPersistent(obj))
                {
                    storeObject0(obj, finalized);
                }
                oid = GetOid(obj);
            }
            return oid;
        }

        internal ClassDescriptor findClassDescriptor(int oid)
        {
            return (ClassDescriptor)lookupObject(oid, typeof(ClassDescriptor)) ;

        }

        internal object unswizzle(byte[] body, ref int offs, Type cls, object parent, bool recursiveLoading)
        {
            int oid = Bytes.unpack4(body, offs);
            offs += 4;
            if (oid < 0)
            {
                object val;
                switch ((ClassDescriptor.FieldType)(-1-oid))
                {
                    case ClassDescriptor.FieldType.tpBoolean:
                        val = body[offs++] != 0;
                        break;
                    case ClassDescriptor.FieldType.tpByte:
                        val = body[offs++];
                        break;
                    case ClassDescriptor.FieldType.tpSByte:
                        val = (sbyte)body[offs++];
                        break;
                    case ClassDescriptor.FieldType.tpChar:
                        val = (char)Bytes.unpack2(body, offs);
                        offs += 2;
                        break;
                    case ClassDescriptor.FieldType.tpShort:
                        val = Bytes.unpack2(body, offs);
                        offs += 2;
                        break;
                    case ClassDescriptor.FieldType.tpUShort:
                        val = (ushort)Bytes.unpack2(body, offs);
                        offs += 2;
                        break;
                    case ClassDescriptor.FieldType.tpInt:
                    case ClassDescriptor.FieldType.tpOid:
                        val = Bytes.unpack4(body, offs);
                        offs += 4;
                        break;
                    case ClassDescriptor.FieldType.tpUInt:
                        val = (uint)Bytes.unpack4(body, offs);
                        offs += 4;
                        break;
                    case ClassDescriptor.FieldType.tpLong:
                        val = Bytes.unpack8(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpULong:
                        val = (ulong)Bytes.unpack8(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpFloat:
                        val = Bytes.unpackF4(body, offs);
                        offs += 4;
                        break;
                    case ClassDescriptor.FieldType.tpDouble:
                        val = Bytes.unpackF8(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpDate:
                        val = Bytes.unpackDate(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpGuid:
                        val = Bytes.unpackGuid(body, offs);
                        offs += 16;
                        break;
                    case ClassDescriptor.FieldType.tpDecimal:
                        val = Bytes.unpackDecimal(body, offs);
                        offs += 16;
                        break;
                    case ClassDescriptor.FieldType.tpString:
                    {
                        string str;
                        offs = Bytes.unpackString(body, offs, out str, encoding);
                        val = str;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpType:
                    {
                        string str;
                        offs = Bytes.unpackString(body, offs, out str, encoding);
                        val = ClassDescriptor.lookup(this, str);
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfByte:
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        byte[] arr = new byte[len];
                        Array.Copy(body, offs, arr, 0, len);
                        offs += len;
                        val = arr;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfObject:
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        object[] arr = new object[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = unswizzle(body, ref offs, typeof(object), parent, recursiveLoading);
                        }
                        val = arr;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfRaw:
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        int typeOid = Bytes.unpack4(body, offs);
                        offs += 4;
                        ClassDescriptor desc = findClassDescriptor(typeOid);
                        Type elemType = desc.cls;
                        Array arr = Array.CreateInstance(elemType, len);
                        for (int i = 0; i < len; i++)
                        {
                            arr.SetValue(unswizzle(body, ref offs, elemType, parent, recursiveLoading), i);
                        }
                        val = arr;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpCustom:
                    {
                        MemoryReader reader = new MemoryReader(this, body, offs, parent, recursiveLoading, false);
                        val = serializer.Unpack(reader);
                        offs = reader.Position;
                        break;
                    }
                    default:
                    {
                        if (oid < -(int)ClassDescriptor.FieldType.tpValueTypeBias)
                        {
                            int typeOid = -(int)ClassDescriptor.FieldType.tpValueTypeBias - oid;
                            ClassDescriptor desc = findClassDescriptor(typeOid);
                            if (desc.isCollection)
                            {
                                int len = Bytes.unpack4(body, offs);
                                offs += 4;
                                IList list = (IList)desc.newInitializedInstance();
                                for (int i = 0; i < len; i++)
                                {
                                    list.Add(unswizzle(body, ref offs, typeof(object), parent, recursiveLoading));
                                }
                                val = list;
                            }
                            else if (desc.isDictionary)
                            {
                                int len = Bytes.unpack4(body, offs);
                                offs += 4;
                                IDictionary map = (IDictionary)desc.newInitializedInstance();
                                for (int i = 0; i < len; i++)
                                {
                                    object key = unswizzle(body, ref offs, typeof(object), parent, recursiveLoading);
                                    object value = unswizzle(body, ref offs, typeof(object), parent, recursiveLoading);
                                    map.Add(key, value);
                                }
                                val = map;
                            }
                            else
                            {
                                val = desc.newInstance();
                                offs = unpackObject(val, desc, recursiveLoading, body, offs, parent);
                            }
                        }
                        else
                        {
                            throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_TYPE);
                        }
                        break;
                    }
                }
                return val;
            }
            else
            {
                return unswizzle(oid, cls, recursiveLoading);
            }
        }

        internal object unswizzle(int oid, Type cls, bool recursiveLoading)
        {
            if (oid == 0)
            {
                return null;
            }
            if (recursiveLoading)
            {
                return lookupObject(oid, cls);
            }
            object stub = objectCache.get(oid);
            if (stub != null)
            {
                return stub;
            }
            ClassDescriptor desc;
            if (cls == typeof(object) || (desc = findClassDescriptor(cls)) == null || desc.hasSubclasses)
            {
                long pos = getPos(oid);
                int offs = (int)pos & (Page.pageSize - 1);
                if ((offs & (dbFreeHandleFlag | dbPageObjectFlag)) != 0)
                {
                    throw new StorageError(StorageError.ErrorCode.DELETED_OBJECT);
                }
                Page pg = pool.getPage(pos - offs);
                int typeOid = ObjectHeader.getType(pg.data, offs & ~dbFlagsMask);
                pool.unfix(pg);
                desc = findClassDescriptor(typeOid);
            }
#if !COMPACT_NET_FRAMEWORK && !WP7
            if (desc.serializer != null)
            {
                stub = desc.serializer.newInstance();
            }
            else
#endif
            {
                stub = desc.newInstance();
            }
            AssignOid(stub, oid, true);
            objectCache.put(oid, stub);
            return stub;
        }

        internal object loadStub(int oid, object obj, Type cls)
        {
            long pos = getPos(oid);
            if ((pos & (dbFreeHandleFlag | dbPageObjectFlag)) != 0)
            {
                throw new StorageError(StorageError.ErrorCode.DELETED_OBJECT);
            }
            byte[] body = pool.get(pos & ~dbFlagsMask);
            ClassDescriptor desc;
            int typeOid = ObjectHeader.getType(body, 0);
            if (typeOid == 0)
            {
                desc = findClassDescriptor(cls);
            }
            else
            {
                desc = findClassDescriptor(typeOid);
            }
            if (obj == null)
            {
                if (desc.customSerializable)
                {
                     obj = serializer.Create(desc.cls);
                }
#if !COMPACT_NET_FRAMEWORK && !WP7
                else if (desc.serializer != null)
                {
                    obj = desc.serializer.newInstance();
                }
                else
#endif
                {
                    obj = desc.newInstance();
                }
                objectCache.put(oid, obj);
            }
            AssignOid(obj, oid, false);
#if !COMPACT_NET_FRAMEWORK && !WP7
            if (desc.serializer != null)
            {
                desc.serializer.unpack(this, obj, body, RecursiveLoading(obj), encoding);
            }
            else
#endif
            if (obj is ISelfSerializable)
            {
                ((ISelfSerializable)obj).Unpack(new MemoryReader(this, body, ObjectHeader.Sizeof, obj, RecursiveLoading(obj), false));
            }
            else if (desc.customSerializable)
            {
                serializer.Unpack(obj, new MemoryReader(this, body, ObjectHeader.Sizeof, obj, RecursiveLoading(obj), false));
            }
            else
            {
                unpackObject(obj, desc, RecursiveLoading(obj), body, ObjectHeader.Sizeof, obj);
            }
            if (obj is ILoadable)
            {
                ((ILoadable)obj).OnLoad();
            }
            if (listener != null)
            {
                listener.OnObjectLoad(obj);
            }
            return obj;
        }

        internal int unpackObject(object obj, ClassDescriptor desc, bool recursiveLoading, byte[] body, int offs, object parent)
        {
            ClassDescriptor.FieldDescriptor[] all = desc.allFields;
            for (int i = 0, n = all.Length; i < n; i++)
            {
                ClassDescriptor.FieldDescriptor fd = all[i];
                if (obj == null || (fd.field == null && fd.property == null))
                {
                    offs = skipField(body, offs, fd, fd.type);
                }
                else if (offs < body.Length)
                {
                    object val = obj;
                    offs = unpackField(body, offs, recursiveLoading, ref val, fd, fd.type, parent);
                    fd.SetValue(obj, val);
                }
            }
            return offs;
        }

        internal int skipObjectReference(byte[] obj, int offs)
        {
            int oid = Bytes.unpack4(obj, offs);
            offs += 4;
            if (oid < 0)
            {
                int tid = -1 - oid;
                switch ((ClassDescriptor.FieldType)tid)
                {
                    case ClassDescriptor.FieldType.tpString:
                    case ClassDescriptor.FieldType.tpType:
                    {
                        offs = Bytes.skipString(obj, offs);
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfByte:
                    {
                        int len = Bytes.unpack4(obj, offs);
                        offs += len + 4;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfObject:
                    {
                        int len = Bytes.unpack4(obj, offs);
                        offs += 4;
                        for (int i = 0; i < len; i++)
                        {
                            offs = skipObjectReference(obj, offs);
                        }
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfRaw:
                    {
                        int len = Bytes.unpack4(obj, offs);
                        offs += 8;
                        for (int i = 0; i < len; i++)
                        {
                             offs = skipObjectReference(obj, offs);
                        }
                        break;
                    }
                    default:
                    {
                        if (tid >= (int)ClassDescriptor.FieldType.tpValueTypeBias)
                        {
                            int typeOid = -(int)ClassDescriptor.FieldType.tpValueTypeBias - oid;
                            ClassDescriptor desc = findClassDescriptor(typeOid);
                            if (desc.isCollection)
                            {
                                int len = Bytes.unpack4(obj, offs);
                                offs += 4;
                                for (int i = 0; i < len; i++)
                                {
                                    offs = skipObjectReference(obj, offs);
                                }
                            }
                            else if (desc.isDictionary)
                            {
                                int len = Bytes.unpack4(obj, offs);
                                offs += 4;
                                for (int i = 0; i < len; i++)
                                {
                                    offs = skipObjectReference(obj, offs);
                                    offs = skipObjectReference(obj, offs);
                                }
                            }
                            else
                            {
                                offs = unpackObject(null, findClassDescriptor(typeOid), false, obj, offs, null);
                            }
                        }
                        else
                        {
                            offs += ClassDescriptor.Sizeof[tid];
                        }
                        break;
                    }
                }
            }
            return offs;
        }

        public int skipField(byte[] body, int offs, ClassDescriptor.FieldDescriptor fd, ClassDescriptor.FieldType type)
        {
            int len;
            switch (type)
            {
#if NET_FRAMEWORK_20
                case ClassDescriptor.FieldType.tpNullableBoolean:
                case ClassDescriptor.FieldType.tpNullableByte:
                case ClassDescriptor.FieldType.tpNullableSByte:
                    if (body[offs++] != 0)
                    {
                        offs += 1;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableChar:
                case ClassDescriptor.FieldType.tpNullableShort:
                case ClassDescriptor.FieldType.tpNullableUShort:
                    if (body[offs++] != 0)
                    {
                        offs += 2;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableInt:
                case ClassDescriptor.FieldType.tpNullableUInt:
                case ClassDescriptor.FieldType.tpNullableEnum:
                case ClassDescriptor.FieldType.tpNullableFloat:
                    if (body[offs++] != 0)
                    {
                        offs += 4;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableLong:
                case ClassDescriptor.FieldType.tpNullableULong:
                case ClassDescriptor.FieldType.tpNullableDouble:
                case ClassDescriptor.FieldType.tpNullableDate:
                    if (body[offs++] != 0)
                    {
                        offs += 8;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableDecimal:
                case ClassDescriptor.FieldType.tpNullableGuid:
                    if (body[offs++] != 0)
                    {
                        offs += 16;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableValue:
                    if (body[offs++] != 0)
                    {
                        offs = unpackObject(null, fd.valueDesc, false, body, offs, null);
                    }
                    break;
#endif
                case ClassDescriptor.FieldType.tpBoolean:
                case ClassDescriptor.FieldType.tpByte:
                case ClassDescriptor.FieldType.tpSByte:
                    return offs + 1;
                case ClassDescriptor.FieldType.tpChar:
                case ClassDescriptor.FieldType.tpShort:
                case ClassDescriptor.FieldType.tpUShort:
                    return offs + 2;
                case ClassDescriptor.FieldType.tpInt:
                case ClassDescriptor.FieldType.tpUInt:
                case ClassDescriptor.FieldType.tpFloat:
                    return offs + 4;
                case ClassDescriptor.FieldType.tpObject:
                case ClassDescriptor.FieldType.tpOid:
                    return skipObjectReference(body, offs);
                case ClassDescriptor.FieldType.tpLong:
                case ClassDescriptor.FieldType.tpULong:
                case ClassDescriptor.FieldType.tpDouble:
                case ClassDescriptor.FieldType.tpDate:
                    return offs + 8;
                case ClassDescriptor.FieldType.tpDecimal:
                case ClassDescriptor.FieldType.tpGuid:
                    return offs + 16;
                case ClassDescriptor.FieldType.tpString:
                case ClassDescriptor.FieldType.tpType:
                    return Bytes.skipString(body, offs);
                case ClassDescriptor.FieldType.tpValue:
                    return unpackObject(null, fd.valueDesc, false, body, offs, null);
#if SUPPORT_RAW_TYPE
                case ClassDescriptor.FieldType.tpRaw:
#endif
                case ClassDescriptor.FieldType.tpArrayOfByte:
                case ClassDescriptor.FieldType.tpArrayOfSByte:
                case ClassDescriptor.FieldType.tpArrayOfBoolean:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len > 0)
                    {
                        offs += len;
                    }
                    else if (len < -1)
                    {
                        offs += ClassDescriptor.Sizeof[-2 - len];
                    }
                    break;
                case ClassDescriptor.FieldType.tpCustom:
                    {
                        MemoryReader reader = new MemoryReader(this, body, offs, null, false, false);
                        serializer.Unpack(reader);
                        offs = reader.Position;
                        break;
                    }
                case ClassDescriptor.FieldType.tpArrayOfShort:
                case ClassDescriptor.FieldType.tpArrayOfUShort:
                case ClassDescriptor.FieldType.tpArrayOfChar:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len > 0)
                    {
                        offs += len * 2;
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfObject:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    for (int j = 0; j < len; j++)
                    {
                        offs = skipObjectReference(body, offs);
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfInt:
                case ClassDescriptor.FieldType.tpArrayOfUInt:
                case ClassDescriptor.FieldType.tpArrayOfFloat:
                case ClassDescriptor.FieldType.tpArrayOfOid:
                case ClassDescriptor.FieldType.tpLink:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len > 0)
                    {
                        offs += len * 4;
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfLong:
                case ClassDescriptor.FieldType.tpArrayOfULong:
                case ClassDescriptor.FieldType.tpArrayOfDouble:
                case ClassDescriptor.FieldType.tpArrayOfDate:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len > 0)
                    {
                        offs += len * 8;
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfString:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    for (int j = 0; j < len; j++)
                    {
                        offs = Bytes.skipString(body, offs);
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfValue:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len > 0)
                    {
                        ClassDescriptor valueDesc = fd.valueDesc;
                        for (int j = 0; j < len; j++)
                        {
                            offs = unpackObject(null, valueDesc, false, body, offs, null);
                        }
                    }
                    break;
            }
            return offs;
        }

#if SUPPORT_RAW_TYPE
        private int unpackRawValue(byte[] body, int offs, out object val, bool recursiveLoading)
        {
            int len = Bytes.unpack4(body, offs);
            offs += 4;
            if (len >= 0)
            {
                MemoryStream ms = new MemoryStream(body, offs, len);
                val = objectFormatter.Deserialize(ms);
                ms.Close();
                offs += len;
            }
            else
            {
                switch ((ClassDescriptor.FieldType)(-2 - len))
                {
                    case ClassDescriptor.FieldType.tpBoolean:
                        val = body[offs++] != 0;
                        break;
                    case ClassDescriptor.FieldType.tpByte:
                        val = body[offs++];
                        break;
                    case ClassDescriptor.FieldType.tpSByte:
                        val = (sbyte)body[offs++];
                        break;
                    case ClassDescriptor.FieldType.tpChar:
                        val = (char)Bytes.unpack2(body, offs);
                        offs += 2;
                        break;
                    case ClassDescriptor.FieldType.tpShort:
                        val = Bytes.unpack2(body, offs);
                        offs += 2;
                        break;
                    case ClassDescriptor.FieldType.tpUShort:
                        val = (ushort)Bytes.unpack2(body, offs);
                        offs += 2;
                        break;
                    case ClassDescriptor.FieldType.tpInt:
                    case ClassDescriptor.FieldType.tpOid:
                        val = Bytes.unpack4(body, offs);
                        offs += 4;
                        break;
                    case ClassDescriptor.FieldType.tpUInt:
                        val = (uint)Bytes.unpack4(body, offs);
                        offs += 4;
                        break;
                    case ClassDescriptor.FieldType.tpLong:
                        val = Bytes.unpack8(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpULong:
                        val = (ulong)Bytes.unpack8(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpFloat:
                        val = Bytes.unpackF4(body, offs);
                        offs += 4;
                        break;
                    case ClassDescriptor.FieldType.tpDouble:
                        val = Bytes.unpackF8(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpDate:
                        val = Bytes.unpackDate(body, offs);
                        offs += 8;
                        break;
                    case ClassDescriptor.FieldType.tpGuid:
                        val = Bytes.unpackGuid(body, offs);
                        offs += 16;
                        break;
                    case ClassDescriptor.FieldType.tpDecimal:
                        val = Bytes.unpackDecimal(body, offs);
                        offs += 16;
                        break;
                    case ClassDescriptor.FieldType.tpObject:
                        val = unswizzle(Bytes.unpack4(body, offs),
                            typeof(Persistent),
                            recursiveLoading);
                        offs += 4;
                        break;
                    default:
                        val = null;
                        break;
                }
            }
            return offs;
        }
#endif

        public int unpackField(byte[] body, int offs, bool recursiveLoading, ref object val, ClassDescriptor.FieldDescriptor fd, ClassDescriptor.FieldType type, object parent)
        {
            int len;
            switch (type)
            {
#if NET_FRAMEWORK_20
                case ClassDescriptor.FieldType.tpNullableBoolean:
                    val = body[offs++] == 0 ? null : (object)(body[offs++] != 0);
                    break;
                case ClassDescriptor.FieldType.tpNullableByte:
                    val = body[offs++] == 0 ? null : (object)body[offs++];
                    break;
                case ClassDescriptor.FieldType.tpNullableSByte:
                    val = body[offs++] == 0 ? null : (object)(sbyte)body[offs++];
                    break;
                case ClassDescriptor.FieldType.tpNullableChar:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = (char)Bytes.unpack2(body, offs);
                        offs += 2;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableShort:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpack2(body, offs);
                        offs += 2;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableUShort:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = (ushort)Bytes.unpack2(body, offs);
                        offs += 2;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableInt:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpack4(body, offs);
                        offs += 4;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableUInt:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = (uint)Bytes.unpack4(body, offs);
                        offs += 4;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableLong:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpack8(body, offs);
                        offs += 8;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableULong:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = (ulong)Bytes.unpack8(body, offs);
                        offs += 8;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableFloat:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpackF4(body, offs);
                        offs += 4;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableDouble:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpackF8(body, offs);
                        offs += 8;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableDate:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpackDate(body, offs);
                        offs += 8;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableGuid:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpackGuid(body, offs);
                        offs += 16;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableDecimal:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Bytes.unpackDecimal(body, offs);
                        offs += 16;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableEnum:
                    if (body[offs++] == 0) {
                        val = null;
                    } else {
                        val = Enum.ToObject(fd.MemberType, Bytes.unpack4(body, offs));
                        offs += 4;
                    }
                    break;
                case ClassDescriptor.FieldType.tpNullableValue:
                    if (body[offs++] != 0)
                    {
                        val = fd.valueDesc.newInstance();
                        offs = unpackObject(val, fd.valueDesc, recursiveLoading, body, offs, parent);
                    }
                    else
                    {
                        val = null;
                    }
                    break;
#endif
                case ClassDescriptor.FieldType.tpBoolean:
                    val = body[offs++] != 0;
                    break;

                case ClassDescriptor.FieldType.tpByte:
                    val = body[offs++];
                    break;

                case ClassDescriptor.FieldType.tpSByte:
                    val = (sbyte)body[offs++];
                    break;

                case ClassDescriptor.FieldType.tpChar:
                    val = (char)Bytes.unpack2(body, offs);
                    offs += 2;
                    break;

                case ClassDescriptor.FieldType.tpShort:
                    val = Bytes.unpack2(body, offs);
                    offs += 2;
                    break;

                case ClassDescriptor.FieldType.tpUShort:
                    val = (ushort)Bytes.unpack2(body, offs);
                    offs += 2;
                    break;

                case ClassDescriptor.FieldType.tpEnum:
                    val = Enum.ToObject(fd.MemberType, Bytes.unpack4(body, offs));
                    offs += 4;
                    break;

                case ClassDescriptor.FieldType.tpInt:
                case ClassDescriptor.FieldType.tpOid:
                    val = Bytes.unpack4(body, offs);
                    offs += 4;
                    break;

                case ClassDescriptor.FieldType.tpUInt:
                    val = (uint)Bytes.unpack4(body, offs);
                    offs += 4;
                    break;

                case ClassDescriptor.FieldType.tpLong:
                    val = Bytes.unpack8(body, offs);
                    offs += 8;
                    break;

                case ClassDescriptor.FieldType.tpULong:
                    val = (ulong)Bytes.unpack8(body, offs);
                    offs += 8;
                    break;

                case ClassDescriptor.FieldType.tpFloat:
                    val = Bytes.unpackF4(body, offs);
                    offs += 4;
                    break;

                case ClassDescriptor.FieldType.tpDouble:
                    val = Bytes.unpackF8(body, offs);
                    offs += 8;
                    break;

                case ClassDescriptor.FieldType.tpDecimal:
                    val = Bytes.unpackDecimal(body, offs);
                    offs += 16;
                    break;

                case ClassDescriptor.FieldType.tpGuid:
                    val = Bytes.unpackGuid(body, offs);
                    offs += 16;
                    break;

                case ClassDescriptor.FieldType.tpString:
                    {
                        string str;
                        offs = Bytes.unpackString(body, offs, out str, encoding);
                        val = str;
                        break;
                    }

                case ClassDescriptor.FieldType.tpType:
                    {
                        string str;
                        offs = Bytes.unpackString(body, offs, out str, encoding);
                        val = ClassDescriptor.lookup(this, str);
                        break;
                    }

                case ClassDescriptor.FieldType.tpDate:
                    val = Bytes.unpackDate(body, offs);
                    offs += 8;
                    break;

                case ClassDescriptor.FieldType.tpObject:
                    if (fd == null)
                    {
                        val = unswizzle(body, ref offs, typeof(object), parent, recursiveLoading);
                    }
                    else
                    {
                        val = unswizzle(body, ref offs, fd.MemberType, parent, fd.recursiveLoading | recursiveLoading);
                    }
                    break;

                case ClassDescriptor.FieldType.tpValue:
                    val = fd.GetValue(val);
                    offs = unpackObject(val, fd.valueDesc, recursiveLoading, body, offs, parent);
                    break;

#if SUPPORT_RAW_TYPE
                case ClassDescriptor.FieldType.tpRaw:
                    offs = unpackRawValue(body, offs, out val, recursiveLoading);
                    break;
#endif

                case ClassDescriptor.FieldType.tpCustom:
                    {
                        MemoryReader reader = new MemoryReader(this, body, offs, parent, fd.recursiveLoading | recursiveLoading, false);
                        val = serializer.Unpack(reader);
                        offs = reader.Position;
                        break;
                    }
                case ClassDescriptor.FieldType.tpArrayOfByte:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        byte[] arr = new byte[len];
                        Array.Copy(body, offs, arr, 0, len);
                        offs += len;
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfSByte:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        sbyte[] arr = new sbyte[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = (sbyte)body[offs++];
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfBoolean:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        bool[] arr = new bool[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = body[offs++] != 0;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfShort:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        short[] arr = new short[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpack2(body, offs);
                            offs += 2;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfUShort:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        ushort[] arr = new ushort[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = (ushort)Bytes.unpack2(body, offs);
                            offs += 2;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfChar:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        char[] arr = new char[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = (char)Bytes.unpack2(body, offs);
                            offs += 2;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfEnum:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        System.Type elemType = fd.MemberType.GetElementType();
                        Array arr = Array.CreateInstance(elemType, len);
                        for (int j = 0; j < len; j++)
                        {
                            arr.SetValue(Enum.ToObject(elemType, Bytes.unpack4(body, offs)), j);
                            offs += 4;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfInt:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        int[] arr = new int[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpack4(body, offs);
                            offs += 4;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfUInt:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        uint[] arr = new uint[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = (uint)Bytes.unpack4(body, offs);
                            offs += 4;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfLong:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        long[] arr = new long[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpack8(body, offs);
                            offs += 8;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfULong:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        ulong[] arr = new ulong[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = (ulong)Bytes.unpack8(body, offs);
                            offs += 8;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfFloat:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        float[] arr = new float[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpackF4(body, offs);
                            offs += 4;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfDouble:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        double[] arr = new double[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpackF8(body, offs);
                            offs += 8;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfDate:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        System.DateTime[] arr = new System.DateTime[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpackDate(body, offs);
                            offs += 8;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfString:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        string[] arr = new string[len];
                        for (int j = 0; j < len; j++)
                        {
                            offs = Bytes.unpackString(body, offs, out arr[j], encoding);
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfDecimal:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        decimal[] arr = new decimal[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpackDecimal(body, offs);
                            offs += 16;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfGuid:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        Guid[] arr = new Guid[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpackGuid(body, offs);
                            offs += 16;
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfObject:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        Type elemType = fd.MemberType.GetElementType();
                        object[] arr = (object[])Array.CreateInstance(elemType, len);
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = unswizzle(body, ref offs, elemType, parent, recursiveLoading);
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfValue:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        Type elemType = fd.MemberType.GetElementType();
                        Array arr = Array.CreateInstance(elemType, len);
                        ClassDescriptor valueDesc = fd.valueDesc;
                        for (int j = 0; j < len; j++)
                        {
                            object elem = arr.GetValue(j);
                            offs = unpackObject(elem, valueDesc, recursiveLoading, body, offs, parent);
                            arr.SetValue(elem, j);
                        }
                        val = arr;
                    }
                    break;

                case ClassDescriptor.FieldType.tpLink:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        object[] arr = new object[len];
                        for (int j = 0; j < len; j++)
                        {
                            int elemOid = Bytes.unpack4(body, offs);
                            offs += 4;
                            if (elemOid != 0)
                            {
                                arr[j] = new PersistentStub(this, elemOid);
                            }
                        }
#if USE_GENERICS
                        val = fd.constructor.Invoke(this, new object[]{arr, parent});
#else
                        val = new LinkImpl(this, arr, parent);
#endif
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfOid:
                    len = Bytes.unpack4(body, offs);
                    offs += 4;
                    if (len < 0)
                    {
                        val = null;
                    }
                    else
                    {
                        int[] arr = new int[len];
                        for (int j = 0; j < len; j++)
                        {
                            arr[j] = Bytes.unpack4(body, offs);
                            offs += 4;
                        }
#if USE_GENERICS
                        val = fd.constructor.Invoke(this, new object[]{arr, parent});
#else
                        val = new PArrayImpl(this, arr, parent);
#endif
                    }
                    break;
            }
            return offs;
        }

        internal byte[] packObject(object obj, bool finalized)
        {
            ByteBuffer buf = new ByteBuffer(this, obj, finalized);
            int offs = ObjectHeader.Sizeof;
            buf.extend(offs);
            ClassDescriptor desc = getClassDescriptor(obj.GetType());
#if !COMPACT_NET_FRAMEWORK && !WP7
            if (desc.serializer != null)
            {
                offs = desc.serializer.pack(this, obj, buf);
            }
            else
#endif
            if (obj is ISelfSerializable)
            {
                ((ISelfSerializable)obj).Pack(buf.GetWriter());
                offs = buf.used;
            }
            else if (desc.customSerializable)
            {
                serializer.Pack(obj, buf.GetWriter());
                offs = buf.used;
            }
            else
            {
                offs = packObject(obj, desc, offs, buf);
            }
            ObjectHeader.setSize(buf.arr, 0, offs);
            ObjectHeader.setType(buf.arr, 0, desc.Oid);
            return buf.arr;
        }

        public int packObject(object obj, ClassDescriptor desc, int offs, ByteBuffer buf)
        {
            ClassDescriptor.FieldDescriptor[] flds = desc.allFields;

            for (int i = 0, n = flds.Length; i < n; i++)
            {
                ClassDescriptor.FieldDescriptor fd = flds[i];
                offs = packField(buf, offs, fd.GetValue(obj), fd, fd.type);
            }
            return offs;
        }

#if SUPPORT_RAW_TYPE
        public int packRawValue(ByteBuffer buf, int offs, object val, bool finalized)
        {
            if (val == null)
            {
                buf.extend(offs + 4);
                Bytes.pack4(buf.arr, offs, -1);
                offs += 4;
            }
            else if (val is IPersistent)
            {
                buf.extend(offs + 8);
                Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpObject);
                Bytes.pack4(buf.arr, offs + 4, swizzle((IPersistent)val, finalized));
                offs += 8;
            }
            else
            {
                Type t = val.GetType();
                if (t == typeof(bool))
                {
                    buf.extend(offs + 5);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpBoolean);
                    buf.arr[offs + 4] = (byte)((bool)val ? 1 : 0);
                    offs += 5;
                }
                else if (t == typeof(char))
                {
                    buf.extend(offs + 6);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpChar);
                    Bytes.pack2(buf.arr, offs + 4, (short)(char)val);
                    offs += 6;
                }
                else if (t == typeof(byte))
                {
                    buf.extend(offs + 5);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpByte);
                    buf.arr[offs + 4] = (byte)val;
                    offs += 5;
                }
                else if (t == typeof(sbyte))
                {
                    buf.extend(offs + 5);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpSByte);
                    buf.arr[offs + 4] = (byte)(sbyte)val;
                    offs += 5;
                }
                else if (t == typeof(short))
                {
                    buf.extend(offs + 6);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpShort);
                    Bytes.pack2(buf.arr, offs + 4, (short)val);
                    offs += 6;
                }
                else if (t == typeof(ushort))
                {
                    buf.extend(offs + 6);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpUShort);
                    Bytes.pack2(buf.arr, offs + 4, (short)(ushort)val);
                    offs += 6;
                }
                else if (t == typeof(int))
                {
                    buf.extend(offs + 8);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpInt);
                    Bytes.pack4(buf.arr, offs + 4, (int)val);
                    offs += 8;
                }
                else if (t == typeof(uint))
                {
                    buf.extend(offs + 8);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpUInt);
                    Bytes.pack4(buf.arr, offs + 4, (int)(uint)val);
                    offs += 8;
                }
                else if (t == typeof(long))
                {
                    buf.extend(offs + 12);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpLong);
                    Bytes.pack8(buf.arr, offs + 4, (long)val);
                    offs += 12;
                }
                else if (t == typeof(ulong))
                {
                    buf.extend(offs + 12);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpULong);
                    Bytes.pack8(buf.arr, offs + 4, (long)(ulong)val);
                    offs += 12;
                }
                else if (t == typeof(float))
                {
                    buf.extend(offs + 8);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpFloat);
                    Bytes.packF4(buf.arr, offs + 4, (float)val);
                    offs += 8;
                }
                else if (t == typeof(double))
                {
                    buf.extend(offs + 12);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpDouble);
                    Bytes.packF8(buf.arr, offs + 4, (double)val);
                    offs += 12;
                }
                else if (t == typeof(DateTime))
                {
                    buf.extend(offs + 12);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpDate);
                    Bytes.packDate(buf.arr, offs + 4, (DateTime)val);
                    offs += 12;
                }
                else if (t == typeof(Guid))
                {
                    buf.extend(offs + 20);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpGuid);
                    Bytes.packGuid(buf.arr, offs + 4, (Guid)val);
                    offs += 20;
                }
                else if (t == typeof(Decimal))
                {
                    buf.extend(offs + 20);
                    Bytes.pack4(buf.arr, offs, -2 - (int)ClassDescriptor.FieldType.tpDecimal);
                    Bytes.packDecimal(buf.arr, offs + 4, (decimal)val);
                    offs += 20;
                }
                else
                {
                    System.IO.MemoryStream ms = new System.IO.MemoryStream();
                    objectFormatter.Serialize(ms, val);
                    ms.Close();
                    byte[] arr = ms.ToArray();
                    int len = arr.Length;
                    buf.extend(offs + 4 + len);
                    Bytes.pack4(buf.arr, offs, len);
                    offs += 4;
                    Array.Copy(arr, 0, buf.arr, offs, len);
                    offs += len;
                }
            }
            return offs;
        }
#endif

        public int packField(ByteBuffer buf, int offs, object val, ClassDescriptor.FieldDescriptor fd, ClassDescriptor.FieldType type)
        {
            switch (type)
            {
#if NET_FRAMEWORK_20
                case ClassDescriptor.FieldType.tpNullableByte:
                    return val == null ? buf.packI1(offs, 0) : buf.packI1(buf.packI1(offs, 1), (byte)val);
                case ClassDescriptor.FieldType.tpNullableSByte:
                    return val == null ? buf.packI1(offs, 0) : buf.packI1(buf.packI1(offs, 1), (sbyte)val);
                case ClassDescriptor.FieldType.tpNullableBoolean:
                    return val == null ? buf.packI1(offs, 0) : buf.packBool(buf.packI1(offs, 1), (bool)val);
                case ClassDescriptor.FieldType.tpNullableShort:
                    return val == null ? buf.packI1(offs, 0) : buf.packI2(buf.packI1(offs, 1), (short)val);
                case ClassDescriptor.FieldType.tpNullableUShort:
                    return val == null ? buf.packI1(offs, 0) : buf.packI2(buf.packI1(offs, 1), (ushort)val);
                case ClassDescriptor.FieldType.tpNullableChar:
                    return val == null ? buf.packI1(offs, 0) : buf.packI2(buf.packI1(offs, 1), (char)val);
                case ClassDescriptor.FieldType.tpNullableEnum:
                case ClassDescriptor.FieldType.tpNullableInt:
                    return val == null ? buf.packI1(offs, 0) : buf.packI4(buf.packI1(offs, 1), (int)val);
                case ClassDescriptor.FieldType.tpNullableUInt:
                    return val == null ? buf.packI1(offs, 0) : buf.packI4(buf.packI1(offs, 1), (int)(uint)val);
                case ClassDescriptor.FieldType.tpNullableLong:
                    return val == null ? buf.packI1(offs, 0) : buf.packI8(buf.packI1(offs, 1), (long)val);
                case ClassDescriptor.FieldType.tpNullableULong:
                    return val == null ? buf.packI1(offs, 0) : buf.packI8(buf.packI1(offs, 1), (long)(ulong)val);
                case ClassDescriptor.FieldType.tpNullableFloat:
                    return val == null ? buf.packI1(offs, 0) : buf.packF4(buf.packI1(offs, 1), (float)val);
                case ClassDescriptor.FieldType.tpNullableDouble:
                    return val == null ? buf.packI1(offs, 0) : buf.packF8(buf.packI1(offs, 1), (double)val);
                case ClassDescriptor.FieldType.tpNullableDecimal:
                    return val == null ? buf.packI1(offs, 0) : buf.packDecimal(buf.packI1(offs, 1), (decimal)val);
                case ClassDescriptor.FieldType.tpNullableGuid:
                    return val == null ? buf.packI1(offs, 0) : buf.packGuid(buf.packI1(offs, 1), (Guid)val);
                case ClassDescriptor.FieldType.tpNullableDate:
                    return val == null ? buf.packI1(offs, 0) : buf.packDate(buf.packI1(offs, 1), (DateTime)val);
                case ClassDescriptor.FieldType.tpNullableValue:
                    return val == null ? buf.packI1(offs, 0) : packObject(val, fd.valueDesc, buf.packI1(offs, 1), buf);
 #endif
                case ClassDescriptor.FieldType.tpByte:
                    return buf.packI1(offs, (byte)val);
                case ClassDescriptor.FieldType.tpSByte:
                    return buf.packI1(offs, (sbyte)val);
                case ClassDescriptor.FieldType.tpBoolean:
                    return buf.packBool(offs, (bool)val);
                case ClassDescriptor.FieldType.tpShort:
                    return buf.packI2(offs, (short)val);
                case ClassDescriptor.FieldType.tpUShort:
                    return buf.packI2(offs, (ushort)val);
                case ClassDescriptor.FieldType.tpChar:
                    return buf.packI2(offs, (char)val);
                case ClassDescriptor.FieldType.tpEnum:
                    return buf.packI4(offs, Convert.ToInt32(val));
                case ClassDescriptor.FieldType.tpInt:
                case ClassDescriptor.FieldType.tpOid:
                    return buf.packI4(offs, (int)val);
                case ClassDescriptor.FieldType.tpUInt:
                    return buf.packI4(offs, (int)(uint)val);
                case ClassDescriptor.FieldType.tpLong:
                    return buf.packI8(offs, (long)val);
                case ClassDescriptor.FieldType.tpULong:
                    return buf.packI8(offs, (long)(ulong)val);
                case ClassDescriptor.FieldType.tpFloat:
                    return buf.packF4(offs, (float)val);
                case ClassDescriptor.FieldType.tpDouble:
                    return buf.packF8(offs, (double)val);
                case ClassDescriptor.FieldType.tpDecimal:
                    return buf.packDecimal(offs, (decimal)val);
                case ClassDescriptor.FieldType.tpGuid:
                    return buf.packGuid(offs, (Guid)val);
                case ClassDescriptor.FieldType.tpDate:
                    return buf.packDate(offs, (DateTime)val);
                case ClassDescriptor.FieldType.tpString:
                    return buf.packString(offs, (string)val);
                case ClassDescriptor.FieldType.tpType:
                    return buf.packString(offs, ClassDescriptor.getTypeName((Type)val));
                case ClassDescriptor.FieldType.tpValue:
                    return packObject(val, fd.valueDesc, offs, buf);
                case ClassDescriptor.FieldType.tpObject:
                    return swizzle(buf, offs, val);

#if SUPPORT_RAW_TYPE
                case ClassDescriptor.FieldType.tpRaw:
                    offs = packRawValue(buf, offs, val, false);
                    break;
#endif
                case ClassDescriptor.FieldType.tpCustom:
                    {
                        serializer.Pack(val, buf.GetWriter());
                        offs = buf.used;
                        break;
                    }
                case ClassDescriptor.FieldType.tpArrayOfByte:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        byte[] arr = (byte[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        Array.Copy(arr, 0, buf.arr, offs, len);
                        offs += len;
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfSByte:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        sbyte[] arr = (sbyte[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++, offs++)
                        {
                            buf.arr[offs] = (byte)arr[j];
                        }
                        offs += len;
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfBoolean:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        bool[] arr = (bool[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++, offs++)
                        {
                            buf.arr[offs] = (byte)(arr[j] ? 1 : 0);
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfShort:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        short[] arr = (short[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 2);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack2(buf.arr, offs, arr[j]);
                            offs += 2;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfUShort:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        ushort[] arr = (ushort[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 2);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack2(buf.arr, offs, (short)arr[j]);
                            offs += 2;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfChar:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        char[] arr = (char[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 2);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack2(buf.arr, offs, (short)arr[j]);
                            offs += 2;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfEnum:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        Array arr = (Array)val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack4(buf.arr, offs, (int)arr.GetValue(j));
                            offs += 4;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfInt:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        int[] arr = (int[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack4(buf.arr, offs, arr[j]);
                            offs += 4;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfUInt:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        uint[] arr = (uint[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack4(buf.arr, offs, (int)arr[j]);
                            offs += 4;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfLong:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        long[] arr = (long[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 8);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack8(buf.arr, offs, arr[j]);
                            offs += 8;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfULong:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        ulong[] arr = (ulong[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 8);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack8(buf.arr, offs, (long)arr[j]);
                            offs += 8;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfFloat:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        float[] arr = (float[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.packF4(buf.arr, offs, arr[j]);
                            offs += 4;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfDouble:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        double[] arr = (double[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 8);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.packF8(buf.arr, offs, arr[j]);
                            offs += 8;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfValue:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        Array arr = (Array)val;
                        int len = arr.Length;
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        ClassDescriptor elemDesc = fd.valueDesc;
                        for (int j = 0; j < len; j++)
                        {
                            offs = packObject(arr.GetValue(j), elemDesc, offs, buf);
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfDate:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        DateTime[] arr = (DateTime[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 8);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.packDate(buf.arr, offs, arr[j]);
                            offs += 8;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfDecimal:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        decimal[] arr = (decimal[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 16);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.packDecimal(buf.arr, offs, arr[j]);
                            offs += 16;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfGuid:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        Guid[] arr = (Guid[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 16);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.packGuid(buf.arr, offs, arr[j]);
                            offs += 16;
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfString:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        string[] arr = (string[])val;
                        int len = arr.Length;
                        buf.extend(offs + 4 + len * 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            offs = buf.packString(offs, arr[j]);
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpArrayOfObject:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        object[] arr = (object[])val;
                        int len = arr.Length;
                        offs = buf.packI4(offs, len);
                        for (int j = 0; j < len; j++)
                        {
                            offs = swizzle(buf, offs, arr[j]);
                        }
                    }
                    break;

                case ClassDescriptor.FieldType.tpLink:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        GenericLink link = (GenericLink)val;
                        link.SetOwner(buf.parent);
                        int len = link.Size();
                        buf.extend(offs + 4 + len * 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack4(buf.arr, offs, swizzle(link.GetRaw(j), buf.finalized));
                            offs += 4;
                        }
                        if (!IsDeleted(buf.parent))
                        {
                            link.Unpin();
                        }
                    }
                    break;
                case ClassDescriptor.FieldType.tpArrayOfOid:
                    if (val == null)
                    {
                        buf.extend(offs + 4);
                        Bytes.pack4(buf.arr, offs, -1);
                        offs += 4;
                    }
                    else
                    {
                        GenericPArray arr = (GenericPArray)val;
                        arr.SetOwner(buf.parent);
                        int len = arr.Size();
                        buf.extend(offs + 4 + len * 4);
                        Bytes.pack4(buf.arr, offs, len);
                        offs += 4;
                        for (int j = 0; j < len; j++)
                        {
                            Bytes.pack4(buf.arr, offs, arr.GetOid(j));
                            offs += 4;
                        }
                    }
                    break;
            }
            return offs;
        }

        public ClassLoader Loader
        {

            set
            {
                loader = value;
            }

            get
            {
                return loader;
            }
        }

#if USE_GENERICS
        class HashEnumerator<T> : IEnumerator<T>, PersistentEnumerator where T:class
#else
        class HashEnumerator : PersistentEnumerator
#endif
        {
            StorageImpl db;
            IEnumerator oids;

            public HashEnumerator(StorageImpl db, Hashtable result)
            {
                this.db = db;
                oids = result.Keys.GetEnumerator();
            }

            public void Reset()
            {
                oids.Reset();
            }

            public bool MoveNext()
            {
                return oids.MoveNext();
            }

#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    return (T)db.lookupObject((int)oids.Current, null);
                }
            }

            public T Current
            {
                get
                {
                    return (T)db.lookupObject((int)oids.Current, null);
                }
            }
#else
            public object Current
            {
                get
                {
                    return db.lookupObject((int)oids.Current, null);
                }
            }
#endif

            public int CurrentOid
            {
                get
                {
                    return (int)oids.Current;
                }
            }

            public void Dispose()
            {
            }
        }

#if USE_GENERICS
        public IEnumerator<T> Merge<T>(IEnumerator<T>[] selections) where T:class
#else
        public IEnumerator Merge(IEnumerator[] selections)
#endif
        {
            Hashtable result = null;
            foreach (PersistentEnumerator e in selections)
            {
                Hashtable newResult = new Hashtable();
                while (e.MoveNext())
                {
                    int oid = e.CurrentOid;
                    if (result == null || result.ContainsKey(oid))
                    {
                        newResult[oid] = this;
                    }
                }
                result = newResult;
                if (result.Count == 0)
                {
                    break;
                }
            }
            if (result == null)
            {
                result = new Hashtable();
            }
#if USE_GENERICS
             return new HashEnumerator<T>(this, result);
#else
            return new HashEnumerator(this, result);
#endif
        }

#if USE_GENERICS
        public IEnumerator<T> Join<T>(IEnumerator<T>[] selections) where T:class
#else
        public IEnumerator Join(IEnumerator[] selections)
#endif
        {
            Hashtable result = new Hashtable();
            foreach (PersistentEnumerator e in selections)
            {
                while (e.MoveNext())
                {
                    result[e.CurrentOid] = this;
                }
            }
#if USE_GENERICS
             return new HashEnumerator<T>(this, result);
#else
            return new HashEnumerator(this, result);
#endif
        }

        public void RegisterCustomAllocator(Type cls, CustomAllocator allocator)
        {
            lock (this)
            {
                lock (objectCache)
                {
                    ClassDescriptor desc = getClassDescriptor(cls);
                    desc.allocator = allocator;
                    storeObject0(desc, false);
                    if (customAllocatorMap == null)
                    {
                        customAllocatorMap = new Hashtable();
                        customAllocatorList = new ArrayList();
                    }
                    customAllocatorMap[cls] = allocator;
                    customAllocatorList.Add(allocator);
                    reserveLocation(allocator.SegmentBase, allocator.SegmentSize);
                    reservedChainLength = 0;
                }
            }
        }

        public CustomAllocator CreateBitmapAllocator(int quantum, long baseAddr, long extension, long limit)
        {
            return new BitmapCustomAllocator(this, quantum, baseAddr, extension, limit);
        }

        public void SetCustomSerializer(CustomSerializer serializer)
        {
            this.serializer = serializer;
        }

        public void Deallocate(object obj)
        {
            deallocateObject(obj);
        }

        public void Store(object obj)
        {
            if (obj is IPersistent)
            {
                ((IPersistent)obj).Store();
            }
            else
            {
                lock (this)
                {
                    lock (objectCache)
                    {
                        lock (objMap)
                        {
                            ObjectMap.Entry e = objMap.Put(obj);
                            if ((e.state & Persistent.ObjectState.RAW) != 0)
                            {
                                throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
                            }
                            storeObject(obj);
                            e.state &= ~Persistent.ObjectState.DIRTY;
                        }
                    }
                }
            }
        }

        internal void UnassignOid(object obj)
        {
            if (obj is IPersistent)
            {
                ((IPersistent)obj).UnassignOid();
            }
            else
            {
                objMap.Remove(obj);
            }
        }

        internal void AssignOid(object obj, int oid, bool raw)
        {
            if (obj is IPersistent)
            {
                ((IPersistent)obj).AssignOid(this, oid, raw);
            }
            else
            {
                lock (objMap)
                {
                    ObjectMap.Entry e = objMap.Put(obj);
                    e.oid = oid;
                    if (raw)
                    {
                        e.state = Persistent.ObjectState.RAW;
                    }
                }
            }
            if (listener != null)
            {
                listener.OnObjectAssignOid(obj);
            }
        }

        public void Modify(object obj)
        {
            if (obj is IPersistent)
            {
                ((IPersistent)obj).Modify();
            }
            else
            {
                if (useSerializableTransactions)
                {
                    ThreadTransactionContext ctx = TransactionContext;
                    if (ctx.nested != 0)
                    { // serializable transaction
                        ctx.modified.Add(obj);
                        return;
                    }
                }
                lock (this)
                {
                    lock (objectCache)
                    {
                        lock (objMap)
                        {
                            ObjectMap.Entry e = objMap.Put(obj);
                            if ((e.state & Persistent.ObjectState.DIRTY) == 0 && e.oid != 0)
                            {
                                if ((e.state & Persistent.ObjectState.RAW) != 0)
                                {
                                    throw new StorageError(StorageError.ErrorCode.ACCESS_TO_STUB);
                                }
                                Debug.Assert((e.state & Persistent.ObjectState.DELETED) == 0);
                                storeObject(obj);
                                e.state &= ~Persistent.ObjectState.DIRTY;
                            }
                        }
                    }
                }
            }
        }

        public void Invalidate(object obj)
        {
            if (obj is IPersistent)
            {
                ((IPersistent)obj).Invalidate();
            }
            else
            {
                lock (objMap)
                {
                    ObjectMap.Entry e = objMap.Put(obj);
                    e.state &= ~Persistent.ObjectState.DIRTY;
                    e.state |= Persistent.ObjectState.RAW;
                    e.pin = null;
                }
            }
        }

        public void Load(object obj)
        {
            if (obj is IPersistent)
            {
                ((IPersistent)obj).Load();
            }
            else
            {
                lock (objMap)
                {
                    ObjectMap.Entry e = objMap.Get(obj);
                    if (e == null || (e.state & Persistent.ObjectState.RAW) == 0 || e.oid == 0)
                    {
                        return;
                    }
                }
                loadObject(obj);
            }
        }

        internal bool IsLoaded(object obj)
        {
            if (obj is IPersistent)
            {
                IPersistent po = (IPersistent)obj;
                return !po.IsRaw() && po.IsPersistent();
            }
            else
            {
                lock (objMap)
                {
                    ObjectMap.Entry e = objMap.Get(obj);
                    return e != null && (e.state & Persistent.ObjectState.RAW) == 0 && e.oid != 0;
                }
            }
        }

        public int GetOid(object obj)
        {
            return (obj is IPersistent) ? ((IPersistent)obj).Oid : obj == null ? 0 : objMap.GetOid(obj);
        }

        public bool SetRecursiveLoading(Type type, bool enabled)
        {
            lock (recursiveLoadingPolicy)
            {
                object prevValue = recursiveLoadingPolicy[type];
                recursiveLoadingPolicy[type] = enabled;
                recursiveLoadingPolicyDefined = true;
                return prevValue == null ? true : (bool)prevValue;
            }
        }

        public SqlOptimizerParameters SqlOptimizerParams
        {
            get
            {
                return sqlOptimizerParams;
            }
        }

        internal bool IsPersistent(object obj)
        {
            return GetOid(obj) != 0;
        }

        internal bool IsDeleted(object obj)
        {
            return (obj is IPersistent) ? ((IPersistent)obj).IsDeleted() : obj == null ? false : (objMap.GetState(obj) & Persistent.ObjectState.DELETED) != 0;
        }

        internal bool RecursiveLoading(object obj)
        {
            if (recursiveLoadingPolicyDefined)
            {
                lock (recursiveLoadingPolicy)
                {
                    Type type = obj.GetType();
                    do {
                        object enabled = recursiveLoadingPolicy[type];
                        if (enabled != null)
                        {
                            return (bool)enabled;
                        }
#if WINRT_NET_FRAMEWORK
                    } while ((type = type.GetTypeInfo().BaseType) != null);
#else
                    } while ((type = type.BaseType) != null);
#endif
                }
            }
            return (obj is IPersistent) ? ((IPersistent)obj).RecursiveLoading() : true;
        }

        internal bool IsModified(object obj)
        {
            return (obj is IPersistent) ? ((IPersistent)obj).IsModified() : obj == null ? false : (objMap.GetState(obj) & Persistent.ObjectState.DIRTY) != 0;
        }

        internal bool IsRaw(object obj)
        {
            return (obj is IPersistent) ? ((IPersistent)obj).IsRaw() : obj == null ? false : (objMap.GetState(obj) & Persistent.ObjectState.RAW) != 0;
        }


        private ObjectMap objMap;

        private int initIndexSize = dbDefaultInitIndexSize;
        private int objectCacheInitSize = dbDefaultObjectCacheInitSize;
        private long extensionQuantum = dbDefaultExtensionQuantum;
        private string cacheKind = "default";
        internal FileParameters fileParameters;
        private bool multiclientSupport = false;
        private bool alternativeBtree = false;
        private bool backgroundGc = false;
        private long pagePoolLruLimit = dbDefaultPagePoolLruLimit;
        private bool reloadObjectsOnRollback = false;
        private bool reuseOid = true;
        internal bool ignoreMissedClasses = false;
        private bool serializeSystemCollections = true;

        internal SqlOptimizerParameters sqlOptimizerParams = new SqlOptimizerParameters();

        private Hashtable customAllocatorMap;
        private ArrayList customAllocatorList;
        private CustomAllocator defaultAllocator;

        private Location reservedChain;
        private int      reservedChainLength;

        private CloneNode cloneList;
        private bool insideCloneBitmap;

        internal bool replicationAck = false;
        internal bool concurrentIterator = false;
        internal int slaveConnectionTimeout = 60; // seconds
        internal int replicationReceiveTimeout = 5*1000; // milliseconds

        private Hashtable properties = new Hashtable();

        internal PagePool pool;
        internal Header header; // base address of database file mapping
        internal int[] dirtyPagesMap; // bitmap of changed pages in current index
        internal bool modified;

        internal int currRBitmapPage; //current bitmap page for allocating records
        internal int currRBitmapOffs; //offset in current bitmap page for allocating unaligned records
        internal int currPBitmapPage; //current bitmap page for allocating page objects
        internal int currPBitmapOffs; //offset in current bitmap page for allocating page objects

        internal int committedIndexSize;
        internal int currIndexSize;

        internal enum RuntimeCodeGeneration
        {
            Disabled,
            Synchronous,
            Asynchronous
        };

        internal RuntimeCodeGeneration runtimeCodeGeneration = RuntimeCodeGeneration.Asynchronous;

#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
        internal static ArrayList assemblies;
        CNetMonitor transactionMonitor;
#if !WP7
        Hashtable wrapperHash = new Hashtable();
#endif
#else
        internal Thread codeGenerationThread;
        object transactionMonitor;
        Hashtable wrapperHash = new Hashtable();
#endif
        int nNestedTransactions;
        int nBlockedTransactions;
        int nCommittedTransactions;
        long scheduledCommitTime;
        PersistentResource transactionLock;

#if SUPPORT_RAW_TYPE
        internal System.Runtime.Serialization.Formatters.Binary.BinaryFormatter objectFormatter;
#endif
        internal int currIndex; // copy of header.root, used to allow read access to the database
        // during transaction commit
        internal long usedSize; // total size of allocated objects since the beginning of the session
        internal int[] bitmapPageAvailableSpace;
        internal bool opened;

        internal int[] greyBitmap; // bitmap of visited during GC but not yet marked object
        internal int[] blackBitmap;    // bitmap of objects marked during GC
        internal long gcThreshold;
        internal long allocatedDelta;
        internal bool gcDone;
        internal bool gcActive;
        internal bool gcGo;
        internal object backgroundGcMonitor;
        internal object backgroundGcStartMonitor;
#if !WINRT_NET_FRAMEWORK
        internal Thread gcThread;
#endif
        internal Encoding encoding;
        private int bitmapExtentBase;

        internal StorageListener listener;
        internal long transactionId;
        internal IFile file;

        internal CustomSerializer serializer;

        private ClassLoader loader;

        internal Hashtable resolvedTypes;
        internal Hashtable recursiveLoadingPolicy;
        internal bool      recursiveLoadingPolicyDefined;

        internal OidHashTable objectCache;
        internal Hashtable classDescMap;
        internal ClassDescriptor descList;
#if COMPACT_NET_FRAMEWORK
        internal static readonly LocalDataStoreSlot transactionContext = Thread.AllocateDataSlot();
#elif WP7
        internal static Dictionary<int, ThreadTransactionContext> transactionContext = new Dictionary<int,ThreadTransactionContext>();
#else
        [ThreadStatic]
        internal static ThreadTransactionContext transactionContext;
#endif
        internal bool useSerializableTransactions;


    }

    class RootPage
    {
        internal long size; // database file size
        internal long index; // offset to object index
        internal long shadowIndex; // offset to shadow index
        internal long usedSize; // size used by objects
        internal int indexSize; // size of object index
        internal int shadowIndexSize; // size of object index
        internal int indexUsed; // userd part of the index
        internal int freeList; // L1 list of free descriptors
        internal int bitmapEnd; // index of last allocated bitmap page
        internal int rootObject; // OID of root object
        internal int classDescList; // List of class descriptors
        internal int bitmapExtent;  // Offset of extended bitmap pages in object index

        internal const int Sizeof = 64;
    }

    class Header
    {
        internal int curr; // current root
        internal bool dirty; // database was not closed normally
        internal byte databaseFormatVersion;

        internal RootPage[] root;
        internal long       transactionId;

        internal static int Sizeof = 3 + RootPage.Sizeof * 2 + 8;

        internal void pack(byte[] rec)
        {
            int offs = 0;
            rec[offs++] = (byte)curr;
            rec[offs++] = (byte)(dirty ? 1 : 0);
            rec[offs++] = databaseFormatVersion;
            for (int i = 0; i < 2; i++)
            {
                Bytes.pack8(rec, offs, root[i].size);
                offs += 8;
                Bytes.pack8(rec, offs, root[i].index);
                offs += 8;
                Bytes.pack8(rec, offs, root[i].shadowIndex);
                offs += 8;
                Bytes.pack8(rec, offs, root[i].usedSize);
                offs += 8;
                Bytes.pack4(rec, offs, root[i].indexSize);
                offs += 4;
                Bytes.pack4(rec, offs, root[i].shadowIndexSize);
                offs += 4;
                Bytes.pack4(rec, offs, root[i].indexUsed);
                offs += 4;
                Bytes.pack4(rec, offs, root[i].freeList);
                offs += 4;
                Bytes.pack4(rec, offs, root[i].bitmapEnd);
                offs += 4;
                Bytes.pack4(rec, offs, root[i].rootObject);
                offs += 4;
                Bytes.pack4(rec, offs, root[i].classDescList);
                offs += 4;
                Bytes.pack4(rec, offs, root[i].bitmapExtent);
                offs += 4;
            }
            Bytes.pack8(rec, offs, transactionId);
            offs += 8;
            Debug.Assert(offs == Sizeof);
        }

        internal void unpack(byte[] rec)
        {
            int offs = 0;
            curr = rec[offs++];
            dirty = rec[offs++] != 0;
            databaseFormatVersion = rec[offs++];
            root = new RootPage[2];
            for (int i = 0; i < 2; i++)
            {
                root[i] = new RootPage();
                root[i].size = Bytes.unpack8(rec, offs);
                offs += 8;
                root[i].index = Bytes.unpack8(rec, offs);
                offs += 8;
                root[i].shadowIndex = Bytes.unpack8(rec, offs);
                offs += 8;
                root[i].usedSize = Bytes.unpack8(rec, offs);
                offs += 8;
                root[i].indexSize = Bytes.unpack4(rec, offs);
                offs += 4;
                root[i].shadowIndexSize = Bytes.unpack4(rec, offs);
                offs += 4;
                root[i].indexUsed = Bytes.unpack4(rec, offs);
                offs += 4;
                root[i].freeList = Bytes.unpack4(rec, offs);
                offs += 4;
                root[i].bitmapEnd = Bytes.unpack4(rec, offs);
                offs += 4;
                root[i].rootObject = Bytes.unpack4(rec, offs);
                offs += 4;
                root[i].classDescList = Bytes.unpack4(rec, offs);
                offs += 4;
                root[i].bitmapExtent = Bytes.unpack4(rec, offs);
                offs += 4;
            }
            transactionId = Bytes.unpack8(rec, offs);
            offs += 8;
            Debug.Assert(offs == Sizeof);
        }
    }
}