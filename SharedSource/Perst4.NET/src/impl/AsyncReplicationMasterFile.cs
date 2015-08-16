namespace Perst.Impl    
{
    using System;
    using System.Threading;
    using System.Net;
    using System.Net.Sockets;
    using Perst;
	
    /// <summary>
    /// File performing asynchronous replication of changed pages to specified slave nodes.
    /// </summary>
    public class AsyncReplicationMasterFile : ReplicationMasterFile 
    {
        /// <summary>
        /// Constructor of replication master file
        /// <param name="storage">replication storage</param>
        /// <param name="file">local file used to store data locally</param>
        /// <param name="asyncBufSize">size of asynchronous buffer</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        /// </summary>
        public AsyncReplicationMasterFile(ReplicationMasterStorageImpl storage, IFile file, int asyncBufSize, String pageTimestampFile)
            : base(storage, file, pageTimestampFile)
        {
            this.asyncBufSize = asyncBufSize;
            start();
        }


        /// <summary>
        /// Constructor of replication master file
        /// <param name="file">local file used to store data locally</param>
        /// <param name="hosts">slave node hosts to which replicastion will be performed</param>
        /// <param name="asyncBufSize">size of asynchronous buffer</param>
        /// <param name="ack">whether master should wait acknowledgment from slave node during trasanction commit</param>
        /// </summary>
        public AsyncReplicationMasterFile(IFile file, String[] hosts, int asyncBufSize, bool ack) 
            : this(file, hosts, asyncBufSize, ack, null)
        {
        }

        /// <summary>
        /// Constructor of replication master file
        /// <param name="file">local file used to store data locally</param>
        /// <param name="hosts">slave node hosts to which replicastion will be performed</param>
        /// <param name="asyncBufSize">size of asynchronous buffer</param>
        /// <param name="ack">whether master should wait acknowledgment from slave node during trasanction commit</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        /// </summary>
        public AsyncReplicationMasterFile(IFile file, String[] hosts, int asyncBufSize, bool ack, String pageTimestampFile) 
            : base(file, hosts, ack, pageTimestampFile)
        {
            this.asyncBufSize = asyncBufSize;
            start();
        }

        private void start() 
        {
            go = new object();
            async = new object();
            thread = new Thread(new ThreadStart(writeThread));
            thread.Start();
        }
                
        class Parcel 
        {
            public byte[] data;
            public long   pos;
            public int    host;
            public Parcel next;
        }
    
        public override void Write(long pos, byte[] buf) 
        {
            file.Write(pos, buf);
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
            }
            for (int i = 0; i < replicas.Length; i++) 
            { 
                if (replicas[i].socket != null) 
                {                
                    byte[] data = new byte[txBuf.Length];
                    Bytes.pack8(data, 0, pos);
                    Array.Copy(buf, 0, data, 8, buf.Length);
                    if (pageTimestamps != null) 
                    { 
                        Bytes.pack4(data, Page.pageSize + 8, timestamp);
                    }                            
                    Parcel p = new Parcel();
                    p.data = data;
                    p.pos = pos;
                    p.host = i;

                    lock(async) 
                    { 
                        buffered += data.Length;
                        while (buffered > asyncBufSize && buffered != data.Length) 
                        { 
                            Monitor.Wait(async);
                        }
                    }
                    
                    lock(go) 
                    { 
                        if (head == null) 
                        { 
                            head = tail = p;
                        } 
                        else 
                        { 
                            tail = tail.next = p;
                        }
                        Monitor.Pulse(go);
                    }
                }
            }
        }

        public void writeThread() 
        { 
            while (true) 
            { 
                Parcel p;
                lock(go) 
                {
                    while (head == null) 
                    { 
                        if (closed) 
                        { 
                            return;
                        }
                        Monitor.Wait(go);
                    }
                    p = head;
                    head = p.next;
                }  
            
                lock(async) 
                { 
                    if (buffered > asyncBufSize) 
                    { 
                        Monitor.PulseAll(async);
                    }
                    buffered -= p.data.Length;
                }
                int i = p.host;
                if (!Send(i, p.data)) 
                { 
                    break;
                }
                if (ack && p.pos == 0 && !Receive(i, rcBuf)) 
                {
                    Console.WriteLine("Failed to receive ACK from node " + replicas[i].host);
                    break;
                }
            }
        }

        public override void Close() 
        {
            lock(go) 
            {
                closed = true;
                Monitor.Pulse(go);
            }
            thread.Join();
            base.Close();
        }

        private int     asyncBufSize;
        private int     buffered;
        private bool    closed;
        private object  go;
        private object  async;
        private Parcel  head;
        private Parcel  tail;    
        private Thread  thread;
    }
}
