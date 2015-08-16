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
    internal class RtreeRn<T>:PersistentCollection<T>, SpatialIndexRn<T> where T:class
#else
    internal class RtreeRn:PersistentCollection, SpatialIndexRn
#endif
    {
        internal int         height;
        internal int         n;
        internal RtreeRnPage root;
        [NonSerialized()]
        private int         updateCounter;

        internal RtreeRn() {}

        public override int Count 
        { 
            get 
            {
                return n;
            }
        }

#if USE_GENERICS
        public void Put(RectangleRn r, T obj) 
#else
        public void Put(RectangleRn r, object obj) 
#endif
        {
            if (root == null) 
            { 
                root = new RtreeRnPage(Storage, obj, r);
                height = 1;
            } 
            else 
            { 
                RtreeRnPage p = root.insert(Storage, r, obj, height); 
                if (p != null) 
                {
                    root = new RtreeRnPage(Storage, root, p);
                    height += 1;
                }
            }
            n += 1;
            updateCounter += 1;
            Modify();
        }
    
        public int Size() 
        { 
            return n;
        }

#if USE_GENERICS
        public void Remove(RectangleRn r, T obj) 
#else
        public void Remove(RectangleRn r, object obj) 
#endif
        {
            if (root == null) 
            { 
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_FOUND);
            }
            ArrayList reinsertList = new ArrayList();
            int reinsertLevel = root.remove(r, obj, height, reinsertList);
            if (reinsertLevel < 0) 
            { 
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_FOUND);
            }        
            for (int i = reinsertList.Count; --i >= 0;) 
            {
                RtreeRnPage p = (RtreeRnPage)reinsertList[i];
                for (int j = 0, pn = p.n; j < pn; j++) 
                { 
                    RtreeRnPage q = root.insert(Storage, p.b[j], p.branch[j], height - reinsertLevel); 
                    if (q != null) 
                    { 
                        // root splitted
                        root = new RtreeRnPage(Storage, root, q);
                        height += 1;
                    }
                }
                reinsertLevel -= 1;
                p.Deallocate();
            }
            if (root.n == 1 && height > 1) 
            { 
                RtreeRnPage newRoot = (RtreeRnPage)root.branch[0];
                root.Deallocate();
                root = newRoot;
                height -= 1;
            }
            n -= 1;
            updateCounter += 1;
            Modify();
        }
    
#if USE_GENERICS
        public T[] Get(RectangleRn r) 
#else
        public object[] Get(RectangleRn r) 
#endif
        {
            ArrayList result = new ArrayList();
            if (root != null) 
            { 
                root.find(r, result, height);
            }
#if USE_GENERICS
            return (T[])result.ToArray(typeof(T));
#else
            return result.ToArray();
#endif
        }

        public RectangleRn WrappingRectangle
        {
            get 
            {
                return (root != null) 
                    ? root.cover()
                    : new RectangleRn(new double[0]);
            }
        }

        public override void Clear() 
        {
            if (root != null) 
            { 
                root.purge(height);
                root = null;
            }
            height = 0;
            n = 0;
            updateCounter += 1;
            Modify();
        }

        public override void Deallocate() 
        {
            Clear();
            base.Deallocate();
        }

#if USE_GENERICS
        public IEnumerable<T> Overlaps(RectangleRn r) 
#else
        public IEnumerable Overlaps(RectangleRn r) 
#endif
        { 
            return new RtreeEnumerable(this, r);
        }

#if USE_GENERICS
        public override IEnumerator<T> GetEnumerator() 
#else
        public override IEnumerator GetEnumerator() 
#endif
        {
            return Overlaps(WrappingRectangle).GetEnumerator();
        }

        public IDictionaryEnumerator GetDictionaryEnumerator(RectangleRn r) 
        { 
            return new RtreeEntryEnumerator(this, r);
        }

        public IDictionaryEnumerator GetDictionaryEnumerator() 
        { 
            return GetDictionaryEnumerator(WrappingRectangle);
        }

