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

    [Serializable]
#if USE_GENERICS
    class ThickFieldIndex<K,V> : ThickIndex<K,V>, FieldIndex<K,V> where V:class
#else
    class ThickFieldIndex : ThickIndex, FieldIndex
#endif
    {
        struct KeyMember 
        {
            public MemberInfo info;
            public Type       type;
        }

        internal String   fieldName;
        internal Type     cls;
        internal ClassDescriptor.FieldType type;
 
        [NonSerialized()]
        KeyMember mbr;
 
        internal ThickFieldIndex()
        {
        }

        static KeyMember lookupKey(Type cls, String fieldName) 
        {            
            KeyMember key;
            key.info = ClassDescriptor.lookupComponent(cls, fieldName, out key.type);
            if (key.info == null)  
            {
                throw new StorageError(StorageError.ErrorCode.INDEXED_FIELD_NOT_FOUND, cls.FullName + "." + fieldName);
            }
#if USE_GENERICS
            if (cls != typeof(V))  {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_VALUE_TYPE, cls);
            }
            if (key.type != typeof(K)) { 
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE, key.type);
            }    
#endif
            return key;
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
                return mbr.info;
            }
        }

        public override void OnLoad()
        {
            mbr = lookupKey(cls, fieldName);
        }
		
#if USE_GENERICS
        internal ThickFieldIndex(StorageImpl db, String fieldName) 
        : this(db, typeof(V), fieldName, lookupKey(typeof(V), fieldName))
        {
        }

        private ThickFieldIndex(StorageImpl db, Type cls, string fieldName, KeyMember mbr) 
        : base(db)
#else
        internal ThickFieldIndex(StorageImpl db, Type cls, String fieldName) 
        : this(db, cls, fieldName, lookupKey(cls, fieldName))
        {
        }

        private ThickFieldIndex(StorageImpl db, Type cls, string fieldName, KeyMember mbr) 
        : base(db, mbr.type)
#endif
        {          
            this.mbr = mbr;
            this.cls = cls;
            this.fieldName = fieldName;
            type = ClassDescriptor.convertToNotNullable(ClassDescriptor.getTypeCode(mbr.type));          
        }

        private Key extractKey(object obj) 
        { 
            Object val = mbr.info is FieldInfo ? ((FieldInfo)mbr.info).GetValue(obj) : ((PropertyInfo)mbr.info).GetValue(obj, null);
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
                    key = new Key(transformStringKey((string)val));
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
                Object val = mbr.info is FieldInfo ? ((FieldInfo)mbr.info).GetValue(obj) : ((PropertyInfo)mbr.info).GetValue(obj, null);
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
            return key != null && base.RemoveIfExists(key, obj);
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
            object[] mbrs = Get(key, key);
            for (int i = 0; i < mbrs.Length; i++) { 
                if (mbrs[i] == obj) { 
                    return true;
                }
            }
            return false;
        }

#if USE_GENERICS
        public void Append(V obj)
#else
        public void Append(object obj)
#endif
        {
            throw new InvalidOperationException("ThickFieldIndex.Append");
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
    class ThickCaseInsensitiveFieldIndex<K,V>:ThickFieldIndex<K,V> where V:class
#else
    class ThickCaseInsensitiveFieldIndex:ThickFieldIndex
#endif
    {    
#if USE_GENERICS
        internal ThickCaseInsensitiveFieldIndex(StorageImpl db, String fieldName) 
        : base(db, fieldName)
        {
        }
#else
        internal ThickCaseInsensitiveFieldIndex(StorageImpl db, Type cls, String fieldName) 
        : base(db, cls, fieldName)
        {
        }
#endif

        internal ThickCaseInsensitiveFieldIndex()
        {
        }

        protected override Key transformKey(Key key) 
        { 
            if (key != null && key.oval is string) 
            { 
                key = new Key(((string)key.oval).ToLower(), key.inclusion != 0);
            }
            return key;
        }

        protected override string transformStringKey(string key)         
        { 
            return key.ToLower();
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

