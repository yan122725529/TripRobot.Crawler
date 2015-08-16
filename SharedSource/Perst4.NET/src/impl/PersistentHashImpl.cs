namespace Perst.Impl
{
    using System;
    using Perst;
#if USE_GENERICS
    using System.Collections.Generic;
    using Link=Link<object>;
#endif
    using System.Collections;

    public class HashStatistic
    {
        public int    maxHeight;
        public int    nPages;
        public int    nItems;
        public int    nChains;
        public int    maxCollisionChainLength;
        public double aveCollisionChainLength;
        public double aveHeight;
        
        public override string ToString() 
        {
            return "maxHeight=" + maxHeight + ", aveHeight=" + aveHeight + ", nPages=" + nPages + ", nItems=" + nItems 
                 + ", maxCollisionChainLength=" + maxCollisionChainLength + ", aveCollisionChainLength=" + aveCollisionChainLength + ", nChains=" + nChains;
        }
    }


    [Serializable]
#if USE_GENERICS
    public class PersistentHashImpl<K,V>:PersistentResource,IPersistentMap<K,V> where V:class
#else
    public class PersistentHashImpl:PersistentCollection,IPersistentMap
#endif
    {
        internal class HashPage : Persistent 
        {
            internal Link items;
         
            internal HashPage(Storage db, uint pageSize) : base(db)
            { 
                items = db.CreateLink((int)pageSize);
                items.Length = (int)pageSize;
            }

            internal HashPage() {}

            public override void Deallocate()
            {
                foreach (object child in items)
                {
                    if (child is HashPage)
                    {
                        ((HashPage)child).Deallocate();
                    }
                    else 
                    {
                        CollisionItem next;
                        for (CollisionItem item = (CollisionItem)child; item != null; item = next)
                        {
                            next = item.next;
                            item.Deallocate();
                         }
                    }
                }
                base.Deallocate();
            }        
        }

        internal class CollisionItem : Persistent 
        { 
            internal object        key;
            internal object        obj;
            internal int           hashCode;
            internal CollisionItem next;
            
            internal CollisionItem(object key, object obj, int hashCode) 
            {
                this.key = key;
                this.obj = obj;
                this.hashCode = hashCode;
            }
             
            internal CollisionItem() {}
        }    
             
        internal HashPage root;
        internal int      nElems;
        internal int      loadFactor;
        internal uint     pageSize;
 
#if USE_GENERICS
        [NonSerialized] 
        ICollection<V> valueSet;
        [NonSerialized] 
        ICollection<K> keySet;
#else
        [NonSerialized] 
        ICollection valueSet;
        [NonSerialized] 
        ICollection keySet;
#endif

        internal PersistentHashImpl(Storage storage, int pageSize, int loadFactor) 
            : base(storage)
        {
            this.pageSize = (uint)pageSize;
            this.loadFactor = loadFactor;
        }

        internal PersistentHashImpl() {}

                                                                                                                
#if USE_GENERICS
        public int Count 
#else
        public override int Count 
#endif
        {
            get 
            {
                return nElems;
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

        public void Set(object key, object obj)
        {
            int hashCode = key.GetHashCode();
            HashPage pg = root;
            if (pg == null) 
            {
                pg = new HashPage(Storage, pageSize);
                int h = (int)((uint)hashCode % pageSize);
                pg.items[h] = new CollisionItem(key, obj, hashCode);
                root = pg;
                nElems = 1;
                Modify();
            }
            else
            { 
                uint divisor = 1;
                while (true)
                {
                    int h = (int)((uint)hashCode / divisor % pageSize);
                    object child = pg.items[h];
                    if (child is HashPage)
                    {
                        pg = (HashPage)child;
                        divisor *= pageSize;                        
                    } 
                    else
                    { 
                        CollisionItem prev = null;
                        CollisionItem last = null;
                        int collisionChainLength = 0;
                        for (CollisionItem item = (CollisionItem)child; item != null; item = item.next)
                        {
                             if (item.hashCode == hashCode) 
                             {
                                 if (item.key.Equals(key))
                                 {  
                                     item.obj = obj;
                                     item.Modify();
                                     return;
                                 }      
                                 if (prev == null || prev.hashCode != hashCode)
                                 {
                                     collisionChainLength += 1;
                                 }                           
                                 prev = item; 
                             }
                             else
                             {      
                                 collisionChainLength += 1;
                             }            
                             last = item;                
                        }
                        if (prev == null || prev.hashCode != hashCode)
                        {
                            collisionChainLength += 1;
                        }
                        if (collisionChainLength > loadFactor) 
                        {
                            HashPage newPage = new HashPage(Storage, pageSize);
                            divisor *= pageSize;
                            CollisionItem next;
                            for (CollisionItem item = (CollisionItem)child; item != null; item = next)
                            {
                                next = item.next;
                                int hc = (int)((uint)item.hashCode / divisor % pageSize);                        
                                item.next = (CollisionItem)newPage.items[hc];
                                newPage.items[hc] = item;    
                                item.Modify();
                            }
                            pg.items[h] = newPage;
                            pg.Modify();
                            pg = newPage;      
                        }
                        else
                        {
                            CollisionItem newItem = new CollisionItem(key, obj, hashCode);
                            if (prev == null)
                            {
                                prev = last;
                            }
                            if (prev != null)
                            {
                                newItem.next = prev.next;
                                prev.next = newItem;
                                prev.Modify();
                            }
                            else 
                            {
                                pg.items[h] = newItem;
                                pg.Modify();
                            }
                            nElems += 1;
                            Modify();
                            return;
                        }                            
                    }
                }
            }
        }
        
#if USE_GENERICS
        public bool ContainsKey(K key) 
        {
            V val;
            return TryGetValue(key, out val); 
        }

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
                Set(key, value);
            }
        }
#else
        public bool Contains(object key) 
        {
            object val;
            return TryGetValue(key, out val);
        }

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
                Set(key, value);
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
#else
        public void Remove(object key) 
#endif
        {
            HashPage pg = root; 
            if (pg != null) 
            {
                uint divisor = 1;
                int hashCode = key.GetHashCode();
                while (true)
                {
                    int h = (int)((uint)hashCode / divisor % pageSize);
                    object child = pg.items[h];
                    if (child is HashPage) 
                    {
                        pg = (HashPage)child; 
                        divisor *= pageSize;
                    }
                    else
                    {
                        CollisionItem prev = null;
                        for (CollisionItem item = (CollisionItem)child; item != null; item = item.next)
                        {
                            if (item.hashCode == hashCode && item.key.Equals(key)) 
                            {
                                if (prev != null)
                                {
                                    prev.next = item.next;
                                    prev.Modify();
                                }
                                else
                                {
                                    pg.items[h] = item.next;
                                    pg.Modify();
                                }
                                nElems -= 1;
                                Modify();
#if USE_GENERICS
                                return true;
#else
                                return;
#endif
                            }
                            prev = item;
                        }
                        break;
                    }
                }
            }
#if USE_GENERICS
            return false;
#endif
        }                        

#if USE_GENERICS
        public void Clear() 
#else
        public override void Clear() 
#endif
        {       
            if (root != null)
            {
                root.Deallocate();
                root = null;
                nElems = 0;
                Modify();
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
            HashPage pg = root; 
            if (pg != null) 
            {
                uint divisor = 1;
                int hashCode = key.GetHashCode();
                while (true)
                {
                    int h = (int)((uint)hashCode / divisor % pageSize);
                    object child = pg.items[h];
                    if (child is HashPage) 
                    {
                        pg = (HashPage)child; 
                        divisor *= pageSize;
                    }
                    else
                    {
                        for (CollisionItem item = (CollisionItem)child; item != null; item = item.next)
                        {
                            if (item.hashCode == hashCode && item.key.Equals(key)) 
                            {
#if USE_GENERICS
                                val = (V)item.obj;
#else
                                val = item.obj;
#endif
                                return true;
                            }
                        }
                        break;
                    }
                }
            }
            val = null;
            return false;
        }

        private long CollectStatistic(HashPage pg, HashStatistic stat, int height)
        {
            long totalHeight = 0;
            stat.nPages += 1;
            if (++height > stat.maxHeight) 
            {
                stat.maxHeight = height;
            }   
            foreach (object child in pg.items)
            {
                if (child is HashPage)    
                {
                    totalHeight += CollectStatistic((HashPage)child, stat, height);
                }
                else if (child != null)
                {
                    int collisionChainLength = 0;
                    for (CollisionItem item = (CollisionItem)child; item != null; item = item.next)
                    {
                        collisionChainLength += 1;
                    }
                    if (stat.maxCollisionChainLength < collisionChainLength)
                    {
                        stat.maxCollisionChainLength = collisionChainLength;
                    }                 
                    stat.nItems += collisionChainLength;
                    stat.nChains += 1;
                    totalHeight += height;
                }
            }
            return totalHeight;
        }

        public HashStatistic GetStatistic() 
        {
            HashStatistic stat = new HashStatistic();
            if (root != null)
            {
                long totalHeight = CollectStatistic(root, stat, 0);
                if (stat.nChains != 0) 
                {
                    stat.aveHeight = (double)totalHeight / stat.nChains;
                    stat.aveCollisionChainLength = (double)stat.nItems / stat.nChains;
                }
            }
            return stat;
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
            return new HashEnumerator(this);
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

        class StackElem
        {
            internal HashPage page;
            internal int      pos;

            internal StackElem(HashPage page, int pos) 
            {
                this.page = page;
                this.pos = pos;
            }
        }

        class HashEnumerator:IDictionaryEnumerator 
        {
#if USE_GENERICS
            PersistentHashImpl<K,V> hash;
            List<StackElem> stack = new List<StackElem>();
#else
            PersistentHashImpl hash;
            ArrayList stack = new ArrayList();
#endif
            CollisionItem currItem;
            CollisionItem nextItem;

#if USE_GENERICS
            public HashEnumerator(PersistentHashImpl<K,V> hash) 
#else
            public HashEnumerator(PersistentHashImpl hash) 
#endif
            {
                this.hash = hash;
                Reset();
            }

            public void Reset() 
            {
                currItem = null;
                nextItem = null;
                stack.Clear();
                HashPage pg = hash.root;
                
                if (pg != null) 
                {
                    int start = 0;
                    int sp = 0;
                  DepthFirst:
                    while (true)       
                    { 
                        for (int i = start; i < pg.items.Count; i++) 
                        { 
                            object child = pg.items[i];
                            if (child != null) 
                            {
                                stack.Add(new StackElem(pg, i));
                                sp += 1;
                                if (child is HashPage) 
                                {
                                    pg = (HashPage)child;
                                    start = 0;
                                    goto DepthFirst;
                                }
                                else 
                                {
                                    nextItem = (CollisionItem)child;
                                    return;
                                }
                            }
                        }
                        if (sp != 0)
                        {
                            StackElem top = (StackElem)stack[--sp];
                            stack.RemoveAt(sp);
                            pg = top.page;
                            start = top.pos + 1;
                        } 
                        else
                        {
                            break;
                        }
                    }
                } 
            }

            public bool MoveNext()
            {
                if (nextItem != null)
                {
                    currItem = nextItem;
                    if ((nextItem = nextItem.next) == null)
                    {
                        int sp = stack.Count;
                        do
                        {
                            StackElem top = (StackElem)stack[--sp];
                            stack.RemoveAt(sp);
                            HashPage pg = top.page;
                            int start = top.pos + 1;                           

                          DepthFirst:
                            while (true)
                            {
                                for (int i = start; i < pg.items.Count; i++) 
                                { 
                                    object child = pg.items[i];
                                    if (child != null) 
                                    {
                                        stack.Add(new StackElem(pg, i));
                                        sp += 1;
                                        if (child is HashPage) 
                                        {
                                            pg = (HashPage)child;
                                            start = 0;
                                            goto DepthFirst;
                                        }
                                        else 
                                        {
                                            nextItem = (CollisionItem)child;
                                            return true;
                                        }
                                    }
                                }
                                break;
                            } 
                        } while (sp != 0);
                    }
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
                    if (currItem == null)
                    {
                        throw new InvalidOperationException();
                    }               
                    return currItem.key;
                }
            }

            public object Value 
            {
                get 
                {
                    if (currItem == null)
                    {
                        throw new InvalidOperationException();
                    }               
                    return currItem.obj;
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
            protected PersistentHashImpl<K,V> hash;
            
            protected ReadOnlyCollection(PersistentHashImpl<K,V> hash)
            {
                this.hash = hash;
            }

            public int Count 
            {
                get 
                {
                    return hash.Count;
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
            public ValueSet(PersistentHashImpl<K,V> hash) : base(hash) {}
  
            public override IEnumerator<V> GetEnumerator() 
            {
                return new ValueEnumerator(hash.GetDictionaryEnumerator());
            }
        }

                     

        class KeySet:ReadOnlyCollection<K>
        {
            public KeySet(PersistentHashImpl<K,V> hash) : base(hash) {}
 
            public override IEnumerator<K> GetEnumerator() 
            {
                return new KeyEnumerator(hash.GetDictionaryEnumerator());
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
            protected PersistentHashImpl hash;
            
            protected ReadOnlyCollection(PersistentHashImpl hash)
            {
                this.hash = hash;
            }

            public int Count 
            {
                get 
                {
                    return hash.Count;
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
            public ValueSet(PersistentHashImpl hash) : base(hash) {}
  
            public override IEnumerator GetEnumerator() 
            {
                return new ValueEnumerator(hash.GetDictionaryEnumerator());
            }
        }

                     

        class KeySet:ReadOnlyCollection 
        {
            public KeySet(PersistentHashImpl hash) : base(hash) {}
 
            public override IEnumerator GetEnumerator() 
            {
                return new KeyEnumerator(hash.GetDictionaryEnumerator());
            }
        }
#endif
    }
}
