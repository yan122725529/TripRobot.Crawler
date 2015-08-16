namespace Perst.Impl
{
    using System;
#if USE_GENERICS
using System.Collections.Generic;
#endif
using System.Collections;
    using Perst;
	
#if USE_GENERICS
    public class LinkImpl<T> : Link<T> where T:class
#else
    public class LinkImpl : Link
#endif
    {
        private void Modify() 
        {
            if (owner != null) 
            {
                db.Modify(owner);
            }
        } 
                
    

        public int Count 
        { 
            get 
            {
                return used;
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

#if USE_GENERICS
        public bool IsReadOnly 
        {
            get
            {
                return false;
            }
        }
#endif
 
#if USE_GENERICS
        public void CopyTo(T[] dst, int i) 
#else
        public void CopyTo(Array dst, int i) 
#endif
        {
            Array.Copy(arr, 0, dst, i, used);
        }

        public virtual int Size()
        {
            return used;
        }
		
        public virtual int Length 
        {
            get 
            {
                return used;
            }

            set 
            {
                if (value < used) 
                { 
                    Array.Clear(arr, value, used);
                } 
                else 
                { 
                    reserveSpace(value - used);            
                }
                used = value;
                Modify();
            }
        }        

#if USE_GENERICS
        public virtual T this[int i] 
#else
        public virtual object this[int i] 
#endif
        {
             get
             {
                 return Get(i);
             }
           
             set 
             { 
                 Set(i, value);
             }
        }    
   
#if USE_GENERICS
        public virtual T Get(int i)
#else
        public virtual object Get(int i)
#endif
        {
            if (i < 0 || i >= used)
            {
                throw new IndexOutOfRangeException();
            }
            return loadElem(i);
        }
		
        public virtual object GetRaw(int i)
        {
            if (i < 0 || i >= used)
            {
                throw new IndexOutOfRangeException();
            }
            return arr[i];
        }
		
#if USE_GENERICS
        public virtual void Set(int i, T obj)
#else
        public virtual void Set(int i, object obj)
#endif
        {
            if (i < 0 || i >= used)
            {
                throw new IndexOutOfRangeException();
            }
            arr[i] = obj;
            Modify();
        }
		
#if USE_GENERICS
        public bool Remove(T obj) 
#else
        public bool Remove(object obj) 
#endif
        {
            int i = IndexOf(obj);
            if (i >= 0) 
            { 
                Remove(i);
                return true;
            }
            return false;
        }

#if USE_GENERICS
        public virtual void RemoveAt(int i)
        {
            Remove(i);
        }
#endif

        public virtual void Remove(int i)
        {
            if (i < 0 || i >= used)
            {
                throw new IndexOutOfRangeException();
            }
            used -= 1;
            Array.Copy(arr, i + 1, arr, i, used - i);
            arr[used] = null;
            Modify();
        }
		
        internal void reserveSpace(int len)
        {
            if (used + len > arr.Length)
            {
                object[] newArr = new object[used + len > arr.Length * 2?used + len:arr.Length * 2];
                Array.Copy(arr, 0, newArr, 0, used);
                arr = newArr;
            }
        }
		
#if USE_GENERICS
        public virtual void Insert(int i, T obj)
#else
        public virtual void Insert(int i, object obj)
#endif
        {
            if (i < 0 || i > used)
            {
                throw new IndexOutOfRangeException();
            }
            reserveSpace(1);
            Array.Copy(arr, i, arr, i + 1, used - i);
            arr[i] = obj;
            used += 1;
            Modify();
        }
		
#if USE_GENERICS
        public virtual void Add(T obj)
#else
        public virtual void Add(object obj)
#endif
        {
            reserveSpace(1);
            arr[used++] = obj;
            Modify();
        }
		
#if USE_GENERICS
        public virtual void AddAll(T[] a)
#else
        public virtual void AddAll(object[] a)
#endif
        {
            AddAll(a, 0, a.Length);
        }
		
#if USE_GENERICS
        public virtual void AddAll(T[] a, int from, int length)
#else
        public virtual void AddAll(object[] a, int from, int length)
#endif
        {
            reserveSpace(length);
            Array.Copy(a, from, arr, used, length);
            used += length;
            Modify();
        }
		
#if USE_GENERICS
        public virtual void AddAll(Link<T> link)
#else
        public virtual void AddAll(Link link)
#endif
        {
            int n = link.Length;
            reserveSpace(n);
            for (int i = 0, j = used; i < n; i++, j++)
            {
                arr[j] = link.GetRaw(i);
            }
            used += n;
            Modify();
        }
		
        public virtual Array ToRawArray()
        {
            return arr;
        }

#if USE_GENERICS
        public virtual T[] ToArray()
        {
            T[] a = new T[used];
#else
        public virtual object[] ToArray()
        {
            object[] a = new object[used];
#endif
            for (int i = used; --i >= 0; )
            {
                a[i] = loadElem(i);
            }
            return a;
        }
		
        public virtual Array ToArray(Type elemType)
        {
            Array a = Array.CreateInstance(elemType, used);
            for (int i = used; --i >= 0; )
            {
                a.SetValue(loadElem(i), i);
            }
            return a;
        }
		
#if USE_GENERICS
        public virtual bool Contains(T obj)
#else
        public virtual bool Contains(object obj)
#endif
        {
            return IndexOf(obj) >= 0;
        }
		
#if USE_GENERICS
        public virtual int IndexOf(T obj)
#else
        public virtual int IndexOf(object obj)
#endif
        {
            int oid = db.GetOid(obj);
            if (oid != 0) 
            { 
                for (int i = used; --i >= 0;) 
                {
                    if (db.GetOid(arr[i]) == oid) 
                    {
                        return i;
                    }
                }
            } 
            else 
            { 
                for (int i = used; --i >= 0;) 
                {
                    if (arr[i] == obj) 
                    {
                        return i;
                    }
                }
            }
            return - 1;
        }
		
#if USE_GENERICS
        public virtual bool ContainsElement(int i, T obj) 
#else
        public virtual bool ContainsElement(int i, object obj) 
#endif
        {
            object elem = arr[i];
            int oid;
            return elem == obj || (elem != null && (oid = db.GetOid(obj)) != 0 && oid == db.GetOid(elem));
        }

        public virtual void Clear()
        {
            Array.Clear(arr, 0, used);
            used = 0;
            Modify();
        }
		
        public void DeallocateMembers()
        {
            foreach (object o in this) 
            { 
                db.Deallocate(o);
            }
            Clear();
        }

#if USE_GENERICS
        class LinkEnumerator : IBidirectionalEnumerator<T>, PersistentEnumerator { 
#else
        class LinkEnumerator : PersistentEnumerator, IBidirectionalEnumerator { 
#endif
            public void Dispose() {}

            public bool MoveNext() 
            {
                if (i+1 < link.Length) { 
                    i += 1;
                    return true;
                }
                return false;
            }

            public bool MovePrevious() 
            {
                if (i > 0) { 
                    i -= 1;
                    return true;
                }
                return false;
            }

#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    return link[i];
                }
            }

            public T Current
#else
            public object Current
#endif
            {
                get 
                {
                    return link[i];
                }
            }

            public int CurrentOid 
            {
                get 
                {
                    return link.db.GetOid(link.GetRaw(i));
                }
            }
 
            public void Reset() 
            {
                i = start;
            }

#if USE_GENERICS
            internal LinkEnumerator(LinkImpl<T> link, int start) { 
#else
            internal LinkEnumerator(LinkImpl link, int start) { 
#endif
                this.link = link;
                this.start = start;
                i = start;
            }

            private int i;
            private int start;
#if USE_GENERICS
            private LinkImpl<T> link;
#else
            private LinkImpl link;
#endif
        }      

#if USE_GENERICS
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator(-1);
        }

        public IEnumerator<T> GetEnumerator() 
#else
        public IEnumerator GetEnumerator() 
#endif
        { 
            return GetEnumerator(-1);
        }

#if USE_GENERICS
        public IBidirectionalEnumerator<T> GetEnumerator(int start) 
#else
        public IBidirectionalEnumerator GetEnumerator(int start) 
#endif
        {
            return new LinkEnumerator(this, start);
        }

        public void Pin() 
        { 
            for (int i = 0, n = used; i < n; i++) 
            { 
                arr[i] = loadElem(i);
            }
        }

        public void Unpin() 
        { 
            for (int i = 0, n = used; i < n; i++) 
            { 
                object elem = arr[i];
                if (elem != null && db.IsLoaded(elem))
                { 
                    arr[i] = new PersistentStub(db, db.GetOid(elem));
                }
            }
        }

#if USE_GENERICS
        private T loadElem(int i)
#else
        private object loadElem(int i)
#endif
        {
            object elem = arr[i];
            if (db.IsRaw(elem))
            {
                elem = db.lookupObject(db.GetOid(elem), null);
            }
#if USE_GENERICS
            return (T)elem;
#else
            return elem;
#endif
        }
		
        public void SetOwner(object owner)
        { 
             this.owner = owner;
        }

#if USE_GENERICS
        public IEnumerable<T> Select(string predicate) 
        { 
            Query<T> query = new QueryImpl<T>(null);
            return query.Select(this, predicate);
        }
#else
        public IEnumerable Select(Type cls, string predicate) 
        { 
            Query query = new QueryImpl(null);
            return query.Select(cls, this, predicate);
        }
#endif


        internal LinkImpl(StorageImpl db)
        {
            this.db = db;
        }
		
        internal LinkImpl(StorageImpl db, int initSize)
        {
            this.db = db;
            arr = new object[initSize];
        }
		
        internal LinkImpl(StorageImpl db, object[] arr, object owner)
        {
            this.db = db;
            this.arr = arr;
            this.owner = owner;
            used = arr.Length;
        }
		
        object[] arr;
        int      used;
        [NonSerialized()]
        object   owner;        
        [NonSerialized()]
        StorageImpl db;        
    }
}