using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst.Impl
{
    [Serializable]
#if USE_GENERICS
    internal class ScalableSet<T> : PersistentCollection<T>, ISet<T> where T:class
    { 
        internal Link<T> link;
        internal ISet<T> pset;
#else
    internal class ScalableSet : PersistentCollection, ISet 
    { 
        internal Link link;
        internal ISet pset;
#endif
        const int BTREE_THRESHOLD = 128;

        internal ScalableSet(StorageImpl storage, int initialSize) 
            : base(storage)
        {
#if USE_GENERICS
            if (initialSize <= BTREE_THRESHOLD) 
            { 
                link = storage.CreateLink<T>(initialSize);
            } 
            else 
            { 
                pset = storage.CreateSet<T>();
            }
#else
            if (initialSize <= BTREE_THRESHOLD) 
            { 
                link = storage.CreateLink(initialSize);
            } 
            else 
            { 
                pset = storage.CreateSet();
            }
#endif
        }

        internal ScalableSet() {}

        public override int Count 
        { 
            get 
            {
                return link != null ? link.Count : pset.Count;
            }
        }

        public override void Clear() 
        { 
            if (link != null) 
            { 
                link.Clear();
                Modify();
            } 
            else 
            { 
                pset.Clear();
            }
        }

#if USE_GENERICS
        public override bool Contains(T o) 
#else
        public bool Contains(object o) 
#endif
        {
            return link != null ? link.Contains(o) : pset.Contains(o);
        }
    
#if USE_GENERICS
        public T[] ToArray() 
#else
        public object[] ToArray() 
#endif
        { 
            return link != null ? link.ToArray() : pset.ToArray();
        }

        public Array ToArray(Type elemType) 
        { 
            return link != null ? link.ToArray(elemType) : pset.ToArray(elemType);
        }

#if USE_GENERICS
        public override IEnumerator<T> GetEnumerator() 
        { 
            return link != null ? ((IEnumerable<T>)link).GetEnumerator() : ((IEnumerable<T>)pset).GetEnumerator();
        }
#else
        public override IEnumerator GetEnumerator() 
        { 
            return link != null ? link.GetEnumerator() : pset.GetEnumerator();
        }
#endif

        private int binarySearch(object obj) 
        { 
            int l = 0, r = link.Count;
            int oid = Storage.GetOid(obj);
            while (l < r) 
            { 
                int m = (l + r) >> 1;
                if (Storage.GetOid(link.GetRaw(m)) > oid) 
                { 
                    l = m + 1;
                } 
                else 
                { 
                    r = m;
                }
            }
            return r;
        }

#if USE_GENERICS
        public override void Add(T o) 
#else
        public void Add(object o) 
#endif
        { 
            if (link != null) 
            { 
                int i = binarySearch(o);
                int n = link.Count;
                if (i < n && link.GetRaw(i).Equals(o)) 
                { 
                    return;
                }
                if (n == BTREE_THRESHOLD) 
                { 
#if USE_GENERICS
                    pset = Storage.CreateSet<T>();
#else
                    pset = Storage.CreateSet();
#endif
                    for (i = 0; i < n; i++) 
                    { 
#if USE_GENERICS
                        pset.Add(link[i]);
#else
                        pset.Add(link.GetRaw(i));
#endif
                    }
                    link = null;
                    Modify();
                    pset.Add(o);
                } 
                else 
                { 
                    Modify();
                    link.Insert(i, o);
                }
            } 
            else 
            { 
                pset.Add(o);
            }
        }

#if USE_GENERICS
        public override bool Remove(T o) 
#else
        public bool Remove(object o) 
#endif
        { 
            if (link != null) 
            {  
                int i = link.IndexOf(o);        
                if (i < 0) 
                { 
                    return false;
                }
                link.Remove(i);
                Modify();
                return true;
            } 
            else 
            { 
                return pset.Remove(o);
            }
        }
    
    
#if USE_GENERICS
        public bool ContainsAll(ICollection<T> c) 
        { 
            foreach (T o in c) 
            {
                if (!Contains(o)) 
                {
                    return false;
                }
            }
            return true;
        }
#else
        public bool ContainsAll(ICollection c)  
        { 
            foreach (object o in c) 
            {
                if (!Contains(o)) 
                {
                    return false;
                }
            }
            return true;
        }
#endif

    
#if USE_GENERICS
        public bool AddAll(ICollection<T> c) 
        {
            bool modified = false;
            foreach (T o in c) 
            {
                if (!Contains(o)) 
                {
                    modified = true;
                    Add(o);
                }
            }
            return modified;
        }
#else
        public bool AddAll(ICollection c) 
        {
            bool modified = false;
            foreach (object o in c) 
            {
                if (!Contains(o)) 
                {
                    modified = true;
                    Add(o);
                }
            }
            return modified;
        }
#endif

 
#if USE_GENERICS
        public bool RemoveAll(ICollection<T> c) 
        {
            bool modified = false;
            foreach (T o in c) 
            {
                modified |= Remove(o);
            }
            return modified;
        }
#else
        public bool RemoveAll(ICollection c) 
        {
            bool modified = false;
            foreach (object o in c) 
            {
                modified |= Remove(o);
            }
            return modified;
        }
#endif

        public override  bool Equals(object o) 
        {
            if (o == this) 
            {
                return true;
            }
#if USE_GENERICS
            ISet<T> s = o as ISet<T>;
#else
            ISet s = o as ISet;
#endif
            if (s == null) 
            {
                return false;
            }
            if (s.Count != Count) 
            {
                return false;
            }
            return ContainsAll(s);
        }

        public override int GetHashCode() 
        {
            int h = 0;
            foreach (object o in this) 
            {
                h += Storage.GetOid(o);
            }
            return h;
        }

        public override void Deallocate() 
        { 
            if (pset != null) 
            { 
                pset.Deallocate();
            }
            base.Deallocate();
        }

#if USE_GENERICS        
        public IEnumerable<T> Join(IEnumerable<T> with)         
        { 
            return with == null ? (IEnumerable<T>)this : new JoinSetEnumerable<T>(Storage, this, with);
        }    
#else
        public IEnumerable Join(IEnumerable with)         
        { 
            return with == null ? (IEnumerable)this : new JoinSetEnumerable(Storage, this, with);
        }    
#endif
    }
}
