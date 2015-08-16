namespace Perst.Impl
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
    using System.Diagnostics;
    using Perst;

    enum BtreeResult {
        Done,
        Overflow,
        Underflow,
        NotFound,
        Duplicate,
        Overwrite
    }

    class KeyBuilder
    {
        public static Key getKeyFromObject(object o) 
        {
            if (o == null) 
            { 
                return null;
            }
            else if (o is byte) 
            { 
                return new Key((byte)o);
            }
            else if (o is sbyte) 
            {
                return new Key((sbyte)o);
            }
            else if (o is short) 
            {
                return new Key((short)o);
            }
            else if (o is ushort) 
            {
                return new Key((ushort)o);
            }
            else if (o is int) 
            {
                return new Key((int)o);
            }
            else if (o is uint) 
            {
                return new Key((uint)o);
            }
            else if (o is long) 
            {
                return new Key((long)o);
            }
            else if (o is ulong) 
            {
                return new Key((ulong)o);
            }
            else if (o is float) 
            {
                return new Key((float)o);
            }
            else if (o is double) 
            {
                return new Key((double)o);
            }
            else if (o is bool) 
            {
                return new Key((bool)o);
            }
            else if (o is char) 
            {
                return new Key((char)o);
            }
            else if (o is String) 
            {
                return new Key((String)o);
            }
            else if (o is DateTime) 
            {
                return new Key((DateTime)o);
            }
            else if (o is byte[]) 
            {
                return new Key((byte[])o);
            }
            else if (o is object[]) 
            {
                return new Key((object[])o);
            }
            else if (o is Enum) 
            {
                return new Key((Enum)o);
            }
            else if (o is Guid)
            {
                return new Key((Guid)o);
            }
            else if (o is Decimal)
            {
                return new Key((Decimal)o);
            }
            else 
            { 
                return new Key(o);
            }
        }

        public static Key getKeyFromObject(Type type, object o) 
        {
            if (o == null) 
            { 
                return null;
            }
            else if (type == typeof(byte))
            { 
                return new Key(Convert.ToByte(o));
            }
            else if (type == typeof(sbyte))
            {
                return new Key(Convert.ToSByte(o));
            }
            else if (type == typeof(short))
            {
                return new Key(Convert.ToInt16(o));
            }
            else if (type == typeof(ushort))
            {
                return new Key(Convert.ToUInt16(o));
            }
            else if (type == typeof(int))
            {
                return new Key(Convert.ToInt32(o));
            }
            else if (type == typeof(uint))
            {
                return new Key(Convert.ToUInt32(o));
            }
            else if (type == typeof(long))
            {
                return new Key(Convert.ToInt64(o));
            }
            else if (type == typeof(ulong))
            {
                return new Key(Convert.ToUInt64(o));
            }
            else if (type == typeof(float))
            {
                return new Key(Convert.ToSingle(o));
            }
            else if (type == typeof(double))
            {
                return new Key(Convert.ToDouble(o));
            }
            else if (type == typeof(bool))
            {
                return new Key((bool)o);
            }
            else if (type == typeof(char))
            {
                return new Key((char)o);
            }
            else if (type == typeof(String))
            {
                return new Key((String)o);
            }
            else if (type == typeof(DateTime))
            {
                return new Key((DateTime)o);
            }
            else if (type == typeof(byte[]))
            {
                return new Key((byte[])o);
            }
            else if (type == typeof(object[]))
            {
                return new Key((object[])o);
            }
            else if (type == typeof(Enum))
            {
                return new Key((Enum)o);
            }
            else if (type == typeof(Guid))
            {
                return new Key((Guid)o);
            }
            else if (type == typeof(Decimal))
            {
                return new Key((Decimal)o);
            }
            else 
            { 
                return new Key(o);
            }
        }
     }

#if USE_GENERICS
    internal interface Btree : IPersistent {
        int  markTree();
        void export(XMLExporter exporter);
        int  insert(Key key, object obj, bool overwrite);
        ClassDescriptor.FieldType FieldType {get;}
        ClassDescriptor.FieldType[] FieldTypes {get;}
        bool IsUnique{get;}
        int compareByteArrays(Key key, Page pg, int i);
        int HeaderSize{get;}
        void init(Type cls, ClassDescriptor.FieldType[] types, string[] fieldNames, bool unique, long autoincCount);
    }

    [Serializable]
    internal class Btree<K,V>:PersistentCollection<V>, Index<K,V>, Btree where V:class
#else
    [Serializable]
    internal class Btree:PersistentCollection, Index
#endif
    {
        internal int root;
        internal int height;
        internal ClassDescriptor.FieldType type;
        internal int nElems;
        internal bool unique;
        [NonSerialized()]
        internal int  updateCounter;
		
        internal static int Sizeof = ObjectHeader.Sizeof + 4 * 4 + 1;

        internal Btree()
        {
        }
		
        internal Btree(byte[] obj, int offs) 
        {
            height = Bytes.unpack4(obj, offs);
            offs += 4;
            nElems = Bytes.unpack4(obj, offs);
            offs += 4;
            root = Bytes.unpack4(obj, offs);
            offs += 4;
            type = (ClassDescriptor.FieldType)Bytes.unpack4(obj, offs);
            offs += 4;
            unique = obj[offs] != 0;
        }


#if USE_GENERICS
        internal Btree(bool unique)
        {
            type = checkType(typeof(K));
            this.unique = unique;
        }
#else
        internal Btree(Type cls, bool unique)
        {
            type = checkType(cls);
            this.unique = unique;
        }
#endif

#if USE_GENERICS
        public override void OnLoad()
        {
             if (type != ClassDescriptor.getTypeCode(typeof(K))) {
                 throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE, typeof(K));
            }
        }
