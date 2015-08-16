namespace Perst.Impl
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
    using System.Reflection;
    using System.Diagnostics;
    using Perst;
	
#if USE_GENERICS
    class FieldValue : IComparable<FieldValue>
#else
    class FieldValue : IComparable 
#endif
    {
        public IComparable value;
        public object      obj;
    
#if USE_GENERICS
        public int CompareTo(FieldValue f) 
        { 
            return value.CompareTo(f.value);
        }
#else
        public int CompareTo(object f) 
        { 
            return value.CompareTo(((FieldValue)f).value);
        }
#endif
    
        public FieldValue(Object obj, Object value) 
        { 
            this.obj = obj;
            this.value = (IComparable)value;
        }
    }

    [Serializable]
#if USE_GENERICS
    class BtreeFieldIndex<K,V>:Btree<K,V>, FieldIndex<K,V> where V:class
#else
    class BtreeFieldIndex:Btree, FieldIndex
#endif
    {
        internal String className;
        internal String fieldName;
        internal long   autoincCount;
        [NonSerialized()]
        Type cls;
        [NonSerialized()]
        MemberInfo mbr;
        [NonSerialized()]
        Type mbrType;
 
        internal BtreeFieldIndex()
        {
        }

        private void lookupField(String name) 
        {
            mbr = ClassDescriptor.lookupComponent(cls, name, out mbrType);
            if (mbr == null)  
            {
                throw new StorageError(StorageError.ErrorCode.INDEXED_FIELD_NOT_FOUND, className + "." + fieldName);
            }
#if USE_GENERICS
            if (mbrType != typeof(K)) { 
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE, mbrType);
            }    
#endif
        }

        public Type IndexedClass 
        {
            get 
            { 
                return cls;
            }
        }

        public MemberInfo KeyField 
        {
            get 
            { 
                return mbr;
            }
        }

        public override void OnLoad()
        {
            cls = ClassDescriptor.lookup(Storage, className);
#if USE_GENERICS
            if (cls != typeof(V)) 
            {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_VALUE_TYPE, cls);
            }
#endif
            lookupField(fieldName);
        }
		
#if USE_GENERICS
        internal BtreeFieldIndex(String fieldName, bool unique) 
        : this(fieldName, unique, 0)
        {
        }
#else
        internal BtreeFieldIndex(Type cls, String fieldName, bool unique) 
        : this(cls, fieldName, unique, 0)
        {
        }
#endif

#if USE_GENERICS
        internal BtreeFieldIndex(String fieldName, bool unique, long autoincCount) 
        : this(typeof(V), fieldName, unique, autoincCount)
        {
        }
