using System;
#if USE_GENERICS
using System.Collections.Generic;
#endif
using System.Collections;
using Perst.Impl;

namespace Perst
{
    /// <summary>
    /// Base class for all persistent collections
    /// </summary>
    [Serializable]
#if USE_GENERICS
    public abstract class PersistentCollection<T> : PersistentResource, ITable<T> where T:class
#else
    public abstract class PersistentCollection : PersistentResource, ITable
#endif
    {
        public PersistentCollection()
        {
        }

        public PersistentCollection(Storage storage)
        : base(storage) 
        {
        }

#if USE_GENERICS
        class EnumeratorWrapper<E> : IEnumerator 
        { 
             IEnumerator<E> enumerator;

             public bool MoveNext()
             {
                 return enumerator.MoveNext();
             }

             public object Current 
             { 
                 get 
                 {
                     return enumerator.Current;
                 }   
             }
             
             public void Reset()
             { 
                 throw new InvalidOperationException("Reset not supported");
             }

             internal EnumeratorWrapper(IEnumerator<E> enumerator) 
             { 
                  this.enumerator = enumerator;
             } 
        } 

        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new EnumeratorWrapper<T>(this.GetEnumerator());
        }

        public virtual IEnumerable<T> Select(string predicate) 
        { 
            Query<T> query = new QueryImpl<T>(Storage);
            return query.Select(this, predicate);
        }
#else
        public abstract IEnumerator GetEnumerator();

        public virtual IEnumerable Select(Type cls, string predicate) 
        { 
            Query query = new QueryImpl(Storage);
            return query.Select(cls, this, predicate);
        }
#endif
        
        
        public abstract int Count 
        { 
            get;
        }

        public virtual bool IsSynchronized 
        {
            get 
            {
                return true;
            }
        }

        public virtual object SyncRoot 
        {
            get 
            {
                return this;
            }
        }

#if USE_GENERICS
        public virtual void CopyTo(T[] dst, int i) 
#else
        public virtual void CopyTo(Array dst, int i) 
#endif
        {
            foreach (object o in this) 
            { 
                dst.SetValue(o, i++);
            }
        }

        public abstract void Clear();

        public virtual void DeallocateMembers()
        {
            foreach (object o in this) 
            { 
                Storage.Deallocate(o);
            }
            Clear();
        }

#if USE_GENERICS
        public virtual void Add(T obj)
        {
            throw new InvalidOperationException("Add is not supported");
        }


        public virtual bool IsReadOnly 
        { 
            get
            { 
                return false;
            } 
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
            throw new InvalidOperationException("Remove is not supported");
        }
#endif
    }
}