#endif

        internal Btree(ClassDescriptor.FieldType type, bool unique)
        {
            this.type = type;
            this.unique = unique;
        }
		
        public virtual void init(Type cls, ClassDescriptor.FieldType[] types, string[] fieldNames, bool unique, long autoincCount) 
        {
            this.type = types[0];
            this.unique = unique;
        }

        static protected ClassDescriptor.FieldType checkType(Type c) 
        { 
            ClassDescriptor.FieldType elemType = ClassDescriptor.convertToNotNullable(ClassDescriptor.getTypeCode(c));
            if ((int)elemType > (int)ClassDescriptor.FieldType.tpOid
                && elemType != ClassDescriptor.FieldType.tpArrayOfByte
                && elemType != ClassDescriptor.FieldType.tpDecimal
                && elemType != ClassDescriptor.FieldType.tpGuid) 
            { 
                throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE, c);
            }
            return elemType;
        }

        public virtual int compareByteArrays(byte[] key, byte[] item, int offs, int length) 
        { 
            int n = key.Length >= length ? length : key.Length;
            for (int i = 0; i < n; i++) 
            { 
                int diff = key[i] - item[i+offs];
                if (diff != 0) 
                { 
                    return diff;
                }
            }
            return key.Length - length;
        }
        
        public override int Count 
        { 
            get 
            {
                return nElems;
            }
        }

        public bool IsUnique
        {
            get
            { 
                 return unique;
            }
        }
        
        public int HeaderSize
        {
            get
            { 
                 return Sizeof;
            }
        }
        

        public ClassDescriptor.FieldType FieldType 
        {
            get
            { 
                 return type;
            }
        }

        public virtual ClassDescriptor.FieldType[] FieldTypes 
        {
            get
            { 
                 return new ClassDescriptor.FieldType[]{type};
            }
        }

        public Type KeyType
        {
            get 
            { 
#if USE_GENERICS
                return typeof(K);
#else
                return mapKeyType(type);
#endif
            }
        }


        internal static Type mapKeyType(ClassDescriptor.FieldType type)
        { 
            switch (type) 
            { 
                case ClassDescriptor.FieldType.tpBoolean:
                    return typeof(bool);
                case ClassDescriptor.FieldType.tpSByte:
                    return typeof(sbyte);
                case ClassDescriptor.FieldType.tpByte:
                    return typeof(byte);
                case ClassDescriptor.FieldType.tpChar:
                    return typeof(char);
                case ClassDescriptor.FieldType.tpShort:
                    return typeof(short);
                case ClassDescriptor.FieldType.tpUShort:
                    return typeof(ushort);
                case ClassDescriptor.FieldType.tpInt:
                    return typeof(int);
                case ClassDescriptor.FieldType.tpUInt:
                    return typeof(uint);
                case ClassDescriptor.FieldType.tpLong:
                    return typeof(long);
                case ClassDescriptor.FieldType.tpULong:
                    return typeof(ulong);
                case ClassDescriptor.FieldType.tpFloat:
                    return typeof(float);
                case ClassDescriptor.FieldType.tpDouble:
                    return typeof(double);
                case ClassDescriptor.FieldType.tpString:
                    return typeof(string);
                case ClassDescriptor.FieldType.tpDate:
                    return typeof(DateTime);
                case ClassDescriptor.FieldType.tpArrayOfByte:
                    return typeof(byte[]);
                case ClassDescriptor.FieldType.tpGuid:
                    return typeof(Guid);
                case ClassDescriptor.FieldType.tpDecimal:
                    return typeof(decimal);
                case ClassDescriptor.FieldType.tpEnum:
                    return typeof(Enum);
                case ClassDescriptor.FieldType.tpValue:
                    return typeof(ValueType);                    
                default:
                    return typeof(object);
            }
        }

#if USE_GENERICS
        public V[] this[K from, K till] 
#else
        public object[] this[object from, object till] 
#endif
        {
            get 
            {
                return Get(from, till);
            }
        }

#if USE_GENERICS
        public V this[K key] 
#else
        public object this[object key] 
#endif
        {
            get 
            {
                return Get(key);
            }
            set 
            {
                Set(key, value);
            }
        } 
     
        protected virtual Key checkKey(Key key) 
        {
            if (key != null) 
            { 
                if (key.type != type)
                {
                    throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
                }
                if ((type == ClassDescriptor.FieldType.tpObject 
                     || type == ClassDescriptor.FieldType.tpOid) 
                    && key.ival == 0 && key.oval != null)
                {
                    object obj = key.oval;
                    key = new Key(obj, Storage.MakePersistent(obj), key.inclusion != 0);
                }
                if (type == ClassDescriptor.FieldType.tpString && key.oval is string) 
                {
                    key = new Key(((string)key.oval).ToCharArray(), key.inclusion != 0);
                }
            }
            return key;
        }

#if USE_GENERICS
        public virtual V Get(Key key)
        {
            key = checkKey(key);
            if (root != 0)
            {
                ArrayList list = new ArrayList();
                BtreePage.find((StorageImpl) Storage, root, key, key, this, height, list);
                if (list.Count > 1)
                {
                    throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
                }
                else if (list.Count == 0)
                {
                    return default(V);
                }
                else
                {
                    return (V) list[0];
                }
            }
            return default(V);
        }
#else
        public virtual object Get(Key key)
        {
            key = checkKey(key);
            if (root != 0)
            {
                ArrayList list = new ArrayList();
                BtreePage.find((StorageImpl) Storage, root, key, key, this, height, list);
                if (list.Count > 1)
                {
                    throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
                }
                else if (list.Count == 0)
                {
                    return null;
                }
                else
                {
                    return list[0];
                }
            }
            return null;
        }
#endif

#if USE_GENERICS
        public virtual V Get(K key) 
#else
        public virtual object Get(object key) 
#endif
        {
            return Get(KeyBuilder.getKeyFromObject(key));
        }

#if USE_GENERICS
        internal static V[] emptySelection = new V[0];
        
        public virtual V[] Get(Key from, Key till)
#else
        internal static object[] emptySelection = new object[0];
		
        public virtual object[] Get(Key from, Key till)
#endif
        {
            if (root != 0)
            {
                ArrayList list = new ArrayList();
                BtreePage.find((StorageImpl) Storage, root, checkKey(from), checkKey(till), this, height, list);
                if (list.Count != 0)
                {
#if USE_GENERICS
                    return (V[]) list.ToArray(typeof(V));
#else
                    return list.ToArray();
#endif
                }
            }
            return emptySelection;
        }
		
#if USE_GENERICS
        public V[] PrefixSearch(string key) 
#else
        public object[] PrefixSearch(string key) 
#endif
        { 
            if (ClassDescriptor.FieldType.tpString != type) 
            { 
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            if (root != 0) 
            { 
                ArrayList list = new ArrayList();
                BtreePage.prefixSearch((StorageImpl)Storage, root, key, height, list);
                if (list.Count != 0) 
                { 
#if USE_GENERICS
                    return (V[]) list.ToArray(typeof(V));
#else
                    return list.ToArray();
#endif
                }
            }
            return emptySelection;
        }

#if USE_GENERICS
        public virtual V[] Get(K from, K till)
#else
        public virtual object[] Get(object from, object till)
#endif
        {
            return Get(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till));
        }
            
#if USE_GENERICS
        public virtual V[] GetPrefix(string prefix)
#else
        public virtual object[] GetPrefix(string prefix)
#endif
        {
            return Get(new Key(prefix), 
                       new Key(prefix + Char.MaxValue, false));
        }
        
#if USE_GENERICS
        public virtual bool Put(Key key, V obj)
#else
        public virtual bool Put(Key key, object obj)
#endif
        {
            return insert(key, obj, false) >= 0;
        }

#if USE_GENERICS
        public virtual bool Put(K key, V obj)
#else
        public virtual bool Put(object key, object obj)
#endif
        {
            return Put(KeyBuilder.getKeyFromObject(key), obj);
        }