#endif

        internal BtreeFieldIndex(Type cls, string fieldName, bool unique, long autoincCount) 
        {
            init(cls, null, new string[]{fieldName}, unique, autoincCount);
        }
        
        public override void init(Type cls, ClassDescriptor.FieldType[] types, string[] fieldNames, bool unique, long autoincCount) 
        {    
            this.cls = cls;
            this.unique = unique;
            this.fieldName = fieldNames[0];
            this.className = ClassDescriptor.getTypeName(cls);
            this.autoincCount = autoincCount;
            lookupField(fieldNames[0]);
            type = checkType(mbrType);
        }

        protected override object unpackEnum(int val) 
        {
            return Enum.ToObject(mbrType, val);
        }

        private Key extractKey(object obj) 
        { 
            Object val = mbr is FieldInfo ? ((FieldInfo)mbr).GetValue(obj) : ((PropertyInfo)mbr).GetValue(obj, null);
            if (val == null)
            {
                return null;
            }
            Key key = null;
            switch (type) 
            {
                case ClassDescriptor.FieldType.tpBoolean:
                    key = new Key((bool)val);
                    break;
                case ClassDescriptor.FieldType.tpByte:
                    key = new Key((byte)val);
                    break;
                case ClassDescriptor.FieldType.tpSByte:
                    key = new Key((sbyte)val);
                    break;
                case ClassDescriptor.FieldType.tpShort:
                    key = new Key((short)val);
                    break;
                case ClassDescriptor.FieldType.tpUShort:
                    key = new Key((ushort)val);
                    break;
                case ClassDescriptor.FieldType.tpChar:
                    key = new Key((char)val);
                    break;
                case ClassDescriptor.FieldType.tpInt:
                    key = new Key((int)val);
                    break;            
                case ClassDescriptor.FieldType.tpUInt:
                    key = new Key((uint)val);
                    break;            
                case ClassDescriptor.FieldType.tpOid:
                    key = new Key(ClassDescriptor.FieldType.tpOid, (int)val);
                    break;
                case ClassDescriptor.FieldType.tpObject:
                    key = new Key(val, Storage.MakePersistent(val), true);
                    break;
                case ClassDescriptor.FieldType.tpLong:
                    key = new Key((long)val);
                    break;            
                case ClassDescriptor.FieldType.tpULong:
                    key = new Key((ulong)val);
                    break;            
                case ClassDescriptor.FieldType.tpDate:
                    key = new Key((DateTime)val);
                    break;
                case ClassDescriptor.FieldType.tpFloat:
                    key = new Key((float)val);
                    break;
                case ClassDescriptor.FieldType.tpDouble:
                    key = new Key((double)val);
                    break;
                case ClassDescriptor.FieldType.tpDecimal:
                    key = new Key((decimal)val);
                    break;
                case ClassDescriptor.FieldType.tpGuid:
                    key = new Key((Guid)val);
                    break;
                case ClassDescriptor.FieldType.tpString:
                    key = new Key((string)val);
                    break;
                case ClassDescriptor.FieldType.tpEnum:
                    key = new Key((Enum)val);
                    break;
                default:
                    Debug.Assert(false, "Invalid type");
                    break;
            }
            return key;
        }
 
#if USE_GENERICS
        public override void Add(V obj)
        {
            Put(obj);
        }
#endif

#if USE_GENERICS
        public bool Put(V obj) 
#else
        public bool Put(object obj) 
#endif
        {
            Key key = extractKey(obj);
            return key != null && base.Put(key, obj);
        }

#if USE_GENERICS
        public V Set(V obj) 
#else
        public object Set(object obj) 
#endif
        {
            Key key = extractKey(obj);
            if (key == null) 
            {
                throw new StorageError(StorageError.ErrorCode.KEY_IS_NULL);
            }
            return base.Set(key, obj);
        }

#if USE_GENERICS
        public void BulkLoad(IEnumerable<V> members)
#else
        public void BulkLoad(IEnumerable members)
#endif
        {
            ArrayList list = new ArrayList();
            foreach (object obj in members) 
            {
                Object val = mbr is FieldInfo ? ((FieldInfo)mbr).GetValue(obj) : ((PropertyInfo)mbr).GetValue(obj, null);
                list.Add(new FieldValue(obj, val));
            }   
            FieldValue[] arr = (FieldValue[])list.ToArray(typeof(FieldValue));
            Array.Sort(arr);
            for (int i = 0; i < arr.Length; i++) 
            { 
#if USE_GENERICS
                 Put((V)arr[i].obj);
#else
                 Put(arr[i].obj);
#endif
            }
        }


#if USE_GENERICS
        public override bool Remove(V obj) 
#else
        public bool Remove(object obj) 
#endif
        {
            Key key = extractKey(obj);
            if (key == null) 
            {
                 return false;
            }
            try 
            {
                base.Remove(key, obj);
            }
            catch (StorageError x) 
            { 
                if (x.Code == StorageError.ErrorCode.KEY_NOT_FOUND) 
                { 
                    return false;
                }
                throw x;
            }
            return true;
        }
        
#if USE_GENERICS
        public override bool Contains(V obj) 
