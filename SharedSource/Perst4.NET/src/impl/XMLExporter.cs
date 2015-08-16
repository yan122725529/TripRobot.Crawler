namespace Perst.Impl    
{
    using System;
    using System.Reflection;
    using System.Diagnostics;
    using System.Text;
    using System.IO;
    using Perst;

    public class XMLExporter
    {
        public XMLExporter(StorageImpl storage, System.IO.StreamWriter writer)
        {
            this.storage = storage;
            this.writer = writer;
        }
		
        public virtual void  exportDatabase(int rootOid)
        {
            if (storage.encoding != null) 
            { 
                writer.Write("<?xml version=\"1.0\" encoding=\"" + storage.encoding + "\"?>\n");
            }
            else
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            }
            writer.Write("<database root=\"" + rootOid + "\">\n");
            exportedBitmap = new int[(storage.currIndexSize + 31) / 32];
            markedBitmap = new int[(storage.currIndexSize + 31) / 32];
            markedBitmap[rootOid >> 5] |= 1 << (rootOid & 31);
            int nExportedObjects;
            do 
            {
                nExportedObjects = 0;
                for (int i = 0; i < markedBitmap.Length; i++)
                {
                    int mask = markedBitmap[i];
                    if (mask != 0)
                    {
                        for (int j = 0, bit = 1; j < 32; j++, bit <<= 1)
                        {
                            if ((mask & bit) != 0)
                            {
                                int oid = (i << 5) + j;
                                exportedBitmap[i] |= bit;
                                markedBitmap[i] &= ~ bit;
                                try             
                                {
                                    byte[] obj = storage.get(oid);
                                    int typeOid = ObjectHeader.getType(obj, 0);
                                    ClassDescriptor desc = storage.findClassDescriptor(typeOid);
                                    string name = desc.name;
#if USE_GENERICS
                                    if (desc.cls == null) 
                                    { 
                                        if (name.StartsWith("Perst.Impl.Btree<") || name.StartsWith("Perst.Impl.BitIndex<")) 
                                        { 
                                            exportIndex(oid, obj, name);
                                        }
                                        else if (name.StartsWith("Perst.Impl.PersistentSet<"))
                                        {
                                            exportSet(oid, obj, name);
                                        }
                                        else if (name.StartsWith("Perst.Impl.BtreeFieldIndex<") || name.StartsWith("Perst.Impl.BtreeCaseInsensitiveFieldIndex<"))
                                        {
                                            exportFieldIndex(oid, obj, name);
                                        }
                                        else if (name.StartsWith("Perst.Impl.BtreeMultiFieldIndex<") || name.StartsWith("Perst.Impl.BtreeCaseInsensitiveMultiFieldIndex<"))
                                        {
                                            exportMultiFieldIndex(oid, obj, name);
                                        }
                                        else if (name.StartsWith("Perst.Impl.BtreeCompoundIndex<"))
                                        {
                                            exportCompoundIndex(oid, obj, name);
                                        }
                                        else
                                        {   
                                            String className = exportIdentifier(desc.name);
                                            writer.Write(" <" + className + " id=\"" + oid + "\">\n");
                                            exportObject(desc, obj, ObjectHeader.Sizeof, 2);
                                            writer.Write(" </" + className + ">\n");
                                        }
                                    }
                                    else if (typeof(Btree).IsAssignableFrom(desc.cls)) 
                                    {
                                        Type t = desc.cls.GetGenericTypeDefinition();
                                        if (t == typeof(Btree<,>) || t == typeof(BitIndex<>)) 
                                        { 
                                            exportIndex(oid, obj, name);
                                        }
                                        else if (t == typeof(PersistentSet<>))
                                        {
                                             exportSet(oid, obj, name);
                                        }
                                        else if (t == typeof(BtreeFieldIndex<,>) || t == typeof(BtreeCaseInsensitiveFieldIndex<,>) )
                                        {
                                             exportFieldIndex(oid, obj, name);
                                        }
                                        else if (t == typeof(BtreeMultiFieldIndex<>) || t == typeof(BtreeCaseInsensitiveMultiFieldIndex<>))
                                        {
                                            exportMultiFieldIndex(oid, obj, name);
                                        }
                                        else if (t == typeof(BtreeCompoundIndex<>))
                                        {
                                            exportCompoundIndex(oid, obj, name);
                                        }
                                    }
#else
                                    if (desc.cls == typeof(Btree) || desc.cls == typeof(BitIndexImpl)) 
                                    {
                                        exportIndex(oid, obj, name);
                                    }
                                    else if (desc.cls == typeof(PersistentSet))
                                    {
                                        exportSet(oid, obj, name);
                                    }
                                    else if (desc.cls == typeof(BtreeFieldIndex) || desc.cls == typeof(BtreeCaseInsensitiveFieldIndex))
                                    {
                                        exportFieldIndex(oid, obj, name);
                                    }
                                    else if (desc.cls == typeof(BtreeMultiFieldIndex) || desc.cls == typeof(BtreeCaseInsensitiveMultiFieldIndex))
                                    {
                                        exportMultiFieldIndex(oid, obj, name);
                                    }
                                    else if (desc.cls == typeof(BtreeCompoundIndex))
                                    {
                                        exportCompoundIndex(oid, obj, name);
                                    }
#endif
                                    else
                                    {
                                        String className = exportIdentifier(desc.name);
                                        writer.Write(" <" + className + " id=\"" + oid + "\">\n");
                                        exportObject(desc, obj, ObjectHeader.Sizeof, 2);
                                        writer.Write(" </" + className + ">\n");
                                    }
                                    nExportedObjects += 1;
                                } 
                                catch (Exception x)
                                {
                                    if (storage.listener != null) 
                                    {
                                        if (!storage.listener.ObjectNotExported(oid, x)) 
                                        {
                                            throw;
                                        }
                                    }
#if !WINRT_NET_FRAMEWORK
                                    else
                                    {       
                                        Console.WriteLine("XML export failed for object " + oid + ": " + x);
                                    }
#endif
                                }
                            }
                        }
                    }
                }
            }
            while (nExportedObjects != 0);
            writer.Write("</database>\n");
        }
		
        internal String exportIdentifier(String name) 
        { 
            name = name.Replace('+', '-');
//#if USE_GENERICS
            name = name.Replace("`", ".1");
            name = name.Replace(",", ".2");
            name = name.Replace("[", ".3");
            name = name.Replace("]", ".4");
            name = name.Replace("=", ".5");
            name = name.Replace(" ", ".6");
//#endif
            return name;
        }

        Btree createBtree(int oid, byte[] data) 
        {
            Btree btree = storage.createBtreeStub(data, 0);
            storage.AssignOid(btree, oid, false);
            return btree;
        }

        internal void exportSet(int oid, byte[] data, string name) 
        { 
            Btree btree = createBtree(oid, data);
            name = exportIdentifier(name);
            writer.Write(" <" + name + " id=\"" + oid + "\" unique=\"" + (btree.IsUnique ? '1' : '0') + "\">\n");
            btree.export(this);
            writer.Write(" </" + name + ">\n");
        }

        internal void  exportIndex(int oid, byte[] data, string name)
        {
            Btree btree = createBtree(oid, data);
            name = exportIdentifier(name);
            writer.Write(" <" + name + " id=\"" + oid + "\" unique=\"" + (btree.IsUnique ? '1' : '0') 
                + "\" type=\"" + btree.FieldType + "\">\n");
            btree.export(this);
            writer.Write(" </" + name + ">\n");
        }
		
        internal void  exportFieldIndex(int oid, byte[] data, string name)
        {
            Btree btree = createBtree(oid, data);
            name = exportIdentifier(name);
            writer.Write(" <" + name + " id=\"" + oid + "\" unique=\"" + (btree.IsUnique?'1':'0') + "\"");
            int offs = btree.HeaderSize;
            writer.Write(" autoinc=\"" + Bytes.unpack8(data, offs) + "\"");
            offs += 8;
            writer.Write(" class=");
            offs = exportString(data, offs);
            writer.Write(" field=");
            offs = exportString(data, offs);
            writer.Write(">\n");
            btree.export(this);
            writer.Write(" </" + name + ">\n");
        }
		
        internal void exportMultiFieldIndex(int oid, byte[] data, string name) 
        { 
            Btree btree = createBtree(oid, data);
            name = exportIdentifier(name);
            writer.Write(" <" + name + " id=\"" + oid + "\" unique=\"" + (btree.IsUnique ? '1' : '0') 
                + "\" class=");
            int offs = exportString(data, btree.HeaderSize);
            int nFields = Bytes.unpack4(data, offs);
            offs += 4;
            for (int i = 0; i < nFields; i++) 
            { 
                writer.Write(" field" + i + "=");
                offs = exportString(data, offs);
            }
            writer.Write(">\n");
            int nTypes = Bytes.unpack4(data, offs);
            offs += 4;
            compoundKeyTypes = new ClassDescriptor.FieldType[nTypes];
            for (int i = 0; i < nTypes; i++) 
            { 
                compoundKeyTypes[i] = (ClassDescriptor.FieldType)Bytes.unpack4(data, offs);
                offs += 4;
            }
            btree.export(this); 
            compoundKeyTypes = null;
            writer.Write(" </" + name + ">\n");
        }

        internal void exportCompoundIndex(int oid, byte[] data, string name) 
        { 
            Btree btree = createBtree(oid, data);
            name = exportIdentifier(name);
            writer.Write(" <" + name + " id=\"" + oid + "\" unique=\"" + (btree.IsUnique ? '1' : '0') + "\"");
            int offs = btree.HeaderSize;
            int nTypes = Bytes.unpack4(data, offs);
            offs += 4;
            compoundKeyTypes = new ClassDescriptor.FieldType[nTypes];
            for (int i = 0; i < nTypes; i++) 
            { 
                ClassDescriptor.FieldType type = (ClassDescriptor.FieldType)Bytes.unpack4(data, offs);
                compoundKeyTypes[i] = type;
                writer.Write(" type" + i + "=\"" + type + "\"");
                offs += 4;
            }
            writer.Write(">\n");
            btree.export(this); 
            compoundKeyTypes = null;
            writer.Write(" </" + name + ">\n");
        }

        int exportKey(byte[] body, int offs, int size, ClassDescriptor.FieldType type) 
        {
            switch (type)
            {
                case ClassDescriptor.FieldType.tpBoolean: 
                    writer.Write(body[offs++] != 0?"1":"0");
                    break;
				
                case ClassDescriptor.FieldType.tpByte: 
                    writer.Write(System.Convert.ToString((byte) body[offs++]));
                    break;
				
                case ClassDescriptor.FieldType.tpSByte: 
                    writer.Write(System.Convert.ToString((sbyte) body[offs++]));
                    break;
				
                case ClassDescriptor.FieldType.tpChar: 
                    writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                    offs += 2;
                    break;
				
                case ClassDescriptor.FieldType.tpShort: 
                    writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                    offs += 2;
                    break;
				
                case ClassDescriptor.FieldType.tpUShort: 
                    writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                    offs += 2;
                    break;
				
                case ClassDescriptor.FieldType.tpInt: 
                    writer.Write(System.Convert.ToString(Bytes.unpack4(body, offs)));
                    offs += 4;
                    break;
				
                case ClassDescriptor.FieldType.tpUInt: 
                case ClassDescriptor.FieldType.tpObject:  
                case ClassDescriptor.FieldType.tpOid:  
                case ClassDescriptor.FieldType.tpEnum:
                    writer.Write(System.Convert.ToString((uint)Bytes.unpack4(body, offs)));
                    offs += 4;
                    break;
				
                case ClassDescriptor.FieldType.tpLong: 
                    writer.Write(System.Convert.ToString(Bytes.unpack8(body, offs)));
                    offs += 8;
                    break;
				
                case ClassDescriptor.FieldType.tpULong: 
                    writer.Write(System.Convert.ToString((ulong)Bytes.unpack8(body, offs)));
                    offs += 8;
                    break;
				
                case ClassDescriptor.FieldType.tpFloat: 
                    writer.Write(System.Convert.ToString(Bytes.unpackF4(body, offs)));
                    offs += 4;
                    break;
				
                case ClassDescriptor.FieldType.tpDouble: 
                    writer.Write(System.Convert.ToString(Bytes.unpackF8(body, offs)));
                    offs += 8;
                    break;
				
                case ClassDescriptor.FieldType.tpGuid:
                    writer.Write(Bytes.unpackGuid(body, offs).ToString());
                    offs += 16;
                    break;

                case ClassDescriptor.FieldType.tpDecimal:
                    writer.Write(Bytes.unpackDecimal(body, offs).ToString());
                    offs += 16;
                    break;

                case ClassDescriptor.FieldType.tpString: 
                case ClassDescriptor.FieldType.tpType: 
                    for (int i = 0; i < size; i++)
                    {
                        exportChar((char) Bytes.unpack2(body, offs));
                        offs += 2;
                    }
                    break;
				
                case ClassDescriptor.FieldType.tpArrayOfByte:
                    for (int i = 0; i < size; i++) 
                    { 
                        byte b = body[offs++];
                        writer.Write(hexDigit[(b >> 4) & 0xF]);
                        writer.Write(hexDigit[b & 0xF]);
                    }
                    break;

                case ClassDescriptor.FieldType.tpDate: 
                    writer.Write(Bytes.unpackDate(body, offs).ToString());
                    offs += 8;
                    break;

                default:
                    Debug.Assert(false, "Invalid type");
                    break;
            }              
            return offs;                                            
        }
                                                                    
        void exportCompoundKey(byte[] body, int offs, int size, ClassDescriptor.FieldType type) 
        { 
            Debug.Assert(type == ClassDescriptor.FieldType.tpArrayOfByte);
            int end = offs + size;
            for (int i = 0; i < compoundKeyTypes.Length; i++) 
            { 
                type = compoundKeyTypes[i];
                if (type == ClassDescriptor.FieldType.tpArrayOfByte || type == ClassDescriptor.FieldType.tpString) 
                { 
                    size = Bytes.unpack4(body, offs);
                    offs += 4;
                }
                writer.Write(" key" + i + "=\"");
                offs = exportKey(body, offs, size, type); 
                writer.Write("\"");
            }
            Debug.Assert(offs == end);
        }

        internal void  exportAssoc(int oid, byte[] body, int offs, int size, ClassDescriptor.FieldType type)
        {
            writer.Write("  <ref id=\"" + oid + "\"");
            if ((exportedBitmap[oid >> 5] & (1 << (oid & 31))) == 0)
            {
                markedBitmap[oid >> 5] |= 1 << (oid & 31);
            }
            if (compoundKeyTypes != null) 
            { 
                exportCompoundKey(body, offs, size, type);
            } 
            else 
            { 
                writer.Write(" key=\"");
                exportKey(body, offs, size, type);
                writer.Write("\"");
            }
            writer.Write("/>\n");
        }
		
        internal void  indentation(int indent)
        {
            while (--indent >= 0)
            {
                writer.Write(' ');
            }
        }
		
        internal void  exportChar(char ch)
        {
            switch (ch)
            {
                case '<': 
                    writer.Write("&lt;");
                    break;
				
                case '>': 
                    writer.Write("&gt;");
                    break;
				
                case '&': 
                    writer.Write("&amp;");
                    break;
				
                case '"': 
                    writer.Write("&quot;");
                    break;
				
                case '\'': 
                    writer.Write("&apos;");
                    break;
				
                default: 
                    writer.Write(ch);
                    break;
				
            }
        }
		
        internal int exportString(byte[] body, int offs)
        {
            int len = Bytes.unpack4(body, offs);
            offs += 4;
            if (len >= 0)
            {
                writer.Write("\"");
                while (--len >= 0)
                {
                    exportChar((char) Bytes.unpack2(body, offs));
                    offs += 2;
                }
                writer.Write("\"");
            } 
            else if (len < -1) 
            { 
                writer.Write("\"");   
                string s;
                if (storage.encoding != null) 
                { 
                    s = storage.encoding.GetString(body, offs, -len-2);
                } 
                else 
                { 
#if SILVERLIGHT
                    s = Encoding.UTF8.GetString(body, offs, -len-2);
#else
                    s = Encoding.Default.GetString(body, offs, -len-2);
#endif
                }
                offs -= len+2;
                for (int i = 0, n = s.Length; i < n; i++) 
                { 
                    exportChar(s[i]);
                }
                writer.Write("\"");   
            }
            else
            {
                writer.Write("null");
            }
            return offs;
        }
		
        internal static char[] hexDigit = new char[]{'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};
		
        internal int exportRef(byte[] body, int offs, int indent) 
        { 
            int oid = Bytes.unpack4(body, offs);
            offs += 4;
            if (oid < 0) 
            {
                int tid = -1-oid;
                switch ((ClassDescriptor.FieldType)tid)
                {
                    case ClassDescriptor.FieldType.tpType:
                        writer.Write("<type name=\"");
                        offs = exportString(body, offs);
                        writer.Write("\"/>");
                        break;
                    case ClassDescriptor.FieldType.tpString:
                    {
                        offs = exportString(body, offs);
                        break;
                    }
                    case ClassDescriptor.FieldType.tpBoolean:
                    case ClassDescriptor.FieldType.tpByte:
                    case ClassDescriptor.FieldType.tpSByte:
                    case ClassDescriptor.FieldType.tpChar:
                    case ClassDescriptor.FieldType.tpShort:
                    case ClassDescriptor.FieldType.tpUShort:
                    case ClassDescriptor.FieldType.tpInt:
                    case ClassDescriptor.FieldType.tpUInt:
                    case ClassDescriptor.FieldType.tpLong:
                    case ClassDescriptor.FieldType.tpULong:
                    case ClassDescriptor.FieldType.tpFloat:
                    case ClassDescriptor.FieldType.tpDouble:
                    case ClassDescriptor.FieldType.tpDate:
                    case ClassDescriptor.FieldType.tpDecimal:
                    case ClassDescriptor.FieldType.tpGuid:
                    case ClassDescriptor.FieldType.tpEnum:
                    {
                        int len = ClassDescriptor.Sizeof[tid];
                        writer.Write("<scalar type=\"" + tid + "\" value=\"");
                        while (--len >= 0) {
                            byte b = body[offs++];
                            writer.Write(hexDigit[(b >> 4) & 0xF]);
                            writer.Write(hexDigit[b & 0xF]);
                        }
                        writer.Write("\"/>");
                        break;
                    }
                    case ClassDescriptor.FieldType.tpCustom:
                    {
                        MemoryReader reader = new MemoryReader(storage, body, offs, null, true, false);
                        object obj = storage.serializer.Unpack(reader);
                        offs = reader.Position;                        
                        writer.Write("<scalar type=\"" + tid + "\" value=\"");
                        foreach (char ch in storage.serializer.Print(obj)) 
                        { 
                            exportChar(ch);
                        }
                        writer.Write("\"/>");
                        break;
                    }
                    default:
                    {
                        if (tid >= (int)ClassDescriptor.FieldType.tpValueTypeBias) 
                        { 
                            int typeOid = -(int)ClassDescriptor.FieldType.tpValueTypeBias - oid;
                            ClassDescriptor desc = storage.findClassDescriptor(typeOid);
                            if (desc.isCollection) 
                            { 
                                int len = Bytes.unpack4(body, offs);   
                                offs += 4;
                                String className = exportIdentifier(desc.name);
                                writer.Write("\n");
                                indentation(indent + 1);
                                writer.Write("<" + className + ">\n");
                                for (int i = 0; i < len; i++) 
                                { 
                                    indentation(indent + 2);
                                    writer.Write("<element>");
                                    offs = exportRef(body, offs, indent + 2);
                                    writer.Write("</element>\n");
                                }                            
                                indentation(indent + 1);
                                writer.Write("</" + className + ">\n");
                                indentation(indent);
                            } 
                            else if (desc.isDictionary) 
                            { 
                                int len = Bytes.unpack4(body, offs);   
                                offs += 4;
                                String className = exportIdentifier(desc.name);
                                writer.Write("\n");
                                indentation(indent + 1);
                                writer.Write("<" + className + ">\n");
                                for (int i = 0; i < len; i++) 
                                { 
                                    indentation(indent + 2);
                                    writer.Write("<element>\n");
                                    indentation(indent + 4);
                                    writer.Write("<key>");
                                    offs = exportRef(body, offs, indent + 4);
                                    writer.Write("</key>\n");
                                    indentation(indent + 4);
                                    writer.Write("<value>");
                                    offs = exportRef(body, offs, indent + 4);
                                    writer.Write("</value>\n");
                                    indentation(indent + 2);
                                    writer.Write("</element>\n");
                                }                            
                                indentation(indent + 1);
                                writer.Write("</" + className + ">\n");
                                indentation(indent);
                            } 
                            else 
                            {
                                string className = exportIdentifier(desc.name);
                                writer.Write("\n");
                                indentation(indent + 1);
                                writer.Write("<" + className + ">\n");
                                offs = exportObject(desc, body, offs, indent + 2);
                                indentation(indent + 1);
                                writer.Write("</" + className + ">\n");
                                indentation(indent);
                            }
                            break;
                        } 
                        else 
                        { 
                            throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_TYPE);
                        }
                    }
                }       
            } 
            else
            {
                writer.Write("<ref id=\"" + oid + "\"/>");
                if (oid != 0 && (exportedBitmap[oid >> 5] & (1 << (oid & 31))) == 0) 
                { 
                    markedBitmap[oid >> 5] |= 1 << (oid & 31);
                }
            }
            return offs;
        }

        internal int exportBinary(byte[] body, int offs)
        {
            int len = Bytes.unpack4(body, offs);
            offs += 4;
            if (len < 0) 
            { 
                writer.Write("null");
            } 
            else 
            {
                writer.Write('\"');
                while (--len >= 0) 
                {
                    byte b = body[offs++];
                    writer.Write(hexDigit[(b >> 4) & 0xF]);
                    writer.Write(hexDigit[b & 0xF]);
                }
                writer.Write('\"');
            }
            return offs;
        }
    
        internal int exportObject(ClassDescriptor desc, byte[] body, int offs, int indent)
        {
            ClassDescriptor.FieldDescriptor[] all = desc.allFields;
			
            for (int i = 0, n = all.Length; i < n; i++)
            {
                ClassDescriptor.FieldDescriptor fd = all[i];
                FieldInfo f = fd.field;
                indentation(indent);
                String fieldName = exportIdentifier(fd.fieldName);
                writer.Write("<" + fieldName + ">");
                switch (fd.type)
                {
#if NET_FRAMEWORK_20
                    case ClassDescriptor.FieldType.tpNullableBoolean: 
                        writer.Write(body[offs++] == 0 ? "null" : body[offs++] != 0?"1":"0");
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableByte: 
                        writer.Write(body[offs++] == 0 ? "null" : System.Convert.ToString((byte) body[offs++]));
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableSByte: 
                        writer.Write(body[offs++] == 0 ? "null" : System.Convert.ToString((sbyte) body[offs++]));
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableChar: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                            offs += 2;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableShort: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                            offs += 2;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableUShort: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                            offs += 2;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableInt: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString(Bytes.unpack4(body, offs)));
                            offs += 4;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableUInt: 
                    case ClassDescriptor.FieldType.tpNullableEnum:
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString((uint)Bytes.unpack4(body, offs)));
                            offs += 4;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableLong: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString(Bytes.unpack8(body, offs)));
                            offs += 8;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableULong: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString((ulong)Bytes.unpack8(body, offs)));
                            offs += 8;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableFloat: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString(Bytes.unpackF4(body, offs)));
                            offs += 4;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableDouble: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(System.Convert.ToString(Bytes.unpackF8(body, offs)));
                            offs += 8;
                        }
                        break;
    				
                    case ClassDescriptor.FieldType.tpNullableGuid:
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(Bytes.unpackGuid(body, offs).ToString());
                            offs += 16;
                        }
                        break;
    
                    case ClassDescriptor.FieldType.tpNullableDecimal:
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write(Bytes.unpackDecimal(body, offs).ToString());
                            offs += 16;
                        }
                        break;
    
                    case ClassDescriptor.FieldType.tpNullableDate: 
                        if (body[offs++] == 0) { 
                            writer.Write("null");
                        } else {
                            writer.Write("\"" + Bytes.unpackDate(body, offs).ToString() + "\"");
                            offs += 8;
                        }
                        break;
