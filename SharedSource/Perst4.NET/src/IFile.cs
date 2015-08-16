namespace Perst
{
    /// <summary> Interface of file.
    /// Prorgemmer can provide its own impleentation of this interface, adding such features
    /// as support of flash cards, encrypted files,...
    /// Implentation of this interface should throw StorageError exception in case of failure
    /// </summary>
    public interface IFile 
    { 
        /// <summary> Write data to the file
        /// </summary>
        /// <param name="pos"> offset in the file
        /// </param>
        /// <param name="buf"> array with data to be writter (size is always equal to database page size)
        /// </param>
        /// 
        void Write(long pos, byte[] buf);

        /// <summary> Reade data from the file
        /// </summary>
        /// <param name="pos"> offset in the file
        /// </param>
        /// <param name="buf"> array to receive readen data (size is always equal to database page size)
        /// </param>
        /// <returns> param number of bytes actually readen
        /// </returns>
        int Read(long pos, byte[] buf);

        /// <summary> Flush all fiels changes to the disk
        /// </summary>
        void Sync();
    
        /// <summary>
        /// Prevent other processes from modifying the file
        /// </summary>
        /// <param name="shared">true: shared lock, false: exclusive</param>
        void Lock(bool shared);

        /// <summary>
        /// Unlock file
        /// </summary>
        void Unlock();

        /// <summary> Close file
        /// </summary>
        void Close();

        /// <summary>
        /// Boolean property. Set to <c>true</c> to avoid flushing the stream, or <c>false</c> to flush the stream with every calls to <see cref="Sync"/>
        /// </summary>
        bool NoFlush
        {
            get;
            set;
        }

        /// <summary>
        /// Boolean property indicating whether file is encrypted or not.
        /// </summary>
        bool IsEncrypted
        {
            get;
        }        

        /// <summary>
        /// Length of the file
        /// </summary>
        /// <returns>length of file in bytes</returns>
        long Length
        {
            get;
        }
    }

    /// <summary>
    /// Structure contaning variuos file parameters
    /// </summary>
    public struct FileParameters
    {
        /// <summary>Whether file is readonly</summary>        
        public bool readOnly;
        /// <summary>Whether file should be truncated</summary>        
        public bool truncate;
        /// <summary>Whether file buffers need to be flushed</summary>        
        public bool noFlush;
        /// <summary>Whether file has to be locked to prevent concurrent access</summary>        
        public bool lockFile;
        /// <summary>Initial quota value for Silverlight isolated storage</summary>        
        public long initialQuota; 
        /// <summary>Silverlight isolated storage quota increase quantum</summary>        
        public long quotaIncreaseQuantum;
        /// <summary>Silverlight isolated storage quota increase percent</summary>        
        public int  quotaIncreasePercent;
        /// <summary>DAtabase file size extension quantum</summary>        
        public long fileExtensionQuantum; 
        /// <summary>Database file size extension percent</summary>        
        public int  fileExtensionPercent; 
        /// <summary>File buffer size</summary>        
        public int  fileBufferSize; 

        public FileParameters(bool readOnly, bool truncate, bool noFlush, long fileExtensionQuantum)
        {
            this.readOnly = readOnly;
            this.truncate = truncate;
            this.noFlush = noFlush;
            lockFile = false;
            initialQuota = 0;
            quotaIncreaseQuantum = 0;
            quotaIncreasePercent = 100;
            this.fileExtensionQuantum = fileExtensionQuantum;
            fileExtensionPercent = 10;
            fileBufferSize = 1024*1024; 
        }                
    }
}
