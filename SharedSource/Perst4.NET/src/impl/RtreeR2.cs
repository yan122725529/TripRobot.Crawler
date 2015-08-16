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
    internal class RtreeR2<T>:PersistentCollection<T>, SpatialIndexR2<T> where T:class
#else
    internal class RtreeR2:PersistentCollection, SpatialIndexR2
#endif
    {
        internal int         height;
        internal int         n;
        internal RtreeR2Page root;
        [NonSerialized()]
        private int         updateCounter;

        internal RtreeR2() {}

        public override int Count 
        { 
            get 
            {
                return n;
            }
        }

#if USE_GENERICS
        public void Put(RectangleR2 r, T obj) 
#else
        public void Put(RectangleR2 r, object obj) 
#endif
        {
            if (root == null) 
            { 
                root = new RtreeR2Page(Storage, obj, r);
                height = 1;
            } 
            else 
            { 
                RtreeR2Page p = root.insert(Storage, r, obj, height); 
                if (p != null) 
                {
                    root = new RtreeR2Page(Storage, root, p);
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
        public void Remove(RectangleR2 r, T obj) 
#else
        public void Remove(RectangleR2 r, object obj) 
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
                RtreeR2Page p = (RtreeR2Page)reinsertList[i];
                for (int j = 0, pn = p.n; j < pn; j++) 
                { 
                    RtreeR2Page q = root.insert(Storage, p.b[j], p.branch[j], height - reinsertLevel); 
                    if (q != null) 
                    { 
                        // root splitted
                        root = new RtreeR2Page(Storage, root, q);
                        height += 1;
                    }
                }
                reinsertLevel -= 1;
                p.Deallocate();
            }
            if (root.n == 1 && height > 1) 
            { 
                RtreeR2Page newRoot = (RtreeR2Page)root.branch[0];
                root.Deallocate();
                root = newRoot;
                height -= 1;
            }
            n -= 1;
            updateCounter += 1;
            Modify();
        }
    
#if USE_GENERICS
        public T[] Get(RectangleR2 r) 
#else
        public object[] Get(RectangleR2 r) 
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

        public RectangleR2 WrappingRectangle
        {
            get 
            {
                return (root != null) 
                    ? root.cover()
                    : new RectangleR2(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
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
        public IEnumerable<T> Overlaps(RectangleR2 r) 
#else
        public IEnumerable Overlaps(RectangleR2 r) 
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

        public IDictionaryEnumerator GetDictionaryEnumerator(RectangleR2 r) 
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
            internal RtreeEnumerable(RtreeR2<T> tree, RectangleR2 r) 
#else
        class RtreeEnumerable : IEnumerable
        {
            internal RtreeEnumerable(RtreeR2 tree, RectangleR2 r) 
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
            RtreeR2<T>    tree;
#else
            RtreeR2       tree;
#endif
            RectangleR2   r;
        }

#if USE_GENERICS
        class RtreeEnumerator : IEnumerator<T>, PersistentEnumerator
        {
            internal RtreeEnumerator(RtreeR2<T> tree, RectangleR2 r) 
#else
        class RtreeEnumerator : PersistentEnumerator
        {
            internal RtreeEnumerator(RtreeR2 tree, RectangleR2 r) 
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
                pageStack = new RtreeR2Page[height];
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

            private bool gotoFirstItem(int sp, RtreeR2Page pg) 
            { 
                for (int i = 0, n = pg.n; i < n; i++) 
                { 
                    if (r.Intersects(pg.b[i])) 
                    { 
                        if (sp+1 == height || gotoFirstItem(sp+1, (RtreeR2Page)pg.branch[i])) 
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
                RtreeR2Page pg = pageStack[sp];
                for (int i = posStack[sp], n = pg.n; ++i < n;) 
                { 
                    if (r.Intersects(pg.b[i])) 
                    { 
                        if (sp+1 == height || gotoFirstItem(sp+1, (RtreeR2Page)pg.branch[i])) 
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
              
 
            protected RtreeR2Page[] pageStack;
            protected int[]         posStack;
            protected int           counter;
            protected int           height;
            protected int           pos;
            protected bool          hasNext;
            protected RtreeR2Page   page;
#if USE_GENERICS
            protected RtreeR2<T>    tree;
#else
            protected RtreeR2       tree;
#endif
            protected RectangleR2   r;
        }

        class RtreeEntryEnumerator : RtreeEnumerator, IDictionaryEnumerator 
        {
#if USE_GENERICS
            internal RtreeEntryEnumerator(RtreeR2<T> tree, RectangleR2 r) 
#else
            internal RtreeEntryEnumerator(RtreeR2 tree, RectangleR2 r) 
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
            internal NeighborEnumerable(RtreeR2<T> tree, double x, double y) 
#else
        class NeighborEnumerable : IEnumerable
        {
            internal NeighborEnumerable(RtreeR2 tree, double x, double y) 
#endif
            {
                this.x = x;
                this.y = y;
                this.tree = tree;
            }

#if USE_GENERICS
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new NeighborEnumerator(tree, x, y);
            }

            public IEnumerator<T> GetEnumerator()
#else
            public IEnumerator GetEnumerator()
#endif
            {
                return new NeighborEnumerator(tree, x, y);
            }

#if USE_GENERICS
            RtreeR2<T>    tree;
#else
            RtreeR2       tree;
#endif                                
            double x;
            double y; 
        }
 
#if USE_GENERICS
        class NeighborEnumerator : IEnumerator<T>, PersistentEnumerator
        {
            internal NeighborEnumerator(RtreeR2<T> tree, double x, double y) 
#else
        class NeighborEnumerator: PersistentEnumerator
        {
            internal NeighborEnumerator(RtreeR2 tree, double x, double y) 
#endif
            {
                this.x = x;
                this.y = y;
                this.tree = tree;
                Reset();
            }

            public void Reset()
            {
                counter = tree.updateCounter;
                curr = null;
                list = tree.height == 0 ? null : new Neighbor(tree.root, tree.root.cover().Distance(x, y), tree.height);
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
                    RtreeR2Page pg = (RtreeR2Page)neighbor.child;
                    for (int i = 0, n = pg.n; i < n; i++) 
                    { 
                        Insert(new Neighbor(pg.branch[i], pg.b[i].Distance(x, y), neighbor.level-1));
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
            RtreeR2<T>    tree;
#else
            RtreeR2       tree;
#endif                                
            Object curr;
            Neighbor list;
            int counter;
            double x;
            double y; 
        }
        
#if USE_GENERICS
        public IEnumerable<T> Neighbors(double x, double y)
#else
        public IEnumerable Neighbors(double x, double y)
#endif
        {
            return new NeighborEnumerable(this, x, y);           
        }
    }
}
