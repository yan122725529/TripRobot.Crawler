namespace Perst.Impl
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using Perst;
    
    public class ReplicationDynamicSlaveStorageImpl : ReplicationSlaveStorageImpl
    { 
        public ReplicationDynamicSlaveStorageImpl(string host, int port, String pageTimestampFilePath) 
        : base(pageTimestampFilePath)
        { 
            this.host = host;
            this.port = port;
        }

        public override void Open(IFile file, long pagePoolSize) 
        {
            initialized = false;
            prevIndex = -1;
            outOfSync = true;
            base.Open(file, pagePoolSize);
        }

        protected override Socket GetSocket() 
        { 
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress addr;
            if (IPAddress.TryParse(host, out addr))
            {
                try 
                {
                     socket.Connect(new IPEndPoint(addr, port));	
                     if (socket.Connected)
                     {	
                         return socket;
                     }
                 } 
                 catch (SocketException x) 
                 {
                     Console.WriteLine("Failed to establish connection with " + addr + ":" + port + " -> " + x);
                 }
                 return null;
            }
            else
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
                             return socket;
                         }
                     } 
                     catch (SocketException x) 
                     {
                         Console.WriteLine("Failed to establish connection with " + ip + ":" + port + " -> " + x);
                     }
                }      
                return null;
            }
        }
        protected string host;
        protected int    port;
    }
}    

    
                                               