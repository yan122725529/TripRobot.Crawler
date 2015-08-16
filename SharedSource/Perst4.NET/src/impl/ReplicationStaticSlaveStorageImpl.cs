namespace Perst.Impl
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using Perst;
    
    public class ReplicationStaticSlaveStorageImpl : ReplicationSlaveStorageImpl
    {
        public ReplicationStaticSlaveStorageImpl(int port, String pageTimestampFilePath) 
        : base(pageTimestampFilePath)
        { 
            this.port = port;
        }

        public override void Open(IFile file, long pagePoolSize) 
        {
            acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            acceptor.Bind(new IPEndPoint(IPAddress.Any, port));           
            acceptor.Listen(ListenQueueSize);
            if (file.Length > 0) 
            { 
                byte[] rootPage = new byte[Page.pageSize];
                try 
                { 
                    file.Read(0, rootPage);
                    prevIndex =  rootPage[DB_HDR_CURR_INDEX_OFFSET];
                    initialized = rootPage[DB_HDR_INITIALIZED_OFFSET] != 0;
                } 
                catch (StorageError) 
                {
                    initialized = false;
                    prevIndex = -1;
                }
            } 
            else 
            { 
                prevIndex = -1;
                initialized = false;
            }
            outOfSync = false;
            base.Open(file, pagePoolSize);
        }

        protected override Socket GetSocket()
        { 
             return acceptor.Accept();
        }
   
        protected override void cancelIO() 
        { 
            try 
            { 
                Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
#if NET_FRAMEWORK_20
                s.Connect(new IPEndPoint(Dns.GetHostEntry("localhost").AddressList[0], port));	
#else
                s.Connect(new IPEndPoint(Dns.Resolve("localhost").AddressList[0], port));	
#endif
                s.Close();
            } 
            catch (SocketException) {}
        }

        protected Socket acceptor;
        protected int    port;
    }
}