#if USE_GENERICS
        public virtual V Set(Key key, V obj)
        {
            int oid = insert(key, obj, true);
            return (oid != 0) ? (V)((StorageImpl)Storage).lookupObject(oid, null) : null;
        }
#else
        public virtual object Set(Key key, object obj)
        {
            int oid = insert(key, obj, true);
            return (oid != 0) ? ((StorageImpl)Storage).lookupObject(oid, null) : null;
        }
#endif


#if USE_GENERICS
        public virtual V Set(K key, V obj)
#else
        public virtual object Set(object key, object obj)
#endif
        {
            return Set(KeyBuilder.getKeyFromObject(key), obj);
        }

        public int insert(Key key, object obj, bool overwrite)
        {
            StorageImpl db = (StorageImpl) Storage;
            if (db == null) 
            { 
                throw new StorageError(Perst.StorageError.ErrorCode.DELETED_OBJECT);
            }
            BtreeKey ins = new BtreeKey(checkKey(key), db.MakePersistent(obj));
            if (root == 0)
            {
                root = BtreePage.allocate(db, 0, type, ins);
                height = 1;
            }
            else
            {
                BtreeResult result = BtreePage.insert(db, root, this, ins, height, unique, overwrite);
                if (result == BtreeResult.Overflow)
                {
                    root = BtreePage.allocate(db, root, type, ins);
                    height += 1;
                }
                else if (result == BtreeResult.Duplicate)
                {
                    return -1;
                }
                else if (result == BtreeResult.Overwrite)
                {
                    return ins.oldOid;
                }
            }
            nElems += 1;
            updateCounter += 1;
            Modify();
            return 0;
        }
		
#if USE_GENERICS
        public virtual void  Remove(Key key, V obj)
#else
        public virtual void  Remove(Key key, object obj)
#endif
        {
            remove(new BtreeKey(checkKey(key), Storage.GetOid(obj)));
        }
		
        internal virtual bool removeIfExists(Key key, object obj)
        {
            return removeIfExists(new BtreeKey(checkKey(key), Storage.GetOid(obj)));
        }

#if USE_GENERICS
        public virtual void  Remove(K key, V obj)
#else
        public virtual void  Remove(object key, object obj)
#endif
        {
            remove(new BtreeKey(checkKey(KeyBuilder.getKeyFromObject(key)), Storage.GetOid(obj)));    
        }
 		
#if USE_GENERICS
        public virtual bool Unlink(Key key, V obj)
#else
        public virtual bool Unlink(Key key, object obj)
#endif
        {
            return removeIfExists(key, obj);
        }
 		
        internal virtual void remove(BtreeKey rem)
        {
            if (!removeIfExists(rem)) { 
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_FOUND);
            }
        }
            
        internal virtual bool removeIfExists(BtreeKey rem)
        {
            StorageImpl db = (StorageImpl) Storage;
            if (db == null) 
            { 
                throw new StorageError(Perst.StorageError.ErrorCode.DELETED_OBJECT);
            }
            if (root == 0)
            {
                return false;
            }
            BtreeResult result = BtreePage.remove(db, root, this, rem, height);
            if (result == BtreeResult.NotFound)
            {
                return false;
            }
            nElems -= 1;
            if (result == BtreeResult.Underflow)
            {
                Page pg = db.getPage(root);
                if (BtreePage.getnItems(pg) == 0)
                {
                    int newRoot = 0;
                    if (height != 1)  
                    {         
                        newRoot = (type == ClassDescriptor.FieldType.tpString || type == ClassDescriptor.FieldType.tpArrayOfByte)
                            ? BtreePage.getKeyStrOid(pg, 0)
                            : BtreePage.getReference(pg, BtreePage.maxItems - 1);
                    }
                    db.freePage(root);
                    root = newRoot;
                    height -= 1;
                }
                db.pool.unfix(pg);
            }
            else if (result == BtreeResult.Overflow)
            {
                root = BtreePage.allocate(db, root, type, rem);
                height += 1;
            }
            updateCounter += 1;
            Modify();
            return true;
        }
		
               
                
#if USE_GENERICS
        public virtual V Remove(Key key)
#else
        public virtual object Remove(Key key)
#endif
        {
            if (!unique)
            {
                throw new StorageError(StorageError.ErrorCode.KEY_NOT_UNIQUE);
            }
            BtreeKey rk = new BtreeKey(checkKey(key), 0);
            StorageImpl db = (StorageImpl)Storage;
            remove(rk);
#if USE_GENERICS
            return (V)db.lookupObject(rk.oldOid, null);
#else
            return db.lookupObject(rk.oldOid, null);
#endif
        }		
            
#if USE_GENERICS
        public virtual V RemoveKey(K key)
#else
        public virtual object RemoveKey(object key)
#endif
        {
            return Remove(KeyBuilder.getKeyFromObject(key));
        }

        public virtual int Size()
        {
            return nElems;
        }
		
        public override void Clear() 
        {
            if (root != 0)
            {
                BtreePage.purge((StorageImpl) Storage, root, type, height);
                root = 0;
                nElems = 0;
                height = 0;
                updateCounter += 1;
                Modify();
            }
        }
		
