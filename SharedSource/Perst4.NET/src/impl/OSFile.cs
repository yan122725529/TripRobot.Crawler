namespace Perst.Impl    
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Diagnostics;
    using Perst;
#if WINRT_NET_FRAMEWORK
    using Windows.Storage.Streams;
    using Windows.Storage;
    using System.Runtime.InteropServices.WindowsRuntime;
#elif SILVERLIGHT
    using System.IO.IsolatedStorage;
#endif
	
    public class OSFile : IFile
    {
#if __MonoCS__
        [DllImport("__Internal")] 
        public static extern int fsync(int fd);
        public static int FlushFileBuffers(IntPtr ptr) {
            return fsync(ptr.ToInt32());
        }

//        public static extern bool FlushFileBuffers(IntPtr hFile); 
#else
        [DllImport("kernel32.dll", SetLastError=true)] 
        public static extern int FlushFileBuffers(IntPtr hFile); 
#endif

        public virtual void Write(long pos, byte[] buf)
        {
            Write(pos, buf, 0, buf.Length);
        }

        public void Write(long pos, byte[] buf, int offset, int length)
        {
#if WINRT_NET_FRAMEWORK
            if (currPos != pos) 
            { 
                file.Seek((ulong)pos);
                currPos = pos;
            }
            System.Threading.Tasks.Task<uint> t = file.WriteAsync(buf.AsBuffer(offset, length, length)).AsTask<uint, uint>();
            t.Wait();
            if (!t.IsCompleted || t.Result != length)
            {
                throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR, t.Status);
            }
            currPos = pos + length;
            if (currPos > fileSize)
            {
                fileSize = currPos;
            }
#else
#if SILVERLIGHT 
            while (true) try
#endif
            {
                long end = pos + length;
                if (end > fileSize) 
                { 
                    long newFileSize = fileSize + Math.Max(parameters.fileExtensionQuantum, 
                                                           fileSize*parameters.fileExtensionPercent/100);
                    if (newFileSize > end) 
                    {
                        file.SetLength(newFileSize);
                        fileSize = newFileSize;
                    }
                    else
                    {
                        fileSize = end;
                    }              
                }
                if (currPos != pos) 
                { 
                    file.Seek(pos, SeekOrigin.Begin);
                    currPos = pos;
                }
                file.Write(buf, offset, length);
                currPos = pos + length;
                return;
            }
#if SILVERLIGHT 
            catch (IsolatedStorageException x) 
            { 
                IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication();
                long oldQuota = isf.Quota;
                long newQuota = oldQuota + Math.Max(parameters.quotaIncreaseQuantum, oldQuota*parameters.quotaIncreasePercent/100);
                if (oldQuota == newQuota || !isf.IncreaseQuotaTo(newQuota))
                {
                    throw x;
                }
            }
#endif
#endif
        }

        public virtual int Read(long pos, byte[] buf)
        {
            int rc;
            if (pos >= fileSize)
            {
                return 0;
            }
            if (currPos != pos) 
            { 
#if WINRT_NET_FRAMEWORK
                file.Seek((ulong)pos);
#else
                file.Seek(pos, SeekOrigin.Begin);
#endif
                currPos = pos;
            }
#if WINRT_NET_FRAMEWORK
            System.Threading.Tasks.Task<IBuffer> t = file.ReadAsync(buf.AsBuffer(0, buf.Length), (uint)buf.Length, InputStreamOptions.Partial).AsTask<IBuffer,uint>();
            t.Wait();
            if (!t.IsCompleted)
            {
                throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR, t.Status);
            }
            IBuffer buffer = t.Result;
            rc = (int)buffer.Length;
            if (rc >= 0)
            {
                currPos = pos + rc;
            }
            return rc;
#else
            rc = file.Read(buf, 0, buf.Length);
            if (rc >= 0) 
            { 
                currPos = pos + rc;
            }
            return rc;
#endif
        }
		
        public virtual void  Sync()
        {
#if WINRT_NET_FRAMEWORK
            System.Threading.Tasks.Task<bool> t = file.FlushAsync().AsTask<bool>();
            t.Wait();
            if (!t.IsCompleted || !t.Result)
            {
                throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR);
            }
