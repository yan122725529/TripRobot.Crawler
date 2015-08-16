using System;
#if USE_GENERICS
using System.Collections.Generic;
#endif
using System.Collections;
using Perst;

namespace Perst.Impl
{
    [Serializable]
#if USE_GENERICS
    internal class ThickIndex<K,V> : PersistentCollection<V>, Index<K,V> where V:class
#else
    internal class ThickIndex : PersistentCollection, Index 
#endif
    { 
#if USE_GENERICS
        internal Index<K,object> index;
#else
        internal Index index;
#endif
        internal int   nElems;

        const int BTREE_THRESHOLD = 128;

#if USE_GENERICS
        internal ThickIndex(StorageImpl db) 
            : base(db)
        {
            index = db.CreateIndex<K,object>(true);
        }
#else
        internal ThickIndex(StorageImpl db, Type keyType) 
            : base(db)
        {
            index = db.CreateIndex(keyType, true);
        }
#endif
    
        internal ThickIndex() {}

        public override int Count 
        { 
            get 
            {
                return nElems;
            }
        }

#if USE_GENERICS
        public V this[K key] 
#else
        public object this[object key] 
#endif
        {
            get 
            {
                return Get(key);
            }
            set 
            {
                Set(key, value);
            }
        } 
    
#if USE_GENERICS
        public V[] this[K from, K till] 
#else
        public object[] this[object from, object till] 
#endif
        {
            get 
            {
                return Get(from, till);
            }
        }

        protected virtual Key transformKey(Key key) 
        { 
            return key;
        }

        protected virtual string transformStringKey(string key)         
        { 
            return key;
        }


#if USE_GENERICS
        public V Get(Key key) 
#else
        public object Get(Key key) 
#endif
        {
            object s = index.Get(transformKey(key));
            if (s == null) 
            { 
                return null;
            }
#if USE_GENERICS
            Relation<V,V> r = s as Relation<V,V>;
#else
            Relation r = s as Relation;
#endif
            if (r != null)
            { 
                if (r.Count == 1) 
                { 
                    return r[0];
                }
            }
            throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
        }
                  
#if USE_GENERICS
        public V[] Get(Key from, Key till) 
#else
        public object[] Get(Key from, Key till) 
#endif
        {
            return extend(index.Get(transformKey(from), transformKey(till)));
        }

#if USE_GENERICS
        public V Get(K key) 
#else
        public object Get(object key) 
#endif
        {
            return Get(KeyBuilder.getKeyFromObject(key));
        }
    
#if USE_GENERICS
        public V[] Get(K from, K till) 
#else
        public object[] Get(object from, object till) 
#endif
        {
            return Get(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till));
        }

#if USE_GENERICS
        private V[] extend(object[] s) 
        { 
            List<V> list = new List<V>();
            for (int i = 0; i < s.Length; i++) 
            { 
                list.AddRange((ICollection<V>)s[i]);
            }
            return list.ToArray();
        }
#else
        private object[] extend(object[] s) 
        { 
            ArrayList list = new ArrayList();
            for (int i = 0; i < s.Length; i++) 
            { 
                list.AddRange((ICollection)s[i]);
            }
            return list.ToArray();
        }
#endif

                      
#if USE_GENERICS
        public V[] GetPrefix(string prefix) 
#else
        public object[] GetPrefix(string prefix) 
#endif
        { 
            return extend(index.GetPrefix(transformStringKey(prefix)));
        }
    
#if USE_GENERICS
        public V[] PrefixSearch(string word) 
#else
        public object[] PrefixSearch(string word) 
#endif
        { 
            return extend(index.PrefixSearch(transformStringKey(word)));
        }
           
        public int Size() 
        { 
            return nElems;
        }
    
        public override void Clear() 
        { 
            foreach (IPersistent p in index) 
            { 
                p.Deallocate();
            }
            index.Clear();
            nElems = 0;
            Modify();
        }

#if USE_GENERICS
        public V[] ToArray() 
#else
        public object[] ToArray() 
#endif
        { 
            return extend(index.ToArray());
        }
        
