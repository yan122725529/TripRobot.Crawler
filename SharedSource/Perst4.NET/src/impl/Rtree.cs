using System;
#if USE_GENERICS
using System.Collections.Generic;
#endif
using System.Collections;
using Perst;

namespace Perst.Impl
{
    [Serializable]	
#if USE_GENERICS
    internal class Rtree<T>:PersistentCollection<T>, SpatialIndex<T> where T:class
#else
    internal class Rtree:PersistentCollection, SpatialIndex
#endif
    {
        internal int         height;
        internal int         n;
        internal RtreePage   root;
        [NonSerialized()]
        private int         updateCounter;

        internal Rtree() {}

        public override int Count 
        { 
            get 
            {
                return n;
            }
        }

#if USE_GENERICS
        public void Put(Rectangle r, T obj) 
#else
        public void Put(Rectangle r, object obj) 
#endif
        {
            if (root == null) 
            { 
                root = new RtreePage(Storage, obj, r);
                height = 1;
            } 
            else 
            { 
                RtreePage p = root.insert(Storage, r, obj, height); 
                if (p != null) 
                {
                    root = new RtreePage(Storage, root, p);
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
        public void Remove(Rectangle r, T obj) 
#else
        public void Remove(Rectangle r, object obj) 
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
                RtreePage p = (RtreePage)reinsertList[i];
                for (int j = 0, pn = p.n; j < pn; j++) 
                { 
                    RtreePage q = root.insert(Storage, p.b[j], p.branch[j], height - reinsertLevel); 
                    if (q != null) 
                    { 
                        // root splitted
                        root = new RtreePage(Storage, root, q);
                        height += 1;
                    }
                }
                reinsertLevel -= 1;
                p.Deallocate();
            }
            if (root.n == 1 && height > 1) 
            { 
                RtreePage newRoot = (RtreePage)root.branch[0];
                root.Deallocate();
                root = newRoot;
                height -= 1;
            }
            n -= 1;
            updateCounter += 1;
            Modify();
        }
    
#if USE_GENERICS
        public T[] Get(Rectangle r) 
#else
        public object[] Get(Rectangle r) 
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

        public Rectangle WrappingRectangle
        {
            get 
            {
                return (root != null) 
                    ? root.cover()
                    : new Rectangle(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
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
        public IEnumerable<T> Overlaps(Rectangle r) 
#else
        public IEnumerable Overlaps(Rectangle r) 
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

        public IDictionaryEnumerator GetDictionaryEnumerator(Rectangle r) 
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
            internal RtreeEnumerable(Rtree<T> tree, Rectangle r) 
#else
        class RtreeEnumerable : IEnumerable
        {
            internal RtreeEnumerable(Rtree tree, Rectangle r) 
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
            Rtree<T>    tree;
#else
            Rtree       tree;
#endif
            Rectangle   r;
        }

#if USE_GENERICS
        class RtreeEnumerator : IEnumerator<T>, PersistentEnumerator
        {
            internal RtreeEnumerator(Rtree<T> tree, Rectangle r) 
#else
        class RtreeEnumerator : PersistentEnumerator
        {
            internal RtreeEnumerator(Rtree tree, Rectangle r) 
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
                pageStack = new RtreePage[height];
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

            private bool gotoFirstItem(int sp, RtreePage pg) 
            { 
                for (int i = 0, n = pg.n; i < n; i++) 
                { 
                    if (r.Intersects(pg.b[i])) 
                    { 
                        if (sp+1 == height || gotoFirstItem(sp+1, (RtreePage)pg.branch[i])) 
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
                RtreePage pg = pageStack[sp];
                for (int i = posStack[sp], n = pg.n; ++i < n;) 
                { 
                    if (r.Intersects(pg.b[i])) 
                    { 
                        if (sp+1 == height || gotoFirstItem(sp+1, (RtreePage)pg.branch[i])) 
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
              
 
            protected RtreePage[] pageStack;
            protected int[]       posStack;
            protected int         counter;
            protected int         height;
            protected int         pos;
            protected bool        hasNext;
            protected RtreePage   page;
#if USE_GENERICS
            protected Rtree<T>    tree;
#else
            protected Rtree       tree;
#endif
            protected Rectangle   r;
        }

        class RtreeEntryEnumerator : RtreeEnumerator, IDictionaryEnumerator 
        {
#if USE_GENERICS
            internal RtreeEntryEnumerator(Rtree<T> tree, Rectangle r) 
#else
            internal RtreeEntryEnumerator(Rtree tree, Rectangle r) 
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
            internal NeighborEnumerable(Rtree<T> tree, int x, int y) 
#else
        class NeighborEnumerable : IEnumerable
        {
            internal NeighborEnumerable(Rtree tree, int x, int y) 
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
            Rtree<T>    tree;
#else
            Rtree       tree;
#endif                                
            int x;
            int y; 
        }
 
#if USE_GENERICS
        class NeighborEnumerator : IEnumerator<T>, PersistentEnumerator
        {
            internal NeighborEnumerator(Rtree<T> tree, int x, int y) 
#else
        class NeighborEnumerator: PersistentEnumerator
        {
            internal NeighborEnumerator(Rtree tree, int x, int y) 
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
                    RtreePage pg = (RtreePage)neighbor.child;
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
            Rtree<T>    tree;
#else
            Rtree       tree;
#endif                                
            Object curr;
            Neighbor list;
            int counter;
            int x;
            int y; 
        }
        
#if USE_GENERICS
        public IEnumerable<T> Neighbors(int x, int y)
#else
        public IEnumerable Neighbors(int x, int y)
#endif
        {
            return new NeighborEnumerable(this, x, y);           
        }
    }   
}
