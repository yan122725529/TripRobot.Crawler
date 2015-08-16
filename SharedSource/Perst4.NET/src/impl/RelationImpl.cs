using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif
using Perst;


namespace Perst.Impl
{
	
#if USE_GENERICS
    public class RelationImpl<M,O>:Relation<M,O> where M:class where O:class
#else
    public class RelationImpl:Relation
#endif
    {
        public override int Count 
        { 
            get 
            {
                return link.Count;
            }
        }

#if USE_GENERICS
        public override void CopyTo(M[] dst, int i) 
#else
        public override void CopyTo(Array dst, int i) 
#endif
        {
            link.CopyTo(dst, i);
        }

        public override int Length 
        {
            get 
            {
                return link.Length;
            }
            set 
            {
                link.Length = value;
            }
        }       

#if USE_GENERICS
        public override M this[int i] 
#else
        public override object this[int i] 
#endif
        {
            get 
            {
                return link.Get(i);
            }
            set 
            {
                link.Set(i, value);
            }
        }

        public override int Size()
        {
            return link.Length;
        }
		
#if USE_GENERICS
        public override M Get(int i)
#else
        public override object Get(int i)
#endif
        {
            return link.Get(i);
        }
		
        public override object GetRaw(int i)
        {
            return link.GetRaw(i);
        }
		
#if USE_GENERICS
        public override void Set(int i, M obj)
#else
        public override void Set(int i, object obj)
#endif
        {
            link.Set(i, obj);
        }
		
#if USE_GENERICS
        public override bool Remove(M obj) 
#else
        public override bool Remove(object obj) 
#endif
        {
            return link.Remove(obj);
        }

#if USE_GENERICS
        public override void RemoveAt(int i)
        {
            Remove(i);
        }
#endif

        public override void Remove(int i)
        {
            link.Remove(i);
        }
		
#if USE_GENERICS
        public override void Insert(int i, M obj)
#else
        public override void Insert(int i, object obj)
#endif
        {
            link.Insert(i, obj);
        }
		
#if USE_GENERICS
        public override void Add(M obj)
#else
        public override void Add(object obj)
#endif
        {
            link.Add(obj);
        }
		
#if USE_GENERICS
        public override void AddAll(M[] arr)
#else
        public override void AddAll(object[] arr)
#endif
        {
            link.AddAll(arr);
        }
		
#if USE_GENERICS
        public override void AddAll(M[] arr, int from, int length)
#else
        public override void AddAll(object[] arr, int from, int length)
#endif
        {
            link.AddAll(arr, from, length);
        }
		
#if USE_GENERICS
        public override void AddAll(Link<M> anotherLink)
#else
        public override void AddAll(Link anotherLink)
#endif
        {
            link.AddAll(anotherLink);
        }
		
#if USE_GENERICS
        public override M[] ToArray()
#else
        public override object[] ToArray()
#endif
        {
            return link.ToArray();
        }
		
        public override Array ToRawArray()
        {
            return link.ToRawArray();
        }
		
        public override Array ToArray(Type elemType)
        {
            return link.ToArray(elemType);
        }
		
#if USE_GENERICS
        public override bool Contains(M obj)
#else
        public override bool Contains(object obj)
#endif
        {
            return link.Contains(obj);
        }
		
#if USE_GENERICS
        public override bool ContainsElement(int i, M obj)
#else
        public override bool ContainsElement(int i, object obj)
#endif
        {
            return link.ContainsElement(i, obj);
        }

#if USE_GENERICS
        public override int IndexOf(M obj)
#else
        public override int IndexOf(object obj)
#endif
        {
            return link.IndexOf(obj);
        }
		
#if USE_GENERICS
        public override IEnumerator<M> GetEnumerator() 
        {
            return ((IEnumerable<M>)link).GetEnumerator();
        }

        public override IBidirectionalEnumerator<M> GetEnumerator(int start) 
        {
            return link.GetEnumerator(start);
        }
#else
        public override IEnumerator GetEnumerator() 
        {
            return link.GetEnumerator();
        }
        public override IBidirectionalEnumerator GetEnumerator(int start) 
        {
            return link.GetEnumerator(start);
        }
#endif

        public override void Clear() 
        {
            link.Clear();
        }
		
        public override void Unpin()
        {
            link.Unpin();
        }

        public override void Pin()
        {
            link.Pin();
        }

#if USE_GENERICS
        public override IEnumerable<M> Select(string predicate) 
        { 
            Query<M> query = new QueryImpl<M>(Storage);
            return query.Select(link, predicate);
        }
#else
        public override IEnumerable Select(Type cls, string predicate) 
        { 
            Query query = new QueryImpl(Storage);
            return query.Select(cls, link, predicate);
        }
#endif

#if USE_GENERICS
        internal RelationImpl(StorageImpl db, O owner):base(owner)
        {
            link = new LinkImpl<M>(db, 8);
        }
#else
        internal RelationImpl(StorageImpl db, object owner):base(owner)
        {
            link = new LinkImpl(db, 8);
        }
#endif
		
        internal RelationImpl() {}

#if USE_GENERICS
        internal Link<M> link;
#else
        internal Link    link;
#endif
    }
}