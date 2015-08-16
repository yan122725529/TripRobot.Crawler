using System;
using System.IO;
using Perst.Impl;
using System.Diagnostics;

namespace Perst
{
    /// <summary> Stream class implementation on top of Perst file.
    /// Can be used in Storage.Backup method
    /// </summary>
    public class IFileStream : Stream
    {
        public override int Read(byte[] buffer, int dstOffset, int count)
        {
            int n = 0;
            while (count > 0) 
            { 
                int srcOffset = (int)(currPos % page.Length);
                if (srcOffset == 0)
                {
                    available = file.Read(currPos, page);
                    if (available == 0)
                    {
                        return 0;
                    }
                }
                int quant = available - srcOffset > count ? count : available - srcOffset;
                Array.Copy(page, srcOffset, buffer, dstOffset, quant);
                dstOffset += quant;
                currPos += quant;
                count -= quant;
                n += quant;
            }
            return n;
        }

        public override void Write(byte[] buffer, int srcOffset, int count)
        {
            while (count > 0) 
            { 
                int quant = page.Length - buffered > count ? count : page.Length - buffered;
                Array.Copy(buffer, srcOffset, page, buffered, quant);                
                srcOffset += quant;
                buffered += quant;
                currPos += quant;
                count -= quant;
                if (buffered == page.Length) 
                {
                    file.Write(currPos - buffered, page);
                    buffered = 0;
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Seek is not supported");
        }
        
        public override void SetLength(long value)
        {
            throw new InvalidOperationException("SetLength is not supported");
        }
        
        public override void Flush()
        {
            if (buffered != 0)
            {
                file.Write(currPos - buffered, page);
            }
        }

#if !WINRT_NET_FRAMEWORK
        public override void Close()
        {
            Flush();
            file.Close();
        }
#endif
                
        public IFileStream(IFile file)
        {
            page = new byte[Page.pageSize];
            this.file = file;
        }

        public override long Length
        {
            get
            {
                return file.Length;
            }
        }

        public override long Position
        {
            get
            {
                return currPos;
            }
            set
            {
                throw new InvalidOperationException("Position.set is not supported");
            }            
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }


        byte[] page;
        IFile  file;
        long   currPos;
        int    available;
        int    buffered;
    }
}
