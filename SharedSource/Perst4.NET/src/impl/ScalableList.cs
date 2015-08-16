namespace Perst.Impl
{
    using System;
    using Perst;
    using System.Collections;
#if USE_GENERICS
    using System.Collections.Generic;
#endif

    [Serializable]
#if USE_GENERICS
    public class ScalableList<T> : PersistentCollection<T>, IPersistentList<T> where T:class
    {
        internal Link<T>            small;
        internal IPersistentList<T> large;
#else
    public class ScalableList : PersistentCollection, IPersistentList
    {
        internal Link            small;
        internal IPersistentList large;
#endif
        const int BtreeThreshold = 128;

#if USE_GENERICS
        public T this[int i] 
#else
        public object this[int i]
#endif
        { 
            get 
            {
                return small != null ? small[i] : large[i];
             }
            set 
            {
                if (small != null) 
                { 
#if USE_GENERICS
                    small[i] = value;
#else
                    small[i] = value;
#endif
                } 
                else 
                {
                    large[i] = value;
                }
            }
        }
    
         
#if USE_GENERICS
        public T[] ToArray()
#else
        public object[] ToArray()
#endif
        {
            return small != null ? small.ToArray() : large.ToArray();
        }

        public Array ToArray(Type elemType)
        {
            return small != null ? small.ToArray(elemType) : large.ToArray(elemType);
        }
            
#if USE_GENERICS
        public override bool Contains(T obj)
        {
            return small != null ? small.Contains(obj) : large.Contains(obj);
        }
#else
        public bool Contains(object obj)
        {
            return small != null ? small.Contains(obj) : large.Contains(obj);
        }
#endif
		
#if USE_GENERICS
        public int IndexOf(T obj)
        {
            return small != null ? small.IndexOf(obj) : large.IndexOf(obj);
        }
#else
        public int IndexOf(object obj)
        {
            return small != null ? small.IndexOf(obj) : large.IndexOf(obj);
        }
#endif


        public override void Clear()
        {
            if (small != null) 
            {
                small.Clear();
            } 
            else 
            { 
                large.Clear();
            }
        }
        
#if !USE_GENERICS
        public bool IsReadOnly 
        {
            get 
            {
                return false;
            }
        }
#endif

        public bool IsFixedSize
        {
            get 
            {
                return false;
            }
        }

        public override int Count 
        {
            get 
            {
                return small != null ? small.Count : large.Count;
            }
        }

 
#if USE_GENERICS
        public override void Add(T o) 
        {
            Insert(Count, o);
        }
#else
        public int Add(Object o) 
        {
            int pos = Count;
            Insert(pos, o);
            return pos;
        }
#endif

#if USE_GENERICS
        public void Insert(int i, T o)
#else
        public void Insert(int i, object o)
#endif
        {
            if (small != null) 
            { 
                if (small.Count == BtreeThreshold) 
                { 
#if USE_GENERICS
                    large = Storage.CreateList<T>();
                    foreach (T obj in small) 
                    {
                        large.Add(obj);
                    }
#else
                    large = Storage.CreateList();
                    foreach (object obj in small) 
                    {
                        large.Add(obj);
                    }
#endif
                    large.Insert(i, o);
                    Modify();
                    small = null;
                } 
                else 
                { 
#if USE_GENERICS
                    small.Insert(i, o);
#else
                    small.Insert(i, o);
#endif
                }
            } 
            else 
            { 
                large.Insert(i, o);
            }
        }

        public void RemoveAt(int i) 
        {
            if (small != null) 
            {
                small.Remove(i);
            } 
            else 
            {
                large.RemoveAt(i);
            }
        }
 
#if USE_GENERICS
        public override bool Remove(T obj)
        {
            return small != null ? small.Remove(obj) : large.Remove(obj);
        }
#else
        public void Remove(object obj)
        {
            if (small != null) 
            {                 
                small.Remove(obj);
            } 
            else 
            {
                large.Remove(obj);
            }
        }
#endif

#if USE_GENERICS
        public override IEnumerator<T> GetEnumerator() 
        { 
            return small != null ? ((IEnumerable<T>)small).GetEnumerator() : ((IEnumerable<T>)large).GetEnumerator();
        }
#else
        public override IEnumerator GetEnumerator() 
        { 
            return small != null ? small.GetEnumerator() : large.GetEnumerator();
        }
#endif

#if USE_GENERICS
        public IBidirectionalEnumerator<T> GetEnumerator(int start) 
        { 
            return small != null ? small.GetEnumerator(start) : large.GetEnumerator(start);
        }
#else
        public IBidirectionalEnumerator GetEnumerator(int start) 
        { 
            return small != null ? small.GetEnumerator(start) : large.GetEnumerator(start);
        }
#endif

        internal ScalableList() {}
    
        internal ScalableList(Storage storage, int initialSize) : base(storage)
        {
#if USE_GENERICS
            if (initialSize <= BtreeThreshold) 
            { 
                small = storage.CreateLink<T>(initialSize);
            } 
            else 
            { 
                large = storage.CreateList<T>();
            }
#else
            if (initialSize <= BtreeThreshold) 
            { 
                small = storage.CreateLink(initialSize);
            } 
            else 
            { 
                large = storage.CreateList();
            }
#endif
        }
    }
}