        public Array ToArray(Type elemType) 
        { 
            ArrayList list = new ArrayList();
            foreach (ICollection c in index) 
            { 
                list.AddRange(c);
            }
            return list.ToArray(elemType);
        }

#if USE_GENERICS
        class ExtendEnumerable : IEnumerable<V>, IEnumerable
#else
        class ExtendEnumerable : IEnumerable
#endif
        {

#if USE_GENERICS
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ExtendEnumerator(outer.GetEnumerator());
            }

            public IEnumerator<V> GetEnumerator() 
#else
            public IEnumerator GetEnumerator() 
#endif
            {
                return new ExtendEnumerator(outer.GetEnumerator());
            }

#if USE_GENERICS
            internal ExtendEnumerable(IEnumerable<object> enumerable) 
#else
            internal ExtendEnumerable(IEnumerable enumerable) 
#endif
            { 
                outer = enumerable;
            }

#if USE_GENERICS
            IEnumerable<object> outer;
#else
            IEnumerable outer;
#endif
       }


#if USE_GENERICS
        class ExtendEnumerator : IEnumerator<V>, PersistentEnumerator
#else
        class ExtendEnumerator : PersistentEnumerator
#endif
        {  
            public void Dispose() {}

            public bool MoveNext() 
            { 
                while (inner == null || !inner.MoveNext()) 
                {                 
                    if (outer.MoveNext()) 
                    {
#if USE_GENERICS
                        inner = ((IEnumerable<V>)outer.Current).GetEnumerator();
#else
                        inner = ((IEnumerable)outer.Current).GetEnumerator();
#endif
                    } 
                    else 
                    { 
                        return false;
                    }
                }
                return true;
            }

#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    if (inner == null)
                    {
                        throw new InvalidOperationException();
                    }
                    return inner.Current;
                }
            }

            public V Current 
#else
            public object Current 
#endif
            {
                get 
                {
                    if (inner == null)
                    {
                        throw new InvalidOperationException();
                    }
                    return inner.Current;
                }
            }

            public int CurrentOid 
            {
                get 
                {
                    return ((PersistentEnumerator)inner).CurrentOid;
                }
            }

            public void Reset() 
            {
#if !USE_GENERICS
                outer.Reset();
#endif
                if (outer.MoveNext()) 
                {
#if USE_GENERICS
                    inner = ((IEnumerable<V>)outer.Current).GetEnumerator();
#else
                    inner = ((IEnumerable)outer.Current).GetEnumerator();
#endif
                }
            }

#if USE_GENERICS
            internal ExtendEnumerator(IEnumerator<object> enumerator) 
#else
            internal ExtendEnumerator(IEnumerator enumerator) 
#endif
            { 
                outer = enumerator;
                Reset();
            }

#if USE_GENERICS
            private IEnumerator<object> outer;
            private IEnumerator<V>           inner;
#else
            private IEnumerator outer;
            private IEnumerator inner;