#if USE_GENERICS
        public virtual V[] ToArray()
        {
            V[] arr = new V[nElems];
#else
        public virtual object[] ToArray()
        {
            object[] arr = new object[nElems];
#endif
            if (root != 0)
            {
                BtreePage.traverseForward((StorageImpl) Storage, root, type, height, arr, 0);
            }
            return arr;
        }
		
        public virtual Array ToArray(Type elemType)
        {
            Array arr = Array.CreateInstance(elemType, nElems);
            if (root != 0)
            {
                BtreePage.traverseForward((StorageImpl) Storage, root, type, height, (object[])arr, 0);
            }
            return arr;
        }
		
        public override void Deallocate()
        {
            if (root != 0)
            {
                BtreePage.purge((StorageImpl) Storage, root, type, height);
            }
            base.Deallocate();
        }

        public void export(XMLExporter exporter)
        {
            if (root != 0)
            {
                BtreePage.exportPage((StorageImpl) Storage, exporter, root, type, height);
            }
        }		

        public int markTree() 
        { 
            return (root != 0) ? BtreePage.markPage((StorageImpl)Storage, root, type, height) : 0;
        }        

        protected virtual object unpackEnum(int val) 
        {
            // Base B-Tree class has no information about particular enum type
            // so it is not able to correctly unpack enum key
            return val;
        }

        internal object unpackKey(StorageImpl db, Page pg, int pos)
        {
            int offs = BtreePage.firstKeyOffs + pos*ClassDescriptor.Sizeof[(int)type];
            byte[] data = pg.data;
			
            switch (type)
            {
                case ClassDescriptor.FieldType.tpBoolean: 
                    return data[offs] != 0;
				
                case ClassDescriptor.FieldType.tpSByte: 
                    return (sbyte)data[offs];
                   
                case ClassDescriptor.FieldType.tpByte: 
                    return data[offs];
 				
                case ClassDescriptor.FieldType.tpShort: 
                    return Bytes.unpack2(data, offs);

                case ClassDescriptor.FieldType.tpUShort: 
                    return (ushort)Bytes.unpack2(data, offs);
				
                case ClassDescriptor.FieldType.tpChar: 
                    return (char) Bytes.unpack2(data, offs);

                case ClassDescriptor.FieldType.tpInt: 
                    return Bytes.unpack4(data, offs);
                    
                case ClassDescriptor.FieldType.tpEnum: 
                    return unpackEnum(Bytes.unpack4(data, offs));
            
                case ClassDescriptor.FieldType.tpUInt:
                    return (uint)Bytes.unpack4(data, offs);
 
                case ClassDescriptor.FieldType.tpOid: 
                case ClassDescriptor.FieldType.tpObject: 
                    return db.lookupObject(Bytes.unpack4(data, offs), null);
 				
                case ClassDescriptor.FieldType.tpLong: 
                    return Bytes.unpack8(data, offs);

                case ClassDescriptor.FieldType.tpDate: 
                    return new DateTime(Bytes.unpack8(data, offs));

                case ClassDescriptor.FieldType.tpULong: 
                    return (ulong)Bytes.unpack8(data, offs);
 				
                case ClassDescriptor.FieldType.tpFloat: 
                    return Bytes.unpackF4(data, offs);

                case ClassDescriptor.FieldType.tpDouble: 
                    return Bytes.unpackF8(data, offs);

                case ClassDescriptor.FieldType.tpGuid:
                    return Bytes.unpackGuid(data, offs);
                
                case ClassDescriptor.FieldType.tpDecimal:
                    return Bytes.unpackDecimal(data, offs);

                case ClassDescriptor.FieldType.tpString:
                {
                    int len = BtreePage.getKeyStrSize(pg, pos);
                    offs = BtreePage.firstKeyOffs + BtreePage.getKeyStrOffs(pg, pos);
                    char[] sval = new char[len];
                    for (int j = 0; j < len; j++)
                    {
                        sval[j] = (char) Bytes.unpack2(pg.data, offs);
                        offs += 2;
                    }
                    return new String(sval);
                }
                case ClassDescriptor.FieldType.tpArrayOfByte:
                {
                    return unpackByteArrayKey(pg, pos);
                }
                default: 
                    Debug.Assert(false, "Invalid type");
                    return null;
            }
        }

        protected virtual object unpackByteArrayKey(Page pg, int pos)
        {
            int len = BtreePage.getKeyStrSize(pg, pos);
            int offs = BtreePage.firstKeyOffs + BtreePage.getKeyStrOffs(pg, pos);
            byte[] val = new byte[len];
            Array.Copy(pg.data, offs, val, 0, len);
            return val;
        }
    
        public virtual IDictionaryEnumerator GetDictionaryEnumerator() 
        { 
            return GetDictionaryEnumerator(null, null, IterationOrder.AscentOrder);
        }

#if USE_GENERICS
        public override IEnumerator<V> GetEnumerator() 
#else
        public override IEnumerator GetEnumerator() 
#endif
        { 
             return GetEnumerator(null, null, IterationOrder.AscentOrder);
        }

        public int compareByteArrays(Key key, Page pg, int i) 
        { 
            return compareByteArrays((byte[])key.oval, 
                pg.data, 
                BtreePage.getKeyStrOffs(pg, i) + BtreePage.firstKeyOffs, 
                BtreePage.getKeyStrSize(pg, i));
        }


#if USE_GENERICS        
        class BtreeSelectionEnumerable : IEnumerable<V>, IEnumerable
        { 
            internal BtreeSelectionEnumerable(Btree<K,V> tree, Key from, Key till, IterationOrder order) 
#else
        class BtreeSelectionEnumerable : IEnumerable
        { 
            internal BtreeSelectionEnumerable(Btree tree, Key from, Key till, IterationOrder order) 
#endif
            { 
                this.from = from;
                this.till = till;
                this.order = order;
                this.tree = tree;
            }

#if USE_GENERICS        
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new BtreeSelectionEnumerator(tree, from, till, order);
            }

            public IEnumerator<V> GetEnumerator() 
#else
            public IEnumerator GetEnumerator() 
#endif
            {
                return new BtreeSelectionEnumerator(tree, from, till, order);
            }

#if USE_GENERICS
            Btree<K,V>      tree;
#else
            Btree           tree;
#endif
            Key             from;
            Key             till;
            IterationOrder  order;
        }


#if USE_GENERICS        
        class BtreeSelectionEnumerator : IEnumerator<V>, PersistentEnumerator
        { 
            internal BtreeSelectionEnumerator(Btree<K,V> tree, Key from, Key till, IterationOrder order) 
#else
        class BtreeSelectionEnumerator : PersistentEnumerator
        { 
            internal BtreeSelectionEnumerator(Btree tree, Key from, Key till, IterationOrder order) 
#endif
            { 
                this.from = from;
                this.till = till;
                this.type = tree.type;
                this.order = order;
                this.tree = tree;
                Reset();
            }

            public virtual void Reset() 
            {
                int i, l, r;
                Page pg;
                int height = tree.height;
                int pageId = tree.root;
                updateCounter = tree.updateCounter;
                hasCurrent = false;
                sp = 0;
            
                if (height == 0) 
                { 
                    return;
                }
                db = (StorageImpl)tree.Storage;
                if (db == null) 
                { 
                    throw new StorageError(Perst.StorageError.ErrorCode.DELETED_OBJECT);
                }
                pageStack = new int[height];
                posStack = new int[height];

                if (type == ClassDescriptor.FieldType.tpString) 
                { 
                    if (order == IterationOrder.AscentOrder) 
                    { 
                        if (from == null) 
                        { 
                            while (--height >= 0) 
                            { 
                                posStack[sp] = 0;
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                pageId = BtreePage.getKeyStrOid(pg, 0);
                                end = BtreePage.getnItems(pg);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                        } 
                        else 
                        { 
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                l = 0;
                                r = BtreePage.getnItems(pg);
                                while (l < r)  
                                {
                                    i = (l+r) >> 1;
                                    if (BtreePage.compareStr(from, pg, i) >= from.inclusion) 
                                    {
                                        l = i + 1; 
                                    } 
                                    else 
                                    { 
                                        r = i;
                                    }
                                }
                                Debug.Assert(r == l); 
                                posStack[sp] = r;
                                pageId = BtreePage.getKeyStrOid(pg, r);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            l = 0;
                            end = r = BtreePage.getnItems(pg);
                            while (l < r)  
                            {
                                i = (l+r) >> 1;
                                if (BtreePage.compareStr(from, pg, i) >= from.inclusion) 
                                {
                                    l = i + 1; 
                                } 
                                else 
                                { 
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l); 
                            if (r == end) 
                            {
                                sp += 1;
                                gotoNextItem(pg, r-1);
                            } 
                            else 
                            { 
                                posStack[sp++] = r;
                                db.pool.unfix(pg);
                            }
                        }
                        if (sp != 0 && till != null) 
                        { 
                            pg = db.getPage(pageStack[sp-1]);
                            if (-BtreePage.compareStr(till, pg, posStack[sp-1]) >= till.inclusion) 
                            { 
                                sp = 0;
                            }
                            db.pool.unfix(pg);
                        }
                    } 
                    else 
                    { // descent order
                        if (till == null) 
                        { 
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                posStack[sp] = BtreePage.getnItems(pg);
                                pageId = BtreePage.getKeyStrOid(pg, posStack[sp]);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            posStack[sp++] = BtreePage.getnItems(pg)-1;
                            db.pool.unfix(pg);
                        } 
                        else 
                        {
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                l = 0;
                                r = BtreePage.getnItems(pg);
                                while (l < r)  
                                {
                                    i = (l+r) >> 1;
                                    if (BtreePage.compareStr(till, pg, i) >= 1-till.inclusion) 
                                    {
                                        l = i + 1; 
                                    } 
                                    else 
                                    { 
                                        r = i;
                                    }
                                }
                                Debug.Assert(r == l); 
                                posStack[sp] = r;
                                pageId = BtreePage.getKeyStrOid(pg, r);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            l = 0;
                            r = BtreePage.getnItems(pg);
                            while (l < r)  
                            {
                                i = (l+r) >> 1;
                                if (BtreePage.compareStr(till, pg, i) >= 1-till.inclusion) 
                                {
                                    l = i + 1; 
                                } 
                                else 
                                { 
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l); 
                            if (r == 0) 
                            {
                                sp += 1;
                                gotoNextItem(pg, r);
                            } 
                            else 
                            { 
                                posStack[sp++] = r-1;
                                db.pool.unfix(pg);
                            }
                        }
                        if (sp != 0 && from != null) 
                        { 
                            pg = db.getPage(pageStack[sp-1]);
                            if (BtreePage.compareStr(from, pg, posStack[sp-1]) >= from.inclusion) 
                            { 
                                sp = 0;
                            }
                            db.pool.unfix(pg);
                        }
                    }
                } 
                else if (type == ClassDescriptor.FieldType.tpArrayOfByte) 
                { 
                    if (order == IterationOrder.AscentOrder) 
                    { 
                        if (from == null) 
                        { 
                            while (--height >= 0) 
                            { 
                                posStack[sp] = 0;
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                pageId = BtreePage.getKeyStrOid(pg, 0);
                                end = BtreePage.getnItems(pg);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                        } 
                        else 
                        { 
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                l = 0;
                                r = BtreePage.getnItems(pg);
                                while (l < r)  
                                {
                                    i = (l+r) >> 1;
                                    if (tree.compareByteArrays(from, pg, i) >= from.inclusion) 
                                    {
                                        l = i + 1; 
                                    } 
                                    else 
                                    { 
                                        r = i;
                                    }
                                }
                                Debug.Assert(r == l); 
                                posStack[sp] = r;
                                pageId = BtreePage.getKeyStrOid(pg, r);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            l = 0;
                            end = r = BtreePage.getnItems(pg);
                            while (l < r)  
                            {
                                i = (l+r) >> 1;
                                if (tree.compareByteArrays(from, pg, i) >= from.inclusion) 
                                {
                                    l = i + 1; 
                                } 
                                else 
                                { 
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l); 
                            if (r == end) 
                            {
                                sp += 1;
                                gotoNextItem(pg, r-1);
                            } 
                            else 
                            { 
                                posStack[sp++] = r;
                                db.pool.unfix(pg);
                            }
                        }
                        if (sp != 0 && till != null) 
                        { 
                            pg = db.getPage(pageStack[sp-1]);
                            if (-tree.compareByteArrays(till, pg, posStack[sp-1]) >= till.inclusion) 
                            { 
                                sp = 0;
                            }
                            db.pool.unfix(pg);
                        }
                    } 
                    else 
                    { // descent order
                        if (till == null) 
                        { 
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                posStack[sp] = BtreePage.getnItems(pg);
                                pageId = BtreePage.getKeyStrOid(pg, posStack[sp]);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            posStack[sp++] = BtreePage.getnItems(pg)-1;
                            db.pool.unfix(pg);
                        } 
                        else 
                        {
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                l = 0;
                                r = BtreePage.getnItems(pg);
                                while (l < r)  
                                {
                                    i = (l+r) >> 1;
                                    if (tree.compareByteArrays(till, pg, i) >= 1-till.inclusion) 
                                    {
                                        l = i + 1; 
                                    } 
                                    else 
                                    { 
                                        r = i;
                                    }
                                }
                                Debug.Assert(r == l); 
                                posStack[sp] = r;
                                pageId = BtreePage.getKeyStrOid(pg, r);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            l = 0;
                            r = BtreePage.getnItems(pg);
                            while (l < r)  
                            {
                                i = (l+r) >> 1;
                                if (tree.compareByteArrays(till, pg, i) >= 1-till.inclusion) 
                                {
                                    l = i + 1; 
                                } 
                                else 
                                { 
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l); 
                            if (r == 0) 
                            {
                                sp += 1;
                                gotoNextItem(pg, r);
                            } 
                            else 
                            { 
                                posStack[sp++] = r-1;
                                db.pool.unfix(pg);
                            }
                        }
                        if (sp != 0 && from != null) 
                        { 
                            pg = db.getPage(pageStack[sp-1]);
                            if (tree.compareByteArrays(from, pg, posStack[sp-1]) >= from.inclusion) 
                            { 
                                sp = 0;
                            }
                            db.pool.unfix(pg);
                        }
                    }
                } 
                else 
                { // scalar type
                    if (order == IterationOrder.AscentOrder) 
                    { 
                        if (from == null) 
                        { 
                            while (--height >= 0) 
                            { 
                                posStack[sp] = 0;
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                pageId = BtreePage.getReference(pg, BtreePage.maxItems-1);
                                end = BtreePage.getnItems(pg);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                        } 
                        else 
                        { 
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                l = 0;
                                r = BtreePage.getnItems(pg);
                                while (l < r)  
                                {
                                    i = (l+r) >> 1;
                                    if (BtreePage.compare(from, pg, i) >= from.inclusion) 
                                    {
                                        l = i + 1; 
                                    } 
                                    else 
                                    { 
                                        r = i;
                                    }
                                }
                                Debug.Assert(r == l); 
                                posStack[sp] = r;
                                pageId = BtreePage.getReference(pg, BtreePage.maxItems-1-r);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            l = 0;
                            r = end = BtreePage.getnItems(pg);
                            while (l < r)  
                            {
                                i = (l+r) >> 1;
                                if (BtreePage.compare(from, pg, i) >= from.inclusion) 
                                {
                                    l = i + 1; 
                                } 
                                else 
                                { 
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l); 
                            if (r == end) 
                            {
                                sp += 1;
                                gotoNextItem(pg, r-1);
                            } 
                            else 
                            { 
                                posStack[sp++] = r;
                                db.pool.unfix(pg);
                            }
                        }
                        if (sp != 0 && till != null) 
                        { 
                            pg = db.getPage(pageStack[sp-1]);
                            if (-BtreePage.compare(till, pg, posStack[sp-1]) >= till.inclusion) 
                            { 
                                sp = 0;
                            }
                            db.pool.unfix(pg);
                        }
                    } 
                    else 
                    { // descent order
                        if (till == null) 
                        { 
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                posStack[sp] = BtreePage.getnItems(pg);
                                pageId = BtreePage.getReference(pg, BtreePage.maxItems-1-posStack[sp]);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            posStack[sp++] = BtreePage.getnItems(pg)-1;
                            db.pool.unfix(pg);
                        } 
                        else 
                        {
                            while (--height > 0) 
                            { 
                                pageStack[sp] = pageId;
                                pg = db.getPage(pageId);
                                l = 0;
                                r = BtreePage.getnItems(pg);
                                while (l < r)  
                                {
                                    i = (l+r) >> 1;
                                    if (BtreePage.compare(till, pg, i) >= 1-till.inclusion) 
                                    {
                                        l = i + 1; 
                                    } 
                                    else 
                                    { 
                                        r = i;
                                    }
                                }
                                Debug.Assert(r == l); 
                                posStack[sp] = r;
                                pageId = BtreePage.getReference(pg, BtreePage.maxItems-1-r);
                                db.pool.unfix(pg);
                                sp += 1;
                            }
                            pageStack[sp] = pageId;
                            pg = db.getPage(pageId);
                            l = 0;
                            r = BtreePage.getnItems(pg);
                            while (l < r)  
                            {
                                i = (l+r) >> 1;
                                if (BtreePage.compare(till, pg, i) >= 1-till.inclusion) 
                                {
                                    l = i + 1; 
                                } 
                                else 
                                { 
                                    r = i;
                                }
                            }
                            Debug.Assert(r == l);  
                            if (r == 0) 
                            { 
                                sp += 1;
                                gotoNextItem(pg, r);
                            } 
                            else 
                            { 
                                posStack[sp++] = r-1;
                                db.pool.unfix(pg);
                            }
                        }
                        if (sp != 0 && from != null) 
                        { 
                            pg = db.getPage(pageStack[sp-1]);
                            if (BtreePage.compare(from, pg, posStack[sp-1]) >= from.inclusion) 
                            { 
                                sp = 0;
                            }
                            db.pool.unfix(pg);
                        }
                    }
                }
            }
                

            public void Dispose() {}

            public bool MoveNext() 
            {
                if (updateCounter != tree.updateCounter) 
                { 
                    if (db.concurrentIterator) 
                    { 
                        refresh();
                    } 
                    else 
                    { 
                        throw new InvalidOperationException("B-Tree was modified");
                    }
                }
                if (sp != 0) 
                {
                    int pos = posStack[sp-1];   
                    Page pg = db.getPage(pageStack[sp-1]);
                    hasCurrent = true;
                    getCurrent(pg, pos);
                    gotoNextItem(pg, pos);
                    return true;
                }
                hasCurrent = false;
                return false;
            }

            private BtreeKey getCurrentKey(Page pg, int pos) 
            { 
                BtreeKey key;
                switch (type) 
                { 
                    case ClassDescriptor.FieldType.tpString:
                        key = new BtreeKey(null, BtreePage.getKeyStrOid(pg, pos));
                        key.getStr(pg, pos);
                        break;
                    case ClassDescriptor.FieldType.tpArrayOfByte:
                        key = new BtreeKey(null, BtreePage.getKeyStrOid(pg, pos));
                        key.getByteArray(pg, pos);
                        break;
                    default:
                        key = new BtreeKey(null, BtreePage.getReference(pg, BtreePage.maxItems-1-pos));
                        key.extract(pg, BtreePage.firstKeyOffs + pos*ClassDescriptor.Sizeof[(int)type], type);
                        break;
                }
                return key;
            }

            private void refresh() 
            { 
                if (sp != 0) 
                { 
                    if (nextKey == null) 
                    { 
                        Reset();
                    }
                    else
                    { 
                        Key saveFrom = from;
                        Key saveTill = till;
                        int next = nextKey.oid;
                        if (order == IterationOrder.AscentOrder) 
                        { 
                            from = nextKey.key;
                        } 
                        else 
                        { 
                            till = nextKey.key;
                        }
                        Reset();
                        from = saveFrom;        
                        till = saveTill;

                        while (true) 
                        { 
                            int pos = posStack[sp-1];   
                            Page pg = db.getPage(pageStack[sp-1]);
                            int oid = type == ClassDescriptor.FieldType.tpString || type == ClassDescriptor.FieldType.tpArrayOfByte
                                ? BtreePage.getKeyStrOid(pg, pos)
                                : BtreePage.getReference(pg, BtreePage.maxItems-1-pos);
                            if (oid != next) 
                            { 
                                gotoNextItem(pg, pos);
                            } 
                            else 
                            { 
                                db.pool.unfix(pg);
                                break;
                            }
                        }
                    }
                }
                updateCounter = tree.updateCounter;
            }

            protected virtual void getCurrent(Page pg, int pos)
            {
                oid = (type == ClassDescriptor.FieldType.tpString || type == ClassDescriptor.FieldType.tpArrayOfByte)
                    ? BtreePage.getKeyStrOid(pg, pos)
                    : BtreePage.getReference(pg, BtreePage.maxItems-1-pos);
            }
 

            public int CurrentOid 
            {
                get 
                {
                    return oid;
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

            public virtual V Current 
#else
            public virtual object Current 
#endif
            {
                get 
                {
#if USE_GENERICS        
                    return (V)getCurrent();
#else
                    return getCurrent();
#endif
                }
            }

            private object getCurrent()
            {
                if (!hasCurrent)
                {
                    throw new InvalidOperationException();
                }
                return db.lookupObject(oid, null);
            }

            protected void gotoNextItem(Page pg, int pos)
            {
                if (type == ClassDescriptor.FieldType.tpString) 
                { 
                    if (order == IterationOrder.AscentOrder) 
                    {                     
                        if (++pos == end) 
                        { 
                            while (--sp != 0) 
                            { 
                                db.pool.unfix(pg);
                                pos = posStack[sp-1];
                                pg = db.getPage(pageStack[sp-1]);
                                if (++pos <= BtreePage.getnItems(pg)) 
                                {
                                    posStack[sp-1] = pos;
                                    do 
                                    { 
                                        int pageId = BtreePage.getKeyStrOid(pg, pos);
                                        db.pool.unfix(pg);
                                        pg = db.getPage(pageId);
                                        end = BtreePage.getnItems(pg);
                                        pageStack[sp] = pageId;
                                        posStack[sp] = pos = 0;
                                    } while (++sp < pageStack.Length);
                                    break;
                                }
                            }
                        } 
                        else 
                        { 
                            posStack[sp-1] = pos;
                        }
                        if (sp != 0 && till != null && -BtreePage.compareStr(till, pg, pos) >= till.inclusion) 
                        { 
                            sp = 0;
                        }
                    } 
                    else 
                    { // descent order
                        if (--pos < 0) 
                        { 
                            while (--sp != 0) 
                            { 
                                db.pool.unfix(pg);
                                pos = posStack[sp-1];
                                pg = db.getPage(pageStack[sp-1]);
                                if (--pos >= 0) 
                                {
                                    posStack[sp-1] = pos;
                                    do 
                                    { 
                                        int pageId = BtreePage.getKeyStrOid(pg, pos);
                                        db.pool.unfix(pg);
                                        pg = db.getPage(pageId);
                                        pageStack[sp] = pageId;
                                        posStack[sp] = pos = BtreePage.getnItems(pg);
                                    } while (++sp < pageStack.Length);
                                    posStack[sp-1] = --pos;
                                    break;
                                }
                            }
                        } 
                        else 
                        { 
                            posStack[sp-1] = pos;
                        }
                        if (sp != 0 && from != null && BtreePage.compareStr(from, pg, pos) >= from.inclusion) 
                        { 
                            sp = 0;
                        }                    
                    }
                } 
                else if (type == ClassDescriptor.FieldType.tpArrayOfByte) 
                { 
                    if (order == IterationOrder.AscentOrder) 
                    {                     
                        if (++pos == end) 
                        { 
                            while (--sp != 0) 
                            { 
                                db.pool.unfix(pg);
                                pos = posStack[sp-1];
                                pg = db.getPage(pageStack[sp-1]);
                                if (++pos <= BtreePage.getnItems(pg)) 
                                {
                                    posStack[sp-1] = pos;
                                    do 
                                    { 
                                        int pageId = BtreePage.getKeyStrOid(pg, pos);
                                        db.pool.unfix(pg);
                                        pg = db.getPage(pageId);
                                        end = BtreePage.getnItems(pg);
                                        pageStack[sp] = pageId;
                                        posStack[sp] = pos = 0;
                                    } while (++sp < pageStack.Length);
                                    break;
                                }
                            }
                        } 
                        else 
                        { 
                            posStack[sp-1] = pos;
                        }
                        if (sp != 0 && till != null && -tree.compareByteArrays(till, pg, pos) >= till.inclusion) 
                        { 
                            sp = 0;
                        }
                    } 
                    else 
                    { // descent order
                        if (--pos < 0) 
                        { 
                            while (--sp != 0) 
                            { 
                                db.pool.unfix(pg);
                                pos = posStack[sp-1];
                                pg = db.getPage(pageStack[sp-1]);
                                if (--pos >= 0) 
                                {
                                    posStack[sp-1] = pos;
                                    do 
                                    { 
                                        int pageId = BtreePage.getKeyStrOid(pg, pos);
                                        db.pool.unfix(pg);
                                        pg = db.getPage(pageId);
                                        pageStack[sp] = pageId;
                                        posStack[sp] = pos = BtreePage.getnItems(pg);
                                    } while (++sp < pageStack.Length);
                                    posStack[sp-1] = --pos;
                                    break;
                                }
                            }
                        } 
                        else 
                        { 
                            posStack[sp-1] = pos;
                        }
                        if (sp != 0 && from != null && tree.compareByteArrays(from, pg, pos) >= from.inclusion) 
                        { 
                            sp = 0;
                        }                    
                    }
                } 
                else 
                { // scalar type
                    if (order == IterationOrder.AscentOrder) 
                    {                     
                        if (++pos == end) 
                        { 
                            while (--sp != 0) 
                            { 
                                db.pool.unfix(pg);
                                pos = posStack[sp-1];
                                pg = db.getPage(pageStack[sp-1]);
                                if (++pos <= BtreePage.getnItems(pg)) 
                                {
                                    posStack[sp-1] = pos;
                                    do 
                                    { 
                                        int pageId = BtreePage.getReference(pg, BtreePage.maxItems-1-pos);
                                        db.pool.unfix(pg);
                                        pg = db.getPage(pageId);
                                        end = BtreePage.getnItems(pg);
                                        pageStack[sp] = pageId;
                                        posStack[sp] = pos = 0;
                                    } while (++sp < pageStack.Length);
                                    break;
                                }
                            }
                        } 
                        else 
                        { 
                            posStack[sp-1] = pos;
                        }
                        if (sp != 0 && till != null && -BtreePage.compare(till, pg, pos) >= till.inclusion) 
                        { 
                            sp = 0;
                        }
                    } 
                    else 
                    { // descent order
                        if (--pos < 0) 
                        { 
                            while (--sp != 0) 
                            { 
                                db.pool.unfix(pg);
                                pos = posStack[sp-1];
                                pg = db.getPage(pageStack[sp-1]);
                                if (--pos >= 0) 
                                {
                                    posStack[sp-1] = pos;
                                    do 
                                    { 
                                        int pageId = BtreePage.getReference(pg, BtreePage.maxItems-1-pos);
                                        db.pool.unfix(pg);
                                        pg = db.getPage(pageId);
                                        pageStack[sp] = pageId;
                                        posStack[sp] = pos = BtreePage.getnItems(pg);
                                    } while (++sp < pageStack.Length);
                                    posStack[sp-1] = --pos;
                                    break;
                                }
                            }
                        } 
                        else 
                        { 
                            posStack[sp-1] = pos;
                        }
                        if (sp != 0 && from != null && BtreePage.compare(from, pg, pos) >= from.inclusion) 
                        { 
                            sp = 0;
                        }                    
                    }
                }
                if (db.concurrentIterator && sp != 0) 
                { 
                    nextKey = getCurrentKey(pg, pos);
                }
                db.pool.unfix(pg);
            }
            
 
            protected StorageImpl     db;
            protected int[]           pageStack;
            protected int[]           posStack;
#if USE_GENERICS
            protected Btree<K,V>      tree;
#else
            protected Btree           tree;
#endif
            protected int             sp;
            protected int             end;
            protected int             oid;
            protected Key             from;
            protected Key             till;
            private   BtreeKey        nextKey;
            protected bool            hasCurrent;
            protected IterationOrder  order;
            protected ClassDescriptor.FieldType type;
            protected int             updateCounter;
        }


        class BtreeDictionarySelectionEnumerator : BtreeSelectionEnumerator, IDictionaryEnumerator 
        { 
#if USE_GENERICS
            internal BtreeDictionarySelectionEnumerator(Btree<K,V> tree, Key from, Key till, IterationOrder order) 
#else
            internal BtreeDictionarySelectionEnumerator(Btree tree, Key from, Key till, IterationOrder order) 
#endif
                : base(tree, from, till, order)
            {}
               
            protected override void getCurrent(Page pg, int pos)
            {
                base.getCurrent(pg, pos);
                key = tree.unpackKey(db, pg, pos);
            }
             
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
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return new DictionaryEntry(key, db.lookupObject(oid, null));
                }
            }

            public object Key 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return key;
                }
            }

            public object Value 
            {
                get 
                {
                    if (!hasCurrent) 
                    { 
                        throw new InvalidOperationException();
                    }
                    return db.lookupObject(oid, null);
                }
            }

            protected object key;
        }

        class BtreeDictionaryStartFromEnumerator : BtreeDictionarySelectionEnumerator
        { 
#if USE_GENERICS
            internal BtreeDictionaryStartFromEnumerator(Btree<K,V> tree, int start, IterationOrder order) 
#else
            internal BtreeDictionaryStartFromEnumerator(Btree tree, int start, IterationOrder order) 
#endif
                : base(tree, null, null, order)
            {
                this.start = start;
                Reset();
            }

            public override void Reset() 
            {
                base.Reset();
                int skip = (order == IterationOrder.AscentOrder) ? start : tree.nElems - start - 1;
                while (--skip >= 0 && MoveNext()); 
            }

            int start;
        }               
                    
#if USE_GENERICS        
        public V First
        {
            get
            {
                IEnumerator<V> e = GetEnumerator(null, null, IterationOrder.AscentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }

        public V Last
        {
            get
            {
                IEnumerator<V> e = GetEnumerator(null, null, IterationOrder.DescentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }
#else
        public object First
        {
            get
            {
                IEnumerator e = GetEnumerator(null, null, IterationOrder.AscentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }

        public object Last
        {
            get
            {
                IEnumerator e = GetEnumerator(null, null, IterationOrder.DescentOrder);
                return e.MoveNext() ? e.Current : null;
            }
        }
#endif

#if USE_GENERICS        
        IEnumerable GenericIndex.Range(Key from, Key till, IterationOrder order) 
        {
            return (IEnumerable)Range(from, till, order);
        }

        public IEnumerator<V> GetEnumerator(Key from, Key till, IterationOrder order) 
#else
        public IEnumerator GetEnumerator(Key from, Key till, IterationOrder order) 
#endif
        {
            return Range(from, till, order).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(K from, K till, IterationOrder order) 
#else
        public IEnumerator GetEnumerator(object from, object till, IterationOrder order) 
#endif
        {
            return Range(from, till, order).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(Key from, Key till) 
#else
        public IEnumerator GetEnumerator(Key from, Key till) 
#endif
        {
            return Range(from, till).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(K from, K till) 
#else
        public IEnumerator GetEnumerator(object from, object till) 
#endif
        {
            return Range(from, till).GetEnumerator();
        }

#if USE_GENERICS        
        public IEnumerator<V> GetEnumerator(string prefix) 
#else
        public IEnumerator GetEnumerator(string prefix) 
#endif
        {
            return StartsWith(prefix).GetEnumerator();
        }

#if USE_GENERICS
        public IEnumerable<V> Reverse()
#else
        public IEnumerable Reverse()
#endif
        { 
            return new BtreeSelectionEnumerable(this, null, null, IterationOrder.DescentOrder);
        }


#if USE_GENERICS        
        public virtual IEnumerable<V> Range(Key from, Key till, IterationOrder order) 
#else
        public virtual IEnumerable Range(Key from, Key till, IterationOrder order) 
#endif
        { 
            return new BtreeSelectionEnumerable(this, checkKey(from), checkKey(till), order);
        }

#if USE_GENERICS        
        public virtual IEnumerable<V> Range(Key from, Key till) 
#else
        public virtual IEnumerable Range(Key from, Key till) 
#endif
        { 
            return Range(from, till, IterationOrder.AscentOrder);
        }
            
#if USE_GENERICS        
        public IEnumerable<V> Range(K from, K till, IterationOrder order) 
#else
        public IEnumerable Range(object from, object till, IterationOrder order) 
#endif
        { 
            return Range(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till), order);
        }

#if USE_GENERICS        
        public IEnumerable<V> Range(K from, K till) 
#else
        public IEnumerable Range(object from, object till) 
#endif
        { 
            return Range(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till), IterationOrder.AscentOrder);
        }
 
#if USE_GENERICS        
        IEnumerable GenericIndex.StartsWith(string prefix) 
        {
            return (IEnumerable)StartsWith(prefix);
        }

        public IEnumerable<V> StartsWith(string prefix) 
#else
        public IEnumerable StartsWith(string prefix) 
#endif
        {
            return StartsWith(prefix, IterationOrder.AscentOrder); 
        }

#if USE_GENERICS        
        IEnumerable GenericIndex.StartsWith(string prefix, IterationOrder order) 
        {
            return (IEnumerable)StartsWith(prefix, order);
        }

        public IEnumerable<V> StartsWith(string prefix, IterationOrder order) 
#else
        public IEnumerable StartsWith(string prefix, IterationOrder order) 
#endif
        { 
            return Range(new Key(prefix), 
                         new Key(prefix + Char.MaxValue, false), order);
        }
 
        public virtual IDictionaryEnumerator GetDictionaryEnumerator(Key from, Key till, IterationOrder order) 
        { 
            return new BtreeDictionarySelectionEnumerator(this, checkKey(from), checkKey(till), order);
        }

        public virtual int IndexOf(Key key)     
        { 
             PersistentEnumerator iterator = (PersistentEnumerator)GetEnumerator(null, key, IterationOrder.DescentOrder);
             int i;
             for (i = -1; iterator.MoveNext(); i++);
             return i;
        }

#if USE_GENERICS        
        public V GetAt(int i)
#else
        public object GetAt(int i)
#endif
        {
            IDictionaryEnumerator e;
            if (i < 0 || i >= nElems)
            {
                throw new IndexOutOfRangeException("Position " + i + ", index size "  + nElems);
            }            
            if (i <= (nElems/2)) 
            {
                e = GetDictionaryEnumerator(null, null, IterationOrder.AscentOrder);
                while (i-- >= 0) 
                { 
                      e.MoveNext();
                }
            }
            else
            {
                e = GetDictionaryEnumerator(null, null, IterationOrder.DescentOrder);
                i -= nElems;
                while (++i <= 0) 
                { 
                      e.MoveNext();
                }
            }
#if USE_GENERICS        
            return (V)e.Value;   
#else
            return e.Value;   
#endif
        }

        public IDictionaryEnumerator GetDictionaryEnumerator(int start, IterationOrder order) 
        {
            return new BtreeDictionaryStartFromEnumerator(this, start, order);
        }
    }
}