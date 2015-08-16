namespace Perst.Impl
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Perst;
	
    public class XMLImporter
    {
        public XMLImporter(StorageImpl storage, System.IO.TextReader reader)
        {
            this.storage = storage;
            scanner = new XMLScanner(reader);
            classMap = new Dictionary<string,Type>();
        }
		
        public virtual void  importDatabase()
        {
            if (scanner.scan() != XMLScanner.Token.LT || scanner.scan() != XMLScanner.Token.IDENT || !scanner.Identifier.Equals("database"))
            {
                throwException("No root element");
            }
            if (scanner.scan() != XMLScanner.Token.IDENT || !scanner.Identifier.Equals("root") || scanner.scan() != XMLScanner.Token.EQ || scanner.scan() != XMLScanner.Token.SCONST || scanner.scan() != XMLScanner.Token.GT)
            {
                throwException("Database element should have \"root\" attribute");
            }
            int rootId = 0;
            try
            {
                rootId = System.Int32.Parse(scanner.String);
            }
            catch (System.FormatException)
            {
                throwException("Incorrect root object specification");
            }
            idMap = new int[rootId * 2];
            idMap[rootId] = storage.allocateId();
            storage.header.root[1 - storage.currIndex].rootObject = idMap[rootId];
			
            XMLScanner.Token tkn;
            while ((tkn = scanner.scan()) == XMLScanner.Token.LT)
            {
                if (scanner.scan() != XMLScanner.Token.IDENT)
                {
                    throwException("Element name expected");
                }
                string elemName = scanner.Identifier;
                if (elemName.StartsWith("Perst.Impl.Btree") 
                    || elemName.StartsWith("Perst.Impl.BitIndexImpl")
                    || elemName.StartsWith("Perst.Impl.PersistentSet") 
                    || elemName.StartsWith("Perst.Impl.BtreeFieldIndex") 
                    || elemName.StartsWith("Perst.Impl.BtreeCaseInsensitiveFieldIndex") 
                    || elemName.StartsWith("Perst.Impl.BtreeCompoundIndex") 
                    || elemName.StartsWith("Perst.Impl.BtreeMultiFieldIndex")
                    || elemName.StartsWith("Perst.Impl.BtreeCaseInsensitiveMultiFieldIndex"))
                {
                    createIndex(elemName);
                }
                else
                {
                    createObject(readElement(elemName));
                }
            }
            if (tkn != XMLScanner.Token.LTS || scanner.scan() != XMLScanner.Token.IDENT || !scanner.Identifier.Equals("database") || scanner.scan() != XMLScanner.Token.GT)
            {
                throwException("Root element is not closed");
            }
        }
		
        internal class XMLElement
        {
            internal XMLElement NextSibling
            {
                get
                {
                    return next;
                }
				
            }
            internal int Counter
            {
                get
                {
                    return counter;
                }
				
            }
            internal long IntValue
            {
                get
                {
                    return ivalue;
                }
				
                set
                {
                    ivalue = value;
                    valueType = XMLValueType.INT_VALUE;
                }
				
            }
            internal double RealValue
            {
                get
                {
                    return rvalue;
                }
				
                set
                {
                    rvalue = value;
                    valueType = XMLValueType.REAL_VALUE;
                }
				
            }
            internal String StringValue
            {
                get
                {
                    return svalue;
                }
				
                set
                {
                    svalue = value;
                    valueType = XMLValueType.STRING_VALUE;
                }
				
            }
            internal String Name
            {
                get
                {
                    return name;

                }
            }

            private XMLElement   next;
            private XMLElement   prev;
            private String       name;
            private Dictionary<string,XMLElement> siblings;
            private Dictionary<string,string>     attributes;
            private String       svalue;
            private long         ivalue;
            private double       rvalue;
            private XMLValueType valueType;
            private int          counter;
			
            enum XMLValueType 
            { 
                NO_VALUE,
                STRING_VALUE,
                INT_VALUE,
                REAL_VALUE,
                NULL_VALUE
            }
			
            internal XMLElement(string name)
            {
                this.name = name;
                valueType = XMLValueType.NO_VALUE;
            }
			
            internal void  addSibling(XMLElement elem)
            {
                if (siblings == null)
                {
                    siblings = new Dictionary<string,XMLElement>();
                }
                XMLElement head;
                if (siblings.TryGetValue(elem.name, out head))
                {
                    elem.next = null;
                    elem.prev = head.prev;
                    head.prev.next = elem;
                    head.prev = elem;
                    head.counter += 1;
                }
                else
                {
                    elem.prev = elem;
                    siblings[elem.name] = elem;
                    elem.counter = 1;
                }
            }
			
            internal void  addAttribute(string name, string val)
            {
                if (attributes == null)
                {
                    attributes = new Dictionary<string,string>();
                }
                attributes[name] = val;
            }
			
            internal XMLElement getSibling(string name)
            {
                XMLElement elem = null;
                if (siblings != null)
                {
                    siblings.TryGetValue(name, out elem);
                }
                return elem;
            }
			
            static ICollection<XMLElement> EMPTY_COLLECTION = new List<XMLElement>();
        
            internal ICollection<XMLElement> Siblings 
            { 
                get
                {
                    return (siblings != null) ? siblings.Values : EMPTY_COLLECTION;
                }           
            }

            internal XMLElement FirstSibling
            {
                get
                {
                    foreach (XMLElement e in Siblings) 
                    { 
                        return e;
                    }
                    return null;
                }
            } 
				
            internal string getAttribute(string name)
            {
                string value = null;
                if (attributes != null)
                {
                    attributes.TryGetValue(name, out value);
                }
                return value;
            }
			
			
			
			
            internal void  setNullValue()
            {
                valueType = XMLValueType.NULL_VALUE;
            }
			
            internal bool isIntValue()
            {
                return valueType == XMLValueType.INT_VALUE;
            }
			
            internal bool isRealValue()
            {
                return valueType == XMLValueType.REAL_VALUE;
            }
			
            internal bool isStringValue()
            {
                return valueType == XMLValueType.STRING_VALUE;
            }
			
            internal bool isNullValue()
            {
                return valueType == XMLValueType.NULL_VALUE;
            }
        }
		
        internal string getAttribute(XMLElement elem, String name)
        {
            string val = elem.getAttribute(name);
            if (val == null)
            {
                throwException("Attribute " + name + " is not set");
            }
            return val;
        }
		
		
        internal int getIntAttribute(XMLElement elem, String name)
        {
            string val = elem.getAttribute(name);
            if (val == null)
            {
                throwException("Attribute " + name + " is not set");
            }
            try
            {
                return System.Int32.Parse(val);
            }
            catch (System.FormatException)
            {
                throwException("Attribute " + name + " should has integer value");
            }
            return -1;
        }
		
        internal int mapId(int id)
        {
            int oid = 0;
            if (id != 0)
            {
                if (id >= idMap.Length)
                {
                    int[] newMap = new int[id * 2];
                    Array.Copy(idMap, 0, newMap, 0, idMap.Length);
                    idMap = newMap;
                    idMap[id] = oid = storage.allocateId();
                }
                else
                {
                    oid = idMap[id];
                    if (oid == 0)
                    {
                        idMap[id] = oid = storage.allocateId();
                    }
                }
            }
            return oid;
        }
		
        internal ClassDescriptor.FieldType mapType(string signature)
        {
            try
            {
                return (ClassDescriptor.FieldType)ClassDescriptor.parseEnum(typeof(ClassDescriptor.FieldType), signature);
            } 
            catch (ArgumentException) 
            {
                throwException("Bad type");
                return ClassDescriptor.FieldType.tpObject;
            }
        }

        Key createCompoundKey(ClassDescriptor.FieldType[] types, String[] values) 
        {
            ByteBuffer buf = new ByteBuffer();
            int dst = 0;

            for (int i = 0; i < types.Length; i++) 
            { 
                String val = values[i];
                switch (types[i]) 
                { 
                    case ClassDescriptor.FieldType.tpBoolean:
                        dst = buf.packBool(dst, Int32.Parse(val) != 0);
                        break;

                    case ClassDescriptor.FieldType.tpByte:
                    case ClassDescriptor.FieldType.tpSByte:
                        dst = buf.packI1(dst, Int32.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpChar:
                    case ClassDescriptor.FieldType.tpShort:
                    case ClassDescriptor.FieldType.tpUShort:
                        dst = buf.packI2(dst, Int32.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpInt:
                        dst = buf.packI4(dst, Int32.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpEnum:
                    case ClassDescriptor.FieldType.tpUInt:
                        dst = buf.packI4(dst, (int)UInt32.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpObject:
                    case ClassDescriptor.FieldType.tpOid:
                        dst = buf.packI4(dst, mapId((int)UInt32.Parse(val)));
                        break;

                    case ClassDescriptor.FieldType.tpLong:
                        dst = buf.packI8(dst, Int64.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpULong:
                        dst = buf.packI8(dst, (long)UInt64.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpDate:
                        dst = buf.packDate(dst, DateTime.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpFloat:
                        dst = buf.packF4(dst, Single.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpDouble:
                        dst = buf.packF8(dst, Double.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpDecimal: 
                        dst = buf.packDecimal(dst, Decimal.Parse(val));
                        break;

                    case ClassDescriptor.FieldType.tpGuid: 
                        dst = buf.packGuid(dst, new Guid(val));
                        break;

                    case ClassDescriptor.FieldType.tpString:
                    case ClassDescriptor.FieldType.tpType:
                        dst = buf.packString(dst, val);
                        break;

                    case ClassDescriptor.FieldType.tpArrayOfByte:
                        buf.extend(dst + 4 + (val.Length >> 1));
                        Bytes.pack4(buf.arr, dst, val.Length >> 1);
                        dst += 4;
                        for (int j = 0, n = val.Length; j < n; j+=2) 
                        { 
                            buf.arr[dst++] = (byte)((getHexValue(val[j]) << 4) | getHexValue(val[j+1]));
                        }
                        break;
                    default:
                        throwException("Bad key type");
                        break;
                }
            }
            return new Key(buf.toArray());
        }

        Key createKey(ClassDescriptor.FieldType type, String val)
        {
            switch (type)
            {
                case ClassDescriptor.FieldType.tpBoolean: 
                    return new Key(Int32.Parse(val) != 0);
					
                case ClassDescriptor.FieldType.tpByte: 
                    return new Key(Byte.Parse(val));
					
                case ClassDescriptor.FieldType.tpSByte: 
                    return new Key(SByte.Parse(val));
					
                case ClassDescriptor.FieldType.tpChar: 
                    return new Key((char)Int32.Parse(val));
					
                case ClassDescriptor.FieldType.tpShort: 
                    return new Key(Int16.Parse(val));
					
                case ClassDescriptor.FieldType.tpUShort: 
                    return new Key(UInt16.Parse(val));
					
                case ClassDescriptor.FieldType.tpInt: 
                    return new Key(Int32.Parse(val));
					
                case ClassDescriptor.FieldType.tpUInt: 
                case ClassDescriptor.FieldType.tpEnum:
                    return new Key(UInt32.Parse(val));
					
                case ClassDescriptor.FieldType.tpOid: 
                    return new Key(ClassDescriptor.FieldType.tpOid, mapId((int)UInt32.Parse(val)));
                case ClassDescriptor.FieldType.tpObject: 
                    return new Key(new PersistentStub(storage, mapId((int)UInt32.Parse(val))));
					
                case ClassDescriptor.FieldType.tpLong: 
                    return new Key(Int64.Parse(val));
					
                case ClassDescriptor.FieldType.tpULong: 
                    return new Key(UInt64.Parse(val));
					
                case ClassDescriptor.FieldType.tpFloat: 
                    return new Key(Single.Parse(val));
					
                case ClassDescriptor.FieldType.tpDouble: 
                    return new Key(Double.Parse(val));
					
                case ClassDescriptor.FieldType.tpDecimal: 
                    return new Key(Decimal.Parse(val));

                case ClassDescriptor.FieldType.tpGuid: 
                    return new Key(new Guid(val));

                case ClassDescriptor.FieldType.tpString: 
                    return new Key(val);
					
                case ClassDescriptor.FieldType.tpArrayOfByte:
                {
                    byte[] buf = new byte[val.Length >> 1];
                    for (int i = 0; i < buf.Length; i++) 
                    { 
                        buf[i] = (byte)((getHexValue(val[i*2]) << 4) | getHexValue(val[i*2+1]));
                    }
                    return new Key(buf);
                }

                case ClassDescriptor.FieldType.tpDate: 
                    return new Key(DateTime.Parse(val));
					
                default: 
                    throwException("Bad key type");
                    break;
					
            }
            return null;
        }
		
        internal int parseInt(String str)
        {
            return Int32.Parse(str);
        }
		
        internal Type findClassByName(String className) 
        {
            Type type = null;
            if (!classMap.TryGetValue(className, out type))
            {
                type = ClassDescriptor.lookup(storage, className);
                classMap[className] = type;
            }
            return type;
        }

        internal void  createIndex(String indexType)
        {
            XMLScanner.Token tkn;
            int oid = 0;
            bool     unique = false;
            String   className = null;
            String   fieldName = null;
            String[] fieldNames = null;
            long     autoinc = 0;
            String   type = null;
            ClassDescriptor.FieldType[] types = null;

            while ((tkn = scanner.scan()) == XMLScanner.Token.IDENT)
            {
                string attrName = scanner.Identifier;
                if (scanner.scan() != XMLScanner.Token.EQ || scanner.scan() != XMLScanner.Token.SCONST)
                {
                    throwException("Attribute value expected");
                }
                string attrValue = scanner.String;
                if (attrName.Equals("id"))
                {
                    oid = mapId(parseInt(attrValue));
                }
                else if (attrName.Equals("unique"))
                {
                    unique = parseInt(attrValue) != 0;
                }
                else if (attrName.Equals("class"))
                {
                    className = attrValue;
                }
                else if (attrName.Equals("type"))
                {
                    type = attrValue;
                }
                else if (attrName.Equals("autoinc"))
                {
                    autoinc = parseInt(attrValue);
                }
                else if (attrName.Equals("field"))
                {
                    fieldName = attrValue;
                }
                else if (attrName.StartsWith("field"))
                {
                    int fieldNo = Int32.Parse(attrName.Substring(5));
                    if (fieldNames == null || fieldNames.Length <= fieldNo) 
                    { 
                        String[] newFieldNames = new String[fieldNo+1];
                        if (fieldNames != null) 
                        { 
                            Array.Copy(fieldNames, 0, newFieldNames, 0, fieldNames.Length);
                        }
                        fieldNames = newFieldNames;
                     }
                     fieldNames[fieldNo] = attrValue;
                }
                else if (attrName.StartsWith("type"))
                {
                    int typeNo = Int32.Parse(attrName.Substring(4));
                    if (types == null || types.Length <= typeNo) 
                    { 
                        ClassDescriptor.FieldType[] newTypes = new ClassDescriptor.FieldType[typeNo+1];
                        if (types != null) 
                        { 
                            Array.Copy(types, 0, newTypes, 0, types.Length);
                        }
                        types = newTypes;
                     }
                     types[typeNo] = mapType(attrValue);
                }
            }
            if (tkn != XMLScanner.Token.GT)
            {
                throwException("Unclosed element tag");
            }
            if (oid == 0)
            {
                throwException("ID is not specified or index");
            }
#if USE_GENERICS
            ClassDescriptor desc = storage.getClassDescriptor(findClassByName(indexType));
            Btree btree = (Btree)desc.newInstance();
#else
            Btree btree = null;
#endif            
            if (className != null)
            {
                Type cls = findClassByName(className);
                if (fieldName != null) 
                { 
#if USE_GENERICS
                    btree.init(cls, null, new string[]{fieldName}, unique, autoinc);
#else
                    btree = indexType.StartsWith("Perst.Impl.BtreeCaseInsensitiveFieldIndex")
                        ? (Btree)new BtreeCaseInsensitiveFieldIndex(cls, fieldName, unique, autoinc)
                        : (Btree)new BtreeFieldIndex(cls, fieldName, unique, autoinc);
#endif
                } 
                else if (fieldNames != null) 
                { 
#if USE_GENERICS
                    btree.init(cls, null, fieldNames, unique, autoinc);
#else
                    btree = indexType.StartsWith("Perst.Impl.BtreeCaseInsensitiveMultiFieldIndex")
                        ? (Btree)new BtreeCaseInsensitiveMultiFieldIndex(cls, fieldNames, unique)
                        : (Btree)new BtreeMultiFieldIndex(cls, fieldNames, unique);
#endif
                } 
                else
                {
                    throwException("Field name is not specified for field index");
                }
            }
            else
            {
                if (types != null) 
                { 
#if USE_GENERICS
                    btree.init(null, types, null, unique, autoinc);
#else
                    btree = new BtreeCompoundIndex(types, unique);
#endif
                } 
                else if (type == null)
                {
                    if (indexType.StartsWith("Perst.Impl.PersistentSet")) 
                    { 
#if !USE_GENERICS
                        btree = new PersistentSet(unique);
#endif
                    } 
                    else 
                    {
                        throwException("Key type is not specified for index");
                    }
                } 
                else 
                {
                    if (indexType.StartsWith("org.garret.perst.impl.BitIndexImpl")) 
                    { 
#if !USE_GENERICS
                        btree = new BitIndexImpl();
#endif
                    } 
                    else 
                    { 
#if USE_GENERICS
                        btree.init(null, new ClassDescriptor.FieldType[]{mapType(type)}, null, unique, autoinc);
#else
                        btree = new Btree(mapType(type), unique);
#endif
                    }
                }
            }
            storage.AssignOid(btree, oid, false);
			
            while ((tkn = scanner.scan()) == XMLScanner.Token.LT)
            {
                if (scanner.scan() != XMLScanner.Token.IDENT || !scanner.Identifier.Equals("ref"))
                {
                    throwException("<ref> element expected");
                }
                XMLElement refElem = readElement("ref");
                Key key;
                if (fieldNames != null) 
                { 
                    String[] values = new String[fieldNames.Length];                
#if USE_GENERICS
                    types = btree.FieldTypes;
#else
                    types = ((BtreeMultiFieldIndex)btree).types;
#endif
                    for (int i = 0; i < values.Length; i++) 
                    { 
                        values[i] = getAttribute(refElem, "key"+i);
                    }
                    key = createCompoundKey(types, values);
                } 
                else if (types != null) 
                { 
                    String[] values = new String[fieldNames.Length];                
                    for (int i = 0; i < values.Length; i++) 
                    { 
                        values[i] = getAttribute(refElem, "key"+i);
                    }
                    key = createCompoundKey(types, values);
                } 
                else 
                { 
                    key = createKey(btree.FieldType, getAttribute(refElem, "key"));
                }
                object obj = new PersistentStub(storage, mapId(getIntAttribute(refElem, "id")));
                btree.insert(key, obj, false);
            }
            if (tkn != XMLScanner.Token.LTS 
                || scanner.scan() != XMLScanner.Token.IDENT 
                || !scanner.Identifier.Equals(indexType) 
                || scanner.scan() != XMLScanner.Token.GT)
            {
                throwException("Element is not closed");
            }
#if USE_GENERICS
            ByteBuffer buf = new ByteBuffer();
            buf.extend(ObjectHeader.Sizeof);
            int size = storage.packObject(btree, desc, ObjectHeader.Sizeof, buf);
            byte[] data = buf.arr;
            ObjectHeader.setSize(data, 0, size);
            ObjectHeader.setType(data, 0, desc.Oid);
#else
            byte[] data = storage.packObject(btree, false);
            int size = ObjectHeader.getSize(data, 0);
#endif
            long pos = storage.allocate(size, 0);
            storage.setPos(oid, pos | StorageImpl.dbModifiedFlag);
			
            storage.pool.put(pos & ~ StorageImpl.dbFlagsMask, data, size);
        }
		
        internal void  createObject(XMLElement elem)
        {
            ClassDescriptor desc = storage.getClassDescriptor(findClassByName(elem.Name));
            int oid = mapId(getIntAttribute(elem, "id"));
            ByteBuffer buf = new ByteBuffer(storage, null, false);
            int offs = ObjectHeader.Sizeof;
            buf.extend(offs);
			
            offs = packObject(elem, desc, offs, buf);
			
            ObjectHeader.setSize(buf.arr, 0, offs);
            ObjectHeader.setType(buf.arr, 0, desc.Oid);
			
            long pos = storage.allocate(offs, 0);
            storage.setPos(oid, pos | StorageImpl.dbModifiedFlag);
            storage.pool.put(pos, buf.arr, offs);
        }
		
        internal int getHexValue(char ch)
        {
            if (ch >= '0' && ch <= '9')
            {
                return ch - '0';
            }
            else if (ch >= 'A' && ch <= 'F')
            {
                return ch - 'A' + 10;
            }
            else if (ch >= 'a' && ch <= 'f')
            {
                return ch - 'a' + 10;
            }
            else
            {
                throwException("Bad hexadecimal constant");
            }
            return -1;
        }
		
        internal int importBinary(XMLElement elem, int offs, ByteBuffer buf, String fieldName) 
        { 
            if (elem == null || elem.isNullValue()) 
            {
                offs = buf.packI4(offs, -1);
            } 
            else if (elem.isStringValue()) 
            {
                string hexStr = elem.StringValue;
                int len = hexStr.Length;
                buf.extend(offs + 4 + len/2);
                Bytes.pack4(buf.arr, offs, len/2);
                offs += 4;
                for (int j = 0; j < len; j += 2) 
                { 
                    buf.arr[offs++] = (byte)((getHexValue(hexStr[j]) << 4) | getHexValue(hexStr[j+1]));
                }
            } 
            else 
            { 
                XMLElement refElem = elem.getSibling("ref");
                if (refElem != null) 
                { 
                    buf.extend(offs + 4);
                    Bytes.pack4(buf.arr, offs, mapId(getIntAttribute(refElem, "id")));
                    offs += 4;
                } 
                else 
                { 
                    XMLElement item = elem.getSibling("element");
                    int len = (item == null) ? 0 : item.Counter; 
                    buf.extend(offs + 4 + len);
                    Bytes.pack4(buf.arr, offs, len);
                    offs += 4;
                    while (--len >= 0) 
                    { 
                        if (item.isIntValue()) 
                        { 
                            buf.arr[offs] = (byte)item.IntValue;
                        } 
                        else if (item.isRealValue()) 
                        { 
                            buf.arr[offs] = (byte)item.RealValue;
                        } 
                        else 
                        {
                            throwException("Conversion for field " + fieldName + " is not possible");
                        }
                        item = item.NextSibling;
                        offs += 1;
                    }
                }
            }
            return offs;
        }

        internal int importRef(XMLElement elem, int offs, ByteBuffer buf) 
        {
            int oid = 0;
            if (elem != null) {
                if (elem.isStringValue()) {
                    String str = elem.StringValue;
                    offs = buf.packI4(offs, -1-(int)ClassDescriptor.FieldType.tpString);
                    return buf.packString(offs, str);
                } else {
                    XMLElement value = elem.FirstSibling;
                    if (value == null) { 
                        throwException("object reference expected");
                    }
                    string name = value.Name;
                    if (name == "scalar") { 
                        int tid = getIntAttribute(value, "type");
                        string hexStr = getAttribute(value, "value");
                        int len = hexStr.Length;
                        buf.extend(offs + 4 + len/2);
                        Bytes.pack4(buf.arr, offs, -1-tid);
                        offs += 4;
                        if (tid == (int)ClassDescriptor.FieldType.tpCustom) 
                        { 
                            object obj = storage.serializer.Parse(hexStr);  
                            storage.serializer.Pack(obj, buf.GetWriter());                    
                            offs = buf.used;
                        } 
                        else 
                        { 
                            for (int j = 0; j < len; j += 2) 
                            { 
                                 buf.arr[offs++] = (byte)((getHexValue(hexStr[j]) << 4) | getHexValue(hexStr[j+1]));
                             }
                        }
                        return offs;
                    } else if (name == "type") { 
                        string typeName = getAttribute(value, "name");
                        offs = buf.packI4(offs, -1-(int)ClassDescriptor.FieldType.tpType);
                        return buf.packString(offs, typeName);                        
                    } else if (name == "ref") { 
                        oid = mapId(getIntAttribute(value, "id"));
                    } else { 
                        ClassDescriptor desc = storage.getClassDescriptor(findClassByName(name));
                        offs = buf.packI4(offs, -(int)ClassDescriptor.FieldType.tpValueTypeBias - desc.Oid);
                        if (desc.isCollection) { 
                            XMLElement item = value.getSibling("element");
                            int len = (item == null) ? 0 : item.Counter; 
                            offs = buf.packI4(offs, len);
                            while (--len >= 0) { 
                                offs = importRef(item, offs, buf);
                                item = item.NextSibling;
                            }
                        } else if (desc.isDictionary) { 
                            XMLElement item = value.getSibling("element");
                            int len = (item == null) ? 0 : item.Counter; 
                            offs = buf.packI4(offs, len);
                            while (--len >= 0) { 
                                XMLElement key = item.getSibling("key");
                                offs = importRef(key, offs, buf);
                                XMLElement val = item.getSibling("value");
                                offs = importRef(val, offs, buf);
                                item = item.NextSibling;
                            }
                        } else { 
                            offs = packObject(value, desc, offs, buf);
                        }
                        return offs;
                    }
                }
            }
            return buf.packI4(offs, oid);
        }
    
        internal int packObject(XMLElement objElem, ClassDescriptor desc, int offs, ByteBuffer buf)
        {
            ClassDescriptor.FieldDescriptor[] flds = desc.allFields;
            for (int i = 0, n = flds.Length; i < n; i++)
            {
                ClassDescriptor.FieldDescriptor fd = flds[i];
                FieldInfo f = fd.field;
                String fieldName = fd.fieldName;
                XMLElement elem = (objElem != null)?objElem.getSibling(fieldName):null;
                ClassDescriptor.FieldType type = fd.type;				
                switch (fd.type)
                {
#if NET_FRAMEWORK_20
                    case ClassDescriptor.FieldType.tpNullableByte: 
                    case ClassDescriptor.FieldType.tpNullableSByte: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 2);
                                buf.arr[offs++] = (byte) 1;
                                buf.arr[offs++] = (byte) elem.IntValue;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 2);
                                buf.arr[offs++] = (byte) 1;
                                buf.arr[offs++] = (byte) elem.RealValue;
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        } 
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpNullableBoolean: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 2);
                                buf.arr[offs++] = (byte) 1;
                                buf.arr[offs++] = (byte) (elem.IntValue != 0?1:0);
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 2);
                                buf.arr[offs++] = (byte) 1;
                                buf.arr[offs++] = (byte) (elem.RealValue != 0.0?1:0);
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        } 
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpNullableShort: 
                    case ClassDescriptor.FieldType.tpNullableUShort: 
                    case ClassDescriptor.FieldType.tpNullableChar: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 3);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack2(buf.arr, offs, (short) elem.IntValue);
                                offs += 2;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 3);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack2(buf.arr, offs, (short) elem.RealValue);
                                offs += 2;
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        } 
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpNullableEnum: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 5);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack4(buf.arr, offs, (int) elem.IntValue);        
                                offs += 4;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 5);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack4(buf.arr, offs, (int) elem.RealValue);
                                offs += 4;
                            }
                            else if (elem.isStringValue()) 
                            {
                                try 
                                {
                                    buf.extend(offs + 5);
                                    buf.arr[offs++] = (byte) 1;
                                    Bytes.pack4(buf.arr, offs, (int)ClassDescriptor.parseEnum(f.FieldType, elem.StringValue));
                                } 
                                catch (ArgumentException)
                                {
                                    throwException("Invalid enum value");
                                }
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					

                    case ClassDescriptor.FieldType.tpNullableInt: 
                    case ClassDescriptor.FieldType.tpNullableUInt: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 5);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack4(buf.arr, offs, (int) elem.IntValue);
                                offs += 4;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 5);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack4(buf.arr, offs, (int) elem.RealValue);
                                offs += 4;
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpNullableLong: 
                    case ClassDescriptor.FieldType.tpNullableULong: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 9);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack8(buf.arr, offs, elem.IntValue);
                                offs += 9;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 9);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack8(buf.arr, offs, (long) elem.RealValue);
                                offs += 9;
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpNullableFloat: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 5);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.packF4(buf.arr, offs, (float)elem.IntValue);
                                offs += 4;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 5);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.packF4(buf.arr, offs, (float) elem.RealValue);
                                offs += 4;
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpNullableDouble: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 9);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.packF8(buf.arr, offs, (double) elem.IntValue);
                                offs += 8;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.extend(offs + 9);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.packF8(buf.arr, offs, elem.RealValue);
                                offs += 8;
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpNullableDecimal: 
                        if (elem != null)
                        {
                            decimal d = 0;
                            if (elem.isIntValue())
                            {
                                d = elem.IntValue;
                            }
                            else if (elem.isRealValue())
                            {
                                d = (decimal)elem.RealValue;
                            }
                            else if (elem.isStringValue())
                            {
                                try 
                                { 
                                    d = Decimal.Parse(elem.StringValue);
                                } 
                                catch (FormatException) 
                                {
                                    throwException("Invalid date");
                                }
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                                continue;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                            buf.extend(offs + 17);
                            buf.arr[offs++] = (byte) 1;
                            Bytes.packDecimal(buf.arr, offs, d);
                            offs += 16;
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
  
                    case ClassDescriptor.FieldType.tpNullableGuid: 
                        if (elem != null)
                        {
                            if (elem.isStringValue())
                            {
                                buf.extend(offs + 17);
                                buf.arr[offs++] = (byte) 1;
                                Guid guid = new Guid(elem.StringValue);
                                byte[] bits = guid.ToByteArray();
                                Array.Copy(bits, 0, buf.arr, offs, 16);
                                offs += 16;
                            }
                            else if (elem.isNullValue())
                            {
                                 buf.extend(offs + 1);
                                 buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;
                    
                    case ClassDescriptor.FieldType.tpNullableDate: 
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.extend(offs + 9);
                                buf.arr[offs++] = (byte) 1;
                                Bytes.pack8(buf.arr, offs, elem.IntValue);
                                offs += 8;
                            }
                            else if (elem.isStringValue())
                            {
                                try 
                                { 
                                    buf.extend(offs + 9);
                                    buf.arr[offs++] = (byte) 1;
                                    Bytes.packDate(buf.arr, offs, DateTime.Parse(elem.StringValue));
                                    offs += 8;
                                } 
                                catch (FormatException) 
                                {
                                    throwException("Invalid date");
                                }
                            }
                            else if (elem.isNullValue())
                            {
                                buf.extend(offs + 1);
                                buf.arr[offs++] = (byte) 0;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        else 
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        }
                        continue;

                    case ClassDescriptor.FieldType.tpNullableValue: 
                        if (elem != null || elem.isNullValue())
                        {
                            buf.extend(offs + 1);
                            buf.arr[offs++] = (byte) 0;
                        } 
                        else                         
                        { 
                            offs = packObject(elem, fd.valueDesc, offs, buf);
                        }
                        continue;
#endif

                    case ClassDescriptor.FieldType.tpByte: 
                    case ClassDescriptor.FieldType.tpSByte: 
                        buf.extend(offs + 1);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.arr[offs] = (byte) elem.IntValue;
                            }
                            else if (elem.isRealValue())
                            {
                                buf.arr[offs] = (byte) elem.RealValue;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 1;
                        continue;
					
                    case ClassDescriptor.FieldType.tpBoolean: 
                        buf.extend(offs + 1);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                buf.arr[offs] = (byte) (elem.IntValue != 0?1:0);
                            }
                            else if (elem.isRealValue())
                            {
                                buf.arr[offs] = (byte) (elem.RealValue != 0.0?1:0);
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 1;
                        continue;
					
                    case ClassDescriptor.FieldType.tpShort: 
                    case ClassDescriptor.FieldType.tpUShort: 
                    case ClassDescriptor.FieldType.tpChar: 
                        buf.extend(offs + 2);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                Bytes.pack2(buf.arr, offs, (short) elem.IntValue);
                            }
                            else if (elem.isRealValue())
                            {
                                Bytes.pack2(buf.arr, offs, (short) elem.RealValue);
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 2;
                        continue;
					
                    case ClassDescriptor.FieldType.tpEnum: 
                        buf.extend(offs + 4);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                Bytes.pack4(buf.arr, offs, (int) elem.IntValue);
                            }
                            else if (elem.isRealValue())
                            {
                                Bytes.pack4(buf.arr, offs, (int) elem.RealValue);
                            }
                            else if (elem.isStringValue()) 
                            {
                                try
                                {
                                    Bytes.pack4(buf.arr, offs, (int)ClassDescriptor.parseEnum(f.FieldType, elem.StringValue));
                                } 
                                catch (ArgumentException)
                                {
                                    throwException("Invalid enum value");
                                }
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 4;
                        continue;
					

                    case ClassDescriptor.FieldType.tpInt: 
                    case ClassDescriptor.FieldType.tpUInt: 
                        buf.extend(offs + 4);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                Bytes.pack4(buf.arr, offs, (int) elem.IntValue);
                            }
                            else if (elem.isRealValue())
                            {
                                Bytes.pack4(buf.arr, offs, (int) elem.RealValue);
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 4;
                        continue;
					
                    case ClassDescriptor.FieldType.tpLong: 
                    case ClassDescriptor.FieldType.tpULong: 
                        buf.extend(offs + 8);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                Bytes.pack8(buf.arr, offs, elem.IntValue);
                            }
                            else if (elem.isRealValue())
                            {
                                Bytes.pack8(buf.arr, offs, (long) elem.RealValue);
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 8;
                        continue;
					
                    case ClassDescriptor.FieldType.tpFloat: 
                        buf.extend(offs + 4);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                Bytes.packF4(buf.arr, offs, (float)elem.IntValue);
                            }
                            else if (elem.isRealValue())
                            {
                                Bytes.packF4(buf.arr, offs, (float) elem.RealValue);
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 4;
                        continue;
					
                    case ClassDescriptor.FieldType.tpDouble: 
                        buf.extend(offs + 8);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                Bytes.packF8(buf.arr, offs, (double) elem.IntValue);
                            }
                            else if (elem.isRealValue())
                            {
                                Bytes.packF8(buf.arr, offs, elem.RealValue);
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 8;
                        continue;
					
                    case ClassDescriptor.FieldType.tpDecimal: 
                        buf.extend(offs + 16);
                        if (elem != null)
                        {
                            decimal d = 0;
                            if (elem.isIntValue())
                            {
                                d = elem.IntValue;
                            }
                            else if (elem.isRealValue())
                            {
                                d = (decimal)elem.RealValue;
                            }
                            else if (elem.isStringValue())
                            {
                                try 
                                { 
                                    d = Decimal.Parse(elem.StringValue);
                                } 
                                catch (FormatException) 
                                {
                                    throwException("Invalid date");
                                }
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                            Bytes.packDecimal(buf.arr, offs, d);

                        }
                        offs += 16;
                        continue;
  
                    case ClassDescriptor.FieldType.tpGuid: 
                        buf.extend(offs + 16);
                        if (elem != null)
                        {
                            if (elem.isStringValue())
                            {
                                Guid guid = new Guid(elem.StringValue);
                                byte[] bits = guid.ToByteArray();
                                Array.Copy(bits, 0, buf.arr, offs, 16);
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 16;
                        continue;
                    
                    case ClassDescriptor.FieldType.tpDate: 
                        buf.extend(offs + 8);
                        if (elem != null)
                        {
                            if (elem.isIntValue())
                            {
                                Bytes.pack8(buf.arr, offs, elem.IntValue);
                            }
                            else if (elem.isStringValue())
                            {
                                try 
                                { 
                                    Bytes.packDate(buf.arr, offs, DateTime.Parse(elem.StringValue));
                                } 
                                catch (FormatException) 
                                {
                                    throwException("Invalid date");
                                }
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                        }
                        offs += 8;
                        continue;
					
                    case ClassDescriptor.FieldType.tpString: 
                    case ClassDescriptor.FieldType.tpType: 
                        if (elem != null)
                        {
                            string val = null;
                            if (elem.isIntValue())
                            {
                                val = System.Convert.ToString(elem.IntValue);
                            }
                            else if (elem.isRealValue())
                            {
                                val = elem.RealValue.ToString();
                            }
                            else if (elem.isStringValue())
                            {
                                val = elem.StringValue;
                            }
                            else if (elem.isNullValue())
                            {
                                val = null;
                            }
                            else
                            {
                                throwException("Conversion for field " + fieldName + " is not possible");
                            }
                            offs = buf.packString(offs, val);
                            continue;
                        }
                        offs = buf.packI4(offs, -1);
                        continue;
					
                    case ClassDescriptor.FieldType.tpOid: 
                    case ClassDescriptor.FieldType.tpObject: 
                        offs = importRef(elem, offs, buf);
                        continue;
					
                    case ClassDescriptor.FieldType.tpValue: 
                        offs = packObject(elem, fd.valueDesc, offs, buf);
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfByte: 
                    case ClassDescriptor.FieldType.tpArrayOfSByte: 
                        offs = importBinary(elem, offs, buf, fieldName);
                        continue;
					
                    case ClassDescriptor.FieldType.tpCustom:
                    {
                        if (!elem.isStringValue()) 
                        {
                            throwException("text element expected");
                        }
                        String str = elem.StringValue;                    
                        object obj = storage.serializer.Parse(str);                        
                        storage.serializer.Pack(obj, buf.GetWriter());                    
                        offs = buf.used;
                        break;
                    }

                    case ClassDescriptor.FieldType.tpArrayOfBoolean: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isIntValue())
                                {
                                    buf.arr[offs] = (byte) (item.IntValue != 0?1:0);
                                }
                                else if (item.isRealValue())
                                {
                                    buf.arr[offs] = (byte) (item.RealValue != 0.0?1:0);
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                item = item.NextSibling;
                                offs += 1;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfChar: 
                    case ClassDescriptor.FieldType.tpArrayOfShort: 
                    case ClassDescriptor.FieldType.tpArrayOfUShort: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 2);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isIntValue())
                                {
                                    Bytes.pack2(buf.arr, offs, (short) item.IntValue);
                                }
                                else if (item.isRealValue())
                                {
                                    Bytes.pack2(buf.arr, offs, (short) item.RealValue);
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                item = item.NextSibling;
                                offs += 2;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfEnum: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            Type elemType = f.FieldType.GetElementType();
                            buf.extend(offs + 4 + len * 4);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isIntValue())
                                {
                                    Bytes.pack4(buf.arr, offs, (int) item.IntValue);
                                }
                                else if (item.isRealValue())
                                {
                                    Bytes.pack4(buf.arr, offs, (int) item.RealValue);
                                }
                                else if (item.isStringValue()) 
                                {
                                    try
                                    {
                                        Bytes.pack4(buf.arr, offs, (int)ClassDescriptor.parseEnum(elemType, item.StringValue));
                                    } 
                                    catch (ArgumentException)
                                    {
                                        throwException("Invalid enum value");
                                    }
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                item = item.NextSibling;
                                offs += 4;
                            }
                        }
                        continue;
                        

                    case ClassDescriptor.FieldType.tpArrayOfInt: 
                    case ClassDescriptor.FieldType.tpArrayOfUInt: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 4);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isIntValue())
                                {
                                    Bytes.pack4(buf.arr, offs, (int) item.IntValue);
                                }
                                else if (item.isRealValue())
                                {
                                    Bytes.pack4(buf.arr, offs, (int) item.RealValue);
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                item = item.NextSibling;
                                offs += 4;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfLong: 
                    case ClassDescriptor.FieldType.tpArrayOfULong: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 8);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isIntValue())
                                {
                                    Bytes.pack8(buf.arr, offs, item.IntValue);
                                }
                                else if (item.isRealValue())
                                {
                                    Bytes.pack8(buf.arr, offs, (long) item.RealValue);
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                item = item.NextSibling;
                                offs += 8;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfFloat: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 4);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isIntValue())
                                {
                                   Bytes.packF4(buf.arr, offs, (float)item.IntValue);
                                }
                                else if (item.isRealValue())
                                {
                                    Bytes.packF4(buf.arr, offs, (float)item.RealValue);
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                item = item.NextSibling;
                                offs += 4;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfDouble: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 8);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isIntValue())
                                {
                                    Bytes.packF8(buf.arr, offs, (double)item.IntValue);
                                }
                                else if (item.isRealValue())
                                {
                                    Bytes.packF8(buf.arr, offs, item.RealValue);
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                item = item.NextSibling;
                                offs += 8;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfDate: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 8);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isNullValue())
                                {
                                    Bytes.pack8(buf.arr, offs, -1);
                                }
                                else if (item.isStringValue())
                                {
                                    try 
                                    { 
                                        Bytes.packDate(buf.arr, offs, DateTime.Parse(item.StringValue));
                                    }
                                    catch (FormatException)
                                    {
                                        throwException("Conversion for field " + fieldName + " is not possible");
                                    }
                                }
                                item = item.NextSibling;
                                offs += 8;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfDecimal: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 16);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isStringValue())
                                {
                                    try 
                                    { 
                                        Bytes.packDecimal(buf.arr, offs, Decimal.Parse(item.StringValue));
                                    }
                                    catch (FormatException)
                                    {
                                        throwException("Conversion for field " + fieldName + " is not possible");
                                    }
                                }
                                item = item.NextSibling;
                                offs += 16;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfGuid: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4 + len * 16);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                if (item.isStringValue())
                                {
                                    try 
                                    { 
                                        Bytes.packGuid(buf.arr, offs, new Guid(item.StringValue));
                                    }
                                    catch (FormatException)
                                    {
                                        throwException("Conversion for field " + fieldName + " is not possible");
                                    }
                                }
                                item = item.NextSibling;
                                offs += 16;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfString: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            buf.extend(offs + 4);
                            Bytes.pack4(buf.arr, offs, len);
                            offs += 4;
                            while (--len >= 0)
                            {
                                string val = null;
                                if (item.isIntValue())
                                {
                                    val = System.Convert.ToString(item.IntValue);
                                }
                                else if (item.isRealValue())
                                {
                                    val = item.RealValue.ToString();
                                }
                                else if (item.isStringValue())
                                {
                                    val = item.StringValue;
                                }
                                else if (item.isNullValue())
                                {
                                    val = null;
                                }
                                else
                                {
                                    throwException("Conversion for field " + fieldName + " is not possible");
                                }
                                offs = buf.packString(offs, val);  
                                item = item.NextSibling;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfObject: 
                    case ClassDescriptor.FieldType.tpArrayOfOid: 
                    case ClassDescriptor.FieldType.tpLink: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            offs = buf.packI4(offs, len);
                            while (--len >= 0)
                            {
                                offs = importRef(item, offs, buf);
                                item = item.NextSibling;
                            }
                        }
                        continue;
					
                    case ClassDescriptor.FieldType.tpArrayOfValue: 
                        if (elem == null || elem.isNullValue())
                        {
                            offs = buf.packI4(offs, -1);
                        }
                        else
                        {
                            XMLElement item = elem.getSibling("element");
                            int len = (item == null)?0:item.Counter;
                            offs = buf.packI4(offs, len);
                            ClassDescriptor elemDesc = fd.valueDesc;
                            while (--len >= 0)
                            {
                                offs = packObject(item, elemDesc, offs, buf);
                                item = item.NextSibling;
                            }
                        }
                        continue;					
                }
            }
            return offs;
        }
		
        internal XMLElement readElement(string name)
        {
            XMLElement elem = new XMLElement(name);
            string attribute;
            XMLScanner.Token tkn;
            while (true)
            {
                switch (scanner.scan())
                {
                    case XMLScanner.Token.GTS: 
                        return elem;
					
                    case XMLScanner.Token.GT: 
                        while ((tkn = scanner.scan()) == XMLScanner.Token.LT)
                        {
                            if (scanner.scan() != XMLScanner.Token.IDENT)
                            {
                                throwException("Element name expected");
                            }
                            string siblingName = scanner.Identifier;
                            XMLElement sibling = readElement(siblingName);
                            elem.addSibling(sibling);
                        }
                        switch (tkn)
                        {
                            case XMLScanner.Token.SCONST: 
                                elem.StringValue = scanner.String;
                                tkn = scanner.scan();
                                break;
							
                            case XMLScanner.Token.ICONST: 
                                elem.IntValue = scanner.Int;
                                tkn = scanner.scan();
                                break;
							
                            case XMLScanner.Token.FCONST: 
                                elem.RealValue = scanner.Real;
                                tkn = scanner.scan();
                                break;
							
                            case XMLScanner.Token.IDENT: 
                                if (scanner.Identifier.Equals("null"))
                                {
                                    elem.setNullValue();
                                }
                                else
                                {
                                    elem.StringValue = scanner.Identifier;
                                }
                                tkn = scanner.scan();
                                break;
							
                        }
                        if (tkn != XMLScanner.Token.LTS || scanner.scan() != XMLScanner.Token.IDENT || !scanner.Identifier.Equals(name) || scanner.scan() != XMLScanner.Token.GT)
                        {
                            throwException("Element is not closed");
                        }
                        return elem;
					
                    case XMLScanner.Token.IDENT: 
                        attribute = scanner.Identifier;
                        if (scanner.scan() != XMLScanner.Token.EQ || scanner.scan() != XMLScanner.Token.SCONST)
                        {
                            throwException("Attribute value expected");
                        }
                        elem.addAttribute(attribute, scanner.String);
                        continue;
					
                    default: 
                        throwException("Unexpected token");
                        break;
					
                }
            }
        }
		
        internal void  throwException(string message)
        {
            throw new XMLImportException(scanner.Line, scanner.Column, message);
        }
		
        internal StorageImpl storage;
        internal XMLScanner  scanner;
        internal Dictionary<string,Type> classMap;
        internal int[] idMap; 
		
		
        internal class XMLScanner
        {
            internal virtual string Identifier
            {
                get
                {
                    return ident;
                }
				
            }
            internal virtual string String
            {
                get
                {
                    return new string(sconst, 0, slen);
                }
				
            }
            internal virtual long Int
            {
                get
                {
                    return iconst;
                }
				
            }
            internal virtual double Real
            {
                get
                {
                    return fconst;
                }
				
            }
            internal virtual int Line
            {
                get
                {
                    return line;
                }
				
            }
            internal virtual int Column
            {
                get
                {
                    return column;
                }
				
            }
            internal enum Token 
            {
                IDENT,
                SCONST,
                ICONST,
                FCONST,
                LT,
                GT,
                LTS,
                GTS,
                EQ,
                EOF
            };
			
            internal System.IO.TextReader reader;
            internal int    line;
            internal int    column;
            internal char[] sconst;
            internal long   iconst;
            internal double fconst;
            internal int    slen;
            internal String ident;
            internal int    size;
            internal int    ungetChar;
            internal bool   hasUngetChar;
			
            internal XMLScanner(System.IO.TextReader reader)
            {
                this.reader = reader;
                sconst = new char[size = 1024];
                line = 1;
                column = 0;
                hasUngetChar = false;
            }
			
            internal int get()
            {
                if (hasUngetChar)
                {
                    hasUngetChar = false;
                    return ungetChar;
                }
                int ch = reader.Read();
                if (ch == '\n')
                {
                    line += 1;
                    column = 0;
                }
                else if (ch == '\t')
                {
                    column += (column + 8) & ~ 7;
                }
                else
                {
                    column += 1;
                }
                return ch;
            }
			
            internal void  unget(int ch)
            {
                if (ch == '\n')
                {
                    line -= 1;
                }
                else
                {
                    column -= 1;
                }
                ungetChar = ch;
                hasUngetChar = true;
            }
			
            internal Token scan()
            {
                int i, ch, quote;
                bool floatingPoint;
				
                while (true)
                {
                    do 
                    {
                        if ((ch = get()) < 0)
                        {
                            return Token.EOF;
                        }
                    }
                    while (ch <= ' ');
					
                    switch (ch)
                    {
                        case '<': 
                            ch = get();
                            if (ch == '?')
                            {
                                while ((ch = get()) != '?')
                                {
                                    if (ch < 0)
                                    {
                                        throw new XMLImportException(line, column, "Bad XML file format");
                                    }
                                }
                                if ((ch = get()) != '>')
                                {
                                    throw new XMLImportException(line, column, "Bad XML file format");
                                }
                                continue;
                            }
                            if (ch != '/')
                            {
                                unget(ch);
                                return Token.LT;
                            }
                            return Token.LTS;
						
                        case '>': 
                            return Token.GT;
						
                        case '/': 
                            ch = get();
                            if (ch != '>')
                            {
                                unget(ch);
                                throw new XMLImportException(line, column, "Bad XML file format");
                            }
                            return Token.GTS;
						
                        case '=': 
                            return Token.EQ;
						
                        case '"': 
                        case '\'':
                            quote = ch;
                            i = 0;
                            while (true)
                            {
                                ch = get();
                                if (ch < 0)
                                {
                                    throw new XMLImportException(line, column, "Bad XML file format");
                                }
                                else if (ch == '&')
                                {
                                    switch (get())
                                    {
                                        case 'a': 
                                            ch = get();
                                            if (ch == 'm') 
                                            { 
                                                if (get() == 'p' && get() == ';') 
                                                { 
                                                    ch = '&';
                                                    break;
                                                }
                                            } 
                                            else if (ch == 'p' && get() == 'o' && get() == 's' && get() == ';') 
                                            { 
                                                ch = '\'';
                                                break;
                                            }
                                            throw new XMLImportException(line, column, "Bad XML file format");
										
                                        case 'l': 
                                            if (get() != 't' || get() != ';')
                                            {
                                                throw new XMLImportException(line, column, "Bad XML file format");
                                            }
                                            ch = '<';
                                            break;
										
                                        case 'g': 
                                            if (get() != 't' || get() != ';')
                                            {
                                                throw new XMLImportException(line, column, "Bad XML file format");
                                            }
                                            ch = '>';
                                            break;
										
                                        case 'q': 
                                            if (get() != 'u' || get() != 'o' || get() != 't' || get() != ';')
                                            {
                                                throw new XMLImportException(line, column, "Bad XML file format");
                                            }
                                            ch = '"';
                                            break;
										
										
                                        default: 
                                            throw new XMLImportException(line, column, "Bad XML file format");
										
                                    }
                                }
                                else if (ch == quote)
                                {
                                    slen = i;
                                    return Token.SCONST;
                                }
                                if (i == size)
                                {
                                    char[] newBuf = new char[size *= 2];
                                    Array.Copy(sconst, 0, newBuf, 0, i);
                                    sconst = newBuf;
                                }
                                sconst[i++] = (char) ch;
                            }
						
                        case '-': case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9': 
                            i = 0;
                            floatingPoint = false;
                            while (true)
                            {
                                if (!System.Char.IsDigit((char) ch) && ch != '-' && ch != '+' && ch != '.' && ch != ',' && ch != 'E')
                                {
                                    unget(ch);
                                    try
                                    {
                                        if (floatingPoint)
                                        {
                                            fconst = System.Double.Parse(new String(sconst, 0, i));
                                            return Token.FCONST;
                                        }
                                        else
                                        {
                                            iconst = sconst[0] == '-' ? System.Int64.Parse(new String(sconst, 0, i))
                                                : (long)System.UInt64.Parse(new String(sconst, 0, i));
                                            return Token.ICONST;
                                        }
                                    }
                                    catch (System.FormatException)
                                    {
                                        throw new XMLImportException(line, column, "Bad XML file format");
                                    }
                                }
                                if (i == size)
                                {
                                    throw new XMLImportException(line, column, "Bad XML file format");
                                }
                                sconst[i++] = (char) ch;
                                if (ch == '.' || ch == ',')
                                {
                                    floatingPoint = true;
                                }
                                ch = get();
                            }
						
                        default: 
                            i = 0;
                            while (System.Char.IsLetterOrDigit((char) ch) || ch == '-' || ch == ':' || ch == '_' || ch == '.' || ch == ',')
                            {
                                if (i == size)
                                {
                                    throw new XMLImportException(line, column, "Bad XML file format");
                                }
                                if (ch == '-') 
                                { 
                                    ch = '+';
                                }
                                sconst[i++] = (char) ch;
                                ch = get();
                            }
                            unget(ch);
                            if (i == 0)
                            {
                                throw new XMLImportException(line, column, "Bad XML file format");
                            }
                            ident = new String(sconst, 0, i);
//#if USE_GENERICS 
                            ident = ident.Replace(".1", "`");
                            ident = ident.Replace(".2", ",");
                            ident = ident.Replace(".3", "[");
                            ident = ident.Replace(".4", "]");
                            ident = ident.Replace(".5", "=");
                            ident = ident.Replace(".6", " ");
//#endif
                            return Token.IDENT;
                    }
                }
            }
        }
    }
}