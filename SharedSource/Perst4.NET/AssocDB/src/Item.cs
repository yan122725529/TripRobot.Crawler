namespace Perst.Assoc
{
    using System;
    using Perst;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// AssocDB item or object. Item is collection of some attributes (numeric or string)
    /// and it is part of some relations (one-to-one, one-to-many, many-to-many). 
    /// Each attribute has name (string). It is possible to associate with item  several values with the same name - 
    /// something like array. Newly added values will be appended to the end of "array".
    /// The same is true for link (relations) except that AssocDB can make a decision not to embed relation inside object and store it 
    /// externally.
    /// Item has no predefined attributes - you are free to add any attribute you like. For example if you need to maintain class
    /// object objects, you can add "class" attribute.
    /// Attribute names are not stored inside object - just their identifiers (integers). AssocDB class is responsible to
    /// map IDs to names and visa versa.
    /// </summary>
    public class Item : Persistent, IComparable, IComparable<Item>
    {
        internal protected int[] fieldIds;
        internal protected String[] stringFields;
        internal protected double[] numericFields;
        internal protected Link relations;

        [NonSerialized()]
        internal protected AssocDB db;
        [NonSerialized()]
        internal protected String[] fieldNames;

        public int CompareTo(object item)
        {
            return CompareTo((Item)item);
        }

        public int CompareTo(Item item)
        {
            int oid1 = Oid;
            int oid2 = item.Oid;
            return oid1 < oid2 ? -1 : oid1 == oid2 ? 0 : 1;
        }

        /// <summary>
        /// Get list of all item links names
        /// </summary>
        /// <returns>array of unique links in alphabet order</returns>
        public String[] AttributeNames
        {
            get
            {
                if (fieldNames == null)
                {
                    int n = fieldIds.Length;
                    int id = 0;
                    List<String> list = new List<String>(n);
                    for (int i = 0; i < n; i++)
                    {
                        if (fieldIds[i] != id)
                        {
                            id = fieldIds[i];
                            list.Add(db.id2name[id]);
                        }
                    }
                    if (relations == null)
                    {
                        long oid = Oid;
                        IDictionaryEnumerator e = db.root.relations.GetDictionaryEnumerator(new Key(oid << 32, true), new Key((oid + 1) << 32, false), IterationOrder.AscentOrder);
                        while (e.MoveNext())
                        {
                            int nextId = (int)(long)e.Key;
                            if (nextId != id)
                            {
                                id = nextId;
                                list.Add(db.id2name[id]);
                            }
                        }
                    }
                    fieldNames = list.ToArray();
                    Array.Sort(fieldNames);
                }
                return fieldNames;
            }
        }

        /// <summary>
        /// Get value of attribute
        /// </summary> 
        /// <param name="name">attribute name</param>
        /// <returns>one of
        /// <ul>
        /// <li>object (String, Double or Item) if there is only one values associated with this name</li>
        /// <li>array of String, double at Item if there are more than one values associated with this name</li>
        /// <li>null if this item has not attribute with specified name</li>
        /// </ul>
        /// </returns>
        public Object GetAttribute(String name)
        {
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return null;
            }
            return GetAttribute(id);
        }

        internal protected Object GetAttribute(int id)
        {
            int l = 0, n = stringFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            if (r == n || fieldIds[r] != id)
            {
                l = n;
                n += numericFields.Length;
                r = n;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (fieldIds[m] < id)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                if (r == n || fieldIds[r] != id)
                {
                    return GetRelation(id);
                }
            }
            for (r = l + 1; r < n && fieldIds[r] == id; r++) ;
            if (r > l + 1)
            {
                if (l < stringFields.Length)
                {
                    String[] arr = new String[r - l];
                    Array.Copy(stringFields, l, arr, 0, r - l);
                    return arr;
                }
                else
                {
                    double[] arr = new double[r - l];
                    Array.Copy(numericFields, l - stringFields.Length, arr, 0, r - l);
                    return arr;
                }
            }
            else
            {
                return (l < stringFields.Length) ? (Object)stringFields[l] : (Object)numericFields[l - stringFields.Length];
            }
        }

        /// <summary>
        /// Returns value of string attribute
        /// </summary>
        /// <param name="name">attribute name</param>
        /// <returns>String value of attribute or null if the item has no attribute with such name.
        /// If there are several values associated with this name, then first of them is returned></returns>
        public String GetString(String name)
        {
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return null;
            }
            int l = 0, n = stringFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            return (l == n || fieldIds[l] != id) ? null : stringFields[l];
        }

        /// <summary>
        /// Returns value of numeric attribute
        /// </summary>
        /// <param name="name">attribute name</param>
        /// <returns>double value of attribute or null if the item has no attribute with such name.
        /// If there are several values associated with this name, then first of them is returned</returns>
        public double? GetNumber(String name) 
    { 
        int id;
        if (!db.name2id.TryGetValue(name, out id)) { 
            return null;
        }
        int nStrings = stringFields.Length;
        int l = nStrings, n = l + numericFields.Length, r = n;
        while (l < r) { 
            int m = (l + r) >> 1;
            if (fieldIds[m] < id) { 
                l = m + 1;
            } else { 
                r = m;
            }
        }
        if (l == n || fieldIds[l] != id) {
            return null;
        }
        return numericFields[l - nStrings];
    }

        /// <summary>
        /// Get list of all item's attribute values (not including relation links)
        /// </summary>
        /// <returns>array of name-value pairs associated with with item</returns>
        public Pair[] Attributes
        {
            get
            {
                Pair[] pairs = new Pair[stringFields.Length + numericFields.Length];
                int i = 0, j;
                for (j = 0; j < stringFields.Length; i++, j++)
                {
                    pairs[i] = new Pair(db.id2name[fieldIds[i]], stringFields[j]);
                }
                for (j = 0; j < numericFields.Length; i++, j++)
                {
                    pairs[i] = new Pair(db.id2name[fieldIds[i]], numericFields[j]);
                }
                return pairs;
            }
        }

        class SmallRelationEnumerable : IEnumerable<Item>
        {
            public IEnumerator<Item> GetEnumerator()
            {
                return new SmallRelationEnumerator(item, from, till);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new SmallRelationEnumerator(item, from, till);
            }

            Item item;
            int from;
            int till;

            public SmallRelationEnumerable(Item item, int from, int till)
            {
                this.item = item;
                this.from = from;
                this.till = till;
            }
        }

        class SmallRelationEnumerator : IEnumerator<Item>, PersistentEnumerator
        {
            public bool MoveNext()
            {
                return ++i < r;
            }

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (i < l || i >= r)
                    {
                        throw new InvalidOperationException();
                    }
                    return item.relations[i];
                }
            }

            public Item Current
            {
                get
                {
                    return (Item)((IEnumerator)this).Current;
                }
            }

            public int CurrentOid
            {
                get
                {
                    return ((IPersistent)item.relations.GetRaw(i)).Oid;
                }
            }

            public void Reset()
            {
                i = l - 1;
            }

            public SmallRelationEnumerator(Item item, int from, int till)
            {
                this.item = item;
                l = from;
                r = till;
                Reset();
            }

            Item item;
            int i, l, r;
        }

        class SmallRelationPairEnumerable : IEnumerable<Pair>
        {
            public IEnumerator<Pair> GetEnumerator()
            {
                return new SmallRelationPairEnumerator(item);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new SmallRelationPairEnumerator(item);
            }

            Item item;

            public SmallRelationPairEnumerable(Item item)
            {
                this.item = item;
            }
        }

        class SmallRelationPairEnumerator : IEnumerator<Pair>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                return ++i < item.relations.Length;
            }

            object IEnumerator.Current
            {
                get
                {
                    if (i < 0 || i >= item.relations.Length)
                    {
                        throw new InvalidOperationException();
                    }
                    return new Pair(item.db.id2name[item.fieldIds[item.stringFields.Length + item.numericFields.Length]], item.relations[i]);
                }
            }

            public Pair Current
            {
                get
                {
                    if (i < 0 || i >= item.relations.Length)
                    {
                        throw new InvalidOperationException();
                    }
                    return new Pair(item.db.id2name[item.fieldIds[item.stringFields.Length + item.numericFields.Length]], item.relations[i]);
                }
            }

            public int CurrentOid
            {
                get
                {
                    return ((IPersistent)item.relations.GetRaw(i)).Oid;
                }
            }

            public void Reset()
            {
                i = -1;
            }

            public SmallRelationPairEnumerator(Item item)
            {
                this.item = item;
                Reset();
            }

            Item item;
            int i;
        }

        class LargeRelationPairEnumerable : IEnumerable<Pair>
        {
            public IEnumerator<Pair> GetEnumerator()
            {
                return new LargeRelationPairEnumerator(item);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new LargeRelationPairEnumerator(item);
            }

            Item item;

            public LargeRelationPairEnumerable(Item item)
            {
                this.item = item;
            }
        }

        class LargeRelationPairEnumerator : IEnumerator<Pair>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                return e.MoveNext();
            }

            object IEnumerator.Current
            {
                get
                {
                    int id = (int)(long)e.Key;
                    return new Pair(item.db.id2name[id], e.Value);
                }
            }

            public Pair Current
            {
                get
                {
                    int id = (int)(long)e.Key;
                    return new Pair(item.db.id2name[id], e.Value);
                }
            }

            public int CurrentOid
            {
                get
                {
                    return ((PersistentEnumerator)e).CurrentOid;
                }
            }

            public void Reset()
            {
                long oid = item.Oid;
                e = item.db.root.relations.GetDictionaryEnumerator(new Key(oid << 32, true), new Key((oid + 1) << 32, false), IterationOrder.AscentOrder);
            }

            public LargeRelationPairEnumerator(Item item)
            {
                this.item = item;
                Reset();
            }

            Item item;
            IDictionaryEnumerator e;
        }

        internal class ItemEnumerable : IEnumerable<Item>
        {
            public IEnumerator<Item> GetEnumerator()
            {
                return new ItemEnumerator(e.GetEnumerator());
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ItemEnumerator(e.GetEnumerator());
            }

            IEnumerable e;

            public ItemEnumerable(IEnumerable e)
            {
                this.e = e;
            }
        }

        internal class ItemEnumerator : IEnumerator<Item>, PersistentEnumerator
        {
            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                return e.MoveNext();
            }

            object IEnumerator.Current
            {
                get
                {
                    return e.Current;
                }
            }

            public Item Current
            {
                get
                {
                    return (Item)e.Current;
                }
            }

            public int CurrentOid
            {
                get
                {
                    return ((PersistentEnumerator)e).CurrentOid;
                }
            }

            public void Reset()
            {
                e.Reset();
            }

            public ItemEnumerator(IEnumerator e)
            {
                this.e = e;
            }

            IEnumerator e;
        }

        class ItemDictionaryEnumerator : IDictionaryEnumerator
        {
            public bool MoveNext()
            {
                if (i + 1 < item.fieldIds.Length)
                {
                    i += 1;
                    return true;
                }
                else if (item.relations != null)
                {
                    if (e == null)
                    {
                        long oid = item.Oid;
                        e = item.db.root.relations.GetDictionaryEnumerator(new Key(oid << 32, true), new Key((oid + 1) << 32, false), IterationOrder.AscentOrder);
                    }
                    return e.MoveNext();
                }
                return false;
            }

            public DictionaryEntry Entry
            {
                get
                {
                    return new DictionaryEntry(Key, Value);
                }
            }

            public object Current
            {
                get
                {
                    if (i < item.fieldIds.Length)
                    {
                        object val;
                        if (i < item.stringFields.Length)
                        {
                            val = item.stringFields[i];
                        }
                        else if (i < item.stringFields.Length + item.numericFields.Length)
                        {
                            val = item.numericFields[i - item.stringFields.Length];
                        }
                        else
                        {
                            val = item.relations[i - item.stringFields.Length + item.numericFields.Length];
                        }
                        return new DictionaryEntry(item.db.id2name[item.fieldIds[i]], val);
                    }
                    else if (e == null)
                    {
                        throw new InvalidOperationException();
                    }
                    else
                    {
                        return new DictionaryEntry(item.db.id2name[(int)(long)e.Key], e.Value);
                    }
                }
            }

            public object Key
            {
                get
                {
                    if (i < item.fieldIds.Length)
                    {
                        return item.db.id2name[item.fieldIds[i]];
                    }
                    else if (e == null)
                    {
                        throw new InvalidOperationException();
                    }
                    else
                    {
                        return item.db.id2name[(int)(long)e.Key];
                    }
                }
            }

            public object Value
            {
                get
                {
                    if (i < item.fieldIds.Length)
                    {
                        if (i < item.stringFields.Length)
                        {
                            return item.stringFields[i];
                        }
                        else if (i < item.stringFields.Length + item.numericFields.Length)
                        {
                            return item.numericFields[i - item.stringFields.Length];
                        }
                        else
                        {
                            return item.relations[i - item.stringFields.Length + item.numericFields.Length];
                        }
                    }
                    else if (e == null)
                    {
                        throw new InvalidOperationException();
                    }
                    else
                    {
                        return e.Value;
                    }
                }
            }

            public void Reset()
            {
                i = -1;
                e = null;
            }

            public ItemDictionaryEnumerator(Item item)
            {
                this.item = item;
                Reset();
            }

            IDictionaryEnumerator e;
            Item item;
            int i;
        }

        public IEnumerable<Item> GetRelation(String name)
        {
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return null;
            }
            return GetRelation(id);
        }

        internal protected IEnumerable<Item> GetRelation(int id)
    {
        if (relations == null) { 
            long key = ((long)Oid << 32) | (uint)id;
            return new ItemEnumerable(db.root.relations.Range(new Key(key, true), new Key(key+1, false), IterationOrder.AscentOrder));
        }
        int first = stringFields.Length + numericFields.Length;
        int l = first, n = fieldIds.Length, r = n;
        while (l < r) { 
            int m = (l + r) >> 1;
            if (fieldIds[m] < id) { 
                l = m + 1;
            } else { 
                r = m;
            }
        }
        if (l == n || fieldIds[l] != id) { 
            return null;
        }
        for (r = l+1; r < fieldIds.Length && fieldIds[r] == id; r++);
        return new SmallRelationEnumerable(this, l - first, r - first);
    }

        public IEnumerable<Pair> Relations
        {
            get
            {
                return (relations != null)
                    ? (IEnumerable<Pair>)new SmallRelationPairEnumerable(this)
                    : (IEnumerable<Pair>)new LargeRelationPairEnumerable(this);
            }
        }

        public override void OnLoad()
        {
            db = ((AssocDB.Root)Storage.Root).db;
        }

        /// <summary>
        /// Getdictionary iterator through all item attributes (including relations)
        /// </summary>
        /// <returns>Enumerator of item attributes</returns>    
        public IDictionaryEnumerator GetDictionaryEnumerator()
        {
            return new ItemDictionaryEnumerator(this);
        }

        internal protected Item() { }

        internal protected Item(AssocDB db)
            : base(db.storage)
        {
            this.db = db;
            fieldIds = new int[0];
            stringFields = new String[0];
            numericFields = new double[0];
            relations = db.storage.CreateLink(0);
        }
    }
}
    