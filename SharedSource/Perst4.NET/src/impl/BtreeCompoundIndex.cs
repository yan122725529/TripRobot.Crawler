namespace Perst.Impl
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
    using System.Diagnostics;
    using Perst;
	
    [Serializable]
#if USE_GENERICS
    class BtreeCompoundIndex<V>:Btree<object[],V>, CompoundIndex<V> where V:class
#else
    class BtreeCompoundIndex:Btree, CompoundIndex
#endif
    {
        ClassDescriptor.FieldType[] types;

#if USE_GENERICS
        public override void OnLoad()
        {
        }
#endif
        internal BtreeCompoundIndex() 
        {
        }
    
        internal BtreeCompoundIndex(Type[] keyTypes, bool unique) 
        {
            this.unique = unique;
            type = ClassDescriptor.FieldType.tpArrayOfByte;        
            types = new ClassDescriptor.FieldType[keyTypes.Length];
            for (int i = 0; i < keyTypes.Length; i++) 
            {
                types[i] = checkType(keyTypes[i]);
            }
        }

        internal BtreeCompoundIndex(ClassDescriptor.FieldType[] types, bool unique) 
        {
            this.types = types;
            this.unique = unique;
        }

        public override void init(Type cls, ClassDescriptor.FieldType[] types, string[] fieldNames, bool unique, long autoincCount) 
        {
            this.types = types;
            this.unique = unique;
        }               


        public override ClassDescriptor.FieldType[] FieldTypes 
        {
            get
            { 
                return types;
            }
        }

        public Type[] KeyTypes
        {
            get
            { 
                Type[] keyTypes = new Type[types.Length];
                for (int i = 0; i < keyTypes.Length; i++) 
                { 
                     keyTypes[i] = mapKeyType(types[i]);
                }
                return keyTypes;
            }
        }

        public override int compareByteArrays(byte[] key, byte[] item, int offs, int lengtn) 
        { 
            int o1 = 0;
            int o2 = offs;
            byte[] a1 = key;
            byte[] a2 = item;
            for (int i = 0; i < types.Length && o1 < key.Length; i++) 
            {
                int diff = 0;
                switch (types[i]) 
                { 
                    case ClassDescriptor.FieldType.tpBoolean:
                    case ClassDescriptor.FieldType.tpByte:
                        diff = a1[o1++] - a2[o2++];
                        break;
                    case ClassDescriptor.FieldType.tpSByte:
                        diff = (sbyte)a1[o1++] - (sbyte)a2[o2++];
                        break;
                    case ClassDescriptor.FieldType.tpShort:
                        diff = Bytes.unpack2(a1, o1) - Bytes.unpack2(a2, o2);
                        o1 += 2;
                        o2 += 2;
                        break;
                    case ClassDescriptor.FieldType.tpUShort:
                        diff = (ushort)Bytes.unpack2(a1, o1) - (ushort)Bytes.unpack2(a2, o2);
                        o1 += 2;
                        o2 += 2;
                        break;
                    case ClassDescriptor.FieldType.tpChar:
                        diff = (char)Bytes.unpack2(a1, o1) - (char)Bytes.unpack2(a2, o2);
                        o1 += 2;
                        o2 += 2;
                        break;
                    case ClassDescriptor.FieldType.tpInt:
                    {
                        int i1 = Bytes.unpack4(a1, o1);
                        int i2 = Bytes.unpack4(a2, o2);
                        diff = i1 < i2 ? -1 : i1 == i2 ? 0 : 1;
                        o1 += 4;
                        o2 += 4;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpUInt:
                    case ClassDescriptor.FieldType.tpEnum:
                    case ClassDescriptor.FieldType.tpObject:
                    case ClassDescriptor.FieldType.tpOid:
                    {
                        uint u1 = (uint)Bytes.unpack4(a1, o1);
                        uint u2 = (uint)Bytes.unpack4(a2, o2);
                        diff = u1 < u2 ? -1 : u1 == u2 ? 0 : 1;
                        o1 += 4;
                        o2 += 4;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpLong:
                    {
                        long l1 = Bytes.unpack8(a1, o1);
                        long l2 = Bytes.unpack8(a2, o2);
                        diff = l1 < l2 ? -1 : l1 == l2 ? 0 : 1;
                        o1 += 8;
                        o2 += 8;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpULong:
                    case ClassDescriptor.FieldType.tpDate:
                    {
                        ulong l1 = (ulong)Bytes.unpack8(a1, o1);
                        ulong l2 = (ulong)Bytes.unpack8(a2, o2);
                        diff = l1 < l2 ? -1 : l1 == l2 ? 0 : 1;
                        o1 += 8;
                        o2 += 8;
                        break;
                    }
                    
                    case ClassDescriptor.FieldType.tpFloat:
                    {
                        float f1 = Bytes.unpackF4(a1, o1);
                        float f2 = Bytes.unpackF4(a2, o2);
                        diff = f1 < f2 ? -1 : f1 == f2 ? 0 : 1;
                        o1 += 4;
                        o2 += 4;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpDouble:
                    {
                        double d1 = Bytes.unpackF8(a1, o1);
                        double d2 = Bytes.unpackF8(a2, o2);
                        diff = d1 < d2 ? -1 : d1 == d2 ? 0 : 1;
                        o1 += 8;
                        o2 += 8;
                        break;
                    }

                    case ClassDescriptor.FieldType.tpDecimal:
                    {
                        decimal d1 = Bytes.unpackDecimal(a1, o1);
                        decimal d2 = Bytes.unpackDecimal(a2, o2);
                        diff = d1.CompareTo(d2);
                        o1 += 16;
                        o2 += 16;
                        break;
                    }
 
                    case ClassDescriptor.FieldType.tpGuid:
                    {
                        Guid g1 = Bytes.unpackGuid(a1, o1);
                        Guid g2 = Bytes.unpackGuid(a2, o2);
                        diff = g1.CompareTo(g2);
                        o1 += 16;
                        o2 += 16;
                        break;
                    }

                    case ClassDescriptor.FieldType.tpString:
                    {
                        int len1 = Bytes.unpack4(a1, o1);
                        int len2 = Bytes.unpack4(a2, o2);
                        o1 += 4;
                        o2 += 4;
                        int len = len1 < len2 ? len1 : len2;
                        while (--len >= 0) 
                        { 
                            diff = (char)Bytes.unpack2(a1, o1) - (char)Bytes.unpack2(a2, o2);
                            if (diff != 0) 
                            { 
                                return diff;
                            }
                            o1 += 2;
                            o2 += 2;
                        }
                        diff = len1 - len2;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpArrayOfByte:
                    {
                        int len1 = Bytes.unpack4(a1, o1);
                        int len2 = Bytes.unpack4(a2, o2);
                        o1 += 4;
                        o2 += 4;
                        int len = len1 < len2 ? len1 : len2;
                        while (--len >= 0) 
                        { 
                            diff = a1[o1++] - a2[o2++];
                            if (diff != 0) 
                            { 
                                return diff;
                            }
                        }
                        diff = len1 - len2;
                        break;
                    }
                    default:
                        Debug.Assert(false, "Invalid type");
                        break;
                }
                if (diff != 0) 
                { 
                    return diff;
                }
            }
            return 0;
        }

        protected override object unpackByteArrayKey(Page pg, int pos) 
        {
            int offs = BtreePage.firstKeyOffs + BtreePage.getKeyStrOffs(pg, pos);
            byte[] data = pg.data;
            Object[] values = new Object[types.Length];

            for (int i = 0; i < types.Length; i++) 
            {
                Object v = null;
                switch (types[i]) 
                { 
                    case ClassDescriptor.FieldType.tpBoolean: 
                        v = data[offs++] != 0;
                        break;
				
                    case ClassDescriptor.FieldType.tpSByte: 
                        v = (sbyte)data[offs++];
                        break;
                   
                    case ClassDescriptor.FieldType.tpByte: 
                        v = data[offs++];
                        break;
 				
                    case ClassDescriptor.FieldType.tpShort: 
                        v = Bytes.unpack2(data, offs);
                        offs += 2;
                        break;

                    case ClassDescriptor.FieldType.tpUShort: 
                        v = (ushort)Bytes.unpack2(data, offs);
                        offs += 2;
                        break;
				
                    case ClassDescriptor.FieldType.tpChar: 
                        v = (char) Bytes.unpack2(data, offs);
                        offs += 2;
                        break;

                    case ClassDescriptor.FieldType.tpInt: 
                        v = Bytes.unpack4(data, offs);
                        offs += 4;
                        break;
                   
                    case ClassDescriptor.FieldType.tpUInt:
                        v = (uint)Bytes.unpack4(data, offs);
                        offs += 4;
                        break;
 
                    case ClassDescriptor.FieldType.tpOid:
                    case ClassDescriptor.FieldType.tpObject: 
                    {
                        int oid = Bytes.unpack4(data, offs);
                        v = oid == 0 ? null : ((StorageImpl)Storage).lookupObject(oid, null);
                        offs += 4;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpLong: 
                        v = Bytes.unpack8(data, offs);
                        offs += 8;
                        break;

                    case ClassDescriptor.FieldType.tpDate: 
                    {
                        v = new DateTime(Bytes.unpack8(data, offs));
                        offs += 8;
                        break;
                    }
                    case ClassDescriptor.FieldType.tpULong: 
                        v = (ulong)Bytes.unpack8(data, offs);
                        offs += 8;
                        break;
 				
                    case ClassDescriptor.FieldType.tpFloat: 
                        v = Bytes.unpackF4(data, offs);
                        offs += 4;
                        break;

                    case ClassDescriptor.FieldType.tpDouble: 
                        v = Bytes.unpackF8(data, offs);
                        offs += 8;
                        break;

                    case ClassDescriptor.FieldType.tpDecimal:
                        v = Bytes.unpackDecimal(data, offs);
                        offs += 16;
                        break;

                    case ClassDescriptor.FieldType.tpGuid:
                        v = Bytes.unpackGuid(data, offs);
                        offs += 16;
                        break;

                    case ClassDescriptor.FieldType.tpString:
                    {
                        int len = Bytes.unpack4(data, offs);
                        offs += 4;
                        char[] sval = new char[len];
                        for (int j = 0; j < len; j++)
                        {
                            sval[j] = (char) Bytes.unpack2(pg.data, offs);
                            offs += 2;
                        }
                        v = new String(sval);
                        break;
                    }

                    case ClassDescriptor.FieldType.tpArrayOfByte:
                    {
                        int len = Bytes.unpack4(data, offs);
                        offs += 4;
                        byte[] val = new byte[len];
                        Array.Copy(pg.data, offs, val, 0, len);
                        offs += len;
                        v = val;
                        break;
                    }
                    default: 
                        Debug.Assert(false, "Invalid type");
                        break;
                }
                values[i] = v;
            }
            return values;
        }

        private Key convertKey(Key key) 
        { 
            return convertKey(key, true);
        }

        private Key convertKey(Key key, bool prefix) 
        { 
            if (key == null) 
            { 
                return null;
            }
            if (key.type != ClassDescriptor.FieldType.tpArrayOfObject) 
            { 
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            Object[] values = (Object[])key.oval;
            if ((!prefix && values.Length != types.Length) || values.Length > types.Length)
            { 
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            ByteBuffer buf = new ByteBuffer();
            int dst = 0;
                
            for (int i = 0; i < values.Length; i++) 
            { 
                dst = packKeyPart(buf, dst, types[i], values[i]);
            }
            return new Key(buf.toArray(), key.inclusion != 0);
        }

        private int packKeyPart(ByteBuffer buf, int dst, ClassDescriptor.FieldType type, object val)
        {
            switch (type) 
            {
                case ClassDescriptor.FieldType.tpBoolean:
                    dst = buf.packBool(dst, (bool)val);
                    break;
                case ClassDescriptor.FieldType.tpByte:
                    dst = buf.packI1(dst, (byte)val);
                    break;
                case ClassDescriptor.FieldType.tpSByte:
                    dst = buf.packI1(dst, (sbyte)val);
                    break;
                case ClassDescriptor.FieldType.tpShort:
                    dst = buf.packI2(dst, (short)val);
                    break;
                case ClassDescriptor.FieldType.tpUShort:
                    dst = buf.packI2(dst, (ushort)val);
                    break;
                case ClassDescriptor.FieldType.tpChar:
                    dst = buf.packI2(dst, (char)val);
                    break;
                case ClassDescriptor.FieldType.tpInt:
                case ClassDescriptor.FieldType.tpOid:
                    dst = buf.packI4(dst, (int)val);
                    break;            
                case ClassDescriptor.FieldType.tpEnum:
                    dst = buf.packI4(dst, Convert.ToInt32(val));
                    break;            
                case ClassDescriptor.FieldType.tpUInt:
                    dst = buf.packI4(dst, (int)(uint)val);
                    break;            
                case ClassDescriptor.FieldType.tpObject:
                    if (val != null) { 
                        Storage.MakePersistent(val);
                        dst =  buf.packI4(dst, Storage.GetOid(val));
                    } else { 
                        dst =  buf.packI4(dst, 0);
                    }
                    break;            
                case ClassDescriptor.FieldType.tpLong:
                    dst = buf.packI8(dst, (long)val);
                    break;            
                case ClassDescriptor.FieldType.tpULong:
                    dst = buf.packI8(dst, (long)(ulong)val);
                    break;            
                case ClassDescriptor.FieldType.tpDate:
                    dst = buf.packDate(dst, (DateTime)val);
                    break;            
                case ClassDescriptor.FieldType.tpFloat: 
                    dst = buf.packF4(dst, (float)val);
                    break;            
                case ClassDescriptor.FieldType.tpDouble: 
                    dst = buf.packF8(dst, (double)val);
                    break;            
                case ClassDescriptor.FieldType.tpDecimal: 
                    dst = buf.packDecimal(dst, (decimal)val);
                    break;            
                case ClassDescriptor.FieldType.tpGuid: 
                    dst = buf.packGuid(dst, (Guid)val);
                    break;            
                case ClassDescriptor.FieldType.tpString:
                    dst = buf.packString(dst, (string)val);
                    break;            
                case ClassDescriptor.FieldType.tpArrayOfByte:
                    buf.extend(dst+4);
                    if (val != null) 
                    { 
                        byte[] arr = (byte[])val;
                        int len = arr.Length;
                        Bytes.pack4(buf.arr, dst, len);
                        dst += 4;                          
                        buf.extend(dst + len);
                        Array.Copy(arr, 0, buf.arr, dst, len);
                        dst += len;
                    } 
                    else 
                    { 
                        Bytes.pack4(buf.arr, dst, 0);
                        dst += 4;
                    }
                    break;
                default:
                    Debug.Assert(false, "Invalid type");
                    break;
            }
            return dst;
        }
 
#if USE_GENERICS
        public override V Remove(Key key) 
#else
        public override object Remove(Key key) 
#endif
        {
            return base.Remove(convertKey(key, false));
        }       

#if USE_GENERICS
        public override void  Remove(Key key, V obj)
#else
        public override void  Remove(Key key, object obj)
#endif
        {

            base.Remove(convertKey(key, false), obj);
        }       

#if !USE_GENERICS
        public override object[] Get(Key from, Key till)
#else 
        public override V[] Get(Key from, Key till)
#endif
        {
            return base.Get(convertKey(from), convertKey(till));
        }

#if USE_GENERICS
        public override V Get(Key key) 
#else
        public override object Get(Key key) 
#endif
        {
            return base.Get(convertKey(key));
        }

#if USE_GENERICS
        public override bool Put(Key key, V obj)
#else
        public override bool Put(Key key, object obj)
#endif
        {
            return base.Put(convertKey(key, false), obj);
        }

#if USE_GENERICS
        public override V Set(Key key, V obj)
#else
        public override object Set(Key key, object obj)
#endif
        {
            return base.Set(convertKey(key, false), obj);
        }

 
#if USE_GENERICS
        public override IEnumerable<V> Range(Key from, Key till, IterationOrder order) 
#else
        public override IEnumerable Range(Key from, Key till, IterationOrder order) 
#endif
        { 
            return base.Range(convertKey(from), convertKey(till), order);
        }


        public override IDictionaryEnumerator GetDictionaryEnumerator(Key from, Key till, IterationOrder order) 
        {
            return base.GetDictionaryEnumerator(convertKey(from), convertKey(till), order);
        }

        public override int IndexOf(Key key) 
        { 
            return base.IndexOf(convertKey(key));
        }
    }
}