#else
        public bool Contains(object obj) 
#endif
        {
            Key key = extractKey(obj);
            if (key == null)
            { 
                return false;
            }
            if (unique) { 
                return base.Get(key) != null;
            } else { 
                object[] mbrs = Get(key, key);
                for (int i = 0; i < mbrs.Length; i++) { 
                    if (mbrs[i] == obj) { 
                        return true;
                    }
                }
                return false;
            }
        }

#if USE_GENERICS
        public void Append(V obj)
#else
        public void Append(object obj)
#endif
        {
            lock(this) 
            { 
                Key key;
                object val; 
                switch (type) 
                {
                    case ClassDescriptor.FieldType.tpInt:
                        key = new Key((int)autoincCount);
                        val = (int)autoincCount;
                        break;            
                    case ClassDescriptor.FieldType.tpLong:
                        key = new Key(autoincCount);
                        val = autoincCount;
                        break;            
                    default:
                        throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE, mbrType);
                }
                if (mbr is FieldInfo) 
                { 
                    ((FieldInfo)mbr).SetValue(obj, val);
                } 
                else 
                {
                    ((PropertyInfo)mbr).SetValue(obj, val, null);
                }              
                autoincCount += 1;
                Storage.Modify(obj);
                base.insert(key, obj, false);
            }
        }

#if USE_GENERICS
        public override V[] Get(Key from, Key till)
#else
        public override object[] Get(Key from, Key till)
#endif
        {
            ArrayList list = new ArrayList();
            if (root != 0)
            {
                BtreePage.find((StorageImpl) Storage, root, checkKey(from), checkKey(till), this, height, list);
            }
#if USE_GENERICS
            return (V[]) list.ToArray(cls);
#else
            return (object[]) list.ToArray(cls);
#endif
        }

#if USE_GENERICS
        public override V[] ToArray() 
        {
            V[] arr = (V[])Array.CreateInstance(cls, nElems);
#else
        public override object[] ToArray() 
        {
            object[] arr = (object[])Array.CreateInstance(cls, nElems);
#endif
            if (root != 0) { 
                BtreePage.traverseForward((StorageImpl)Storage, root, type, height, arr, 0);
            }
            return arr;
        }

#if !USE_GENERICS
        public IEnumerable Select(string predicate) 
        { 
            Query query = new QueryImpl(Storage);
            return query.Select(cls, this, predicate);
        }
#endif

        public virtual bool IsCaseInsensitive
        {
            get
            {
                return false;
            }
        }
    }

#if USE_GENERICS
    class BtreeCaseInsensitiveFieldIndex<K,V>:BtreeFieldIndex<K,V> where V:class
#else
    class BtreeCaseInsensitiveFieldIndex:BtreeFieldIndex
#endif
    {    
#if USE_GENERICS
        internal BtreeCaseInsensitiveFieldIndex(String fieldName, bool unique) 
        : base(fieldName, unique)
        {
        }
#else
        internal BtreeCaseInsensitiveFieldIndex(Type cls, String fieldName, bool unique) 
        : base(cls, fieldName, unique)
        {
        }
#endif
#if USE_GENERICS
        BtreeCaseInsensitiveFieldIndex(String fieldName, bool unique, long autoincCount) 
        : base(fieldName, unique, autoincCount)
        {
        }
#endif
        internal BtreeCaseInsensitiveFieldIndex(Type cls, string fieldName, bool unique, long autoincCount) 
        : base(cls, fieldName, unique, autoincCount)
        {
        }

        internal BtreeCaseInsensitiveFieldIndex()
        {
        }

        protected override Key checkKey(Key key) 
        { 
            if (key != null && key.oval is string) 
            { 
                key = new Key(((string)key.oval).ToLower(), key.inclusion != 0);
            }
            return base.checkKey(key);
        }

        public override bool IsCaseInsensitive
        {
            get
            {
                return true;
            }
        }
    }  
}