#if USE_GENERICS
        class RtreeEnumerable : IEnumerable<T>, IEnumerable
        {
            internal RtreeEnumerable(RtreeRn<T> tree, RectangleRn r) 
#else
        class RtreeEnumerable : IEnumerable
        {
            internal RtreeEnumerable(RtreeRn tree, RectangleRn r) 
#endif
            { 
                this.tree = tree;
                this.r = r;            
            }
            
#if USE_GENERICS
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new RtreeEnumerator(tree, r);
            }

            public IEnumerator<T> GetEnumerator()
#else
            public IEnumerator GetEnumerator()
#endif
            {
                return new RtreeEnumerator(tree, r);
            }

#if USE_GENERICS
            RtreeRn<T>    tree;
#else
            RtreeRn       tree;
#endif
            RectangleRn   r;
        }

#if USE_GENERICS
        class RtreeEnumerator : IEnumerator<T>, PersistentEnumerator
        {
            internal RtreeEnumerator(RtreeRn<T> tree, RectangleRn r) 
#else
        class RtreeEnumerator : PersistentEnumerator
        {
            internal RtreeEnumerator(RtreeRn tree, RectangleRn r) 
#endif
            { 
                counter = tree.updateCounter;
                height = tree.height;
                this.tree = tree;
                if (height == 0) 
                { 
                    return;
                }
                this.r = r;            
                pageStack = new RtreeRnPage[height];
                posStack = new int[height];
                Reset();
            }

            public void Reset()
            {
                hasNext = gotoFirstItem(0, tree.root);
            }

            public void Dispose() {}

            public bool MoveNext() 
            {
                if (counter != tree.updateCounter) 
                { 
                    throw new InvalidOperationException("Tree was modified");
                }
                if (hasNext) 
                { 
                    page = pageStack[height-1];
                    pos = posStack[height-1];
                    if (!gotoNextItem(height-1)) 
                    { 
                        hasNext = false;
                    }
                    return true;
                } 
                else 
                { 
                    page = null;
                    return false;
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
                if (page == null)
                {
                    throw new InvalidOperationException();
                }
                return page.branch[pos];
            }

            public int CurrentOid 
            {
                get 
                {
                    return tree.Storage.GetOid(page.branch.GetRaw(pos));
                }
            }

            private bool gotoFirstItem(int sp, RtreeRnPage pg) 
            { 
                for (int i = 0, n = pg.n; i < n; i++) 
                { 
                    if (r.Intersects(pg.b[i])) 
                    { 
                        if (sp+1 == height || gotoFirstItem(sp+1, (RtreeRnPage)pg.branch[i])) 
                        { 
                            pageStack[sp] = pg;
                            posStack[sp] = i;
                            return true;
                        }
                    }
                }
                return false;
            }
              
 
            private bool gotoNextItem(int sp) 
            {
                RtreeRnPage pg = pageStack[sp];
                for (int i = posStack[sp], n = pg.n; ++i < n;) 
                { 
                    if (r.Intersects(pg.b[i])) 
                    { 
                        if (sp+1 == height || gotoFirstItem(sp+1, (RtreeRnPage)pg.branch[i])) 
                        { 
                            pageStack[sp] = pg;
                            posStack[sp] = i;
                            return true;
                        }
                    }
                }
                pageStack[sp] = null;
                return (sp > 0) ? gotoNextItem(sp-1) : false;
            }
              
 
            protected RtreeRnPage[] pageStack;
            protected int[]         posStack;
            protected int           counter;
            protected int           height;
            protected int           pos;
            protected bool          hasNext;
            protected RtreeRnPage   page;
#if USE_GENERICS
            protected RtreeRn<T>    tree;
#else
            protected RtreeRn       tree;
#endif
            protected RectangleRn   r;
        }

        class RtreeEntryEnumerator : RtreeEnumerator, IDictionaryEnumerator 
        {
#if USE_GENERICS
            internal RtreeEntryEnumerator(RtreeRn<T> tree, RectangleRn r) 
#else
            internal RtreeEntryEnumerator(RtreeRn tree, RectangleRn r) 
#endif
                : base(tree, r)
            {}

#if USE_GENERICS
            public new virtual object Current 
#else
            public override object Current 
#endif
            {
                get 
                {
                    return Entry;
                }
            }

            public DictionaryEntry Entry 
            {
                get 
                {
                    if (page == null) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return new DictionaryEntry(page.b[pos], page.branch[pos]);
                }
            }

            public object Key 
            {
                get 
                {
                    if (page == null) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return page.b[pos];
                }
            }

            public object Value 
            {
                get 
                {
                    if (page == null) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return page.branch[pos];
                }
            }
        }

        class Neighbor 
        { 
            internal object   child;
            internal Neighbor next;
            internal int      level;
            internal double   distance;

            internal Neighbor(object child, double distance, int level) 
            { 
                this.child = child;
                this.distance = distance;
                this.level = level;
            }
        }

#if USE_GENERICS
        class NeighborEnumerable : IEnumerable<T>, IEnumerable
        {
            internal NeighborEnumerable(RtreeRn<T> tree, PointRn center) 
#else
        class NeighborEnumerable : IEnumerable
        {
            internal NeighborEnumerable(RtreeRn tree, PointRn center) 
#endif
            {
                this.center = center;
                this.tree = tree;
            }

#if USE_GENERICS
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new NeighborEnumerator(tree, center);
            }

            public IEnumerator<T> GetEnumerator()
#else
            public IEnumerator GetEnumerator()
#endif
            {
                return new NeighborEnumerator(tree, center);
            }

#if USE_GENERICS
            RtreeRn<T>    tree;
#else
            RtreeRn       tree;
#endif                                
            PointRn       center;
        }
 
#if USE_GENERICS
        class NeighborEnumerator : IEnumerator<T>, PersistentEnumerator
        {
            internal NeighborEnumerator(RtreeRn<T> tree, PointRn center) 
#else
        class NeighborEnumerator: PersistentEnumerator
        {
            internal NeighborEnumerator(RtreeRn tree, PointRn center) 
#endif
            {
                this.center = center;
                this.tree = tree;
                Reset();
            }

            public void Reset()
            {
                counter = tree.updateCounter;
                curr = null;
                list = tree.height == 0 ? null : new Neighbor(tree.root, tree.root.cover().Distance(center), tree.height);
            }

            void Insert(Neighbor node) 
            { 
                Neighbor prev = null, next = list;
                double distance = node.distance;
                while (next != null && next.distance < distance) 
                { 
                    prev = next;
                    next = prev.next;
                }
                node.next = next;
                if (prev == null) 
                { 
                    list = node;
                } 
                else 
                { 
                    prev.next = node;
                }
            }

            public bool MoveNext() 
            { 
                if (counter != tree.updateCounter) 
                { 
                    throw new InvalidOperationException("Tree was modified");
                }
                while (true) 
                { 
                    Neighbor neighbor = list;
                    if (neighbor == null) 
                    { 
                        return false;
                    }
                    if (neighbor.level == 0) 
                    { 
                        curr = neighbor.child;
                        list = neighbor.next;
                        return true;
                    }
                    list = neighbor.next;
                    RtreeRnPage pg = (RtreeRnPage)neighbor.child;
                    for (int i = 0, n = pg.n; i < n; i++) 
                    { 
                        Insert(new Neighbor(pg.branch[i], pg.b[i].Distance(center), neighbor.level-1));
                    }
                }
            }

            public void Dispose() {}

#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    return curr;
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
                    return (T)curr;
#else
                    return curr;
#endif
                }
            }

            public int CurrentOid 
            {
                get 
                {
                    return tree.Storage.GetOid(curr);
                }
            }

#if USE_GENERICS
            RtreeRn<T>    tree;
#else
            RtreeRn       tree;
#endif                                
            Object curr;
            Neighbor list;
            int counter;
            PointRn center;
        }
        
#if USE_GENERICS
        public IEnumerable<T> Neighbors(PointRn center)
#else
        public IEnumerable Neighbors(PointRn center)
#endif
        {
            return new NeighborEnumerable(this, center);           
        }
    }
}
