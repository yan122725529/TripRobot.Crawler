namespace Perst.Impl
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
    using Perst;

    [Serializable]
#if USE_GENERICS
    internal class Ttree<K,V>:PersistentCollection<V>, SortedCollection<K,V>  where V:class
#else
    internal class Ttree:PersistentCollection, SortedCollection 
#endif
    {
#if USE_GENERICS
        internal PersistentComparator<K,V> comparator;
        internal bool                 unique;
        internal TtreePage<K,V>       root;
        internal int                  nMembers;
#else
        internal PersistentComparator comparator;
        internal bool                 unique;
        internal TtreePage            root;
        internal int                  nMembers;
#endif
    
        internal Ttree() {} 

        public override int Count 
        { 
            get 
            {
                return nMembers;
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
        } 
       
#if USE_GENERICS
        public V[] this[K low, K high] 
#else
        public object[] this[object low, object high] 
#endif
        {
            get
            {
                return Get(low, high);
            }
        }       

        
#if USE_GENERICS
        internal Ttree(Storage db, PersistentComparator<K,V> comparator, bool unique) 
#else
        internal Ttree(Storage db, PersistentComparator comparator, bool unique) 
#endif
        : base(db)
        { 
            this.comparator = comparator;
            this.unique = unique;
        }

#if USE_GENERICS
        public PersistentComparator<K,V> GetComparator() 
#else
        public PersistentComparator GetComparator() 
#endif
        { 
            return comparator;
        }

        public override bool RecursiveLoading() 
        {
            return false;
        }

#if USE_GENERICS
        public V Get(K key) 
#else
        public object Get(object key) 
#endif
        { 
            if (root != null) 
            { 
#if USE_GENERICS
                List<V> list = new List<V>();
#else
                ArrayList list = new ArrayList();
#endif
                root.find(comparator, key, BoundaryKind.Inclusive, key, BoundaryKind.Inclusive, list);
                if (list.Count > 1) 
                { 
                    throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
                } 
                else if (list.Count == 0) 
                { 
                    return null;
                } 
                else 
                { 
#if USE_GENERICS
                    return list[0];
#else
                    return list[0];
#endif
                }
            }
            return null;
        }

            

#if USE_GENERICS
        public V[] Get(K from, K till) 
#else
        public object[] Get(object from, object till) 
#endif
        { 
            return Get(from, BoundaryKind.Inclusive, till, BoundaryKind.Inclusive);
        }

#if USE_GENERICS
        public V[] Get(K from, BoundaryKind fromKind, K till, BoundaryKind tillKind) 
        { 
            List<V> list = new List<V>();
            if (root != null) 
            {                 
                root.find(comparator, from, fromKind, till, tillKind, list);
            }
            return list.ToArray();
        }
#else
        public object[] Get(object from, BoundaryKind fromKind, object till, BoundaryKind tillKind) 
        { 
            ArrayList list = new ArrayList();
            if (root != null) 
            {                 
                root.find(comparator, from, fromKind, till, tillKind, list);
            }
            return list.ToArray();
        }
#endif


#if USE_GENERICS
        public override void Add(V obj) 
        { 
            TtreePage<K,V> newRoot = root;
            if (root == null) 
            { 
                newRoot = new TtreePage<K,V>(Storage, obj);
            } 
            else 
            { 
                if (root.insert(comparator, obj, unique, ref newRoot) == TtreePage<K,V>.NOT_UNIQUE) 
                { 
                    return;
                }
            }
            Modify();
            root = newRoot;
            nMembers += 1;
        }
                
#else
        public void Add(object obj) 
        { 
            TtreePage newRoot = root;
            if (root == null) 
            { 
                newRoot = new TtreePage(Storage, obj);
            } 
            else 
            { 
                if (root.insert(comparator, obj, unique, ref newRoot) == TtreePage.NOT_UNIQUE) 
                { 
                    return;
                }
            }
            Modify();
            root = newRoot;
            nMembers += 1;
        }
                
#endif
                
#if USE_GENERICS
        public override bool Contains(V member) 
#else
        public bool Contains(object member) 
#endif
        {
            return (root != null) ? root.contains(comparator, member) : false;
        }        

#if USE_GENERICS
        public override bool Remove(V obj) 
#else
        public bool Remove(object obj) 
#endif
        {
            if (root == null) 
            {
                return false;
            }
#if USE_GENERICS
            TtreePage<K,V> newRoot = root;
            if (root.remove(comparator, obj, ref newRoot) == TtreePage<K,V>.NOT_FOUND) 
#else
            TtreePage newRoot = root;
            if (root.remove(comparator, obj, ref newRoot) == TtreePage.NOT_FOUND) 
#endif
            {             
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_FOUND);
            }
            Modify();
            root = newRoot;
            nMembers -= 1;   
            return true;     
        }

        public int Size() 
        {
            return nMembers;
        }
    
        public override void Clear() 
        {
            if (root != null) 
            { 
                root.prune();
                Modify();
                root = null;
                nMembers = 0;
            }
        }
 
        public override void Deallocate() 
        {
            if (root != null) 
            { 
                root.prune();
            }
            base.Deallocate();
        }


#if USE_GENERICS
        public V[] ToArray() 
        {
            V[] arr = new V[nMembers];
#else
        public object[] ToArray() 
        {
            object[] arr = new object[nMembers];
#endif
            if (root != null) 
            { 
                root.toArray(arr, 0);
            }
            return arr;
        }

        public virtual Array ToArray(Type elemType)
        {
            Array arr = Array.CreateInstance(elemType, nMembers);
            if (root != null)
            {
                root.toArray((object[])arr, 0);
            }
            return arr;
        }

 
#if USE_GENERICS
        class TtreeEnumerable : IEnumerable<V>, IEnumerable
#else
        class TtreeEnumerable : IEnumerable
#endif
        {
#if USE_GENERICS
            internal TtreeEnumerable(Ttree<K,V> tree, List<V> list) 
#else
            internal TtreeEnumerable(Ttree tree, ArrayList list) 
#endif
            { 
                this.tree = tree;
                this.list = list;
            }
        
#if USE_GENERICS
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new TtreeEnumerator(tree, list);
            }

            public IEnumerator<V> GetEnumerator()
#else
            public IEnumerator GetEnumerator()
#endif
            { 
                return new TtreeEnumerator(tree, list);
            }


#if USE_GENERICS
            List<V>       list;
            Ttree<K,V>    tree;
#else
            ArrayList     list;
            Ttree         tree;
#endif
        } 

#if USE_GENERICS
        class TtreeEnumerator : IEnumerator<V>, PersistentEnumerator
#else
        class TtreeEnumerator : PersistentEnumerator
#endif
        { 
            int           i;
#if USE_GENERICS
            List<V>       list;
            Ttree<K,V>    tree;
#else
            ArrayList     list;
            Ttree         tree;
#endif

#if USE_GENERICS
            internal TtreeEnumerator(Ttree<K,V> tree, List<V> list) 
#else
            internal TtreeEnumerator(Ttree tree, ArrayList list) 
#endif
            { 
                this.tree = tree;
                this.list = list;
                i = -1;
            }        
        
            public void Reset() 
            {
                i = -1;
            }
                
#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    return getCurrent();
                }
            }

            public V Current
            {
                get 
                {
                    return (V)getCurrent();
                }
            }
