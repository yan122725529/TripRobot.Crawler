namespace Perst.Impl
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
    using System.Diagnostics;
    using Perst;

#if USE_GENERICS
    public class KDTree<T> : PersistentCollection<T>, MultidimensionalIndex<T> where T : class
    {
        internal KDTreeNode root;
        internal int nMembers;
        internal int height;
        internal MultidimensionalComparator<T> comparator;

        internal KDTree(Storage storage, MultidimensionalComparator<T> comparator)
            : base(storage)
        {
            this.comparator = comparator;
        }

        internal KDTree(Storage storage, string[] fieldNames, bool treateZeroAsUndefinedValue)
            : base(storage)
        {
            this.comparator = new ReflectionMultidimensionalComparator<T>(storage, fieldNames, treateZeroAsUndefinedValue);
        }

        internal KDTree() {}

        public MultidimensionalComparator<T> Comparator
        {
            get
            {
                return comparator;
            }
        }

        internal enum OpResult
        {
            OK,
            NOT_FOUND,
            TRUNCATE
        };

        internal class KDTreeNode : Persistent
        {
            internal KDTreeNode left;
            internal KDTreeNode right;
            internal T obj;
            internal bool deleted;

            internal KDTreeNode(Storage db, T obj) : base(db)
            {
                this.obj = obj;
            }

            internal KDTreeNode()
            {
            }

            public override void Load()
            {
                base.Load();
                Storage.Load(obj);
            }

            public override bool RecursiveLoading()
            {
                return false;
            }

            internal int Insert(T ins, MultidimensionalComparator<T> comparator, int level)
            {
                Load();
                CompareResult diff = comparator.Compare(ins, obj, level % comparator.NumberOfDimensions);
                if (diff == CompareResult.EQ && deleted)
                {
                    Storage.Deallocate(obj);
                    Modify();
                    obj = ins;
                    deleted = false;
                    return level;
                }
                else if (diff != CompareResult.GT)
                {
                    if (left == null)
                    {
                        Modify();
                        left = new KDTreeNode(Storage, ins);
                        return level+1;
                    }
                    else
                    {
                        return left.Insert(ins, comparator, level + 1);
                    }
                }
                else
                {
                    if (right == null)
                    {
                        Modify();
                        right = new KDTreeNode(Storage, ins);
                        return level+1;
                    }
                    else
                    {
                        return right.Insert(ins, comparator, level + 1);
                    }
                }
            }

            internal OpResult Remove(T rem, MultidimensionalComparator<T> comparator, int level)
            {
                Load();
                if (obj == rem)
                {
                    if (left == null && right == null)
                    {
                        Deallocate();
                        return OpResult.TRUNCATE;
                    }
                    else
                    {
                        Modify();
                        obj = comparator.CloneField(obj, level % comparator.NumberOfDimensions);
                        deleted = true;
                        return OpResult.OK;
                    }
                }
                CompareResult diff = comparator.Compare(rem, obj, level % comparator.NumberOfDimensions);
                if (diff != CompareResult.GT && left != null)
                {
                    OpResult result = left.Remove(rem, comparator, level + 1);
                    if (result == OpResult.TRUNCATE)
                    {
                        Modify();
                        left = null;
                        return OpResult.OK;
                    }
                    else if (result == OpResult.OK)
                    {
                        return OpResult.OK;
                    }
                }
                if (diff != CompareResult.LT && right != null)
                {
                    OpResult result = right.Remove(rem, comparator, level + 1);
                    if (result == OpResult.TRUNCATE)
                    {
                        Modify();
                        right = null;
                        return OpResult.OK;
                    }
                    else if (result == OpResult.OK)
                    {
                        return OpResult.OK;
                    }
                }
                return OpResult.NOT_FOUND;
            }

            public override void Deallocate()
            {
                Load();
                if (deleted)
                {
                    Storage.Deallocate(obj);
                }
                if (left != null)
                {
                    left.Deallocate();
                }
                if (right != null)
                {
                    right.Deallocate();
                }
                base.Deallocate();
            }
        }

        public void Optimize() 
        { 
            int n = nMembers;
            T[] members = new T[n];
            int i = 0;
            foreach (T obj in this) 
            { 
                members[i++] = obj;
            }
            Random rnd = new Random();
            for (i = 0; i < n; i++) 
            { 
                int j = rnd.Next(n);
                T tmp = members[j];
                members[j] = members[i];
                members[i] = tmp;
            }
            Clear();
            for (i = 0; i < n; i++) 
            { 
                Add(members[i]);
            }
        }           

        public override void Add(T obj)
        {
            Modify();
            if (root == null)
            {
                root = new KDTreeNode(Storage, obj);
                height = 1;
            }
            else
            {
                int level = root.Insert(obj, comparator, 0);
                if (level >= height) 
                { 
                    height = level+1;
                }
            }
            nMembers += 1;
        }

        public override bool Remove(T obj)
        {
            if (root == null)
            {
                return false;
            }
            OpResult result = root.Remove(obj, comparator, 0);
            if (result == OpResult.NOT_FOUND)
            {
                return false;
            }
            Modify();
            if (result == OpResult.TRUNCATE)
            {
                root = null;
            }
            nMembers -= 1;
            return true;
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return GetEnumerator(null, null);
        }

        public IEnumerator<T> GetEnumerator(T pattern)
        {
            return GetEnumerator(pattern, pattern);
        }

        public IEnumerator<T> GetEnumerator(T low, T high)
        {
            return new KDTreeEnumerator(this, low, high);
        }

        public IEnumerable<T> Range(T low, T high)
        {
            return new KDTreeEnumerable(this, low, high);
        }

        public T[] QueryByExample(T pattern)
        {
            return QueryByExample(pattern, pattern);
        }

        public T[] QueryByExample(T low, T high)
        {
            IEnumerator<T> i = GetEnumerator(low, high);
            List<T> list = new List<T>();
            while (i.MoveNext())
            {
                list.Add(i.Current);
            }
            return (T[])list.ToArray();
        }

        public override int Count
        {
            get
            {
                return nMembers;
            }
        }

        public int Height
        {
            get
            { 
                return height;
            }
        }

        public override void Clear()
        {
            if (root != null)
            {
                root.Deallocate();
                Modify();
                root = null;
                nMembers = 0;
                height = 0;
            }
        }
        public override bool Contains(T member)
        {
            IEnumerator<T> i = GetEnumerator(member);
            while (i.MoveNext())
            {
                if (i.Current == member)
                {
                    return true;
                }
            }
            return false;
        }

        public override void Deallocate()
        {
            if (root != null)
            {
                root.Deallocate();
            }
            base.Deallocate();
        }



        class KDTreeEnumerable : IEnumerable<T>, IEnumerable
        {
            internal KDTreeEnumerable(KDTree<T> tree, T low, T high)
            {
                this.tree = tree;
                this.low = low;
                this.high = high;
            }
            
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new KDTreeEnumerator(tree, low, high);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new KDTreeEnumerator(tree, low, high);
            }

            T high;
            T low;
            KDTree<T> tree;
        }

        class KDTreeEnumerator : IEnumerator<T>, PersistentEnumerator
        {
            KDTreeNode[] stack;
            int sp;
            int nDims;
            T high;
            T low;
            T curr;
            MultidimensionalComparator<T> comparator;
            KDTreeNode root;
            KDTree<T> tree;

            internal KDTreeEnumerator(KDTree<T> tree, T low, T high)
            {
                this.tree = tree;
                this.low = low;
                this.high = high;
                root = tree.root;
                comparator = tree.comparator;
                nDims = comparator.NumberOfDimensions;
                stack = new KDTreeNode[tree.height+1];
                Reset();
            }

            CompareResult compareAllComponents(T pattern, T obj)
            {
                int n = comparator.NumberOfDimensions;
                CompareResult result = CompareResult.EQ;
                for (int i = 0; i < n; i++)
                {
                    CompareResult diff = comparator.Compare(pattern, obj, i);
                    if (diff == CompareResult.RIGHT_UNDEFINED)
                    {
                        return diff;
                    }
                    else if (diff == CompareResult.LT)
                    {
                        if (result == CompareResult.GT)
                        {
                            return CompareResult.NE;
                        }
                        else
                        {
                            result = CompareResult.LT;
                        }
                    }
                    else if (diff == CompareResult.GT)
                    {
                        if (result == CompareResult.LT)
                        {
                            return CompareResult.NE;
                        }
                        else
                        {
                            result = CompareResult.GT;
                        }
                    }
                }
                return result;
            }

            private bool getMin(KDTreeNode node)
            {
                if (node != null)
                {
                    while (true)
                    {
                        node.Load();
                        stack[sp++] = node;
                        CompareResult diff = low == null
                            ? CompareResult.LEFT_UNDEFINED
                            : comparator.Compare(low, node.obj, (sp - 1) % nDims);
                        if (diff != CompareResult.GT && node.left != null)
                        {
                            node = node.left;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public virtual void Reset()
            {
                sp = 0;
                getMin(root);
            }

            public void Dispose()
            {
            }


            public bool MoveNext()
            {
                curr = null;
                while (sp != 0)
                {
                    KDTreeNode node = stack[--sp];
                    if (node != null)
                    {
                        if (!node.deleted)
                        {
                            CompareResult result;
                            if ((low == null
                                 || (result = compareAllComponents(low, node.obj)) == CompareResult.LT
                                 || result == CompareResult.EQ)
                                && (high == null
                                    || (result = compareAllComponents(high, node.obj)) == CompareResult.GT
                                    || result == CompareResult.EQ))
                            {
                                curr = node.obj;
                            }
                        }
                        if (node.right != null
                            && (high == null
                                || comparator.Compare(high, node.obj, sp % nDims) != CompareResult.LT))
                        {
                            stack[sp++] = null;
                            if (!getMin(node.right))
                            {
                                sp -= 1;
                            }
                        }
                        if (curr != null)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public T Current
            {
                get
                {
                    return curr;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return curr;
                }
            }


            public int CurrentOid
            {
                get
                {
                    return tree.Storage.GetOid(curr);
                }
            }
        }
    }
#else
    public class KDTree : PersistentCollection, MultidimensionalIndex
    {
        internal KDTreeNode root;
        internal int nMembers;
        internal int height;
        internal MultidimensionalComparator comparator;

        internal KDTree(Storage storage, MultidimensionalComparator comparator)
            : base(storage)
        {
            this.comparator = comparator;
        }

        internal KDTree(Storage storage, Type cls, string[] fieldNames, bool treateZeroAsNull)
            : base(storage)
        {
            this.comparator = new ReflectionMultidimensionalComparator(storage, cls, fieldNames, treateZeroAsNull);
        }

        public MultidimensionalComparator Comparator
        {
            get
            {
                return comparator;
            }
        }

        internal KDTree()
        {
        }

        internal enum OpResult
        {
            OK,
            NOT_FOUND,
            TRUNCATE
        };

        internal class KDTreeNode : Persistent
        {
            internal KDTreeNode left;
            internal KDTreeNode right;
            internal object obj;
            internal bool deleted;

            internal KDTreeNode(Storage db, object obj)
                : base(db)
            {
                this.obj = obj;
            }

            internal KDTreeNode()
            {
            }

            public override void Load()
            {
                base.Load();
                Storage.Load(obj);
            }

            public override bool RecursiveLoading()
            {
                return false;
            }

            internal int Insert(object ins, MultidimensionalComparator comparator, int level)
            {
                Load();
                CompareResult diff = comparator.Compare(ins, obj, level % comparator.NumberOfDimensions);
                if (diff == CompareResult.EQ && deleted)
                {
                    Storage.Deallocate(obj);
                    Modify();
                    obj = ins;
                    deleted = false;
                    return level;
                }
                else if (diff != CompareResult.GT)
                {
                    if (left == null)
                    {
                        Modify();
                        left = new KDTreeNode(Storage, ins);
                        return level+1;
                    }
                    else
                    {
                        return left.Insert(ins, comparator, level + 1);
                    }
                }
                else
                {
                    if (right == null)
                    {
                        Modify();
                        right = new KDTreeNode(Storage, ins);
                        return level+1;
                    }
                    else
                    {
                        return right.Insert(ins, comparator, level + 1);
                    }
                }
            }

            internal OpResult Remove(object rem, MultidimensionalComparator comparator, int level)
            {
                Load();
                if (obj == rem)
                {
                    if (left == null && right == null)
                    {
                        Deallocate();
                        return OpResult.TRUNCATE;
                    }
                    else
                    {
                        Modify();
                        obj = comparator.CloneField(obj, level % comparator.NumberOfDimensions);
                        deleted = true;
                        return OpResult.OK;
                    }
                }
                CompareResult diff = comparator.Compare(rem, obj, level % comparator.NumberOfDimensions);
                if (diff != CompareResult.GT && left != null)
                {
                    OpResult result = left.Remove(rem, comparator, level + 1);
                    if (result == OpResult.TRUNCATE)
                    {
                        Modify();
                        left = null;
                        return OpResult.OK;
                    }
                    else if (result == OpResult.OK)
                    {
                        return OpResult.OK;
                    }
                }
                if (diff != CompareResult.LT && right != null)
                {
                    OpResult result = right.Remove(rem, comparator, level + 1);
                    if (result == OpResult.TRUNCATE)
                    {
                        Modify();
                        right = null;
                        return OpResult.OK;
                    }
                    else if (result == OpResult.OK)
                    {
                        return OpResult.OK;
                    }
                }
                return OpResult.NOT_FOUND;
            }

            public override void Deallocate()
            {
                Load();
                if (deleted)
                {
                    Storage.Deallocate(obj);
                }
                if (left != null)
                {
                    left.Deallocate();
                }
                if (right != null)
                {
                    right.Deallocate();
                }
                base.Deallocate();
            }
        }

        public void Optimize() 
        { 
            int n = nMembers;
            object[] members = new object[n];
            int i = 0;
            foreach (object obj in this) 
            { 
                members[i++] = obj;
            }
            Random rnd = new Random();
            for (i = 0; i < n; i++) 
            { 
                int j = rnd.Next(n);
                object tmp = members[j];
                members[j] = members[i];
                members[i] = tmp;
            }
            Clear();
            for (i = 0; i < n; i++) 
            { 
                Add(members[i]);
            }
        }           

 
        public void Add(object obj)
        {
            Modify();
            if (root == null)
            {
                root = new KDTreeNode(Storage, obj);
                height = 1;
            }
            else
            {
                int level = root.Insert(obj, comparator, 0);
                if (level > height) 
                { 
                    height = level;
                }
            }
            nMembers += 1;
        }

        public bool Remove(object obj)
        {
            if (root == null)
            {
                return false;
            }
            OpResult result = root.Remove(obj, comparator, 0);
            if (result == OpResult.NOT_FOUND)
            {
                return false;
            }
            Modify();
            if (result == OpResult.TRUNCATE)
            {
                root = null;
            }
            nMembers -= 1;
            return true;
        }

        public override IEnumerator GetEnumerator()
        {
            return GetEnumerator(null, null);
        }

        public IEnumerator GetEnumerator(object pattern)
        {
            return GetEnumerator(pattern, pattern);
        }

        public IEnumerator GetEnumerator(object low, object high)
        {
            return new KDTreeEnumerator(this, low, high);
        }

        public IEnumerable Range(object low, object high)
        {
            return new KDTreeEnumerable(this, low, high);
        }

        public object[] QueryByExample(object pattern)
        {
            return QueryByExample(pattern, pattern);
        }

        public object[] QueryByExample(object low, object high)
        {
            IEnumerator i = GetEnumerator(low, high);
            ArrayList list = new ArrayList();
            while (i.MoveNext())
            {
                list.Add(i.Current);
            }
            return list.ToArray();
        }

        public override int Count
        {
            get
            {
                return nMembers;
            }
        }

        public int Height
        {
            get
            { 
                return height;
            }
        }

        public override void Clear()
        {
            if (root != null)
            {
                root.Deallocate();
                Modify();
                root = null;
                nMembers = 0;
                height = 0;
            }
        }
        public bool Contains(object member)
        {
            IEnumerator i = GetEnumerator(member);
            while (i.MoveNext())
            {
                if (i.Current == member)
                {
                    return true;
                }
            }
            return false;
        }

        public override void Deallocate()
        {
            if (root != null)
            {
                root.Deallocate();
            }
            base.Deallocate();
        }

        class KDTreeEnumerable : IEnumerable
        {
            internal KDTreeEnumerable(KDTree tree, object low, object high)
            {
                this.low = low;
                this.high = high;
                this.tree = tree;
            }      

            public IEnumerator GetEnumerator()
            {
                return new KDTreeEnumerator(tree, low, high);
            }

            Object high;
            Object low;
            KDTree tree;
        }

        class KDTreeEnumerator : PersistentEnumerator
        {
            KDTreeNode[] stack;
            int sp;
            int nDims;
            Object high;
            Object low;
            object curr;
            MultidimensionalComparator comparator;
            KDTreeNode root;
            KDTree tree;

            CompareResult compareAllComponents(Object pattern, object obj)
            {
                int n = comparator.NumberOfDimensions;
                CompareResult result = CompareResult.EQ;
                for (int i = 0; i < n; i++)
                {
                    CompareResult diff = comparator.Compare(pattern, obj, i);
                    if (diff == CompareResult.RIGHT_UNDEFINED)
                    {
                        return diff;
                    }
                    else if (diff == CompareResult.LT)
                    {
                        if (result == CompareResult.GT)
                        {
                            return CompareResult.NE;
                        }
                        else
                        {
                            result = CompareResult.LT;
                        }
                    }
                    else if (diff == CompareResult.GT)
                    {
                        if (result == CompareResult.LT)
                        {
                            return CompareResult.NE;
                        }
                        else
                        {
                            result = CompareResult.GT;
                        }
                    }
                }
                return result;
            }

            internal KDTreeEnumerator(KDTree tree, object low, object high)
            {
                this.low = low;
                this.high = high;
                root = tree.root;
                this.tree = tree;
                comparator = tree.comparator;
                nDims = comparator.NumberOfDimensions;
                stack = new KDTreeNode[tree.height+1];
                Reset();
            }

            private bool getMin(KDTreeNode node)
            {
                if (node != null)
                {
                    while (true)
                    {
                        node.Load();
                        stack[sp++] = node;
                        CompareResult diff = low == null
                            ? CompareResult.LEFT_UNDEFINED
                            : comparator.Compare(low, node.obj, (sp - 1) % nDims);
                        if (diff != CompareResult.GT && node.left != null)
                        {
                            node = node.left;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public virtual void Reset()
            {
                sp = 0;
                getMin(root);
            }

            public void Dispose()
            {
            }


            public bool MoveNext()
            {
                curr = null;
                while (sp != 0)
                {
                    KDTreeNode node = stack[--sp];
                    if (node != null)
                    {
                        if (!node.deleted)
                        {
                            CompareResult result;
                            if ((low == null
                                 || (result = compareAllComponents(low, node.obj)) == CompareResult.LT
                                 || result == CompareResult.EQ)
                                && (high == null
                                    || (result = compareAllComponents(high, node.obj)) == CompareResult.GT
                                    || result == CompareResult.EQ))
                            {
                                curr = node.obj;
                            }
                        }
                        if (node.right != null
                            && (high == null
                                || comparator.Compare(high, node.obj, sp % nDims) != CompareResult.LT))
                        {
                            stack[sp++] = null;
                            if (!getMin(node.right))
                            {
                                sp -= 1;
                            }
                        }
                        if (curr != null)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public object Current
            {
                get
                {
                    return curr;
                }
            }

            public int CurrentOid
            {
                get
                {
                    return tree.Storage.GetOid(curr);
                }
            }
        }
    }
#endif

}