#elif NET_FRAMEWORK_40
            file.Flush(!parameters.noFlush);
#else
            file.Flush();
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
            if (!parameters.noFlush) 
            { 
#if NET_FRAMEWORK_20
                FlushFileBuffers(file.SafeFileHandle.DangerousGetHandle());
#else
                FlushFileBuffers(file.Handle);
#endif
            }
#endif
#endif
        }

        public virtual bool IsEncrypted
        {
            get
            {
                return false;
            }
        }

        public bool NoFlush
        {
            get { return parameters.noFlush; }
            set { parameters.noFlush = value; }
        }

        public virtual void Close()
        {
#if WINRT_NET_FRAMEWORK
            file.Dispose();
#else
            file.Close();
#endif
        }
		
#if !COMPACT_NET_FRAMEWORK && ! __MonoCS__ && !WINRT_NET_FRAMEWORK
        const int LOCKFILE_FAIL_IMMEDIATELY =  0x00000001;
        const int LOCKFILE_EXCLUSIVE_LOCK =  0x00000002;
        [DllImport("kernel32.dll", SetLastError=true)] 
        public static extern bool LockFileEx(IntPtr hFile, uint flags, uint reserved, uint lowSize, uint highSize, ref NativeOverlapped overlapped);
#endif 

        public virtual void Lock(bool shared) 
        {
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
#if __MonoCS__
            file.Lock(0, long.MaxValue);
#else
            uint flags = shared ? 0U : LOCKFILE_EXCLUSIVE_LOCK;
            NativeOverlapped overlapped = new NativeOverlapped();
            overlapped.OffsetHigh = 0;
            overlapped.OffsetLow = 0;
            overlapped.EventHandle = (IntPtr)0;
            if (!LockFileEx(file.SafeFileHandle.DangerousGetHandle(), flags, 0, 1, 0, ref overlapped))
            {
                throw new StorageError(StorageError.ErrorCode.FILE_ACCESS_ERROR, "File lock failed");
            }
#endif
#endif
        }

        public virtual void Unlock()
        {
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
            file.Unlock(0, 1);
#endif
        }
        
        public long Length
        {
            get { return fileSize; }
        }


        internal OSFile(String filePath, FileParameters parameters)
        {
            this.parameters = parameters;
#if WINRT_NET_FRAMEWORK
            System.Threading.Tasks.Task<StorageFile> tf = ApplicationData.Current.LocalFolder.CreateFileAsync(filePath, parameters.truncate ? CreationCollisionOption.ReplaceExisting : CreationCollisionOption.OpenIfExists).AsTask<StorageFile>();
            tf.Wait();
            System.Threading.Tasks.Task<IRandomAccessStream> ts = tf.Result.OpenAsync(parameters.readOnly ? FileAccessMode.Read : FileAccessMode.ReadWrite).AsTask<IRandomAccessStream>();
            file = ts.Result;
            fileSize = (long)file.Size;
#elif SILVERLIGHT
            IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication();
            if (parameters.initialQuota != 0)
            {
                // Get the storage file for the application
                if (isf.Quota < parameters.initialQuota)
                {
                    isf.IncreaseQuotaTo(parameters.initialQuota);
                } 
            }
            // Open/Create the file for writing
            file = new IsolatedStorageFileStream(filePath,
                                                 parameters.truncate ? FileMode.Create : FileMode.OpenOrCreate, parameters.readOnly ? FileAccess.Read : FileAccess.ReadWrite, 
                                                 isf);
            fileSize = file.Length;
#else
            file = new FileStream(filePath, parameters.truncate ? FileMode.Create : FileMode.OpenOrCreate, 
                                  parameters.readOnly ? FileAccess.Read : FileAccess.ReadWrite,
                                  parameters.readOnly ? FileShare.Read : FileShare.ReadWrite, 
                                  parameters.fileBufferSize
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
                                  , FileOptions.RandomAccess
#endif
                                  );
            fileSize = file.Length;
#endif
            currPos = 0;
        }
#if WINRT_NET_FRAMEWORK
        protected IRandomAccessStream file;
#else
        protected FileStream file;
#endif
        private FileParameters parameters;
        private long fileSize;
        private long currPos;
    }
}