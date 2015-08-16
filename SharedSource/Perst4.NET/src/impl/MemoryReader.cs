namespace Perst.Impl
{
    using System;
    using System.IO;
    using Perst;

    internal class MemoryReader : ObjectReader
    {
        private StorageImpl db;
        private byte[] buf;
        private int offs;
        private object parent;
        private bool recursiveLoading;
        private bool markReferences;

        public MemoryReader(StorageImpl db, byte[] buf, int offs, object parent, bool recursiveLoading, bool markReferences)
        : base(new MemoryStream(buf, offs, buf.Length - offs))
        {
            this.db = db;
            this.buf = buf;
            this.offs = offs;
            this.parent = parent;
            this.recursiveLoading = recursiveLoading;
            this.markReferences = markReferences;
        }
            
        public int Position
        { 
            get
            {
               return offs + (int)BaseStream.Position;
            }
        }

        override public object ReadObject()
        {
            int pos = offs + (int)BaseStream.Position;
            object obj = null;
            if (markReferences) 
            { 
                pos = db.markObjectReference(buf, pos);
            } 
            else 
            {  
                obj = db.unswizzle(buf, ref pos, typeof(object), parent, recursiveLoading);
            }
            BaseStream.Seek(pos - offs, SeekOrigin.Begin);
            return obj;
        }                
    }
}