#endif
        }


        class ExtendDictionaryEnumerator : IDictionaryEnumerator 
        {  
            public object Current 
            {
                get 
                {
                    return Entry;
                }
            }

            public DictionaryEntry Entry 
            {
                get 
                {
                    return new DictionaryEntry(key, inner.Current);
                }
            }

            public object Key 
            {
                get 
                {
                    return key;
                }
            }

            public object Value 
            {
                get 
                {
                    return inner.Current;
                }
            }

            public void Dispose() {}

            public bool MoveNext() 
            { 
                while (inner == null || !inner.MoveNext()) 
                {                 
                    if (outer.MoveNext()) 
                    {
                        key = outer.Key;
#if USE_GENERICS
                        inner = ((IEnumerable<V>)outer.Value).GetEnumerator();
#else
                        inner = ((IEnumerable)outer.Value).GetEnumerator();
#endif
                    } 
                    else 
                    { 
                        return false;
                    }
                }
                return true;
            }

            public virtual void Reset() 
            {
#if !USE_GENERICS
                outer.Reset();
#endif
                if (outer.MoveNext()) 
                {
                    key = outer.Key;
#if USE_GENERICS
                    inner = ((IEnumerable<V>)outer.Value).GetEnumerator();
#else
                    inner = ((IEnumerable)outer.Value).GetEnumerator();
#endif
                }
            }
       
            internal ExtendDictionaryEnumerator(IDictionaryEnumerator enumerator) 
            { 
                outer = enumerator;
                Reset();
            }

            private IDictionaryEnumerator outer;
#if USE_GENERICS
            private IEnumerator<V>        inner;
#else
            private IEnumerator           inner;
#endif
            private object                key;
        }

        class ExtendDictionaryStartFromEnumerator : ExtendDictionaryEnumerator
        {  
#if USE_GENERICS
            internal ExtendDictionaryStartFromEnumerator(ThickIndex<K,V> index, int start, IterationOrder order) 
#else
            internal ExtendDictionaryStartFromEnumerator(ThickIndex index, int start, IterationOrder order) 
#endif
                : base(index.GetDictionaryEnumerator(null, null, order))
            { 
                this.index = index;
                this.start = start;
                this.order = order;
                Reset();
            }
            
            public override void Reset()
            {
                base.Reset();
                int skip = (order == IterationOrder.AscentOrder) ? start : index.Count - start - 1;
                while (--skip >= 0 && MoveNext());
            } 
                
#if USE_GENERICS
            ThickIndex<K,V> index;
#else
            ThickIndex index;
#endif
            IterationOrder order;
            int start;
        }

        public virtual IDictionaryEnumerator GetDictionaryEnumerator() 
        { 
            return new ExtendDictionaryEnumerator(index.GetDictionaryEnumerator());
        }

#if USE_GENERICS
        public override IEnumerator<V> GetEnumerator() 
        { 
            return new ExtendEnumerator(((IEnumerable<object>)index).GetEnumerator());
        }
#else
        public override IEnumerator GetEnumerator() 
        { 
            return new ExtendEnumerator(index.GetEnumerator());
        }
#endif

#if USE_GENERICS
        public IEnumerator<V> GetEnumerator(Key from, Key till, IterationOrder order) 
#else
        public IEnumerator GetEnumerator(Key from, Key till, IterationOrder order) 
#endif
        {
            return Range(from, till, order).GetEnumerator();
        }

#if USE_GENERICS
        public IEnumerator<V> GetEnumerator(K from, K till, IterationOrder order) 
#else
        public IEnumerator GetEnumerator(object from, object till, IterationOrder order) 
#endif
        {
            return Range(from, till, order).GetEnumerator();
        }

#if USE_GENERICS
        public IEnumerator<V> GetEnumerator(Key from, Key till) 
#else
        public IEnumerator GetEnumerator(Key from, Key till) 
#endif
        {
            return Range(from, till).GetEnumerator();
        }

#if USE_GENERICS
        public IEnumerator<V> GetEnumerator(K from, K till) 
#else
        public IEnumerator GetEnumerator(object from, object till) 
#endif
        {
            return Range(from, till).GetEnumerator();
        }

#if USE_GENERICS
        public IEnumerator<V> GetEnumerator(string prefix) 
#else
        public IEnumerator GetEnumerator(string prefix) 
#endif
        {
            return StartsWith(prefix).GetEnumerator();
        }