#endif
                    case ClassDescriptor.FieldType.tpBoolean: 
                        writer.Write(body[offs++] != 0?"1":"0");
                        break;
					
                    case ClassDescriptor.FieldType.tpByte: 
                        writer.Write(System.Convert.ToString((byte) body[offs++]));
                        break;
					
                    case ClassDescriptor.FieldType.tpSByte: 
                        writer.Write(System.Convert.ToString((sbyte) body[offs++]));
                        break;
					
                    case ClassDescriptor.FieldType.tpChar: 
                        writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                        offs += 2;
                        break;
					
                    case ClassDescriptor.FieldType.tpShort: 
                        writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                        offs += 2;
                        break;
					
                    case ClassDescriptor.FieldType.tpUShort: 
                        writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                        offs += 2;
                        break;
					
                    case ClassDescriptor.FieldType.tpInt: 
                        writer.Write(System.Convert.ToString(Bytes.unpack4(body, offs)));
                        offs += 4;
                        break;
					
                    case ClassDescriptor.FieldType.tpEnum:
                        writer.Write(Enum.ToObject(fd.MemberType, Bytes.unpack4(body, offs)));
                        offs += 4;
                        break;

                    case ClassDescriptor.FieldType.tpUInt: 
                        writer.Write(System.Convert.ToString((uint)Bytes.unpack4(body, offs)));
                        offs += 4;
                        break;
					
                    case ClassDescriptor.FieldType.tpLong: 
                        writer.Write(System.Convert.ToString(Bytes.unpack8(body, offs)));
                        offs += 8;
                        break;
					
                    case ClassDescriptor.FieldType.tpULong: 
                        writer.Write(System.Convert.ToString((ulong)Bytes.unpack8(body, offs)));
                        offs += 8;
                        break;
					
                    case ClassDescriptor.FieldType.tpFloat: 
                        writer.Write(System.Convert.ToString(Bytes.unpackF4(body, offs)));
                        offs += 4;
                        break;
				
                    case ClassDescriptor.FieldType.tpDouble: 
                        writer.Write(System.Convert.ToString(Bytes.unpackF8(body, offs)));
                        offs += 8;
                        break;
				
                    case ClassDescriptor.FieldType.tpGuid:
                        writer.Write("\"" + Bytes.unpackGuid(body, offs) + "\"");
                        offs += 16;
                        break;

                    case ClassDescriptor.FieldType.tpDecimal:
                        writer.Write("\"" + Bytes.unpackDecimal(body, offs) + "\"");
                        offs += 16;
                        break;

                    case ClassDescriptor.FieldType.tpString: 
                    case ClassDescriptor.FieldType.tpType: 
                        offs = exportString(body, offs);
                        break;
					
                    case ClassDescriptor.FieldType.tpDate: 
                    {
                        long msec = Bytes.unpack8(body, offs);
                        offs += 8;
                        if (msec >= 0)
                        {
                            writer.Write("\"" + new System.DateTime(msec) + "\"");
                        }
                        else
                        {
                            writer.Write("null");
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpObject: 
                    case ClassDescriptor.FieldType.tpOid: 
                        offs = exportRef(body, offs, indent);
                        break;

                    case ClassDescriptor.FieldType.tpValue: 
                        writer.Write('\n');
                        offs = exportObject(fd.valueDesc, body, offs, indent + 1);
                        indentation(indent);
                        break;
					
                    case ClassDescriptor.FieldType.tpArrayOfByte: 
                    case ClassDescriptor.FieldType.tpArrayOfSByte: 
                        offs = exportBinary(body, offs);
                        break;
					
                    case ClassDescriptor.FieldType.tpCustom:
                    {
                        MemoryReader reader = new MemoryReader(storage, body, offs, null, true, false);
                        object obj = storage.serializer.Unpack(reader);
                        offs = reader.Position;                        
                        writer.Write("\"");
                        foreach (char ch in storage.serializer.Print(obj)) 
                        { 
                            exportChar(ch);
                        }
                        writer.Write("\"");
                        break;
                    }

                    case ClassDescriptor.FieldType.tpArrayOfBoolean: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + (body[offs++] != 0?"1":"0") + "</element>\n");
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfChar: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + (Bytes.unpack2(body, offs) & 0xFFFF) + "</element>\n");
                                offs += 2;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfShort: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + Bytes.unpack2(body, offs) + "</element>\n");
                                offs += 2;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfUShort: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + (ushort)Bytes.unpack2(body, offs) + "</element>\n");
                                offs += 2;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfInt: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + Bytes.unpack4(body, offs) + "</element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfEnum: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            Type elemType = fd.MemberType.GetElementType();
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + Enum.ToObject(elemType, Bytes.unpack4(body, offs)) + "</element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }

                    case ClassDescriptor.FieldType.tpArrayOfUInt: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + (uint)Bytes.unpack4(body, offs) + "</element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfLong: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + Bytes.unpack8(body, offs) + "</element>\n");
                                offs += 8;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfULong: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + (ulong)Bytes.unpack8(body, offs) + "</element>\n");
                                offs += 8;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfFloat: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + Bytes.unpackF4(body, offs) + "</element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfDouble: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>" + Bytes.unpackF8(body, offs) + "</element>\n");
                                offs += 8;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfDate: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>\"" + Bytes.unpackDate(body, offs) + "\"</element>\n");
                                offs += 8;
                            }
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfGuid: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                writer.Write("<element>\"" + Bytes.unpackGuid(body, offs) + "\"</element>\n");
                                offs += 16;
                            }
                        }
                        break;
                    }

                    case ClassDescriptor.FieldType.tpArrayOfDecimal: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                writer.Write("<element>\"" + Bytes.unpackDecimal(body, offs) + "\"</element>\n");
                                offs += 16;
                            }
                        }
                        break;
                    }

                    case ClassDescriptor.FieldType.tpArrayOfString: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>");
                                offs = exportString(body, offs);
                                writer.Write("</element>\n");
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpLink: 
                    case ClassDescriptor.FieldType.tpArrayOfObject: 
                    case ClassDescriptor.FieldType.tpArrayOfOid: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent+1);
                                writer.Write("<element>");
                                offs = exportRef(body, offs, indent+1);
                                writer.Write("</element>\n");
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfValue: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<element>\n");
                                offs = exportObject(fd.valueDesc, body, offs, indent + 2);
                                indentation(indent + 1);
                                writer.Write("</element>\n");
                            }
                            indentation(indent);
                        }
                        break;
                    }					
                }
                writer.Write("</" + fieldName + ">\n");
            }
            return offs;
        }
		
		
        private StorageImpl storage;
        private System.IO.StreamWriter writer;
        private int[] markedBitmap;
        private int[] exportedBitmap;
        private ClassDescriptor.FieldType[] compoundKeyTypes;
    }
}