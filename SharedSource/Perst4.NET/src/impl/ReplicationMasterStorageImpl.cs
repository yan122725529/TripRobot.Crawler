namespace Perst.Impl    
{
    using Perst;

    public class ReplicationMasterStorageImpl : StorageImpl, ReplicationMasterStorage
    { 
        public ReplicationMasterStorageImpl(string localhost, int port, string[] hosts, int asyncBufSize, string pageTimestampFile) 
        { 
            this.port = port;
            this.localhost = localhost;
            this.hosts = hosts;
            this.asyncBufSize = asyncBufSize;
            this.pageTimestampFile = pageTimestampFile;
        }
    
        public override void Open(IFile file, long pagePoolSize) 
        {
            base.Open(asyncBufSize != 0 
                ? (ReplicationMasterFile)new AsyncReplicationMasterFile(this, file, asyncBufSize, pageTimestampFile)
                : new ReplicationMasterFile(this, file, pageTimestampFile),
                pagePoolSize);
        }

        public int GetNumberOfAvailableHosts() 
        { 
            return ((ReplicationMasterFile)pool.file).GetNumberOfAvailableHosts();
        }

        internal string[] hosts;
        internal string   localhost;
        internal int      port;
        internal int      asyncBufSize;
        internal string   pageTimestampFile;
    }
}
