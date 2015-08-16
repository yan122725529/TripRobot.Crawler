
namespace Perst.Impl
{
    using System;
    using Perst;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;

    [Serializable]
#if USE_GENERICS
    public class PersistentMapImpl<K,V>:PersistentResource,IPersistentMap<K,V> where K:IComparable where V:class
#else
    public class PersistentMapImpl:PersistentCollection,IPersistentMap
#endif
    {
#if USE_GENERICS
        internal Index<K,V> index;
        internal Link<V>  values;
        [NonSerialized] 
        ICollection<V> valueSet;
        [NonSerialized] 
        ICollection<K> keySet;
#else
        internal Index    index;
        internal Link     values;
        [NonSerialized] 
        ICollection valueSet;
        [NonSerialized] 
        ICollection keySet;
        internal ClassDescriptor.FieldType type;
#endif
        internal Array keys;

        const int BtreeTreshold = 128;

#if USE_GENERICS
        internal PersistentMapImpl(Storage storage, int initialSize) 
            : base(storage)
        {
            keys = new K[initialSize];
            values = storage.CreateLink<V>(initialSize);
        }
#else
        internal PersistentMapImpl(Storage storage, Type keyType, int initialSize) 
            : base(storage)
        {
            type = ClassDescriptor.getTypeCode(keyType);
            keys = new IComparable[initialSize];
            values = storage.CreateLink(initialSize);
        }
#endif

        internal PersistentMapImpl() {}

                                                                                                                
#if USE_GENERICS
        public int Count 
#else
        public override int Count 
#endif
        {
            get 
            {
                return index != null ? index.Count : values.Count;
            }
        }

#if USE_GENERICS
        public bool IsSynchronized 
        {
            get 
            {
                return true;
            }
        }

        public object SyncRoot 
        {
            get 
            {
                return this;
            }
        }

        public virtual bool Contains(KeyValuePair<K,V> pair) 
        {
            V v;
            return TryGetValue(pair.Key, out v) && pair.Value == v;
        }


        public virtual void CopyTo(KeyValuePair<K,V>[] dst, int i) 
        {
            foreach (KeyValuePair<K,V> pair in this) 
            { 
                dst[i++] = pair;
            }
        }

        public virtual void Add(KeyValuePair<K,V> pair)
        {
            Add(pair.Key, pair.Value);
        }

        public virtual bool Remove(KeyValuePair<K,V> pair) 
        {        
            V v;
            if (TryGetValue(pair.Key, out v) && pair.Value == v)
            {
                return Remove(pair.Key);
            }
            return false;
        }     
#endif 

#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
        class NaturalComparer : IComparer 
        {
            public int Compare(object a, object b)
            {
                return ((IComparable)a).CompareTo(b);
            }
        }
        readonly NaturalComparer comparer = new NaturalComparer();
#endif

        int binarySearch(object key) 
        {
#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
            return Array.BinarySearch((Array)keys, 0, values.Count, key, comparer);
#else
            return Array.BinarySearch((Array)keys, 0, values.Count, key);
#endif
        }


#if USE_GENERICS
        public bool ContainsKey(K key) 
#else
        public bool Contains(Object key) 
#endif
        {
            if (index != null) 
            { 
                Key k = KeyBuilder.getKeyFromObject(key);
                return index.GetDictionaryEnumerator(k, k, IterationOrder.AscentOrder).MoveNext();
            } 
            else 
            {
                int i = binarySearch(key);
                return i >= 0;
            }
        }

        
#if USE_GENERICS
        public V this[K key] 
        {
            get 
            {
                V val;
                if (!TryGetValue(key, out val)) 
                {
                    throw new KeyNotFoundException();
                }
                return val;
            }                
            set 
            {
                if (index == null) 
                { 
                    int size = values.Count;
                    int i = binarySearch(key);
                    if (i >= 0) 
                    {
                        values[i] = value;
                    } 
                    else 
                    {
                        if (size == BtreeTreshold) 
                        { 
                            index = Storage.CreateIndex<K,V>(true);
                            K[] keys = (K[])this.keys;
                            for (i = 0; i < size; i++) 
                            { 
                                index[keys[i]] = values[i];
                            }                
                            index[key] = value;
                            this.keys = null;
                            this.values = null;                
                            Modify();
                        } 
                        else 
                        {
                            K[] oldKeys = (K[])keys;
							i = ~i;
                            if (size >= oldKeys.Length) 
                            { 
                                K[] newKeys = new K[size+1 > oldKeys.Length*2 ? size+1 : oldKeys.Length*2];
                                Array.Copy(oldKeys, 0, newKeys, 0, i);                
                                Array.Copy(oldKeys, i, newKeys, i+1, size-i);
                                keys = newKeys;
                                newKeys[i] = key;
                            } 
                            else 
                            {
                                Array.Copy(oldKeys, i, oldKeys, i+1, size-i);
                                oldKeys[i] = key;
                            }
                            values.Insert(i, value);
                        }
                    }
                } 
                else 
                { 
                    index[key] = value;
                }
            }
        }
#else
        public object this[object key] 
        {
            get 
            {
                object val;
                TryGetValue(key, out val);
                return val;
            }
            set 
            {
                if (index == null) 
                { 
                    int size = values.Count;
                    int i = binarySearch(key);
                    if (i >= 0) 
                    {
                        values[i] = value;
                    } 
                    else 
                    {
                        if (size == BtreeTreshold) 
                        { 
                            index = Storage.CreateIndex(Btree.mapKeyType(type), true);
                            object[] keys = (object[])this.keys;
                            for (i = 0; i < size; i++) 
                            { 
                                index[keys[i]] = values[i];
                            }                
                            index[key] = value;
                            this.keys = null;
                            this.values = null;                
                            Modify();
                        } 
                        else 
                        {
                            object[] oldKeys = (object[])keys;
							i = ~i;
                            if (size >= oldKeys.Length) 
                            { 
                                object[] newKeys = new IComparable[size+1 > oldKeys.Length*2 ? size+1 : oldKeys.Length*2];
                                Array.Copy(oldKeys, 0, newKeys, 0, i);                
                                Array.Copy(oldKeys, i, newKeys, i+1, size-i);
                                keys = newKeys;
                                newKeys[i] = key;
                            } 
                            else 
                            {
                                Array.Copy(oldKeys, i, oldKeys, i+1, size-i);
                                oldKeys[i] = key;
                            }
                            values.Insert(i, value);
                        }
                    }
                } 
                else 
                { 
                    index[key] = value;
                }
            }
        }
#endif

#if USE_GENERICS
        public void Add(K key, V val)
        {
            if (!ContainsKey(key)) 
            {
                this[key] = val;
            }
        }
#else
        public void Add(object key, Object val)
        {
            if (!Contains(key)) 
            {
                this[key] = val;
            }
        }
#endif
    
#if USE_GENERICS
        public bool Remove(K key) 
        {
            if (index == null) 
            { 
                int i = binarySearch(key);
                if (i >= 0) 
                {
                    int size = values.Count;
                    Array.Copy(keys, i+1, keys, i, size-i-1);
                    keys.SetValue(null, size-1);
                    values.RemoveAt(i);
                    return true;
                }
                return false;
            } 
            else 
            {
                try 
                { 
                    index.RemoveKey(key);
                    return true;
                } 
                catch (StorageError x) 
                { 
                    if (x.Code == StorageError.ErrorCode.KEY_NOT_FOUND) 
                    { 
                        return false;
                    }
                    throw x;
                }
            }
        }
#else
        public void Remove(object key) 
        {
            if (index == null) 
            { 
                int i = binarySearch(key);
                if (i >= 0) 
                {
                    int size = values.Count;
                    Array.Copy(keys, i+1, keys, i, size-i-1);
                    keys.SetValue(null, size-1);
                    values.Remove(i);
                }
            } 
            else 
            {
                try 
                { 
                    index.RemoveKey(key);
                } 
                catch (StorageError x) 
                { 
                    if (x.Code == StorageError.ErrorCode.KEY_NOT_FOUND) 
                    { 
                        return;
                    }
                    throw x;
                }
            }
        }
#endif
    

#if USE_GENERICS
        public void Clear() 
#else
        public override void Clear() 
#endif
        {
            if (index != null) 
            { 
                index.Clear();
            } 
            else 
            {
                values.Clear();
                Array.Clear(keys, 0, keys.Length);
            }
        }

        public bool IsFixedSize
        {
            get 
            {
                return false;
            }
        }

        public bool IsReadOnly
        {
            get 
            {
                return false;
            }
        }

#if USE_GENERICS
        public bool TryGetValue(K key, out V val)
#else
        public bool TryGetValue(object key, out object val)
#endif
        {
            if (index != null) 
            { 
                return (val = index[key]) != null;
            } 
            else 
            {
                int i = binarySearch(key);
                if (i >= 0) 
                {
                    val = values[i];
                    return true;
                }
                val = null;
                return false;
            }
        }

#if USE_GENERICS
        class PairEnumerator:IEnumerator<KeyValuePair<K,V>> 
        {
            object IEnumerator.Current 
            {
                get
                {
                    return new KeyValuePair<K,V>((K)e.Key, (V)e.Value);
                }
            }
            
            public KeyValuePair<K,V> Current 
            {
                get
                {
                    return new KeyValuePair<K,V>((K)e.Key, (V)e.Value);
                }
            }
            
            public void Reset()
            {
                e.Reset();
            }

            public bool MoveNext()
            {
                return e.MoveNext();
            }

            public void Dispose() {}

            public PairEnumerator(IDictionaryEnumerator e)
            {
                this.e = e;
            }

            IDictionaryEnumerator e;
        }

        public IEnumerator<KeyValuePair<K,V>> GetEnumerator()
        {
            return new PairEnumerator(GetDictionaryEnumerator());
        } 
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetDictionaryEnumerator();
        }
#else
        public override IEnumerator GetEnumerator()
        {
            return GetDictionaryEnumerator();
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() 
        {
            return GetDictionaryEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetDictionaryEnumerator();
        }
#endif

        public IDictionaryEnumerator GetDictionaryEnumerator()
        {
            if (index != null) 
            { 
                return index.GetDictionaryEnumerator();
            } 
            else 
            {
                return new SmallMapDictionaryEnumerator(this);
            }
        }

#if USE_GENERICS
        public ICollection<K> Keys 
#else
        public ICollection Keys 
#endif
        {
            get 
            {
                if (keySet == null) 
                {
                    keySet = new KeySet(this);
                }
                return keySet;
            }
        }

#if USE_GENERICS
        public ICollection<V> Values 
#else
        public ICollection Values 
#endif
        {
            get 
            {
                if (valueSet == null) 
                {
                    valueSet = new ValueSet(this);
                }
                return valueSet;
            }
        }

        class SmallMapDictionaryEnumerator:IDictionaryEnumerator 
        {
#if USE_GENERICS
            PersistentMapImpl<K,V> map;
#else
            PersistentMapImpl map;
#endif
            int               i;

#if USE_GENERICS
            public SmallMapDictionaryEnumerator(PersistentMapImpl<K,V> map) 
#else
            public SmallMapDictionaryEnumerator(PersistentMapImpl map) 
#endif
            {
                this.map  = map;
                Reset();
            }

            public void Reset() 
            {
                i = -1;
            }

            public bool MoveNext()
            {
                if (i+1 < map.values.Count) 
                {
                    i += 1;
                    return true;
                }
                return false;
            }

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
                    return new DictionaryEntry(Key, Value);
                }
            }

