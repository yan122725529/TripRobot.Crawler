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
    class RndBtreeMultiFieldIndex<T>:RndBtree<object[],T>, MultiFieldIndex<T> where T:class
#else
    class RndBtreeMultiFieldIndex:RndBtree, MultiFieldIndex
#endif
    {
        internal String className;
        internal String[] fieldNames;
        [NonSerialized()]
        Type cls;
        [NonSerialized()]
        MemberInfo[] mbr;
 
        internal RndBtreeMultiFieldIndex()
        {
        }

        private void locateFields() 
        {
            mbr = new MemberInfo[fieldNames.Length];
            for (int i = 0; i < fieldNames.Length; i++) 
            {
                Type compType;
                mbr[i] = ClassDescriptor.lookupComponent(cls, fieldNames[i], out compType);
                if (mbr[i] == null) 
                { 
                   throw new StorageError(StorageError.ErrorCode.INDEXED_FIELD_NOT_FOUND, className + "." + fieldNames[i]);
                }
            }
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
                return mbr[0];
            }
        }

        public MemberInfo[] KeyFields 
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
            if (cls != typeof(T)) 
            {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_VALUE_TYPE, cls);
            }
#endif
            locateFields();
        }
        
#if USE_GENERICS
        internal RndBtreeMultiFieldIndex(string[] fieldNames, bool unique) 
        {
            this.cls = typeof(T);
#else
        internal RndBtreeMultiFieldIndex(Type cls, string[] fieldNames, bool unique) 
        {
            this.cls = cls;
#endif
            this.unique = unique;
            this.fieldNames = fieldNames;
            this.className = ClassDescriptor.getTypeName(cls);
            locateFields();
            type = ClassDescriptor.FieldType.tpValue;        
        }
        
        internal struct CompoundKey : IComparable
        {
            internal object[] keys;
            
            public int CompareTo(object o)
            {
                CompoundKey c = (CompoundKey) o;
                int n = keys.Length < c.keys.Length?keys.Length:c.keys.Length;
                for (int i = 0; i < n; i++)
                {
                    int diff = ((IComparable) keys[i]).CompareTo(c.keys[i]);
                    if (diff != 0)
                    {
                        return diff;
                    }
                }
                return  0; // allow to compare part of the compound key
            }
            
            internal CompoundKey(object[] keys)
            {
                this.keys = keys;
            }
        }
        
        private Key convertKey(Key key)
        {
            if (key == null)
            {
                return null;
            }
            if (key.type != ClassDescriptor.FieldType.tpArrayOfObject)
            {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            return new Key(new CompoundKey((object[]) key.oval), key.inclusion != 0);
        }
        
        private Key extractKey(object obj)
        {
            object[] keys = new object[mbr.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                object val = mbr[i] is FieldInfo ? ((FieldInfo)mbr[i]).GetValue(obj) : ((PropertyInfo)mbr[i]).GetValue(obj, null);
                keys[i] = val;
                if (!ClassDescriptor.IsEmbedded(val))
                {
                    Storage.MakePersistent(val);
                }
            }
            return new Key(new CompoundKey(keys));
        }
        
#if USE_GENERICS
        public override void Add(T obj)
        {
            Put(obj);
        }
#endif

#if USE_GENERICS
        public bool Put(T obj) 
#else
        public bool Put(object obj) 
#endif
        {
            return base.Put(extractKey(obj), obj);
        }

#if USE_GENERICS
        public T Set(T obj) 
#else
        public object Set(object obj) 
#endif
        {
            return base.Set(extractKey(obj), obj);
        }

#if USE_GENERICS
        public void BulkLoad(IEnumerable<T> members)
#else
        public void BulkLoad(IEnumerable members)
#endif
        {
            ArrayList list = new ArrayList();
            foreach (object obj in members) 
            {
                IComparable[] values = new IComparable[mbr.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = (IComparable)(mbr[i] is FieldInfo ? ((FieldInfo)mbr[i]).GetValue(obj) : ((PropertyInfo)mbr[i]).GetValue(obj, null));
                }
                list.Add(new MultiFieldValue(obj, values));
            }   
            MultiFieldValue[] arr = (MultiFieldValue[])list.ToArray(typeof(MultiFieldValue));
            Array.Sort(arr);
            for (int i = 0; i < arr.Length; i++) 
            { 
#if USE_GENERICS
                 Put((T)arr[i].obj);
#else
                 Put(arr[i].obj);
#endif
            }
        }

#if USE_GENERICS
        public override bool Remove(T obj) 
#else
        public bool Remove(object obj) 
#endif
        {
            return base.removeIfExists(extractKey(obj), obj);        
        }
        
#if USE_GENERICS
        public override T Remove(Key key) 
#else
        public override object Remove(Key key) 
#endif
        {
            return base.Remove(convertKey(key));
        }       

#if USE_GENERICS
        public override bool Contains(T obj) 
#else
        public bool Contains(object obj) 
#endif
        {
            Key key = extractKey(obj);
            if (unique) 
            { 
                return base.Get(key) != null;
            } 
            else 
            { 
                object[] mbrs = Get(key, key);
                for (int i = 0; i < mbrs.Length; i++) 
                { 
                    if (mbrs[i] == obj) 
                    { 
                        return true;
                    }
                }
                return false;
            }
        }

#if USE_GENERICS
        public void Append(T obj) 
#else
        public void Append(object obj) 
#endif
        {
            throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE);
        }

#if USE_GENERICS
        public override T[] Get(Key from, Key till)
#else
        public override object[] Get(Key from, Key till)
#endif
        {
            ArrayList list = new ArrayList();
            if (root != null)
            {
                root.find(convertKey(from), convertKey(till), height, list);
            }
#if USE_GENERICS
            return (T[]) list.ToArray(cls);
#else
            return (object[]) list.ToArray(cls);
#endif
        }

#if USE_GENERICS
        public override T[] ToArray() 
        {
            T[] arr = new T[nElems];
#else
        public override object[] ToArray() 
        {
            object[] arr = (object[])Array.CreateInstance(cls, nElems);
#endif
            if (root != null) 
            { 
                root.traverseForward(height, arr, 0);
            }
            return arr;
        }

        public override int IndexOf(Key key) 
        { 
            return base.IndexOf(convertKey(key));
        }

#if USE_GENERICS
        public override T Get(Key key) 
#else
        public override object Get(Key key) 
#endif
        {
            return base.Get(convertKey(key));
        }
 
#if USE_GENERICS
        public override IEnumerable<T> Range(Key from, Key till, IterationOrder order) 
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
    class RndBtreeCaseInsensitiveMultiFieldIndex<T>:RndBtreeMultiFieldIndex<T> where T:class
#else
    class RndBtreeCaseInsensitiveMultiFieldIndex:RndBtreeMultiFieldIndex
#endif
    {
        internal RndBtreeCaseInsensitiveMultiFieldIndex()
        {
        }
    
#if USE_GENERICS
        internal RndBtreeCaseInsensitiveMultiFieldIndex(string[] fieldNames, bool unique) 
        : base(fieldNames, unique)
        { 
        }
#else
        internal RndBtreeCaseInsensitiveMultiFieldIndex(Type cls, string[] fieldNames, bool unique) 
        : base(cls, fieldNames, unique)
        { 
        }
#endif

        internal override Key checkKey(Key key) 
        { 
            if (key != null) { 
                CompoundKey ck = (CompoundKey)key.oval;
                for (int i = 0; i < ck.keys.Length; i++) 
                {
                    string s = ck.keys[i] as string;
                    if (s != null) 
                    {
                        ck.keys[i] = s.ToLower();
                    }
                }
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