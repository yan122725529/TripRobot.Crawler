namespace Perst.Impl
{
    using System;
    using System.IO;
    using Perst;

    [Serializable]
    public class BlobImpl : PersistentResource, Blob 
    { 
        internal long     size;
        internal BlobImpl next;
        internal byte[]   body;

        class BlobStream : Stream 
        {
            protected BlobImpl curr;
            protected BlobImpl first;
            protected int      offs;
            protected long     pos;
            protected long     currPos;
            protected long     size;

            public override bool CanRead 
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
            
            public override long  Length 
            {
                get 
                {
                    return size;
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
                    if (value < 0) 
                    {
                        throw new ArgumentException("Nagative position");
                    }
                    currPos = value;
                }
            }

#if !WINRT_NET_FRAMEWORK
            public override void Close()
            {
                first = curr = null;
                size = 0;
            }
#endif

            public override void Flush()
            {
            }

            protected void SetPointer() 
            {
                long skip = currPos;
                if (skip < pos) 
                {
                    curr = first;
                    offs = 0;
                    pos = 0;
                } 
                else 
                {
                    skip -= pos;
                }
                     
                while (skip > 0) 
                { 
                    if (offs == curr.body.Length) 
                    { 
                        if (curr.next == null) 
                        { 
                            BlobImpl prev = curr;
                            curr = curr.next = new BlobImpl(first.Storage, curr.body.Length);
                            curr.MakePersistent(first.Storage);
                            prev.Store();
                        } 
                        else 
                        {
                            curr = curr.next;
                            curr.Load();
                        }
                        offs = 0;
                    }
                    int n = skip > curr.body.Length - offs ? curr.body.Length - offs : (int)skip; 
                    pos += n;
                    skip -= n;
                    offs += n;
                }
            }

            public override int Read(byte[] buffer, int dst, int count)
            {
                if (currPos >= size) 
                { 
                    return 0;
                }
                SetPointer();

                if (count > size - pos) 
                { 
                    count = (int)(size - pos);
                }
                int beg = dst;
                while (count > 0) 
                { 
                    if (offs == curr.body.Length) 
                    { 
                        curr = curr.next;
                        curr.Load();
                        offs = 0;
                    }
                    int n = count > curr.body.Length - offs ? curr.body.Length - offs : count; 
                    Array.Copy(curr.body, offs, buffer, dst, n);
                    pos += n;
                    dst += n;
                    offs += n;
                    count -= n;
                }
                currPos = pos;
                return dst - beg;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos = -1;
                switch (origin) 
                {
                    case SeekOrigin.Begin:
                        newPos = offset;
                        break;
                    case SeekOrigin.Current:
                        newPos = currPos + offset;
                        break;
                    case SeekOrigin.End:
                        newPos = size - offset;
                        break;
                }
                if (newPos < 0) 
                {
                    throw new ArgumentException("Negative position");
                }
                currPos = newPos;
                return newPos;
            }
             

            public override void SetLength(long length)
            {
                BlobImpl blob = first;
                size = 0;
                if (length > 0) 
                { 
                    while (length > blob.body.Length)  
                    {
                        size += blob.body.Length;
                        if (blob.next == null) 
                        { 
                            BlobImpl prev = blob;
                            blob = blob.next = new BlobImpl(first.Storage, blob.body.Length);
                            blob.MakePersistent(first.Storage);
                            prev.Store();
                        } 
                        else 
                        {
                            blob = blob.next;
                            blob.Load();
                        }
                    }
                    size += length;
                }
                if (pos > size) 
                { 
                    pos = size;
                    curr = blob;
                }
                if (blob.next != null) 
                {
                    BlobImpl.DeallocateAll(blob.next);
                    blob.Modify();
                    blob.next = null;
                }
                first.Modify();
                first.size = size;
            }

            public override void Write(byte[] buffer, int src, int count) 
            {
                SetPointer();

                while (count > 0) 
                { 
                    if (offs == curr.body.Length) 
                    { 
                        if (curr.next == null) 
                        { 
                            BlobImpl prev = curr;
                            curr = curr.next = new BlobImpl(first.Storage, curr.body.Length);
                            curr.MakePersistent(first.Storage);
                            prev.Store();
                        } 
                        else 
                        {
                            curr = curr.next;
                            curr.Load();
                        }
                        offs = 0;
                    }
                    int n = count > curr.body.Length - offs ? curr.body.Length - offs : count; 
                    curr.Modify();
                    Array.Copy(buffer, src, curr.body, offs, n);
                    pos += n;
                    src += n;
                    offs += n;
                    count -= n;
                }
                currPos = pos;
                if (pos > size) 
                {
                    size = pos;
                    first.Modify();
                    first.size = size;
                }
            }

            protected internal BlobStream(BlobImpl first)
            {
                first.Load();
                this.first = first;
                curr = first;
                size = first.size;
                pos = 0;
                offs = 0;
                currPos = 0;
            }
        }

        static protected void DeallocateAll(BlobImpl curr) 
        {
            while (curr != null) 
            {                
                curr.Load();
                BlobImpl next = curr.next;
                curr.Deallocate();
                curr = next;
            }
        }

        public override void Deallocate()
        {
            Load();
            if (size != 0) 
            {
                DeallocateAll(next);
            }
            base.Deallocate();
        }


        public override bool RecursiveLoading() 
        { 
            return false;
        }

        public Stream GetStream()
        {
            return new BlobStream(this);
        }

        protected internal BlobImpl(Storage storage, int size) : base(storage)
        { 
            body = new byte[size];
        }

        internal BlobImpl() {}
    }   
}