#else
            public object Current
            {
                get
                {
                    return getCurrent();
                }
            }
#endif


            private object getCurrent()
            {
                if (i < 0 || i >= list.Count)
                {
                    throw new InvalidOperationException();
                }
                return list[i];
            }

            public int CurrentOid 
            {
                get 
                {
                    return tree.Storage.GetOid(getCurrent());
                }
            }

            public void Dispose() {}

            public bool MoveNext() 
            {
                if (i+1 < list.Count) 
                { 
                    i += 1;
                    return true;
                }
                return false;
            }
        }
        

#if USE_GENERICS
        public override IEnumerator<V> GetEnumerator()
        {
            return GetEnumerator(default(K), BoundaryKind.None, default(K), BoundaryKind.None);
        }
#else
        public override IEnumerator GetEnumerator()
        {
            return GetEnumerator(null, BoundaryKind.None, null, BoundaryKind.None);
        }
#endif

#if USE_GENERICS
        public IEnumerator<V> GetEnumerator(K from, K till) 
#else
        public IEnumerator GetEnumerator(object from, object till) 
#endif
        {
            return Range(from, BoundaryKind.Inclusive, till, BoundaryKind.Inclusive).GetEnumerator();
        }
        
#if USE_GENERICS
        public IEnumerable<V> Range(K from, K till) 
#else
        public IEnumerable Range(object from, object till) 
#endif
        {
            return Range(from, BoundaryKind.Inclusive, till, BoundaryKind.Inclusive);
        }

#if USE_GENERICS
        public IEnumerator<V> GetEnumerator(K from, BoundaryKind fromKind, K till, BoundaryKind tillKind) 
#else
        public IEnumerator GetEnumerator(object from, BoundaryKind fromKind, object till, BoundaryKind tillKind) 
#endif
        {
            return Range(from, fromKind, till, tillKind).GetEnumerator();
        }

#if USE_GENERICS
        public IEnumerable<V> Range(K from, BoundaryKind fromKind, K till, BoundaryKind tillKind) 
        { 
            List<V> list = new List<V>();
#else
        public IEnumerable Range(object from, BoundaryKind fromKind, object till, BoundaryKind tillKind) 
        {
            ArrayList list = new ArrayList();
#endif
            if (root != null) 
            { 
                root.find(comparator, from, fromKind, till, tillKind, list);
            }            
            return new TtreeEnumerable(this, list);
        }
    }
}
