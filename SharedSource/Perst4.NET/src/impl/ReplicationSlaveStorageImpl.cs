namespace Perst.Impl
{
    using System;
    using System.Collections;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using Perst;
    
    public abstract class ReplicationSlaveStorageImpl : StorageImpl, ReplicationSlaveStorage
    { 
        internal const int DB_HDR_CURR_INDEX_OFFSET  = 0;
        internal const int DB_HDR_DIRTY_OFFSET       = 1;
        internal const int DB_HDR_INITIALIZED_OFFSET = 2;
        internal const int PAGE_DATA_OFFSET          = 8;
    
        internal const int REPL_CLOSE = -1;
        internal const int REPL_SYNC  = -2;
        internal const int REPL_PING  = -3;
        internal const int INIT_PAGE_TIMESTAMPS_LENGTH = 64*1024;

        public static int ListenQueueSize = 10;
        public static int LingerTime = 10; // linger parameter for the socket

        protected abstract Socket GetSocket();
        
        internal override  void setDirty() 
        {
            throw new StorageError(StorageError.ErrorCode.REPLICA_MODIFICATION);
        } 

        protected ReplicationSlaveStorageImpl(String pageTimestampFilePath) 
        { 
            if (pageTimestampFilePath != null) { 
                pageTimestampFile = new OSFile(pageTimestampFilePath,fileParameters);
                long fileLength = pageTimestampFile.Length;
                if (fileLength == 0) { 
                    pageTimestamps = new int[INIT_PAGE_TIMESTAMPS_LENGTH];
                } else {
                    pageTimestamps = new int[(int)(fileLength/4)];
                    byte[] page = new byte[Page.pageSize];
                    int i = 0;
                    for (long pos = 0; pos < fileLength; pos += Page.pageSize) { 
                        int rc = pageTimestampFile.Read(pos, page);
                        for (int offs = 0; offs < rc; offs += 4) { 
                            pageTimestamps[i++] = Bytes.unpack4(page, offs);
                        }
                    }
                    if (i != pageTimestamps.Length) { 
                        throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR);
                    }
                }
                dirtyPageTimestampMap = new int[(((pageTimestamps.Length*4 + Page.pageSize - 1) >> Page.pageSizeLog) + 31) >> 5];
            }
        }        
    

        public override void Open(IFile file, long pagePoolSize) 
        {
            if (opened) 
            {
                throw new StorageError(StorageError.ErrorCode.STORAGE_ALREADY_OPENED);
            }
            initialize(file, pagePoolSize);

            lck = new PersistentResource();
            init = new object();
            sync = new object();
            done = new object();
            commit = new object();
            listening = true;
            Connect();
            if (socket != null)
            {
                thread = new Thread(new ThreadStart(run));
                thread.Start();
                WaitSynchronizationCompletion();
                WaitInitializationCompletion();
            }
            opened = true;
            BeginThreadTransaction(TransactionMode.ReplicationSlave);
            reloadScheme();
            EndThreadTransaction();
        }


        /// <summary>
        /// Check if socket is connected to the master host
        /// @return <code>true</code> if connection between slave and master is sucessfully established
        /// </summary>
        public bool IsConnected() 
        {
            return socket != null;
        }
    
        public override void BeginThreadTransaction(TransactionMode mode)
        {
            if (mode != TransactionMode.ReplicationSlave) 
            {
                throw new ArgumentException("Illegal transaction mode");
            }
            lck.SharedLock();
            Page pg = pool.getPage(0);
            header.unpack(pg.data);
            pool.unfix(pg);
            currIndex = 1-header.curr;
            currIndexSize = header.root[1-currIndex].indexUsed;
            committedIndexSize = currIndexSize;
            usedSize = header.root[currIndex].size;
            objectCache.clear();
        }
     
        public override void EndThreadTransaction(int maxDelay)
        {
            lck.Unlock();
        }

        protected void WaitSynchronizationCompletion() 
        {
            lock(sync) 
            { 
                while (outOfSync) 
                { 
                    Monitor.Wait(sync);
                    if (!listening) { 
                        throw new StorageError(StorageError.ErrorCode.CONNECTION_FAILURE);
                    }
                }
            }
        }

        protected void WaitInitializationCompletion() 
        {
            lock(init) 
            { 
                while (!initialized) 
                { 
                    Monitor.Wait(init);
                    if (!listening) { 
                        throw new StorageError(StorageError.ErrorCode.CONNECTION_FAILURE);
                    }
                }
            }
        }

        /// <summary>
        /// Wait until database is modified by master
        /// This method blocks current thread until master node commits trasanction and
        /// this transanction is completely delivered to this slave node
        /// </summary>
        public void WaitForModification() 
        { 
            lock(commit) 
            { 
                if (socket != null) 
                { 
                    Monitor.Wait(commit);
                }
            }
        }

        protected virtual void cancelIO() {}

        void Connect()
        {
            try 
            { 
                socket = GetSocket();
                if (socket == null)
                {
                    HandleError();
                    return;
                }
                if (pageTimestamps != null && socket != null) 
                {
                    int size = pageTimestamps.Length;
                    byte[] psBuf = new byte[4 + size*4];
                    Bytes.pack4(psBuf, 0, size);
                    for (int i = 0; i < size; i++) { 
                         Bytes.pack4(psBuf, (i+1)*4, pageTimestamps[i]);
                    }
                    socket.Send(psBuf, 0, psBuf.Length, SocketFlags.None);
                }
                return;
            } 
            catch (SocketException x) 
            {                               
                Console.WriteLine("Connection failure: " + x);
            }
            HandleError();
            socket = null;
        }

        /// <summary>
        /// When overriden by base class this method perfroms socket error handling
        /// @return <code>true</code> if host should be reconnected and attempt to send data to it should be 
        /// repeated, <code>false</code> if no more attmpts to communicate with this host should be performed 
        /// </summary>     
        public virtual bool HandleError() 
        {
            return (listener != null) ? listener.ReplicationError(null) : false;
        }

        public void run() 
        { 
            try 
            {
                byte[] buf = new byte[Page.pageSize+PAGE_DATA_OFFSET + (pageTimestamps != null ? 4 : 0)];

                while (listening) 
                { 
                    int offs = 0;
                    do 
                    {
                        int rc = -1;
                        try 
                        { 
                            if (socket != null) 
                            { 
                                ArrayList recvSocks = new ArrayList(1);
                                recvSocks.Add(socket);
                                Socket.Select(recvSocks, null, null, replicationReceiveTimeout*2000);
                                if (recvSocks.Count == 1) 
                                {
                                    rc = socket.Receive(buf, offs, buf.Length - offs, SocketFlags.None);
                                }
                                else    
                                {
                                    Console.WriteLine("Receive timeout expired");
                                }
                            }
                        } 
                        catch (SocketException x) 
                        { 
                             Console.WriteLine("Failed to receive data from master: " + x);
                             rc = -1;
                        }
                        lock(done) 
                        { 
                            if (!listening) 
                            { 
                                return;
                            }
                        }
                        if (rc < 0) 
                        { 
                            HandleError(); 
                            hangup();
                            return;
                        } 
                        else 
                        { 
                            offs += rc;
                        }
                    } while (offs < buf.Length);
                
                    long pos = Bytes.unpack8(buf, 0);
                    bool transactionCommit = false;
                    if (pos == 0) 
                    { 
                        if (replicationAck) 
                        { 
                            try 
                            { 
                                socket.Send(buf, 0, 1, SocketFlags.None);
                            } 
                            catch (SocketException x) 
                            {
                                Console.WriteLine("Failed to send request to master: " + x);
                                HandleError();
                                hangup();
                                return;
                            }
                        }
                        if (buf[PAGE_DATA_OFFSET + DB_HDR_CURR_INDEX_OFFSET] != prevIndex) 
                        { 
                            prevIndex = buf[PAGE_DATA_OFFSET + DB_HDR_CURR_INDEX_OFFSET];
                            lck.ExclusiveLock();
                            transactionCommit = true;
                        }
                    } 
                    else if (pos == REPL_SYNC)     
                    { 
                        lock(sync) 
                        { 
                            outOfSync = false;
                            Monitor.Pulse(sync);
                        }
                        continue;   
                    }
                    else if (pos == REPL_PING)
                    {
                        continue;                          
                    }
                    else if (pos == REPL_CLOSE)
                    { 
                        hangup();
                        return;
                    }
                
                    if (pageTimestamps != null) 
                    { 
                        int pageNo = (int)(pos >> Page.pageSizeLog);
                        if (pageNo >= pageTimestamps.Length) 
                        { 
                            int newLength = pageNo >= pageTimestamps.Length*2 ? pageNo+1 : pageTimestamps.Length*2;
        
                            int[] newPageTimestamps = new int[newLength];
                            Array.Copy(pageTimestamps, 0, newPageTimestamps, 0, pageTimestamps.Length);
                            pageTimestamps = newPageTimestamps;
        
                            int[] newDirtyPageTimestampMap = new int[(((newLength*4 + Page.pageSize - 1) >> Page.pageSizeLog) + 31) >> 5];
                            Array.Copy(dirtyPageTimestampMap, 0, newDirtyPageTimestampMap, 0, dirtyPageTimestampMap.Length);
                            dirtyPageTimestampMap = newDirtyPageTimestampMap;                    
                        }
                        int timestamp = Bytes.unpack4(buf, Page.pageSize+PAGE_DATA_OFFSET);
                        pageTimestamps[pageNo] = timestamp;
                        dirtyPageTimestampMap[pageNo >> (Page.pageSizeLog - 2 + 5)] |= 1 << ((pageNo >> (Page.pageSizeLog - 2)) & 31);
                    }
    
                    Page pg = pool.putPage(pos);
                    Array.Copy(buf, PAGE_DATA_OFFSET, pg.data, 0, Page.pageSize);
                    pool.unfix(pg);
                
                    if (pos == 0) 
                    { 
                        if (!initialized && buf[PAGE_DATA_OFFSET + DB_HDR_INITIALIZED_OFFSET] != 0) 
                        { 
                            lock(init) 
                            { 
                                initialized = true;
                                Monitor.Pulse(init);
                            }
                        }
                        if (transactionCommit) 
                        { 
                            lck.Unlock();
                            lock(commit) 
                            { 
                                Monitor.PulseAll(commit);
                            }
                            if (listener != null) 
                            {
                                listener.OnMasterDatabaseUpdate();
                            } 
                            pool.flush();
                            if (pageTimestamps != null) 
                            { 
                                byte[] page = new byte[Page.pageSize];
                                for (int i = 0; i < dirtyPageTimestampMap.Length; i++) 
                                { 
                                    if (dirtyPageTimestampMap[i] != 0) 
                                    { 
                                        for (int j = 0; j < 32; j++) 
                                        { 
                                            if ((dirtyPageTimestampMap[i] & (1 << j)) != 0) 
                                            { 
                                                int pageNo = (i << 5) + j;
                                                int beg = pageNo << (Page.pageSizeLog - 2);
                                                int end = beg + Page.pageSize/4;
                                                if (end > pageTimestamps.Length) 
                                                { 
                                                    end = pageTimestamps.Length;
                                                }
                                                offs = 0;
                                                while (beg < end) 
                                                {
                                                    Bytes.pack4(page, offs, pageTimestamps[beg]);
                                                    beg += 1;
                                                    offs += 4;
                                                }
                                                pageTimestampFile.Write(pageNo << Page.pageSizeLog, page);
                                            }
                                        }
                                    }
                                    dirtyPageTimestampMap[i] = 0;
                                }
                                pageTimestampFile.Sync();
                            }
                        }
                    }
                }            
            }
            finally
            {
                listening = false;
                lock (init)
                {
                    Monitor.Pulse(init);
                }
                lock (sync) 
                {                
                    Monitor.Pulse(sync);                        
                }
            }
        }
        public override void Close() 
        {
            lock(done) 
            {
                listening = false;
            }
            cancelIO();            
            if (thread != null)
            {
                thread.Interrupt();
                thread.Join();
            }
            hangup();

            pool.flush();
            base.Close();
            if (pageTimestampFile != null) 
            { 
               pageTimestampFile.Close();
            }
        }

        private void hangup() 
        { 
            lock(commit) 
            { 
                Monitor.PulseAll(commit);
                if (socket != null) 
                { 
                    try 
                    { 
                        socket.Close();
                    } 
                    catch (SocketException) {}
                    socket = null;
                }
            }
        }

        protected override bool isDirty() 
        { 
            return false;
        }

        protected Socket       socket;
        protected bool         outOfSync;
        protected bool         initialized;
        protected bool         listening;
        protected object       sync;
        protected object       init;
        protected object       done;
        protected object       commit;
        protected int          prevIndex;
        protected IResource    lck;
        protected Thread       thread;
        protected int[]        pageTimestamps;
        protected int[]        dirtyPageTimestampMap;
        protected OSFile       pageTimestampFile;
    }
}

