namespace Perst.Impl        
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#else
    using System.Collections;
#endif
    using Perst;
    
    [Serializable]
#if USE_GENERICS
    class AltPersistentSet<T> : AltBtree<T,T>, Perst.ISet<T> where T:class
#else
    class AltPersistentSet : AltBtree, Perst.ISet
#endif
    {    
        // Default constructor for Silverlight   
        public AltPersistentSet()
        {
        }

        public AltPersistentSet(bool unique) 
        { 
            type = ClassDescriptor.FieldType.tpObject;
            this.unique = unique;
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
#else
        public bool AddAll(ICollection c) 
#endif
        {
            bool modified = false;
#if USE_GENERICS
            foreach (T o in c)
#else
            foreach (object o in c)
#endif
            {
                modified |= base.Put(new Key(o), o);
            }
            return modified;
        }


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
#else
        public bool ContainsAll(ICollection c) 
        { 
            foreach (object o in c)
#endif
            { 
                if (!Contains(o)) 
                {
                    return false;
                }
            }
            return true;
        }


             
#if USE_GENERICS
        public bool RemoveAll(ICollection<T> c) 
#else
        public bool RemoveAll(ICollection c) 
#endif
        {
            bool modified = false;
#if USE_GENERICS
            foreach (T o in c)
#else
            foreach (object o in c)
#endif
            {
                modified |= Remove(o);
            }
            return modified;
        }

        public override bool Equals(object o) 
        {
            if (o == this) 
            {
                return true;
            }
#if USE_GENERICS
            Perst.ISet<T> s = o as Perst.ISet<T>;
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
