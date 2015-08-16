namespace Perst.Assoc
{
    using System;
    using Perst;
    using Perst.FullText;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Read-only transaction.
    /// AssocDB provides MURSIW (multiple readers single writer) isolation model.
    /// It means that only one transaction can update the database at each moment of time, but multiple transactions
    /// can concurrently read it.
    ///
    /// All access to the database (read or write) should be performed within transaction body.
    /// For write access it is enforced by placing all update methods in ReadWriteTransaction class.
    /// But for convenience reasons read-only access methods are left in Item class. It is responsibility of programmer to check that 
    /// them are not invoked outside transaction body.
    /// 
    /// Transaction should be explicitly started by correspondent method of AssocDB and then it has to be either committed, 
    /// either aborted. In any case, it can not be used any more after commit or rollback - you should start another transaction.
    /// </summary>
    public class ReadOnlyTransaction
    {
        /// <summary> 
        /// Locate items matching specified search criteria
        /// </summary>
        /// <param name="predicate">search condition</param>
        /// <returns>iterator through selected results (it also implemented Iterable interface to allow
        /// to use it in <code>for (T x:collection)</code> statement. But you should not use this iterable more than once.
        /// Also IterableIterator class provides some convenient methods for fetching first result, counting results and 
        /// extraction them to array</returns>
        public IEnumerable<Item> Find(Predicate predicate)
        {
            checkIfActive();
            return evaluate(predicate, false);
        }

        /// <summary> 
        /// Locate items matching specified search criteria and sort them in desired order
        /// </summary>
        /// <param name="predicate">search condition</param>
        /// <param name="order">sort criteria</param>
        /// <returns>array in specified sort order</returns>
        public Item[] Find(Predicate predicate, params OrderBy[] order)
        {
            Item[] items = Enumerable.ToArray(Find(predicate));
            Array.Sort(items, 0, items.Length, new ItemComparator(db, order));
            return items;
        }

        /// <summary>
        /// Get all items containing specified attribute in ascending order
        /// </summary>
        /// <param name="name">attribute name</param>
        /// <returns>iterator through all items containing specified attribute or null if there is not such attribute in the database</returns>
        public IEnumerable<Item> GetOccurrences(String name)
        {
            return GetOccurrences(name, IterationOrder.AscentOrder);
        }

        /// <summary>
        /// Get all items containing specified attribute in desired order
        /// </summary>
        /// <param name="name">attribute name</param>
        /// <param name="order">ascending or descending sort order</param>
        /// <returns>iterator through all items containing specified attribute or null if there is not such attribute in the database</returns>
        public IEnumerable<Item> GetOccurrences(String name, IterationOrder order)
        {
            checkIfActive();
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return null;
            }
            Index index = (Index)db.storage.GetObjectByOID(id);
            return new Item.ItemEnumerable(index.Range(null, null, order));
        }

        /// <summary>
        /// Parse and execute full text search query
        /// </summary>
        /// <param name="query">text of the query</param>
        /// <param name="maxResults">maximal amount of selected documents</param>
        /// <param name="timeLimit">limit for query execution time</param>
        /// <returns>result of query execution ordered by rank or null in case of empty or incorrect query</returns>
        public FullTextSearchResult FullTextSearch(String query, int maxResults, int timeLimit)
        {
            checkIfActive();
            return db.root.fullTextIndex.Search(query, db.language, maxResults, timeLimit);
        }

        /// <summary>
        /// Locate all documents containing words started with specified prefix
        /// </summary>
        /// <param name="prefix">word prefix</param>
        /// <param name="maxResults">maximal amount of selected documents</param>
        /// <param name="timeLimit">limit for query execution time</param>
        /// <param name="sort">whether it is necessary to sort result by rank</param>
        /// <returns>result of query execution ordered by rank (if sort==true) or null in case of empty or incorrect query</returns>
        public FullTextSearchResult FullTextSearchPrefix(String prefix, int maxResults, int timeLimit, bool sort)
        {
            checkIfActive();
            return db.root.fullTextIndex.SearchPrefix(prefix, maxResults, timeLimit, sort);
        }

        /// <summary>
        /// Get iterator through full text index keywords started with specified prefix
        /// </summary>
        /// <param name="prefix">keyword prefix (use empty string to get list of all keywords)</param>
        /// <returns>iterator through list of all keywords with specified prefix</returns>
        public IEnumerable<Keyword> GetFullTextSearchKeywords(String prefix)
        {
            checkIfActive();
            return db.root.fullTextIndex.GetKeywords(prefix);
        }

        /// <summary>
        /// Get set of all verbs (attribute names) in the database
        /// </summary>
        /// <returns>unordered set of of verbs in the database</returns>
        public ICollection<String> Verbs
        {
            get
        {
            checkIfActive();
            return db.name2id.Keys;
        }
        }

        /// <summary>
        /// Get attribute type. AssocDB requires that values of attribute in all objects have the same type.
        /// You can not store for example "age" in  one item as number 35 and in other item - as string "5 months".
        /// If it is really needed you have to introduce new attribute.
        /// </summary>
        /// <param name="name">attribute name</param>
        /// <returns>class of the attribute value (String, double or Item) or null if attribute with such name is not found in the database</returns>
        public Type GetAttributeType(String name)
        {
            checkIfActive();
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return null;
            }
            Index index = (Index)db.storage.GetObjectByOID(id);
            Type cls = index.KeyType;
            if (cls == typeof(Object))
            {
                cls = typeof(Item);
            }
            return cls;
        }

        /// <summary>
        /// Commit this transaction.
        /// It is not possible to use this transaction object after it is committed
        /// </summary>
        public virtual void Commit()
        {
            checkIfActive();
            db.Unlock();
            active = false;
        }

        /// <summary>
        /// Rollback this transaction (for read-only transaction is has the same effect as commit).
        /// It is not possible to use this transaction object after it is rollbacked
        /// </summary>
        public virtual void Rollback()
        {
            checkIfActive();
            db.Unlock();
            active = false;
        }

        protected void checkIfActive()
        {
            if (!active)
            {
                throw new InvalidOperationException("Transaction is not active");
            }
        }

        class ItemComparator : IComparer<Item>
        {
            public ItemComparator(AssocDB db, OrderBy[] orderBy)
            {
                ids = new int[orderBy.Length];
                for (int i = 0; i < orderBy.Length; i++)
                {
                    ids[i] = db.name2id[orderBy[i].name];
                }
                this.orderBy = orderBy;
            }

            public int Compare(Item item1, Item item2)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    int id = ids[i];
                    IComparable o1 = (IComparable)item1.GetAttribute(id);
                    IComparable o2 = (IComparable)item2.GetAttribute(id);
                    int diff = o1.CompareTo(o2);
                    if (diff != 0)
                    {
                        return orderBy[i].order == IterationOrder.AscentOrder ? diff : -diff;
                    }
                }
                return 0;
            }

            OrderBy[] orderBy;
            int[] ids;
        }

        protected Key createKey(Object value, bool inclusive)
        {
            return (value is String)
                ? new Key(((String)value).ToLower(), inclusive)
                : (value is IConvertible)
                    ? new Key(((IConvertible)value).ToDouble(null), inclusive)
                    : new Key((Item)value, inclusive);
        }

        protected IEnumerable<Item> getEmptyResultSet()
        {
            return new ArrayEnumerable(new Item[0]);
        }

        protected IEnumerable<Item> evaluate(Predicate predicate, bool sortNeeded) 
    {
        IEnumerable<Item> iterator = null;
        if (predicate is Predicate.Compare) { 
            Predicate.Compare cmp = (Predicate.Compare)predicate;
            int id;
            if (!db.name2id.TryGetValue(cmp.name, out id)) {
                return getEmptyResultSet();
            }
            Index index = (Index)db.storage.GetObjectByOID(id);
            Object value = cmp.value;
            switch (cmp.oper) { 
            case Predicate.Compare.Operation.Equals:
            {
                Key key = createKey(value, true);
                return new Item.ItemEnumerable(index.Range(key, key));
            }
            case Predicate.Compare.Operation.LessThan:
                return new Item.ItemEnumerable(index.Range(null, createKey(value, false)));
            case Predicate.Compare.Operation.LessOrEquals:
                iterator = new Item.ItemEnumerable(index.Range(null, createKey(value, true)));
                break;
            case Predicate.Compare.Operation.GreaterThan:
                iterator = new Item.ItemEnumerable(index.Range(createKey(value, false), null));
                break;
            case Predicate.Compare.Operation.GreaterOrEquals:
                iterator = new Item.ItemEnumerable(index.Range(createKey(value, true), null));
                break;
            case Predicate.Compare.Operation.StartsWith:
                iterator = new Item.ItemEnumerable(index.StartsWith(((String)value).ToLower()));
                break;
            case Predicate.Compare.Operation.IsPrefixOf:
                iterator = new ArrayEnumerable(index.PrefixSearch(((String)value).ToLower()));
                break;
            case Predicate.Compare.Operation.InArray:
            {
                OidEnumerable dst = new OidEnumerable(db);
                if (value is String[]) { 
                    String[] arr = (String[])value;
                    for (int i = 0; i < arr.Length; i++) { 
                        Key key = new Key(arr[i].ToLower());
                        PersistentEnumerator src = (PersistentEnumerator)index.GetEnumerator(key, key);
                        while (src.MoveNext()) { 
                            dst.add(src.CurrentOid);
                        }
                    }
                } else if (value is double[]) { 
                    double[] arr = (double[])value;
                    for (int i = 0; i < arr.Length; i++) { 
                        Key key = new Key(arr[i]);
                        PersistentEnumerator src = (PersistentEnumerator)index.GetEnumerator(key, key);
                        while (src.MoveNext()) { 
                            dst.add(src.CurrentOid);
                        }
                    }
                } else { 
                    Item[] arr = (Item[])value;
                    for (int i = 0; i < arr.Length; i++) { 
                        Key key = new Key(arr[i]);
                        PersistentEnumerator src = (PersistentEnumerator)index.GetEnumerator(key, key);
                        while (src.MoveNext()) { 
                            dst.add(src.CurrentOid);
                        }
                    }
                }
                dst.uniq();
                return dst;
            }
            }
        } else if (predicate is Predicate.And) { 
            Predicate.And and = (Predicate.And)predicate;
            return new JoinEnumerable(db, evaluate(and.left, true), evaluate(and.right, true));
        } else if (predicate is Predicate.Or) { 
            Predicate.Or or = (Predicate.Or)predicate;
            return new MergeEnumerable(db, evaluate(or.left, true), evaluate(or.right, true));
        } else if (predicate is Predicate.Between) { 
            Predicate.Between between = (Predicate.Between)predicate;
            int id;
            if (!db.name2id.TryGetValue(between.name, out id)) {
                return getEmptyResultSet();
            }
            Index index = (Index)db.storage.GetObjectByOID(id);
            iterator = new Item.ItemEnumerable(index.Range(createKey(between.from, true), createKey(between.till, true)));
        } else if (predicate is Predicate.Match) { 
            Predicate.Match match = (Predicate.Match)predicate;
            iterator = new FullTextSearchResultEnumerable(db.root.fullTextIndex.Search(match.query, db.language, match.maxResults, match.timeLimit));
        } else if (predicate is Predicate.In) { 
            Predicate.In isIn = (Predicate.In)predicate;
            int id;
            if (!db.name2id.TryGetValue(isIn.name, out id)) {
                return getEmptyResultSet();
            }
            Index index = (Index)db.storage.GetObjectByOID(id);
            OidEnumerable dst = new OidEnumerable(db);
            foreach (Item item in evaluate(isIn.subquery, false)) {
                Key key = new Key(item);
                PersistentEnumerator src = (PersistentEnumerator)index.GetEnumerator(key, key);
                while (src.MoveNext()) { 
                    dst.add(src.CurrentOid);
                }
            }
            dst.uniq();            
            return dst;
        } else { 
            return null;
        }
        return sortNeeded ? sortResult(iterator) : iterator;
    }

        protected virtual IEnumerable<Item> sortResult(IEnumerable<Item> iterable)
        {
            PersistentEnumerator src = (PersistentEnumerator)iterable.GetEnumerator();
            OidEnumerable dst = new OidEnumerable(db);
            while (src.MoveNext())
            {
                dst.add(src.CurrentOid);
            }
            dst.sort();
            return dst;
        }

        class OidEnumerable : IEnumerable<Item>
        {
            public IEnumerator<Item> GetEnumerator()
            {
                return new OidEnumerator(db, oids, n);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new OidEnumerator(db, oids, n);
            }

            AssocDB db;
            int[] oids;
            int n;

            public OidEnumerable(AssocDB db)
            {
                this.db = db;
                oids = new int[1024];
            }

            internal void add(int oid)
            {
                if (n == oids.Length)
                {
                    int[] newOids = new int[n * 2];
                    Array.Copy(oids, 0, newOids, 0, n);
                    oids = newOids;
                }
                oids[n++] = oid;
            }

            internal void sort()
            {
                Array.Sort(oids, 0, n);
            }

            internal void uniq()
            {
                if (n > 1)
                {
                    sort();
                    int i = 0;
                    int[] buf = oids;
                    for (int j = 1, k = n; j < k; j++)
                    {
                        if (buf[j] != buf[i])
                        {
                            buf[++i] = buf[j];
                        }
                    }
                    n = i + 1;
                }
            }
        }

        class OidEnumerator : IEnumerator<Item>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public OidEnumerator(AssocDB db, int[] oids, int n)
            {
                this.oids = oids;
                this.n = n;
                this.db = db;
                Reset();
            }

            public void Reset()
            {
                i = -1;
            }

            public bool MoveNext()
            {
                return ++i < n;
            }

            public int CurrentOid
            {
                get
                {
                    return oids[i];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (i < 0 || i >= oids.Length)
                    {
                        throw new InvalidOperationException();
                    }
                    return db.storage.GetObjectByOID(oids[i]);
                }
            }

            public Item Current
            {
                get
                {
                    return (Item)((IEnumerator)this).Current;
                }
            }

            AssocDB db;
            int[] oids;
            int i, n;
        }

        class ArrayEnumerable : IEnumerable<Item>
        {
            public ArrayEnumerable(object[] arr)
            {
                this.arr = arr;
            }

            public IEnumerator<Item> GetEnumerator()
            {
                return new ArrayEnumerator(arr);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ArrayEnumerator(arr);
            }

            object[] arr;
        }

        class ArrayEnumerator : IEnumerator<Item>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public ArrayEnumerator(object[] arr)
            {
                this.arr = arr;
                i = -1;
            }

            public void Reset()
            {
                i = -1;
            }

            public bool MoveNext()
            {
                return ++i < arr.Length;
            }

            public int CurrentOid
            {
                get
                {
                    return ((IPersistent)arr[i]).Oid;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (i < 0 || i >= arr.Length)
                    {
                        throw new InvalidOperationException();
                    }
                    return arr[i];
                }
            }

            public Item Current
            {
                get
                {
                    return (Item)((IEnumerator)this).Current;
                }
            }

            object[] arr;
            int i;
        }

        class FullTextSearchResultEnumerable : IEnumerable<Item>
        {
            public FullTextSearchResultEnumerable(FullTextSearchResult result)
            {
                this.result = result;
            }

            public IEnumerator<Item> GetEnumerator()
            {
                return new FullTextSearchResultEnumerator(result);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new FullTextSearchResultEnumerator(result);
            }

            FullTextSearchResult result;
        }

        class FullTextSearchResultEnumerator : IEnumerator<Item>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public FullTextSearchResultEnumerator(FullTextSearchResult result)
            {
                this.result = result;
            }

            public void Reset()
            {
                i = -1;
            }

            public bool MoveNext()
            {
                return ++i < result.Hits.Length;
            }

            public int CurrentOid
            {
                get
                {
                    return ((IPersistent)result.Hits[i]).Oid;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (i < 0 || i >= result.Hits.Length)
                    {
                        throw new InvalidOperationException();
                    }
                    return (Item)result.Hits[i].Document;
                }
            }

            public Item Current
            {
                get
                {
                    return (Item)((IEnumerator)this).Current;
                }
            }

            FullTextSearchResult result;
            int i;
        }

        class JoinEnumerable : IEnumerable<Item>
        {
            public JoinEnumerable(AssocDB db, IEnumerable<Item> e1, IEnumerable<Item> e2)
            {
                this.db = db;
                this.e1 = e1;
                this.e2 = e2;
            }

            public IEnumerator<Item> GetEnumerator()
            {
                return new JoinEnumerator(db, e1.GetEnumerator(), e2.GetEnumerator());
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new JoinEnumerator(db ,e1.GetEnumerator(), e2.GetEnumerator());
            }

            IEnumerable<Item> e1;
            IEnumerable<Item> e2;
            AssocDB db;
        }

        class JoinEnumerator : IEnumerator<Item>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public JoinEnumerator(AssocDB db, IEnumerator<Item> e1, IEnumerator<Item> e2)
            {
                this.db = db;
                this.e1 = (PersistentEnumerator)e1;
                this.e2 = (PersistentEnumerator)e2;
            }

            public void Reset()
            {
                e1.Reset();
                e2.Reset();
            }

            public bool MoveNext()
            {
                int oid2 = 0;
                while (e1.MoveNext())
                {
                    int oid1 = e1.CurrentOid;
                    while (oid2 < oid1)
                    {
                        if (!e2.MoveNext())
                        {
                            oid = 0;
                            return false;
                        }
                        oid2 = e2.CurrentOid;
                    }
                    if (oid2 == oid1)
                    {
                        oid = oid1;
                        return true;
                    }
                }
                oid = 0;
                return false;
            }

            public int CurrentOid
            {
                get
                {
                    return oid;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (oid == 0)
                    {
                        throw new InvalidOperationException();
                    }
                    return db.storage.GetObjectByOID(oid);
                }
            }

            public Item Current
            {
                get
                {
                    return (Item)((IEnumerator)this).Current;
                }
            }

            int oid;
            AssocDB db;
            PersistentEnumerator e1, e2;
        }

        class MergeEnumerable : IEnumerable<Item>
        {
            public MergeEnumerable(AssocDB db, IEnumerable<Item> e1, IEnumerable<Item> e2)
            {
                this.db = db;
                this.e1 = e1;
                this.e2 = e2;
            }

            public IEnumerator<Item> GetEnumerator()
            {
                return new MergeEnumerator(db, e1.GetEnumerator(), e2.GetEnumerator());
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new MergeEnumerator(db, e1.GetEnumerator(), e2.GetEnumerator());
            }

            AssocDB db;
            IEnumerable<Item> e1;
            IEnumerable<Item> e2;
        }


        class MergeEnumerator : IEnumerator<Item>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public MergeEnumerator(AssocDB db, IEnumerator<Item> e1, IEnumerator<Item> e2)
            {
                this.db = db;
                this.e1 = (PersistentEnumerator)e1;
                this.e2 = (PersistentEnumerator)e2;
                Reset();
            }

            public void Reset()
            {
                e1.Reset();
                e2.Reset();
                oid1 = e1.MoveNext() ? e1.CurrentOid : 0;
                oid2 = e2.MoveNext() ? e2.CurrentOid : 0;
                oid = 0;
            }

            public bool MoveNext()
            {
                oid = 0;
                if (oid1 < oid2)
                {
                    if (oid1 == 0)
                    {
                        oid = oid2;
                        oid2 = e2.MoveNext() ? e2.CurrentOid : 0;
                    }
                    else
                    {
                        oid = oid1;
                        oid1 = e1.MoveNext() ? e1.CurrentOid : 0;
                    }
                }
                else if (oid2 != 0)
                {
                    oid = oid2;
                    oid2 = e2.MoveNext() ? e2.CurrentOid : 0;
                    if (oid1 == oid)
                    {
                        oid1 = e1.MoveNext() ? e1.CurrentOid : 0;
                    }
                }
                else
                {
                    return false;
                }
                return true;
            }

            public int CurrentOid
            {
                get
                {
                    return oid;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (oid == 0)
                    {
                        throw new InvalidOperationException();
                    }
                    return db.storage.GetObjectByOID(oid);
                }
            }

            public Item Current
            {
                get
                {
                    return (Item)((IEnumerator)this).Current;
                }
            }

            AssocDB db;
            int oid1, oid2, oid;
            PersistentEnumerator e1, e2;
        }

        internal protected AssocDB db;
        internal protected bool active;

        internal protected ReadOnlyTransaction(AssocDB db)
        {
            this.db = db;
            active = true;
        }
    }

}