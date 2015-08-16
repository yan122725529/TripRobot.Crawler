namespace Perst.Impl    
{
    using System;
    using System.Collections;
    using System.Net.Sockets;
    using System.Net;
    using System.Threading;
    using Perst;
	
    /// <summary>
    /// File performing replication of changed pages to specified slave nodes.
    /// </summary>
    public class ReplicationMasterFile : IFile 
    { 
        /// <summary>
        /// Constructor of replication master file
        /// </summary>
        /// <param name="storage">replication storage</param>
        /// <param name="file">local file used to store data locally</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        public ReplicationMasterFile(ReplicationMasterStorageImpl storage, IFile file, String pageTimestampFile) 
        : this(storage, file, storage.localhost, storage.port, storage.hosts, storage.replicationAck, pageTimestampFile)
        {
        }

        /// <summary>
        /// Constructor of replication master file
        /// </summary>
        /// <param name="file">local file used to store data locally</param>
        /// <param name="hosts">slave node hosts to which replicastion will be performed</param>
        /// <param name="ack">whether master should wait acknowledgment from slave node during trasanction commit</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        public ReplicationMasterFile(IFile file, string[] hosts, bool ack, String pageTimestampFile) 
            : this(null, file, null, -1, hosts, ack, pageTimestampFile)
        {
        }
        
        /// <summary>
        /// Constructor of replication master file
        /// </summary>
        /// <param name="file">local file used to store data locally</param>
        /// <param name="hosts">slave node hosts to which replicastion will be performed</param>
        /// <param name="ack">whether master should wait acknowledgment from slave node during trasanction commit</param>
        public ReplicationMasterFile(IFile file, string[] hosts, bool ack) 
            : this(null, file, null, -1, hosts, ack, null)
        {
        }
        
        private ReplicationMasterFile(ReplicationMasterStorageImpl storage, IFile file, string localhost, int port, string[] hosts, bool ack, String pageTimestampFilePath) 
        {         
            this.storage = storage;
            this.file = file;
            this.ack = ack;
            this.localhost = localhost;
            this.port = port;
            mutex = new object();
            replicas = new Replica[hosts.Length];
            rcBuf = new byte[1];
            nHosts = 0;
            if (pageTimestampFilePath != null) { 
                FileParameters fileParameters = storage != null ? storage.fileParameters : new FileParameters(false, false, false, 1024*1024);
                pageTimestampFile = new OSFile(pageTimestampFilePath, fileParameters);
                long fileLength = pageTimestampFile.Length;
                if (fileLength == 0) { 
                    pageTimestamps = new int[InitPageTimestampsLength];
                } else {
                    pageTimestamps = new int[(int)(fileLength/4)];
                    byte[] page = new byte[Page.pageSize];
                    int i = 0;
                    for (long pos = 0; pos < fileLength; pos += Page.pageSize) { 
                        int rc = pageTimestampFile.Read(pos, page);
                        for (int offs = 0; offs < rc; offs += 4, i++) { 
                            pageTimestamps[i] = Bytes.unpack4(page, offs);
                            if (pageTimestamps[i] > timestamp) { 
                                timestamp = pageTimestamps[i];
                            }
                        }
                    }
                    if (i != pageTimestamps.Length) { 
                        throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR);
                    }
                }
                dirtyPageTimestampMap = new int[(((pageTimestamps.Length*4 + Page.pageSize - 1) >> Page.pageSizeLog) + 31) >> 5];
                txBuf = new byte[12 + Page.pageSize];
            } else { 
                txBuf = new byte[8 + Page.pageSize];
            }
            for (int i = 0; i < hosts.Length; i++) 
            { 
                replicas[i] = new Replica();
                replicas[i].host = hosts[i];
                Connect(i);
            }
            if (port >= 0) 
            {
                storage.SetProperty("perst.alternative.btree", true); // prevent direct modification of pages
                listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listenSocket.Bind(new IPEndPoint(localhost != null && localhost != "localhost" ? IPAddress.Parse(localhost) : IPAddress.Any, port));          
                listenSocket.Listen(ListenQueueSize);
                listening = true;
                listenThread = new Thread(new ThreadStart(run));
                listenThread.Start();
            }
            watchdogThread = new Thread(new ThreadStart(watchdog));
            watchdogThread.Start();
        }

        public void watchdog()
        {
            lock (mutex) 
            { 
                 while (!shutdown)
                 {
                     Monitor.Wait(mutex, storage.replicationReceiveTimeout);
                     if (!shutdown) 
                     {     
                         Bytes.pack8(txBuf, 0, ReplicationSlaveStorageImpl.REPL_PING);
                         for (int i = 0; i < replicas.Length; i++)
                         {
                              Send(i, txBuf);
                         }
                    }
                }
            }
        }

        public void run() 
        { 
            while (true) 
            { 
                Socket s = null;
                try 
                { 
                    s = listenSocket.Accept();
                    s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);
                    s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger,
                                      new System.Net.Sockets.LingerOption(true, LingerTime));
                } 
                catch (SocketException x) 
                {
                    Console.WriteLine("Failed to accept connection: " + x);
                }
                lock (mutex) 
                { 
                    if (!listening) 
                    { 
                        return;
                    }
                }
                if (s != null)  
                { 
                    addConnection(s);
                }
            }
        }
         
        private void addConnection(Socket s) 
        {
            lock (mutex) 
            { 
                int n = replicas.Length;

                Replica[] newReplicas = new Replica[n+1];
                Array.Copy(replicas, 0, newReplicas, 0, n);
                Replica replica = new Replica();
                newReplicas[n] = replica;
                replica.host = s.RemoteEndPoint.ToString();
                replica.socket = s;
                replica.isDynamic = true;
                replicas = newReplicas;

                nHosts += 1;
    
                SynchronizeThread syncThread = new SynchronizeThread(this, n);
                Thread t = new Thread(new ThreadStart(syncThread.run));           
                replica.syncThread = t;
                t.Start();
            }
        }

        protected bool Send(int i, byte[] buf)
        {
            lock (replicas[i])
            {
                Socket s = replicas[i].socket;
                if (s == null)  
                {  
                    return false;
                }
                int offs = 0;
                while (offs < buf.Length)
                {
                    try
                    {
                        int rc = s.Send(buf, offs, buf.Length - offs, SocketFlags.None);
                        if (rc >= 0) 
                        {
                            offs += rc;
                            continue;
                        }
                    } 
                    catch (SocketException x)
                    {
                        Console.WriteLine("Failed to send data to node " + replicas[i].host + ": " + x);
                    }
                    if (!TryReconnect(i))
                    {
                        return false;
                    } 
                }
                return true;
            }
        }

        protected bool Receive(int i, byte[] buf)
        {
            lock (replicas[i])
            {
                Socket s = replicas[i].socket;
                if (s == null)  
                {  
                    return false;
                }
                ArrayList recvSocks = new ArrayList(1);
                recvSocks.Add(s);
                int offs = 0;
                while (offs < buf.Length)
                {
                    try
                    {
                        Socket.Select(recvSocks, null, null, storage.replicationReceiveTimeout*2000);
                        if (recvSocks.Count == 1)
                        {
                            int rc = s.Receive(buf, offs, buf.Length - offs, SocketFlags.None);
                            if (rc >= 0) 
                            { 
                                offs += rc;
                                continue;
                            }
                        } 
                        else
                        { 
                            Console.WriteLine("Receive timeout expired for node " + replicas[i].host);
                        }
                    } 
                    catch (SocketException x)
                    {
                        Console.WriteLine("Failed to receive data from node " + replicas[i].host + ": " + x);
                    }       
                    if (!TryReconnect(i))
                    {
                        return false;
                    }
                }
                return true;
            }
        }            

        void synchronizeNewSlaveNode(int i) 
        {
            long size = storage.DatabaseSize;
            int[] syncNodeTimestamps = null;
            byte[] txBuf;
            if (pageTimestamps != null) 
            { 
                txBuf = new byte[12 + Page.pageSize];   
                byte[] psBuf = new byte[4];
                if (!Receive(i, psBuf)) 
                { 
                    Console.WriteLine("Failed to receive page timestamps length from slave node " + replicas[i].host);
                    return;
                }
                int psSize = Bytes.unpack4(psBuf, 0);
                psBuf = new byte[psSize*4];
                if (!Receive(i, psBuf)) 
                { 
                    Console.WriteLine("Failed to receive page timestamps from slave node " + replicas[i].host);
                    return;
                }
                syncNodeTimestamps = new int[psSize];
                for (int j = 0; j < psSize; j++) { 
                    syncNodeTimestamps[j] = Bytes.unpack4(psBuf, j*4);
                }
            } else { 
                txBuf = new byte[8 + Page.pageSize];                                
            }
            for (long pos = 0; pos < size; pos += Page.pageSize) 
            { 
                int pageNo = (int)(pos >> Page.pageSizeLog);
                if (syncNodeTimestamps != null) { 
                    if (pageNo < syncNodeTimestamps.Length 
                        && pageNo < pageTimestamps.Length 
                        && syncNodeTimestamps[pageNo] == pageTimestamps[pageNo])
                    {
                        continue;
                    }
                }
                lock (storage)
                {   
                    lock (storage.objectCache)
                    {   
                        Page pg = storage.pool.getPage(pos);
                        Bytes.pack8(txBuf, 0, pos);
                        Array.Copy(pg.data, 0, txBuf, 8, Page.pageSize);
                        storage.pool.unfix(pg);
                        if (syncNodeTimestamps != null) 
                        { 
                            Bytes.pack4(txBuf, Page.pageSize + 8, pageNo < pageTimestamps.Length ? pageTimestamps[pageNo] : 0);
                        }                            
                    }
                }
                if (!Send(i, txBuf))
                { 
                    return;
                }
                if (ack && pos == 0 && !Receive(i, rcBuf)) 
                { 
                    Console.WriteLine("Failed to receive ACK from node " + replicas[i].host);
                    return;
                }
            } 
            Bytes.pack8(txBuf, 0, ReplicationSlaveStorageImpl.REPL_SYNC);
            Send(i, txBuf);
        }

        class SynchronizeThread 
        { 
            int i;
            ReplicationMasterFile master;
    
            public SynchronizeThread(ReplicationMasterFile master, int i) 
            { 
                this.i = i;
                this.master = master;
            }
    
            public void run() 
            { 
                master.synchronizeNewSlaveNode(i);
            }
        }          
     
        public int GetNumberOfAvailableHosts() 
        { 
            return nHosts;
        }

        protected bool TryReconnect(int i)
        {
            try     
            {
                replicas[i].socket.Close();
            }
            catch (SocketException) 
            {
            } 
           
            replicas[i].socket = null;
            nHosts -= 1;
            if (HandleError(replicas[i].host) && !replicas[i].isDynamic) 
            { 
                return Connect(i);
            } 
            else
            {       
                return false;
            }
        }

        protected bool Connect(int i)
        {
            String host = replicas[i].host;
            int colon = host.IndexOf(':');
            int port = int.Parse(host.Substring(colon+1));
            host = host.Substring(0, colon);
            Socket socket = null; 
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            int maxAttempts = storage != null 
                ? storage.slaveConnectionTimeout : MaxConnectionAttempts;
            for (int j = 0; j < maxAttempts; j++)  
            { 
#if NET_FRAMEWORK_20
                foreach (IPAddress ip in Dns.GetHostEntry(host).AddressList) 
#else
                foreach (IPAddress ip in Dns.Resolve(host).AddressList) 
#endif
                { 
                    try 
                    {
                        socket.Connect(new IPEndPoint(ip, port));
                        if (socket.Connected)
                        {	
                            replicas[i].socket = socket;
                            nHosts += 1;
                            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger,
                                new System.Net.Sockets.LingerOption(true, LingerTime));
                            if (pageTimestamps != null) 
                            {
                                 synchronizeNewSlaveNode(i);
                            }
                            return true;
                        }
                    } 
                    catch (SocketException x) 
                    { 
                        Console.WriteLine("Failed to establish connection with " + ip + ":" + port + " -> " + x); 
                    }
                }
                Thread.Sleep(ConnectionTimeout);
            }
            HandleError(replicas[i].host);
            return false;
        }

        /// <summary>
        /// When overriden by base class this method perfroms socket error handling
        /// </summary>     
        /// <returns><b>true</b> if host should be reconnected and attempt to send data to it should be 
        /// repeated, <b>false</b> if no more attmpts to communicate with this host should be performed 
        /// </returns>
        public bool HandleError(string host) 
        {
            return (storage != null && storage.listener != null) 
                ? storage.listener.ReplicationError(host) 
                : false;
        }


        public virtual void Write(long pos, byte[] buf) 
        {
            lock (mutex) 
            { 
                if (pageTimestamps != null) { 
                    int pageNo = (int)(pos >> Page.pageSizeLog);
                    if (pageNo >= pageTimestamps.Length) { 
                        int newLength = pageNo >= pageTimestamps.Length*2 ? pageNo+1 : pageTimestamps.Length*2;
    
                        int[] newPageTimestamps = new int[newLength];
                        Array.Copy(pageTimestamps, 0, newPageTimestamps, 0, pageTimestamps.Length);
                        pageTimestamps = newPageTimestamps;
    
                        int[] newDirtyPageTimestampMap = new int[(((newLength*4 + Page.pageSize - 1) >> Page.pageSizeLog) + 31) >> 5];
                        Array.Copy(dirtyPageTimestampMap, 0, newDirtyPageTimestampMap, 0, dirtyPageTimestampMap.Length);
                        dirtyPageTimestampMap = newDirtyPageTimestampMap;                    
                    }
                    pageTimestamps[pageNo] = ++timestamp;
                    dirtyPageTimestampMap[pageNo >> (Page.pageSizeLog - 2 + 5)] |= 1 << ((pageNo >> (Page.pageSizeLog - 2)) & 31);
                }
                Bytes.pack8(txBuf, 0, pos);
                Array.Copy(buf, 0, txBuf, 8, buf.Length);
                if (pageTimestamps != null) 
                { 
                    Bytes.pack4(txBuf, Page.pageSize + 8, timestamp);
                }                            
                for (int i = 0; i < replicas.Length; i++) 
                { 
                    if (Send(i, txBuf)) 
                    {
                        if (ack && pos == 0 && !Receive(i, rcBuf))
                        {
                            Console.WriteLine("Failed to receive ACK from node " + replicas[i].host);
                        }
                    }
                }
            }
            file.Write(pos, buf);
        }
         
        public int Read(long pos, byte[] buf) 
        {
            return file.Read(pos, buf);
        }

        public void Sync() 
        {
            if (pageTimestamps != null) { 
                lock (mutex) { 
                    byte[] page = new byte[Page.pageSize];
                    for (int i = 0; i < dirtyPageTimestampMap.Length; i++) { 
                        if (dirtyPageTimestampMap[i] != 0) { 
                            for (int j = 0; j < 32; j++) { 
                                if ((dirtyPageTimestampMap[i] & (1 << j)) != 0) { 
                                    int pageNo = (i << 5) + j;
                                    int beg = pageNo << (Page.pageSizeLog - 2);
                                    int end = beg + Page.pageSize/4;
                                    if (end > pageTimestamps.Length) { 
                                        end = pageTimestamps.Length;
                                    }
                                    int offs = 0;
                                    while (beg < end) {
                                        Bytes.pack4(page, offs, pageTimestamps[beg]);
                                        beg += 1;
                                        offs += 4;
                                    }
                                    long pos = pageNo << Page.pageSizeLog;
                                    pageTimestampFile.Write(pos, page);
                                }
                            }
                            dirtyPageTimestampMap[i] = 0;
                        }
                    }
                }
                pageTimestampFile.Sync();
            }  
            file.Sync();
        }

        public void Lock(bool shared) 
        { 
            file.Lock(shared);
        }

        public void Unlock() 
        { 
            file.Unlock();
        }

        public bool NoFlush 
        {
            get { return file.NoFlush; }
            set { file.NoFlush =  value; }
        }

        public bool IsEncrypted
        {
            get { return file.IsEncrypted; }
        }

        public virtual void Close() 
        {
            lock (mutex) 
            { 
                shutdown = true;
                Monitor.Pulse(mutex);
            }            
            watchdogThread.Join();
        
            if (listenThread != null) 
            { 
                lock (mutex) 
                { 
                    listening = false;                    
                }
                Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
#if NET_FRAMEWORK_20
                foreach (IPAddress ip in Dns.GetHostEntry("localhost").AddressList) 
#else
                foreach (IPAddress ip in Dns.Resolve("localhost").AddressList) 
#endif
                {  
                    try 
                    {
                         s.Connect(new IPEndPoint(ip, port));	
                         s.Close();
                    }
                    catch (SocketException) {}
                }
                listenThread.Join();

                try 
                {
                    listenSocket.Close();
                }           
                catch (SocketException) {}
            }                
            for (int i = 0; i < replicas.Length; i++) 
            {
                Thread t = replicas[i].syncThread;
                if (t != null) 
                { 
                    t.Join(); 
                }
            }            
            file.Close();
            Bytes.pack8(txBuf, 0, ReplicationSlaveStorageImpl.REPL_CLOSE);
            for (int i = 0; i < replicas.Length; i++) 
            { 
                if (replicas[i].socket != null) 
                {                 
                    try 
                    {  
                        replicas[i].socket.Send(txBuf);
                        replicas[i].socket.Close();
                    } 
                    catch (SocketException) {}
                }
            }
            if (pageTimestampFile != null) { 
                pageTimestampFile.Close();
            } 
        }

        public long Length 
        {
            get { return file.Length; }
        }

        public static int ListenQueueSize = 10;
        public static int LingerTime = 10; // linger parameter for the socket
        public static int MaxConnectionAttempts = 10; // attempts to establish connection with slave node
        public static int ConnectionTimeout = 1000; // timeout between attempts to conbbect to the slave
        public static int InitPageTimestampsLength = 64*1024;

        protected class Replica
        {
            public Socket socket;
            public string host;
            public bool   isDynamic;
            public Thread syncThread;
        }

        protected Replica[]      replicas; 
        protected byte[]         txBuf;
        protected byte[]         rcBuf;
        protected IFile          file;
        protected int            nHosts;
        protected bool           ack;
        protected bool           listening;
        protected bool           shutdown;
        protected Thread         listenThread;
        protected Socket         listenSocket;
        protected int            port;
        protected string         localhost;
        protected object         mutex;
        protected int[]          pageTimestamps;
        protected int[]          dirtyPageTimestampMap;
        protected OSFile         pageTimestampFile;
        protected int            timestamp;
        protected Thread         watchdogThread;
        protected ReplicationMasterStorageImpl storage;
    }
}
 