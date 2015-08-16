using System;
using System.Collections;
#if USE_GENERICS
using System.Collections.Generic;
#endif
using Perst;

namespace Perst.Impl        
{

#if USE_GENERICS
    class JoinSetEnumerable<T> : IEnumerable<T>, IEnumerable where T:class
    {
        public JoinSetEnumerable(Storage storage, IEnumerable<T> left, IEnumerable<T> right)      
        {
            db = storage;
            i1 = left;
            i2 = right;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new JoinSetEnumerator<T>(db, (PersistentEnumerator)i1.GetEnumerator(), (PersistentEnumerator)i2.GetEnumerator());
        }

        public IEnumerator<T> GetEnumerator() 
        {
            return new JoinSetEnumerator<T>(db, (PersistentEnumerator)i1.GetEnumerator(), (PersistentEnumerator)i2.GetEnumerator());
        }

        IEnumerable<T> i1;
        IEnumerable<T> i2;
        Storage db;
   } 
#else
    class JoinSetEnumerable : IEnumerable
    {
        public JoinSetEnumerable(Storage storage, IEnumerable left, IEnumerable right)      
        {
            db = storage;
            i1 = left;
            i2 = right;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new JoinSetEnumerator(db, (PersistentEnumerator)i1.GetEnumerator(), (PersistentEnumerator)i2.GetEnumerator());
        }

        IEnumerable i1;
        IEnumerable i2;
        Storage db;
   } 
#endif

#if USE_GENERICS
    class JoinSetEnumerator<T> : IEnumerator<T>, PersistentEnumerator where T:class
#else
    class JoinSetEnumerator : PersistentEnumerator
#endif
    {
        private PersistentEnumerator i1;
        private PersistentEnumerator i2;
        private int currOid;
        private Storage storage;

        public JoinSetEnumerator(Storage storage, PersistentEnumerator left, PersistentEnumerator right)      

        { 
            this.storage = storage;
            i1 = left;
            i2 = right;
        }
    
        public void Dispose() {}

        public void Reset()
        {
            currOid = 0;
            i1.Reset();
            i2.Reset();
        }
        
        public bool MoveNext() 
        { 
            currOid = 0;
            int oid2 = 0;
            while (i1.MoveNext()) 
            { 
                int oid1 = i1.CurrentOid;
                while (oid1 > oid2) 
                { 
                    if (!i2.MoveNext()) 
                    {
                        return false;
                    }
                    oid2 = i2.CurrentOid;
                }
                if (oid1 == oid2) 
                { 
                    currOid = oid1;
                    return true;
                }
            }
            return false;
        }

        public int CurrentOid 
        {
            get 
            {
                return currOid;
            }
        }

#if USE_GENERICS        
        object IEnumerator.Current
        {
            get
            {
                return getCurrent();
            }
        }

        public virtual T Current 
#else
        public virtual object Current 
#endif
        {
            get 
            {
#if USE_GENERICS        
                return (T)getCurrent();
#else
                return getCurrent();
#endif
            }
        }

        private object getCurrent() 
        {
            if (currOid == 0)
            {
                throw new InvalidOperationException();
            }
            return storage.GetObjectByOID(currOid);
        }    
    }
         
    [Serializable]	
#if USE_GENERICS
    class PersistentSet<T> : Btree<T,T>, ISet<T> where T:class
#else
    class PersistentSet : Btree, ISet
#endif
    {
        public PersistentSet(bool unique) 
        : base (ClassDescriptor.FieldType.tpObject, unique)
        {
        }

        // public constructor is needed for .Net
        public PersistentSet() 
        {
        } 

#if USE_GENERICS
        public override bool Contains(T o) 
        {
            Key key = new Key(o);
            IEnumerator<T> e = GetEnumerator(key, key, IterationOrder.AscentOrder);
            return e.MoveNext();
        }
#else
        public bool Contains(object o) 
        {
            Key key = new Key(o);
            IEnumerator e = GetEnumerator(key, key, IterationOrder.AscentOrder);
            return e.MoveNext();
        }
#endif
    
#if USE_GENERICS
        public override void Add(T o) 
#else
        public void Add(object o) 
#endif
        { 
            base.Put(new Key(o), o);
        }

#if USE_GENERICS
        public bool AddAll(ICollection<T> c) 
        {
            bool modified = false;
            foreach (T o in c) 
            {
                modified |= base.Put(new Key(o), o);
            }
            return modified;
        }
#else
        public bool AddAll(ICollection c) 
        {
            bool modified = false;
            foreach (object o in c) 
            {
                modified |= base.Put(new Key(o), o);
            }
            return modified;
        }
#endif


#if USE_GENERICS
        public override bool Remove(T o) 
#else
        public bool Remove(object o) 
#endif
        { 
            return removeIfExists(new Key(o), o);
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
        
        public override bool Equals(object o) 
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
            if (Count != s.Count) 
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