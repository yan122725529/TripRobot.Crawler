namespace Perst
{
    using System;
    using System.Collections;

    /// <summary>
    /// Class used to merge results of multiple databases searches.
    /// Each bit of bitmap corresponds to object OID.
    /// and/or/xor method can be used to combine different bitmaps.
    /// </summary>
    public class Bitmap:IEnumerable
    {    
        class BitmapEnumerator:PersistentEnumerator
        { 
            public bool MoveNext() 
            { 
                int i = curr, n = n_bits;
                int[] bm = bitmap;
                while (++i < n && (bm[i >> 5] & (1 << (i & 31))) == 0);
                curr = i;
                return i < n;
            }
    
            public int CurrentOid 
            {
                get
                {
                    return curr;
                }
            }

            public object Current 
            { 
                get
                {
                    if ((uint)curr >= (uint)n_bits) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return storage.GetObjectByOID(curr);
                }
            }
            
            public void Reset()
            {
                curr = -1;
            }
    
            public BitmapEnumerator(Storage s, int[] bm, int n) 
            { 
                storage = s;
                bitmap = bm;
                n_bits = n;
                curr = -1;
            } 
    
            Storage storage;
            int[] bitmap;
            int  n_bits;
            int  curr;
        };
    
        /// <summary>
        /// Check if object with this OID is present in bitmap
        /// </summary>
        /// <param name="oid">object identifier</param>
        /// <returns>true if object is repsent in botmap, false otherwise</returns>
        public bool Contains(int oid) 
        { 
            return oid < n_bits && (bitmap[oid >> 5] & (1 << (oid & 31))) != 0;
        }
    
        /// <summary> 
        /// Get enumerator through objects selected in bitmap
        /// </summary>
        /// <returns>selected object enumerator</returns>
        public IEnumerator GetEnumerator()
        {
            return new BitmapEnumerator(storage, bitmap, n_bits);
        }
    
        /// <summary>
        /// Intersect (bit and) two bitmaps
        /// </summary>
        /// <param name="other">bitmaps which will be intersected with this one</param>
        public void And(Bitmap other) 
        { 
            int[] b1 = bitmap;
            int[] b2 = other.bitmap;
            int len = b1.Length < b2.Length ? b1.Length : b2.Length;
            int i;
            for (i = 0; i < len; i++) { 
                b1[i] &= b2[i];
            }
            while (i < b1.Length) { 
                b1[i++] = 0;
            }
            if (n_bits > other.n_bits) { 
                n_bits = other.n_bits;
            }
        }
    
        /// <summary>
        /// Union (bit or) two bitmaps
        /// </summary>
        /// <param name="other">bitmaps which will be combined with this one</param>
        public void Or(Bitmap other) 
        { 
            int[] b1 = bitmap;
            int[] b2 = other.bitmap;
            if (b1.Length < b2.Length) { 
                bitmap = new int[b2.Length];
                Array.Copy(b1, 0, bitmap, 0, b1.Length);
                b1 = bitmap;
            }
            int len = b1.Length < b2.Length ? b1.Length : b2.Length;
            for (int i = 0; i < len; i++) { 
                b1[i] |= b2[i];
            }
            if (n_bits < other.n_bits) { 
                n_bits = other.n_bits;
            }
        }
    
        /// <summary>
        /// Excluysive OR (xor) of two bitmaps
        /// </summary>
        /// <param name="other">bitmaps which will be combined with this one</param>
        public void Xor(Bitmap other) 
        { 
            int[] b1 = bitmap;
            int[] b2 = other.bitmap;
            if (b1.Length < b2.Length) { 
                bitmap = new int[b2.Length];
                Array.Copy(b1, 0, bitmap, 0, b1.Length);
                b1 = bitmap;
            }
            int len = b1.Length < b2.Length ? b1.Length : b2.Length;
            for (int i = 0; i < len; i++) { 
                b1[i] ^= b2[i];
            }
            if (n_bits < other.n_bits) { 
                n_bits = other.n_bits;
            }
        }
    
        /// <summary>
        /// Constructor of bitmap
        /// </summary>
        /// <param name="sto">storage of persistent object selected by this bitmap</param>
        /// <param name="i ">enumerator through persistent object which is used to initialize bitmap</param>
        public Bitmap(Storage sto, IEnumerator i) 
        { 
            storage = sto;
            n_bits = sto.MaxOid;
            int[] bm = new int[(n_bits + 31) >> 5];
            PersistentEnumerator pi = (PersistentEnumerator)i;
            while (pi.MoveNext()) { 
                int oid = pi.CurrentOid;
                bm[oid >> 5] |= 1 << (oid & 31);
            }
            bitmap = bm;
        }
    
        Storage storage;
        int[] bitmap;
        int n_bits;
    }
}                                                                                    