#if USE_GENERICS        
        public V First
        {
            get
            {
                IEnumerator<V> e = GetEnumerator(null, null, IterationOrder.AscentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }

        public V Last
        {
            get
            {
                IEnumerator<V> e = GetEnumerator(null, null, IterationOrder.DescentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }
#else
        public object First
        {
            get
            {
                IEnumerator e = GetEnumerator(null, null, IterationOrder.AscentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }

        public object Last
        {
            get
            {
                IEnumerator e = GetEnumerator(null, null, IterationOrder.DescentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }
#endif

#if USE_GENERICS
        public virtual IEnumerable<V> Range(Key from, Key till, IterationOrder order) 
#else
        public virtual IEnumerable Range(Key from, Key till, IterationOrder order) 
#endif
        { 
            return new ExtendEnumerable(index.Range(transformKey(from), transformKey(till), order));
        }

#if USE_GENERICS
        public virtual IEnumerable<V> Reverse() 
#else
        public virtual IEnumerable Reverse() 
#endif
        { 
            return new ExtendEnumerable(index.Reverse());
        }

#if USE_GENERICS
        public virtual IEnumerable<V> Range(Key from, Key till) 
#else
        public virtual IEnumerable Range(Key from, Key till) 
#endif
        { 
            return Range(from, till, IterationOrder.AscentOrder);
        }
            
#if USE_GENERICS
        IEnumerable GenericIndex.Range(Key from, Key till, IterationOrder order) 
        {
            return (IEnumerable)Range(from, till, order);
        }

        public IEnumerable<V> Range(K from, K till, IterationOrder order) 
#else
        public IEnumerable Range(object from, object till, IterationOrder order) 
#endif
        { 
            return Range(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till), order);
        }

#if USE_GENERICS
        public IEnumerable<V> Range(K from, K till) 
#else
        public IEnumerable Range(object from, object till) 
#endif
        { 
            return Range(from, till, IterationOrder.AscentOrder);
        }
 
#if USE_GENERICS        
        IEnumerable GenericIndex.StartsWith(string prefix) 
        {
            return (IEnumerable)StartsWith(prefix);
        }

        public IEnumerable<V> StartsWith(string prefix) 
#else
        public IEnumerable StartsWith(string prefix) 
#endif
        {
            return StartsWith(prefix, IterationOrder.AscentOrder); 
        }

#if USE_GENERICS        
        IEnumerable GenericIndex.StartsWith(string prefix, IterationOrder order) 
        {
            return (IEnumerable)StartsWith(prefix, order);
        }

        public IEnumerable<V> StartsWith(string prefix, IterationOrder order) 
#else
        public IEnumerable StartsWith(string prefix, IterationOrder order) 
#endif
        { 
            return new ExtendEnumerable(index.StartsWith(transformStringKey(prefix), order));
        }
 
        public virtual IDictionaryEnumerator GetDictionaryEnumerator(Key from, Key till, IterationOrder order) 
        { 
            return new ExtendDictionaryEnumerator(index.GetDictionaryEnumerator(transformKey(from), transformKey(till), order));
        }

        public Type KeyType 
        { 
            get 
            {
                return index.KeyType;
            }
        }

#if USE_GENERICS
        public bool Put(Key key, V obj) 
        { 
            object s = index.Get(key);
            int oid = storage.GetOid(obj);
            if (oid == 0) { 
                oid = storage.MakePersistent(obj);
            }
            if (s == null) 
            { 
                Relation<V,V> r = Storage.CreateRelation<V,V>(null);
                r.Add(obj);
                index.Put(key, r);
            } 
            else if (s is Relation<V,V>) 
            { 
                Relation<V,V> rel = (Relation<V,V>)s;
                if (rel.Count == BTREE_THRESHOLD) 
                {
                    ISet<V> ps = Storage.CreateBag<V>();
                    for (int i = 0; i < BTREE_THRESHOLD; i++) 
                    { 
                        ps.Add(rel[i]);
                    }
                    ps.Add(obj);
                    index.Set(key, ps);
                    rel.Deallocate();
                } 
                else 
                { 
                    int l = 0, n = rel.Count, r = n;
                    while (l < r) { 
                        int m = (l + r) >> 1;
                        if (Storage.GetOid(rel.GetRaw(m)) <= oid) { 
                            l = m + 1;
                        } else { 
                            r = m;
                        }
                    }
                    rel.Insert(r, obj);
                }
            } 
            else 
            { 
                ((ISet<V>)s).Add(obj);
            }
            nElems += 1;
            Modify();
            return true;
        }
#else
        public bool Put(Key key, object obj) 
        { 
            object s = index.Get(key);
            int oid = storage.GetOid(obj);
            if (oid == 0) { 
                oid = storage.MakePersistent(obj);
            }
            if (s == null) 
            { 
                Relation r = Storage.CreateRelation(null);
                r.Add(obj);
                index.Put(key, r);
            } 
            else if (s is Relation) 
            { 
                Relation rel = (Relation)s;
                if (rel.Count == BTREE_THRESHOLD) 
                {
                    ISet ps = Storage.CreateBag();
                    for (int i = 0; i < BTREE_THRESHOLD; i++) 
                    { 
                        ps.Add(rel.GetRaw(i));
                    }
                    ps.Add(obj);
                    index.Set(key, ps);
                    rel.Deallocate();
                } 
                else 
                { 
                    int l = 0, n = rel.Count, r = n;
                    while (l < r) { 
                        int m = (l + r) >> 1;
                        if (Storage.GetOid(rel.GetRaw(m)) <= oid) { 
                            l = m + 1;
                        } else { 
                            r = m;
                        }
                    }
                    rel.Insert(r, obj);
                }
            } 
            else 
            { 
                ((ISet)s).Add(obj);
            }
            nElems += 1;
            Modify();
            return true;
        }
#endif


#if USE_GENERICS
        public V Set(Key key, V obj) 
        {
            object s = index.Get(key);
            int oid = storage.GetOid(obj);
            if (oid == 0) { 
                oid = storage.MakePersistent(obj);
            }
            if (s == null) 
            { 
                Relation<V,V> r = Storage.CreateRelation<V,V>(null);
                r.Add(obj);
                index.Put(key, r);
                nElems += 1;
                Modify();
                return null;
            } 
            else if (s is Relation<V,V>) 
            { 
                Relation<V,V> r = (Relation<V,V>)s;
                if (r.Count == 1) 
                {
                    V prev = r[0];
                    r[0] = obj;
                    return prev;
                } 
            }
            throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
        }
#else
        public object Set(Key key, object obj) 
        {
            object s = index.Get(key);
            int oid = storage.GetOid(obj);
            if (oid == 0) { 
                oid = storage.MakePersistent(obj);
            }
            if (s == null) 
            { 
                Relation r = Storage.CreateRelation(null);
                r.Add(obj);
                index[key] = r;
                nElems += 1;
                Modify();
                return null;
            } 
            else if (s is Relation) 
            { 
                Relation r = (Relation)s;
                if (r.Count == 1) 
                {
                    object prev = r[0];
                    r[0] = obj;
                    return prev;
                } 
            }
            throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
        }
#endif

#if USE_GENERICS
        protected bool RemoveIfExists(Key key, V obj) 
        { 
            object s = index.Get(key);
            if (s is Relation<V,V>) 
            { 
                Relation<V,V> r = (Relation<V,V>)s;
                int i = r.IndexOf(obj);
                if (i >= 0) 
                { 
                    r.Remove(i);
                    if (r.Count == 0) 
                    { 
                        index.Remove(key, r);
                        r.Deallocate();
                    }
                    nElems -= 1;
                    Modify();
                    return true;
                }
            } 
            else if (s is ISet<V>) 
            { 
                ISet<V> ps = (ISet<V>)s;
                if (ps.Remove(obj)) 
                { 
                    if (ps.Count == 0) 
                    { 
                        index.Remove(key, ps);
                        ps.Deallocate();
                    }                    
                    nElems -= 1;
                    Modify();
                    return true;
                }
            }
            return false;
        }
#else
        public bool RemoveIfExists(Key key, object obj) 
        { 
            object s = index.Get(key);
            if (s is Relation) 
            { 
                Relation rel = (Relation)s;
                int oid = Storage.GetOid(obj);
                int l = 0, n = rel.Count, r = n;
                while (l < r) { 
                    int m = (l + r) >> 1;
                    if (Storage.GetOid(rel.GetRaw(m)) < oid) { 
                        l = m + 1;
                    } else { 
                        r = m;
                    }
                }
                if (r < n && Storage.GetOid(rel.GetRaw(r)) == oid)
                { 
                    rel.Remove(r);
                    if (rel.Count == 0) 
                    { 
                        index.Remove(key, rel);
                        rel.Deallocate();
                    }
                    nElems -= 1;
                    Modify();
                    return true;
                }
            } 
            else if (s is ISet) 
            { 
                ISet ps = (ISet)s;
                if (ps.Remove(obj)) 
                { 
                    if (ps.Count == 0) 
                    { 
                        index.Remove(key, ps);
                        ps.Deallocate();
                    }                    
                    nElems -= 1;
                    Modify();
                    return true;
                }
            }
            return false;
        }
#endif
#if USE_GENERICS
        public void Remove(Key key, V obj)
#else 
        public void Remove(Key key, object obj) 
#endif
        {
            if (!RemoveIfExists(key, obj))
            {
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_FOUND);
            }
        }

#if USE_GENERICS
        public bool Unlink(Key key, V obj)
#else 
        public bool Unlink(Key key, object obj) 
#endif
        {
            return RemoveIfExists(key, obj);
        }

#if USE_GENERICS
        public V Remove(Key key) 
#else
        public object Remove(Key key) 
#endif
        {
            throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
        }

#if USE_GENERICS
        public bool Put(K key, V obj) 
#else
        public bool Put(object key, object obj) 
#endif
        {
            return Put(KeyBuilder.getKeyFromObject(key), obj);
        }

#if USE_GENERICS
        public V Set(K key, V obj) 
#else
        public object Set(object key, object obj) 
#endif
        {
            return Set(KeyBuilder.getKeyFromObject(key), obj);
        }

#if USE_GENERICS
        public void Remove(K key, V obj) 
#else
        public void Remove(object key, object obj) 
#endif
        {
            Remove(KeyBuilder.getKeyFromObject(key), obj);
        }

#if USE_GENERICS
        public V RemoveKey(K key) 
#else
        public object RemoveKey(object key) 
#endif
        {
            throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
        }

        public override void Deallocate() 
        {
            Clear();
            index.Deallocate();
            base.Deallocate();
        }

        public virtual int IndexOf(Key key)     
        { 
            PersistentEnumerator iterator = (PersistentEnumerator)GetEnumerator(null, key, IterationOrder.DescentOrder);
            int i;
            for (i = -1; iterator.MoveNext(); i++);
            return i;
        }

#if USE_GENERICS        
        public V GetAt(int i)
#else
        public object GetAt(int i)
#endif
        {
            IDictionaryEnumerator e;
            if (i < 0 || i >= nElems)
            {
                throw new IndexOutOfRangeException("Position " + i + ", index size "  + nElems);
            }            
            if (i <= (nElems/2)) 
            {
                e = GetDictionaryEnumerator(null, null, IterationOrder.AscentOrder);
                while (i-- >= 0) 
                { 
                      e.MoveNext();
                }
            }
            else
            {
                e = GetDictionaryEnumerator(null, null, IterationOrder.DescentOrder);
                i -= nElems;
                while (++i < 0) 
                { 
                      e.MoveNext();
                }
            }
#if USE_GENERICS        
            return (V)e.Value;   
#else
            return e.Value;   
#endif
        }

        public IDictionaryEnumerator GetDictionaryEnumerator(int start, IterationOrder order) 
        {
            return new ExtendDictionaryStartFromEnumerator(this, start, order);
        }
        
        public bool IsUnique
        {     
            get
            {
                return false;
            }                
        }
    }
}