            public object Key 
            {
                get 
                {
                    if (i < 0) 
                    {
                        throw new InvalidOperationException();
                    }               
#if USE_GENERICS
                    return ((K[])map.keys)[i];
#else
                    return ((object[])map.keys)[i];
#endif
                }
            }

            public object Value 
            {
                get 
                {
                    if (i < 0) 
                    {
                        throw new InvalidOperationException();
                    }               
                    return map.values[i];
                }
            }
        }


#if USE_GENERICS

        class ValueEnumerator:IEnumerator<V>,IEnumerator
        {
            IDictionaryEnumerator e;
            
            public ValueEnumerator(IDictionaryEnumerator e)
            {
                this.e = e;
            }
        
            public void Reset() 
            {
                e.Reset();
            }

            public bool MoveNext() 
            {
                return e.MoveNext();
            }

            public void Dispose() {}
            
            object IEnumerator.Current
            {
                get 
                {
                    return e.Value;
                }
            }

            public V Current 
            {
                get 
                {
                    return (V)e.Value;
                }
            }
        }

        class KeyEnumerator:IEnumerator<K>,IEnumerator
        {
            IDictionaryEnumerator e;
            
            public KeyEnumerator(IDictionaryEnumerator e)
            {
                this.e = e;
            }
        
            public void Reset() 
            {
                e.Reset();
            }

            public bool MoveNext() 
            {
                return e.MoveNext();
            }

            public void Dispose() {}

            public K Current 
            {
                get 
                {
                    return (K)e.Key;
                }
            }

            object IEnumerator.Current
            {
                get 
                {
                    return e.Key;
                }
            }
        }

        abstract class ReadOnlyCollection<T>:ICollection<T>
        {
            protected PersistentMapImpl<K,V> map;
            
            protected ReadOnlyCollection(PersistentMapImpl<K,V> map)
            {
                this.map = map;
            }

            public int Count 
            {
                get 
                {
                    return map.Count;
                }
            }

            public bool IsSynchronized 
            {
                get 
                {
                    return false;
                }
            }

            public bool IsReadOnly 
            {
                get 
                {
                    return true;
                }
            }

            public object SyncRoot 
            {
                get 
                {
                    return null;
                }
            }

            public void CopyTo(T[] dst, int i) 
            {
                foreach (T obj in this)
                {
                    dst[i++] = obj;
                }
            }
            public void Add(T obj)
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            public void Clear()
            {
                throw new InvalidOperationException("Collection is readonly");
            }

            public virtual bool Contains(T obj) 
            {
                if (obj == null)
                {
                    foreach (T o in this)
                    { 
                        if (o == null) 
                        {
                            return true;
                        }
                    }
                } 
                else 
                {  
                    foreach (T o in this)
                    {  
                        if (obj.Equals(o)) 
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public virtual bool Remove(T obj) 
            {        
                throw new InvalidOperationException("Collection is readonly");
            }

            public abstract IEnumerator<T> GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)this.GetEnumerator();
            }
        }

        class ValueSet:ReadOnlyCollection<V>
        {
            public ValueSet(PersistentMapImpl<K,V> map) : base(map) {}
  
            public override IEnumerator<V> GetEnumerator() 
            {
                return new ValueEnumerator(map.GetDictionaryEnumerator());
            }
        }

                     

        class KeySet:ReadOnlyCollection<K>
        {
            public KeySet(PersistentMapImpl<K,V> map) : base(map) {}
 
            public override IEnumerator<K> GetEnumerator() 
            {
                return new KeyEnumerator(map.GetDictionaryEnumerator());
            }
        }

#else

        class ValueEnumerator:IEnumerator
        {
            IDictionaryEnumerator e;
            
            public ValueEnumerator(IDictionaryEnumerator e)
            {
                this.e = e;
            }
        
            public void Reset() 
            {
                e.Reset();
            }

            public bool MoveNext() 
            {
                return e.MoveNext();
            }

            public object Current 
            {
                get 
                {
                    return e.Value;
                }
            }
        }

        class KeyEnumerator:IEnumerator
        {
            IDictionaryEnumerator e;
            
            public KeyEnumerator(IDictionaryEnumerator e)
            {
                this.e = e;
            }
        
            public void Reset() 
            {
                e.Reset();
            }

            public bool MoveNext() 
            {
                return e.MoveNext();
            }

            public object Current 
            {
                get 
                {
                    return e.Key;
                }
            }
        }

        abstract class ReadOnlyCollection:ICollection
        {
            protected PersistentMapImpl map;
            
            protected ReadOnlyCollection(PersistentMapImpl map)
            {
                this.map = map;
            }

            public int Count 
            {
                get 
                {
                    return map.Count;
                }
            }

            public bool IsSynchronized 
            {
                get 
                {
                    return false;
                }
            }

            public object SyncRoot 
            {
                get 
                {
                    return null;
                }
            }

            public abstract IEnumerator GetEnumerator();

            public void CopyTo(Array dst, int i) 
            {
                foreach (object o in this) 
                { 
                    dst.SetValue(o, i++);
                }
            }
        }

        class ValueSet:ReadOnlyCollection 
        {
            public ValueSet(PersistentMapImpl map) : base(map) {}
  
            public override IEnumerator GetEnumerator() 
            {
                return new ValueEnumerator(map.GetDictionaryEnumerator());
            }
        }

                     

        class KeySet:ReadOnlyCollection 
        {
            public KeySet(PersistentMapImpl map) : base(map) {}
 
            public override IEnumerator GetEnumerator() 
            {
                return new KeyEnumerator(map.GetDictionaryEnumerator());
            }
        }
#endif
    }
}
