namespace Perst
{
    #region StreamFile
    
    /// <summary>
    /// PERST IFile implementation. Allows to store PERST databases on <see cref="System.IO.Stream"/> instances.
    /// </summary>
    
    internal class StreamFile : IFile 
    {
        private long offset = 0;
        private bool noFlush = false;
        private System.IO.Stream stream;
        
        /// <summary>
        /// Construction
        /// </summary>
        /// <param name="stream">A <see cref="System.IO.Stream"/> where to store the database</param>
        
        public StreamFile(System.IO.Stream stream)
        {
            this.stream = stream;
        }
        
        /// <summary>
        /// Construction
        /// </summary>
        /// <param name="stream">A <see cref="System.IO.Stream"/> where to store the database</param>
        /// <param name="offset">Offset within the stream where to store/find the database</param>
        
        public StreamFile(System.IO.Stream stream, long offset)
        {
            this.stream = stream;
            this.offset = offset;
        }

        /// <summary>
        /// Write method
        /// </summary>
        /// <param name="pos">Zero-based position</param>
        /// <param name="buf">Buffer to write to the stream. The entire buffer is written</param>
        
        public void Write(long pos, byte[] buf)
        {
            stream.Position = pos + offset;
            stream.Write(buf, 0, buf.Length);
        }

        /// <summary>
        /// Read method
        /// </summary>
        /// <param name="pos">Zero-based position</param>
        /// <param name="buf">Buffer where to store <c>buf.Length</c> byte(s) read from the stream</param>
        
        public int Read(long pos, byte[] buf)
        {
            stream.Position = pos + offset;
            return stream.Read(buf, 0, buf.Length);
        }

        /// <summary>
        /// Flushes the stream (subject to the NoFlush property)
        /// </summary>

        public void Sync()
        {
            if (noFlush == false) 
            {
                stream.Flush();
            }
        }

        /// <summary>
        /// Closes the stream (subject to the NoFlush property)
        /// </summary>

        public void Close()
        {
#if WINRT_NET_FRAMEWORK
            stream.Dispose();
#else
            stream.Close ();
#endif
        }

        /// <summary>
        /// Locks the stream (no-op)
        /// </summary>

        public void Lock(bool shared)
        {
        }

        public void Unlock() {}

        /// <summary>
        /// Boolean property. Set to <c>true</c> to avoid flushing the stream, or <c>false</c> to flush the stream with every calls to <see cref="Sync"/>
        /// </summary>
        public bool NoFlush
        {
            get { return this.noFlush; }
            set { this.noFlush = value; }
        }
        public bool IsEncrypted
        {
           get { return false; }
        }

        public long Length 
        {
            get { return stream.Length; }
        }

    }
    
    #endregion
}
