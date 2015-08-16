namespace Perst
{
    /// <summary>
    /// This implementation of <see cref="Perst.NullFile"/> interface can be used
    /// to make Perst an main-memory database. It should be used when pagePoolSize
    /// is set to 0. In this case all pages are cached in memory
    /// and <see cref="Perst.NullFile"/> is used just as a stub.
    /// <see cref="Perst.NullFile"/> should be used only when data is transient - i.e. it should not be saved
    /// between database sessions. If you need in-memory database but which provide data persistency, 
    /// you should use normal file and infinite page pool size. 
    /// </summary>
    public class NullFile : IFile 
    {
         public void Write(long pos, byte[] buf) {}

         public int Read(long pos, byte[] buf) {
             return 0;
         }

         public void Sync() {}

        public void Lock(bool shared) {}

        public void Unlock() {}

        public void Close() {}

        public bool NoFlush
        {
            get { return false; }
            set {}
        }

        public bool IsEncrypted
        {
            get
            {
                return false;
            }
        }

        public long Length
        {
            get { return 0; }
        }
    }
}
