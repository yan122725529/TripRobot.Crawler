namespace Perst.Impl
{
    using System;
    using System.Text;
    using System.IO;
	
    public class ByteBuffer
    {
        public void  extend(int size)
        {
            if (size > arr.Length)
            {
                int newLen = size > arr.Length * 2?size:arr.Length * 2;
                byte[] newArr = new byte[newLen];
                Array.Copy(arr, 0, newArr, 0, used);
                arr = newArr;
            }
            used = size;
        }
		
        public byte[] toArray()
        {
            byte[] result = new byte[used];
            Array.Copy(arr, 0, result, 0, used);
            return result;
        }
		
        public int packI1(int offs, int val)
        {
            extend(offs+1);
            arr[offs++] = (byte)val;
            return offs;
        }

        public int packBool(int offs, bool val)
        {
            extend(offs+1);
            arr[offs++] = (byte)(val?1:0);
            return offs;
        }

        public int packI2(int offs, int val)
        {
            extend(offs+2);
            Bytes.pack2(arr, offs, (short)val);
            return offs + 2;
        }
        public int packI4(int offs, int val)
        {
            extend(offs+4);
            Bytes.pack4(arr, offs, val);
            return offs + 4;
        }
        public int packI8(int offs, long val)
        {
            extend(offs+8);
            Bytes.pack8(arr, offs, val);
            return offs + 8;
        }
        public int packF4(int offs, float val)
        {
            extend(offs+4);
            Bytes.packF4(arr, offs, val);
            return offs + 4;
        }
        public int packF8(int offs, double val)
        {
            extend(offs+8);
            Bytes.packF8(arr, offs, val);
            return offs + 8;
        }
        public int packDecimal(int offs, decimal val)
        {
            extend(offs+16);
            Bytes.packDecimal(arr, offs, val);
            return offs + 16;
        }
        public int packGuid(int offs, Guid val)
        {
            extend(offs+16);
            Bytes.packGuid(arr, offs, val);
            return offs + 16;
        }
        public int packDate(int offs, DateTime val)
        {
            extend(offs+8);
            Bytes.packDate(arr, offs, val);
            return offs + 8;
        }
 
        public int packString(int offs, string s)
        {
            if (s == null)
            {
                extend(offs + 4);
                Bytes.pack4(arr, offs, - 1);
                offs += 4;
            }
            else
            {
                int len = s.Length;
                if (encoding == null) 
                { 
                    extend(offs + 4 + len * 2);
                    Bytes.pack4(arr, offs, len);
                    offs += 4;
                    for (int i = 0; i < len; i++)
                    {
                        Bytes.pack2(arr, offs, (short)s[i]);
                        offs += 2;
                    }
                } 
                else 
                { 
                    byte[] bytes = encoding.GetBytes(s);
                    extend(offs + 4 + bytes.Length);
                    Bytes.pack4(arr, offs, -2-bytes.Length);
                    Array.Copy(bytes, 0, arr, offs+4, bytes.Length);
                    offs += 4 + bytes.Length;
                }
            }
            return offs;
        }

        class ByteBufferOutputStream : Stream 
        { 
            internal int start;
            internal ByteBuffer buf;

            internal ByteBufferOutputStream(ByteBuffer buf) 
            {
                this.buf = buf;
                start = buf.used;
            }

            override public bool CanRead 
            {
                get 
                {
                    return false;
                }
            }

            override public bool CanSeek 
            {
                get 
                {
                    return false;
                }
            }
            
            override public bool CanWrite 
            {
                get 
                {
                    return true;
                }
            }

            override public long Length 
            {
                get 
                {
                    return buf.arr.Length - start;
                }
            }

            override public long Position 
            {
                set 
                {
                    throw new NotSupportedException("ByteBufferOutputStream.Position.set");
                }
                get 
                {
                    return buf.used - start;
                }
            }

            override public void Flush() {}

            override public int Read(byte[] buffer, int offset, int count) 
            {
                throw new NotSupportedException("ByteBufferOutputStream.Read");
            }
                
            override public long Seek(long offset, SeekOrigin origin) {
                throw new NotSupportedException("ByteBufferOutputStream.Seek");
            }
            
            override public void Write(byte[] b, int offset, int count) 
            {
                int pos = buf.used;
                buf.extend(pos + count);
                Array.Copy(b, offset, buf.arr, pos, count);
            }

            override public void SetLength(long value) 
            {
                throw new NotSupportedException("ByteBufferOutputStream.SetLength");
            }
        }

        class ByteBufferWriter : ObjectWriter
        {            
            public ByteBufferWriter(ByteBuffer buf) 
            : base(new ByteBufferOutputStream(buf))
            {
            }
                
            override public void WriteObject(object obj)
            {                
                ByteBuffer buf = ((ByteBufferOutputStream)OutStream).buf;
                Flush();
                buf.db.swizzle(buf, buf.used, obj);
            }
        }

        public ObjectWriter GetWriter() 
        { 
            return new ByteBufferWriter(this);
        }

        public ByteBuffer(StorageImpl db, Object parent, bool finalized)        
        : this()
        {
            this.db = db;
            encoding = db.encoding;
            this.parent = parent;
            this.finalized = finalized;
        }
		
        public ByteBuffer()
        {
            arr = new byte[64];
        }

        internal byte[]   arr;
        internal int      used;
        internal Encoding encoding;
        internal Object   parent;
        internal bool     finalized; 
        internal StorageImpl db;
   }
}