namespace Perst
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;


    /// <summary>
    /// Double linked list.
    /// </summary>
#if USE_GENERICS
    public class L2List<T> : PersistentCollection<T> where T:L2ListElem<T>
#else
    public class L2List : PersistentCollection
#endif
    { 
#if USE_GENERICS
        internal T head;
        internal T tail;
#else
        internal L2ListElem head;
        internal L2ListElem tail;
#endif
        
        internal int nElems;
        internal int updateCounter;

        /// <summary>
        /// Get list head element
        /// </summary>
        /// <returns>list head element or null if list is empty
        /// </returns>>
#if USE_GENERICS
        public T Head 
#else
        public L2ListElem Head 
#endif
        {
            get 
            {
                return head;
            }
        }

        /// <summary>
        /// Get list tail element
        /// </summary>
        /// <returns>list tail element or null if list is empty
        /// </returns>
#if USE_GENERICS
        public T Tail 
#else
        public L2ListElem Tail 
#endif
        { 
            get 
            { 
                return tail;
            }
        }

#if USE_GENERICS
        public override bool Contains(T obj) 
        {
            foreach (T o in this) 
#else
        public bool Contains(L2ListElem obj) 
        {
            foreach (L2ListElem o in this) 
#endif        
            { 
                if (o == obj) 
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Make list empty. 
        /// </summary>
        public override void Clear() 
        { 
            lock (this) 
            {
                head = tail = null;
                nElems = 0;
                updateCounter += 1;
                Modify();
            }
        }

        /// <summary>
        /// Insert element at the beginning of the list
        /// </summary>
#if USE_GENERICS
        public void Prepend(T elem) 
#else
        public void Prepend(L2ListElem elem) 
#endif
        { 
            lock (this) 
            { 
                elem.next = head;
                elem.prev = null;
                elem.Modify();
                if (head != null)
                { 
                    head.prev = elem;
                    head.Modify();
                } 
                else 
                {
                    tail = elem;
                } 
                head = elem;
                nElems += 1;
                updateCounter += 1;
                Modify();
            }
        }

        /// <summary>
        /// Insert element at the end of the list
        /// </summary>
#if USE_GENERICS
        public void Append(T elem) 
#else
        public void Append(L2ListElem elem) 
#endif
        { 
            lock (this) 
            { 
                elem.next = null;
                elem.prev = tail;
                elem.Modify(); 
                if (tail != null) 
                { 
                    tail.next = elem;
                    tail.Modify();
                }
                 else 
                {
                    tail = elem;
                } 
                tail = elem;
                nElems += 1;
                updateCounter += 1;
                Modify();
            }
        }

        /// <summary>
        /// Remove element from the list
        /// </summary>
#if USE_GENERICS
        public override bool Remove(T elem) 
#else
        public bool Remove(L2ListElem elem) 
#endif
        { 
            lock (this) 
            { 
                if (elem.prev != null)
                {
                    elem.prev.next = elem.next;
                    elem.prev.Modify();
                    elem.prev = null;
                    elem.Modify();
                } 
                else 
                {
                    head = head.next;
                } 
                if (elem.next != null) 
                { 
                    elem.next.prev = elem.prev;
                    elem.next.Modify();
                    elem.next = null;
                    elem.Modify();
                } 
                else 
                {
                    tail = tail.prev;
                } 
                nElems -= 1;
                updateCounter += 1;
                Modify();
                return true;
            }
        }

        /// <summary>
        /// Add element to the list
        /// </summary>
#if USE_GENERICS
        public override void Add(T elem) 
#else
        public void Add(L2ListElem elem) 
#endif
        { 
            Append(elem);
        }

        public override int Count 
        { 
            get 
            {
                return nElems;
            }
        }
        
#if USE_GENERICS
        class L2ListEnumerator : IEnumerator<T>,PersistentEnumerator
        {
            private T          curr;
            private int        counter;
            private L2List<T>  list;
            private bool       head;
#else
        class L2ListEnumerator : PersistentEnumerator
        {
            private L2ListElem curr;
            private int        counter;
            private L2List     list;
            private bool       head;
#endif

#if USE_GENERICS
            internal L2ListEnumerator(L2List<T> list) 
#else
            internal L2ListEnumerator(L2List list) 
#endif
            { 
                this.list = list;
                Reset();
            }

            public void Reset() 
            { 
                curr = null;
                counter = list.updateCounter;
                head = true;
            }

#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    return getCurrent();
                }
            }

            public T Current
            { 
                get 
                {
                    return (T)getCurrent();
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
                if (curr == null || counter != list.updateCounter)
                {
                    throw new InvalidOperationException();
                }
                return curr;
            }

            public int CurrentOid 
            {
                get 
                {
                     return curr.Oid;
                }
            }

            public void Dispose() {}

            public bool MoveNext() 
            { 
                if (counter != list.updateCounter) 
                { 
                    throw new InvalidOperationException();
                }
                if (head) 
                {
                    curr = list.head;
                    head = false;
                } 
                else if (curr != null) 
                {
                    curr = curr.next;
                }
                return curr != null;
            }
        }
            
        
#if USE_GENERICS
        public override IEnumerator<T> GetEnumerator() 
#else
        public override IEnumerator GetEnumerator() 
#endif
        { 
            return new L2ListEnumerator(this);
        }
    }
}
