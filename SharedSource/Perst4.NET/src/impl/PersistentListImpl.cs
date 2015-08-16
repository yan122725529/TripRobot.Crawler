namespace Perst.Impl
{
    using System;
    using Perst;
    using System.Collections;
    using System.Diagnostics;
#if USE_GENERICS
    using System.Collections.Generic;
    using Link=Link<object>;
#endif


    [Serializable]
#if USE_GENERICS
    public class PersistentListImpl<T> : PersistentCollection<T>, IPersistentList<T> where T:class
#else
    public class PersistentListImpl : PersistentCollection, IPersistentList
#endif
    {
        internal int      nElems;
        internal ListPage root;

        [NonSerialized()]
        internal int modCount;

        internal const int nLeafPageItems = (Page.pageSize-ObjectHeader.Sizeof-8)/4;
        internal const int nIntermediatePageItems = (Page.pageSize-ObjectHeader.Sizeof-12)/8;

        internal class TreePosition 
        {
            /**
             * B-Tree page where element is located
             */
            internal ListPage page;

            /**
             * Index of first element at the page
             */
            internal int index;
        }

    
        internal class ListPage : Persistent 
        {
            internal int   nItems;
            internal Link  items;

            internal virtual object this[int i] 
            { 
                get
                {
                    return items[i];
                }
                set 
                {
                    items[i] = value;
                }
            }

            internal virtual object getPosition(TreePosition pos, int i) 
            { 
                pos.page = this;
                pos.index -= i;
                return items[i];
            }

            internal virtual object getRawPosition(TreePosition pos, int i) 
            { 
                pos.page = this;
                pos.index -= i;
                return items.GetRaw(i);
            }

            internal virtual void prune() 
            { 
                Deallocate();
            }

            internal void clear(int i, int len) 
            { 
                while (--len >= 0) 
                { 
                    items[i++] = null;
                }
            }

            internal virtual void copy(int dstOffs, ListPage src, int srcOffs, int len) 
            { 
                Array.Copy(src.items.ToRawArray(), srcOffs, items.ToRawArray(), dstOffs, len);
            }

            internal virtual int MaxItems 
            {
                get
                {
                    return nLeafPageItems;
                }
            }

            internal virtual void setItem(int i, object obj) 
            {
                items[i] = obj;
            }

            internal virtual int size() 
            {
                return nItems;
            }

            internal virtual ListPage clonePage() 
            { 
                return new ListPage(Storage);
            }

            internal ListPage() {}

            internal ListPage(Storage storage) : base(storage)
            {
                int max = MaxItems;
                items = storage.CreateLink(max);
                items.Length = max;
            }

            internal virtual void remove(int i) 
            {
                nItems -= 1;
                copy(i, this, i+1, nItems-i);
                items[nItems] = null;
                Modify();
            }

            internal virtual bool underflow() 
            {
                return nItems < MaxItems/3;
            }

            internal virtual ListPage add(int i, object obj) 
            {
                int max = MaxItems;
                Modify();
                if (nItems < max) 
                {
                    copy(i+1, this, i, nItems-i);
                    setItem(i, obj);
                    nItems += 1;
                    return null;
                } 
                else 
                {
                    ListPage b = clonePage();
                    int m = (max+1)/2;
                    if (i < m) 
                    {
                        b.copy(0, this, 0, i);
                        b.copy(i+1, this, i, m-i-1);
                        copy(0, this, m-1, max-m+1);
                        b.setItem(i, obj);
                    } 
                    else 
                    {
                        b.copy(0, this, 0, m);
                        copy(0, this, m, i-m);
                        copy(i-m+1, this, i, max-i);
                        setItem(i-m, obj);
                    }
                    clear(max-m+1, m-1);
                    nItems = max-m+1;
                    b.nItems = m;
                    return b;
                }
            }
        }
 
        internal class ListIntermediatePage : ListPage 
        {
            internal int[] nChildren;

            internal override object getPosition(TreePosition pos, int i) 
            { 
                int j;
                for (j = 0; i >= nChildren[j]; j++) 
                {
                    i -= nChildren[j];
                }
                return ((ListPage)items[j]).getPosition(pos, i);
            }

            internal override object getRawPosition(TreePosition pos, int i) 
            { 
                int j;
                for (j = 0; i >= nChildren[j]; j++) 
                {
                    i -= nChildren[j];
                }
                return ((ListPage)items[j]).getRawPosition(pos, i);
            }
            
            internal override object this[int i] 
            {
                get 
                {
                    int j;
                    for (j = 0; i >= nChildren[j]; j++) 
                    {
                        i -= nChildren[j];
                    }
                    return ((ListPage)items[j])[i];
                }
    
                set 
                {
                    int j;
                    for (j = 0; i >= nChildren[j]; j++) 
                    {
                        i -= nChildren[j];
                    }
                    ((ListPage)items[j])[i] = value;
                }
            }

            internal override ListPage add(int i, object obj) 
            {
                int j;
                for (j = 0; i >= nChildren[j]; j++) 
                {
                    i -= nChildren[j];
                }
                ListPage pg = (ListPage)items[j];
                ListPage overflow = pg.add(i, obj);
                if (overflow != null) 
                { 
                    countChildren(j, pg);
                    overflow = base.add(j, overflow);
                } 
                else 
                {
                    Modify();
                    if (nChildren[j] != int.MaxValue) 
                    { 
                        nChildren[j] += 1;
                    }
                }                
                return overflow;
            }

            internal override void remove(int i) 
            {
                int j;
                for (j = 0; i >= nChildren[j]; j++) 
                {
                    i -= nChildren[j];
                }
                ListPage pg = (ListPage)items[j];
                pg.remove(i);
                Modify();
                if (pg.underflow()) 
                { 
                    handlePageUnderflow(pg, j);
                } 
                else 
                {
                    if (nChildren[j] != int.MaxValue) 
                    { 
                        nChildren[j] -= 1;
                    }
                }
            }

            internal void countChildren(int i, ListPage pg)
            {
                if (nChildren[i] != int.MaxValue) 
                { 
                    nChildren[i] = pg.size();
                }
            }
        
            internal override void prune() 
            { 
                for (int i = 0; i < nItems; i++) 
                {
                    ((ListPage)items[i]).prune();
                }
                Deallocate();
            }

            void handlePageUnderflow(ListPage a, int r) 
            {
                int an = a.nItems;
                int max = a.MaxItems;
                if (r+1 < nItems) 
                { // exists greater page
                    ListPage b = (ListPage)items[r+1];
                    int bn = b.nItems; 
                    Debug.Assert(bn >= an);
                    if (an + bn > max) 
                    { 
                        // reallocation of nodes between pages a and b
                        int i = bn - ((an + bn) >> 1);
                        b.Modify();
                        a.copy(an, b, 0, i);
                        b.copy(0, b, i, bn-i);
                        b.clear(bn-i, i);
                        b.nItems -= i;
                        a.nItems += i;
                        nChildren[r] = a.size();
                        countChildren(r+1, b);
                    } 
                    else 
                    { // merge page b to a  
                        a.copy(an, b, 0, bn);
                        a.nItems += bn;
                        nItems -= 1;
                        nChildren[r] = nChildren[r+1];
                        copy(r+1, this, r+2, nItems-r-1);
                        countChildren(r, a);
                        items[nItems] = null;
                        b.Deallocate();
                    }
                } 
                else 
                { // page b is before a
                    ListPage b = (ListPage)items[r-1];
                    int bn = b.nItems; 
                    Debug.Assert(bn >= an);
                    b.Modify();
                    if (an + bn > max) 
                    { 
                        // reallocation of nodes between pages a and b
                        int i = bn - ((an + bn) >> 1);
                        b.Modify();
                        a.copy(i, a, 0, an);
                        a.copy(0, b, bn-i, i);
                        b.clear(bn-i, i);
                        b.nItems -= i;
                        a.nItems += i;
                        nChildren[r-1] = b.size();
                        countChildren(r, a);
                    } 
                    else 
                    { // merge page b to a
                        b.copy(bn, a, 0, an);
                        b.nItems += an;
                        nItems -= 1;
                        nChildren[r-1] = nChildren[r];
                        countChildren(r-1, b);
                        items[r] = null;
                        a.Deallocate();
                    }
                }
            }

            internal override void copy(int dstOffs, ListPage src, int srcOffs, int len) 
            { 
                base.copy(dstOffs, src, srcOffs, len);
                Array.Copy(((ListIntermediatePage)src).nChildren, srcOffs, nChildren, dstOffs, len); 
            }

            internal override int MaxItems 
            {
                get 
                {
                    return nIntermediatePageItems;
                }
            }

            internal override void setItem(int i, object obj) 
            {
                base.setItem(i, obj);
                nChildren[i] = ((ListPage)obj).size();
            }

            internal override int size() 
            {
                if (nChildren[nItems-1] == int.MaxValue) 
                { 
                    return int.MaxValue;
                } 
                else 
                { 
                    int n = 0;
                    for (int i = 0; i < nItems; i++) 
                    { 
                        n += nChildren[i];
                    }
                    return n;
                }
            }

            internal override ListPage clonePage() 
            { 
                return new ListIntermediatePage(Storage);
            }

            internal ListIntermediatePage() {}

            internal ListIntermediatePage(Storage storage) : base(storage)
            {
                nChildren = new int[nIntermediatePageItems];
            }
        }
    
#if USE_GENERICS
        public T this[int i] 
#else
        public Object this[int i]
#endif
        { 
            get 
            {
                if (i < 0 || i >= nElems) 
                { 
                    throw new IndexOutOfRangeException("index=" + i + ", size=" + nElems);
                }
#if USE_GENERICS
                return (T)root[i];
#else
                return root[i];
#endif
            }
            set 
            {
                if (i < 0 || i >= nElems) 
                { 
                    throw new IndexOutOfRangeException("index=" + i + ", size=" + nElems);
                }
                root[i] = value;
            }
        }
    
        internal object getPosition(TreePosition pos, int i) 
        { 
            if (i < 0 || i >= nElems) 
            { 
                throw new IndexOutOfRangeException("index=" + i + ", size=" + nElems);
            }
            if (pos.page != null && i >= pos.index && i < pos.index + pos.page.nItems) 
            { 
                return pos.page.items[i - pos.index];
            }
            pos.index = i;
            return root.getPosition(pos, i);
        }

        internal object getRawPosition(TreePosition pos, int i) 
        { 
            if (i < 0 || i >= nElems) 
            { 
                throw new IndexOutOfRangeException("index=" + i + ", size=" + nElems);
            }
            if (pos.page != null && i >= pos.index && i < pos.index + pos.page.nItems) 
            { 
                return pos.page.items.GetRaw(i - pos.index);
            }
            pos.index = i;
            return root.getRawPosition(pos, i);
        }

        
#if USE_GENERICS
        public T[] ToArray()
        {
            T[] a = new T[nElems];
            int i = 0;
            foreach (T obj in this) {
                a[i++] = obj;
            }
            return a;
        }
#else
        public object[] ToArray()
        {
            object[] a = new object[nElems];
            int i = 0;
            foreach (object obj in this) 
            {
                a[i++] = obj;
            }
            return a;
        }
#endif

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

        public Array ToArray(Type elemType)
        {
            Array a = Array.CreateInstance(elemType, nElems);
            int i = 0;
            foreach (object obj in this) 
            {
                a.SetValue(obj, i++);
            }
 
            return a;
        }
            
#if USE_GENERICS
        public override bool Contains(T obj)
#else
        public bool Contains(object obj)
#endif
        {
            return IndexOf(obj) >= 0;
        }
		
#if USE_GENERICS
        public int IndexOf(T obj)
#else
        public int IndexOf(object obj)
#endif
        {
            int i = 0;
            if (obj == null) 
            {
                foreach (object o in this) 
                {
                    if (o == null) 
                    {
                        return i;
                    }
                    i += 1;
                }
            } 
            else 
            {
                foreach (object o in this) 
                {
                    if (obj == o) 
                    {
                        return i;
                    }
                    i += 1;
                }
            }
            return -1;
        }

        public override void Clear()
        {
            modCount += 1;
            root.prune();
            root = new ListPage(Storage); 
            nElems = 0;
            Modify();
        }
        
        public override int Count 
        {
            get 
            {
                return nElems;
            }
        }

 
#if USE_GENERICS
        public override void Add(T o) 
        {
            Insert(nElems, o);
        }
#else
        public int Add(object o) 
        {
            Insert(nElems, o);
            return nElems;
        }
#endif

#if USE_GENERICS
        public void Insert(int i, T o)
#else
        public void Insert(int i, object o)
#endif
        {
            if (i < 0 || i > nElems) 
            { 
                throw new IndexOutOfRangeException("index=" + i + ", size=" + nElems);
            }
            ListPage overflow = root.add(i, o);
            if (overflow != null) 
            { 
                ListIntermediatePage pg = new ListIntermediatePage(Storage);
                pg.setItem(0, overflow);            
                pg.items[1] = root;
                pg.nChildren[1] = int.MaxValue;
                pg.nItems = 2;
                root = pg;
            }
            nElems += 1;
            modCount += 1;
            Modify();
        }
   
        public void RemoveAt(int i) 
        {
            if (i < 0 || i >= nElems) 
            { 
                throw new IndexOutOfRangeException("index=" + i + ", size=" + nElems);
            }
            root.remove(i);
            if (root.nItems == 1 && root is ListIntermediatePage) 
            {
                ListPage newRoot = (ListPage)root.items[0];
                root.Deallocate();
                root = newRoot;
            }
            nElems -= 1;
            modCount += 1;
            Modify();
        }

#if USE_GENERICS
        public override bool Remove(T obj)
#else
        public void Remove(object obj)
        {
            RemoveObject(obj);
        }

        public bool RemoveObject(object obj)
#endif
        {
            int i = IndexOf(obj);
            if (i >= 0) 
            {
                RemoveAt(i);
                return true;
            }
            return false;
        }


#if USE_GENERICS
        class ListEnumerator : TreePosition, IBidirectionalEnumerator<T>, PersistentEnumerator 
#else
        class ListEnumerator : TreePosition, PersistentEnumerator, IBidirectionalEnumerator 
#endif
        { 
            public void Dispose() {}

            public bool MoveNext() 
            {
                checkForCommodification();
                if (i+1 < list.Count) 
                { 
                    i += 1;
                    return true;
                }
                return false;
            }

            public bool MovePrevious() 
            {
                checkForCommodification();
                if (i > 0) 
                { 
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
                    checkForCommodification();
                    return list.getPosition(this, i);
                }
            }

            public T Current
            {
                get 
                {
                    checkForCommodification();
                    return (T)list.getPosition(this, i);
                }
            }

#else
            public object Current
            {
                get 
                {
                    checkForCommodification();
                    return list.getPosition(this, i);
                }
            }
#endif
            public int CurrentOid 
            {
                get 
                {
                    checkForCommodification();
                    return list.Storage.GetOid(list.getRawPosition(this, i));
                }
            }
 
            public void Reset() 
            {
                i = start;
            }


            void checkForCommodification()
            {
                if (modCount != list.modCount)
                {
                    throw new InvalidOperationException("B-Tree was modified");
                }
            }
            
#if USE_GENERICS
            internal ListEnumerator(PersistentListImpl<T> list, int start) 
#else
            internal ListEnumerator(PersistentListImpl list, int start) 
#endif
            {
                this.list = list;
                this.start = start;
                modCount = list.modCount;
                i = start;
            }

            private int i;
            private int start;
            private int modCount;
#if USE_GENERICS
            private PersistentListImpl<T> list;
#else
            private PersistentListImpl list;
#endif
        }      

#if USE_GENERICS
        public override IEnumerator<T> GetEnumerator() 
#else
        public override IEnumerator GetEnumerator() 
#endif
        {
            return GetEnumerator(-1);
        }

#if USE_GENERICS
        public IBidirectionalEnumerator<T> GetEnumerator(int i) 
#else
        public IBidirectionalEnumerator GetEnumerator(int i) 
#endif
        { 
            return new ListEnumerator(this, i);
        }

        internal PersistentListImpl() {}
    
        internal PersistentListImpl(Storage storage) : base(storage)
        { 
            root = new ListPage(storage);
        }
    }
}
