namespace Perst.Impl
{
    using System;
    using System.Collections;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Diagnostics;
    using System.Text;
    using Perst;
	
    public sealed class ClassDescriptor:Persistent
    {
        internal ClassDescriptor   next;
        internal String            name;
        internal FieldDescriptor[] allFields;
        internal bool              hasReferences;
        internal CustomAllocator   allocator;

        internal static Module     lastModule;

        public class FieldDescriptor : Persistent 
        { 
            internal String          fieldName;
            internal String          className;
            internal FieldType       type;
            internal ClassDescriptor valueDesc;
            [NonSerialized()]
            internal FieldInfo       field;
            [NonSerialized()]
            internal PropertyInfo    property;
            [NonSerialized()]
            internal bool            recursiveLoading;
#if USE_GENERICS
            [NonSerialized()]
            internal MethodInfo constructor;
#endif
            public bool equals(FieldDescriptor fd) 
            { 
                return fieldName.Equals(fd.fieldName) 
                    && className.Equals(fd.className)
                    && valueDesc == fd.valueDesc
                    && type == fd.type;
            }

            public Type MemberType 
            {
                get
                {
                    return property != null ? property.PropertyType : field.FieldType;
                }
            }
    
            public void SetValue(object obj, object val)
            {
                if (property != null)
                {
#if WINRT_NET_FRAMEWORK
                    property.SetMethod.Invoke(obj,  new object[]{val});
#else
                    property.GetSetMethod(true).Invoke(obj,  new object[]{val});
#endif
                }
                else
                {
                    field.SetValue(obj, val);
                }
            }
    
            public object GetValue(object obj)
            {
                return property != null
#if WINRT_NET_FRAMEWORK
                    ? property.GetMethod.Invoke(obj, new object[0])
#else
                    ? property.GetGetMethod(true).Invoke(obj, new object[0])
#endif
                    : field.GetValue(obj);
            }
        }    

        [NonSerialized()]
        internal Type cls;
        [NonSerialized()]
        internal bool hasSubclasses;
        [NonSerialized()]
        internal bool customSerializable;
        [NonSerialized()]
        internal bool isCollection;
        [NonSerialized()]
        internal bool isDictionary;
        [NonSerialized()]
        internal ConstructorInfo defaultConstructor;
        [NonSerialized()]
        internal bool resolved;
#if !COMPACT_NET_FRAMEWORK && !WP7
        [NonSerialized()]
        internal GeneratedSerializer serializer;
#endif		
        internal static bool serializeNonPersistentObjects;

        public enum FieldType 
        {
            tpBoolean,
            tpByte,
            tpSByte,
            tpShort, 
            tpUShort,
            tpChar,
            tpEnum,
            tpInt,
            tpUInt,
            tpLong,
            tpULong,
            tpFloat,
            tpDouble,
            tpString,
            tpDate,
            tpObject,
            tpOid,
            tpValue,
            tpRaw,
            tpGuid,
            tpDecimal,
            tpLink,
            tpArrayOfBoolean,
            tpArrayOfByte,
            tpArrayOfSByte,
            tpArrayOfShort, 
            tpArrayOfUShort,
            tpArrayOfChar,
            tpArrayOfEnum,
            tpArrayOfInt,
            tpArrayOfUInt,
            tpArrayOfLong,
            tpArrayOfULong,
            tpArrayOfFloat,
            tpArrayOfDouble,
            tpArrayOfString,
            tpArrayOfDate,
            tpArrayOfObject,
            tpArrayOfOid,
            tpArrayOfValue,
            tpArrayOfRaw,
            tpArrayOfGuid,
            tpArrayOfDecimal,
            tpCustom,
            tpNullableBoolean,
            tpNullableByte,
            tpNullableSByte,
            tpNullableShort, 
            tpNullableUShort,
            tpNullableChar,
            tpNullableEnum,
            tpNullableInt,
            tpNullableUInt,
            tpNullableLong,
            tpNullableULong,
            tpNullableFloat,
            tpNullableDouble,
            tpNullableDate,
            tpNullableGuid,
            tpNullableDecimal,
            tpNullableValue,
            tpType,
            tpLast,
            tpValueTypeBias = 100
        };
		
        internal static int[] Sizeof = new int[] 
        {
            1, // tpBoolean,
            1, // tpByte,
            1, // tpSByte,
            2, // tpShort, 
            2, // tpUShort,
            2, // tpChar,
            4, // tpEnum,
            4, // tpInt,
            4, // tpUInt,
            8, // tpLong,
            8, // tpULong,
            4, // tpFloat,
            8, // tpDouble,
            0, // tpString,
            8, // tpDate,
            4, // tpObject,
            4, // tpOid,
            0, // tpValue,
            0, // tpRaw,
            16,// tpGuid,
            16,// tpDecimal,
            0, // tpLink,
            0, // tpArrayOfBoolean,
            0, // tpArrayOfByte,
            0, // tpArrayOfSByte,
            0, // tpArrayOfShort, 
            0, // tpArrayOfUShort,
            0, // tpArrayOfChar,
            0, // tpArrayOfEnum,
            0, // tpArrayOfInt,
            0, // tpArrayOfUInt,
            0, // tpArrayOfLong,
            0, // tpArrayOfULong,
            0, // tpArrayOfFloat,
            0, // tpArrayOfDouble,
            0, // tpArrayOfString,
            0, // tpArrayOfDate,
            0, // tpArrayOfObject,
            0, // tpArrayOfOid,
            0, // tpArrayOfValue,
            0, // tpArrayOfRaw,
            0, // tpArrayOfGuid,
            0, // tpArrayOfDecimal,
            0, // tpCustom
            1+1, // tpNullableBoolean,
            1+1, // tpNullableByte,
            1+1, // tpNullableSByte,
            1+2, // tpNullableShort, 
            1+2, // tpNullableUShort,
            1+2, // tpNullableChar,
            1+4, // tpNullableEnum,
            1+4, // tpNullableInt,
            1+4, // tpNullableUInt,
            1+8, // tpNullableLong,
            1+8, // tpNullableULong,
            1+4, // tpNullableFloat,
            1+8, // tpNullableDouble,
            1+8, // tpNullableDate,
            1+16,// tpNullableGuid,
            1+16,// tpNullableDecimal
            1+0, // tpNullableValue
            0    // tpType
        };
		
        internal static Type[] defaultConstructorProfile = new Type[0];
        internal static object[] noArgs = new object[0];

        static internal bool isNullable(FieldType type)
        {      
            return (uint)type - (uint)FieldType.tpNullableBoolean <= (uint)FieldType.tpNullableValue - (uint)FieldType.tpNullableBoolean;
        }

        static internal FieldType convertToNotNullable(FieldType type)
        {        
            switch (type)       
            {
            case FieldType.tpNullableBoolean:
                return FieldType.tpBoolean;
            case FieldType.tpNullableByte:
                return FieldType.tpByte;
            case FieldType.tpNullableSByte:
                return FieldType.tpSByte;
            case FieldType.tpNullableShort:
                return FieldType.tpShort;
            case FieldType.tpNullableUShort:
                return FieldType.tpUShort;
            case FieldType.tpNullableChar:
                return FieldType.tpChar;
            case FieldType.tpNullableEnum:
                return FieldType.tpEnum;
            case FieldType.tpNullableInt:
                return FieldType.tpInt;
            case FieldType.tpNullableUInt:
                return FieldType.tpUInt;
            case FieldType.tpNullableLong:
                return FieldType.tpLong;
            case FieldType.tpNullableULong:
                return FieldType.tpULong;
            case FieldType.tpNullableFloat:
                return FieldType.tpFloat;
            case FieldType.tpNullableDouble:
                return FieldType.tpDouble;
            case FieldType.tpNullableDate:
                return FieldType.tpDate;
            case FieldType.tpNullableGuid:
                return FieldType.tpGuid;
            case FieldType.tpNullableDecimal:
                return FieldType.tpDecimal;
            case FieldType.tpNullableValue:
                return FieldType.tpValue;
            default:
                return type;
            }
        }
	
        static internal object parseEnum(Type type, String value) 
        {
#if (COMPACT_NET_FRAMEWORK || SILVERLIGHT) && !WINRT_NET_FRAMEWORK
            foreach (FieldInfo fi in type.GetFields()) 
            {
                if (fi.IsLiteral && fi.Name.Equals(value)) 
                {
                    return fi.GetValue(null);
                }
            }
            throw new ArgumentException(value);
#else
            return Enum.Parse(type, value);
#endif
        }

        public bool equals(ClassDescriptor cd) 
        { 
            if (cd == null || allFields.Length != cd.allFields.Length) 
            { 
                return false;
            }
            for (int i = 0; i < allFields.Length; i++) 
            { 
                if (!allFields[i].equals(cd.allFields[i])) 
                { 
                    return false;
                }
            }
            return true;
        }
        
        internal object newInitializedInstance()
        {
            return defaultConstructor.Invoke(noArgs);
        }

        internal object newInstance()
        {
#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
            return defaultConstructor.Invoke(noArgs);
            //return Activator.CreateInstance(cls, true);
#else
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(cls);
#endif
        }

#if COMPACT_NET_FRAMEWORK || WP7 || WINRT_NET_FRAMEWORK
        internal void generateSerializer() {}
#else
        private static CILGenerator serializerGenerator = CILGenerator.Instance;

        internal void generateSerializer()
        {
            if (typeof(ISelfSerializable).IsAssignableFrom(cls))
            {
                return;
            }          
            bool serializeProperties = cls.GetCustomAttributes(typeof(SerializePropertiesAttribute), true).Length != 0;
            if (serializeProperties || !cls.IsPublic || defaultConstructor == null || !defaultConstructor.IsPublic) 
            { 
                return;
            }
            FieldDescriptor[] flds = allFields;
            for (int i = 0, n = flds.Length; i < n; i++) 
            {
                FieldDescriptor fd = flds[i];
                switch (fd.type) 
                { 
                    case FieldType.tpNullableBoolean:
                    case FieldType.tpNullableByte:
                    case FieldType.tpNullableSByte:
                    case FieldType.tpNullableShort:
                    case FieldType.tpNullableUShort:
                    case FieldType.tpNullableChar:
                    case FieldType.tpNullableEnum:
                    case FieldType.tpNullableInt:
                    case FieldType.tpNullableUInt:
                    case FieldType.tpNullableLong:
                    case FieldType.tpNullableULong:
                    case FieldType.tpNullableFloat:
                    case FieldType.tpNullableDouble:
                    case FieldType.tpNullableDate:
                    case FieldType.tpNullableGuid:
                    case FieldType.tpNullableDecimal:
                    case FieldType.tpNullableValue:

                    case FieldType.tpValue:
                    case FieldType.tpArrayOfValue:
                    case FieldType.tpArrayOfObject:
                    case FieldType.tpArrayOfEnum:
                    case FieldType.tpArrayOfRaw:
#if USE_GENERICS
                    case FieldType.tpLink:
                    case FieldType.tpArrayOfOid:
#endif
    
                        return;
                    default:
                        break;
                }
                FieldInfo f = flds[i].field;
                if (f == null || !f.IsPublic) 
                {
                    return;
                }
            }
            serializer = serializerGenerator.Generate(this);
        }
        
        static private bool isObjectProperty(Type cls, FieldInfo f)
        {
            return typeof(PersistentWrapper).IsAssignableFrom(cls) && f.Name.StartsWith("r_");
        }
#endif

#if USE_GENERICS
        MethodInfo GetConstructor(Type type, string name)
        { 
            MethodInfo mi = typeof(StorageImpl).GetMethod(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            return mi.MakeGenericMethod(type.GetGenericArguments());
        }
#endif

        internal static String getTypeName(Type t)
        {
#if USE_GENERICS
            if (t.IsGenericType)
            { 
                Type[] genericArgs = t.GetGenericArguments();
                t = t.GetGenericTypeDefinition();
                StringBuilder buf = new StringBuilder(t.FullName);
                buf.Append('=');
                char sep = '[';
                for (int j = 0; j < genericArgs.Length; j++) 
                { 
                     buf.Append(sep);
                     sep = ',';
                     buf.Append(getTypeName(genericArgs[j]));
                }
                buf.Append(']');
                return buf.ToString();
            }
#endif
            return t.FullName;
        }

        static bool isPerstInternalType(Type t)
        {
            return t.Namespace == typeof(IPersistent).Namespace
                && t != typeof(IPersistent)
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
                && t != typeof(PersistentContext) 
#endif
                && t != typeof(Persistent);
        }

        class FieldComparer : IComparer 
        {
            public int Compare(object o1, object o2) 
            {
                return ((MemberInfo)o1).Name.CompareTo(((MemberInfo)o2).Name);
            }
        }

        static FieldComparer fieldComparator = new FieldComparer();

        internal void  buildFieldList(StorageImpl storage, Type cls, ArrayList list)
        {
#if WINRT_NET_FRAMEWORK
           Type superclass = cls.GetTypeInfo().BaseType;
#else
           Type superclass = cls.BaseType;
#endif
            if (superclass != null
#if !SILVERLIGHT
                && superclass != typeof(MarshalByRefObject)
#endif
                )
            {
                buildFieldList(storage, superclass, list);
            }
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
            bool isWrapper = typeof(PersistentWrapper).IsAssignableFrom(cls);
            bool hasTransparentAttribute = cls.GetCustomAttributes(typeof(TransparentPersistenceAttribute), true).Length != 0;
#else
            bool hasTransparentAttribute = false;
#endif
#if WINRT_NET_FRAMEWORK
            bool serializeProperties = cls.GetTypeInfo().GetCustomAttributes(typeof(SerializePropertiesAttribute), true).GetEnumerator().MoveNext();
#else
            bool serializeProperties = cls.GetCustomAttributes(typeof(SerializePropertiesAttribute), true).Length != 0;
#endif
            if (serializeProperties)
            {
#if WINRT_NET_FRAMEWORK
                PropertyInfo[] props = Enumerable.ToArray<PropertyInfo>(cls.GetRuntimeProperties());
#else
                PropertyInfo[] props = cls.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
#endif
                Array.Sort(props, 0, props.Length, fieldComparator);
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo prop = props[i];
#if WINRT_NET_FRAMEWORK
                    if (prop.GetCustomAttributes(typeof(NonSerializedAttribute), true).GetEnumerator().MoveNext()
                        || prop.GetCustomAttributes(typeof(TransientAttribute), true).GetEnumerator().MoveNext())
#else
                        if (prop.GetCustomAttributes(typeof(NonSerializedAttribute), true).Length != 0
                        || prop.GetCustomAttributes(typeof(TransientAttribute), true).Length != 0)
#endif
                    {
                        continue;
                    }
                    FieldDescriptor fd = new FieldDescriptor();
                    fd.property = prop;
                    fd.fieldName = prop.Name;
                    fd.className = getTypeName(cls);
                    Type fieldType = prop.PropertyType;
                    FieldType type = getTypeCode(fieldType);
                    switch (type) 
                    {
#if USE_GENERICS
                        case FieldType.tpArrayOfOid:
                            fd.constructor = GetConstructor(fieldType, "ConstructArray");
                            hasReferences = true;
                            break;
                        case FieldType.tpLink:
                            fd.constructor = GetConstructor(fieldType, "ConstructLink");
                            hasReferences = true;
                            break;
#else
                        case FieldType.tpArrayOfOid:
                        case FieldType.tpLink:
#endif
                        case FieldType.tpArrayOfObject:
                        case FieldType.tpObject:
                            hasReferences = true;
                            if (hasTransparentAttribute && isPerstInternalType(fieldType))
                            {
                                fd.recursiveLoading = true; 
                            }
                            break;
                        case FieldType.tpValue:
                        case FieldType.tpNullableValue:
                            fd.valueDesc = storage.getClassDescriptor(fieldType);
                            hasReferences |= fd.valueDesc.hasReferences;
                            break;
                        case FieldType.tpArrayOfValue:
                            fd.valueDesc = storage.getClassDescriptor(fieldType.GetElementType());
                            hasReferences |= fd.valueDesc.hasReferences;
                            break;
                    }
                    fd.type = type;
                    list.Add(fd);
                }
            }   
            else  
            {            
#if WINRT_NET_FRAMEWORK
                FieldInfo[] flds = Enumerable.ToArray<FieldInfo>(cls.GetRuntimeFields());
#else
                FieldInfo[] flds = cls.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
#endif
                Array.Sort(flds, 0, flds.Length, fieldComparator);
                for (int i = 0; i < flds.Length; i++)
                {
                    FieldInfo f = flds[i];
#if WINRT_NET_FRAMEWORK
                    if (!f.IsStatic && !typeof(Delegate).GetTypeInfo().IsAssignableFrom(f.FieldType.GetTypeInfo()))
#else
                    if (!f.IsNotSerialized && !f.IsStatic && !typeof(Delegate).IsAssignableFrom(f.FieldType))
#endif
                    {
#if WINRT_NET_FRAMEWORK
                        if (f.GetCustomAttributes(typeof(NonSerializedAttribute), true).GetEnumerator().MoveNext()
                            || f.GetCustomAttributes(typeof(TransientAttribute), true).GetEnumerator().MoveNext())
#else
                            if (f.GetCustomAttributes(typeof(NonSerializedAttribute), true).Length != 0
                            || f.GetCustomAttributes(typeof(TransientAttribute), true).Length != 0)
#endif
                        {
                            continue;
                        }
                        FieldDescriptor fd = new FieldDescriptor();
                        fd.field = f;
                        fd.fieldName = f.Name;
                        fd.className = getTypeName(cls);
                        Type fieldType = f.FieldType;
                        FieldType type = getTypeCode(fieldType);
                        switch (type) 
                        {
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
                            case FieldType.tpInt:
                                if (isWrapper && isObjectProperty(cls, f)) 
                                {
                                    hasReferences = true;
                                    type = FieldType.tpOid;
                                } 
                                break;
#endif
#if USE_GENERICS
                            case FieldType.tpArrayOfOid:
                                fd.constructor = GetConstructor(fieldType, "ConstructArray");
                                hasReferences = true;
                                break;
                            case FieldType.tpLink:
                                fd.constructor = GetConstructor(fieldType, "ConstructLink");
                                hasReferences = true;
                                break;
#else
                            case FieldType.tpArrayOfOid:
                            case FieldType.tpLink:
#endif
                            case FieldType.tpArrayOfObject:
                            case FieldType.tpObject:
                                hasReferences = true;
                                if (hasTransparentAttribute && isPerstInternalType(fieldType))
                                {
                                    fd.recursiveLoading = true; 
                                }
                                break;
                            case FieldType.tpValue:
                            case FieldType.tpNullableValue:
                                fd.valueDesc = storage.getClassDescriptor(fieldType);
                                hasReferences |= fd.valueDesc.hasReferences;
                                break;
                            case FieldType.tpArrayOfValue:
                                fd.valueDesc = storage.getClassDescriptor(fieldType.GetElementType());
                                hasReferences |= fd.valueDesc.hasReferences;
                                break;
                        }
                        fd.type = type;
                        list.Add(fd);
                    }
                }
            }
        }
		
        public static bool IsEmbedded(object obj)
        {
            if (obj != null)
            {
                Type t = obj.GetType();
#if WINRT_NET_FRAMEWORK
                return t.GetTypeInfo().IsPrimitive || t.IsArray || t.GetTypeInfo().IsValueType || t == typeof(string);
#else
                return t.IsPrimitive || t.IsArray || t.IsValueType || t == typeof(string);
#endif
            }
            return false;
        }

        public static FieldType getTypeCode(Type c)
        {
            FieldType type;
            if (c.Equals(typeof(byte)))
            {
                type = FieldType.tpByte;
            }
            else if (c.Equals(typeof(sbyte)))
            {
                type = FieldType.tpSByte;
            }
            else if (c.Equals(typeof(short)))
            {
                type = FieldType.tpShort;
            }
            else if (c.Equals(typeof(ushort)))
            {
                type = FieldType.tpUShort;
            }
            else if (c.Equals(typeof(char)))
            {
                type = FieldType.tpChar;
            }
            else if (c.Equals(typeof(int)))
            {
                type = FieldType.tpInt;
            }
            else if (c.Equals(typeof(uint)))
            {
                type = FieldType.tpUInt;
            }
            else if (c.Equals(typeof(long)))
            {
                type = FieldType.tpLong;
            }
            else if (c.Equals(typeof(ulong)))
            {
                type = FieldType.tpULong;
            }
            else if (c.Equals(typeof(float)))
            {
                type = FieldType.tpFloat;
            }
            else if (c.Equals(typeof(double)))
            {
                type = FieldType.tpDouble;
            }
            else if (c.Equals(typeof(string)))
            {
                type = FieldType.tpString;
            }
            else if (c.Equals(typeof(bool)))
            {
                type = FieldType.tpBoolean;
            }
            else if (c.Equals(typeof(DateTime)))
            {
                type = FieldType.tpDate;
            }
            else if (c.Equals(typeof(decimal)))
            { 
                type = FieldType.tpDecimal;
            }
            else if (c.Equals(typeof(Guid))) 
            { 
                type = FieldType.tpGuid;
            }
            else if (c.Equals(typeof(Type))) 
            { 
                type = FieldType.tpType;
            }
#if NET_FRAMEWORK_20
#if WINRT_NET_FRAMEWORK
            else if (c.GetTypeInfo().IsGenericType && c.GetGenericTypeDefinition() == typeof(Nullable<>))
#else
            else if (c.IsGenericType && c.GetGenericTypeDefinition() == typeof(Nullable<>))
#endif
            {
                if (c.Equals(typeof(byte?)))
                {
                    type = FieldType.tpNullableByte;
                }
                else if (c.Equals(typeof(sbyte?)))
                {
                    type = FieldType.tpNullableSByte;
                }
                else if (c.Equals(typeof(short?)))
                {
                    type = FieldType.tpNullableShort;
                }
                else if (c.Equals(typeof(ushort?)))
                {
                    type = FieldType.tpNullableUShort;
                }
                else if (c.Equals(typeof(char?)))
                {
                    type = FieldType.tpNullableChar;
                }
                else if (c.Equals(typeof(int?)))
                {
                    type = FieldType.tpNullableInt;
                }
                else if (c.Equals(typeof(uint?)))
                {
                    type = FieldType.tpNullableUInt;
                }
                else if (c.Equals(typeof(long?)))
                {
                    type = FieldType.tpNullableLong;
                }
                else if (c.Equals(typeof(ulong?)))
                {
                    type = FieldType.tpNullableULong;
                }
                else if (c.Equals(typeof(float?)))
                {
                    type = FieldType.tpNullableFloat;
                }
                else if (c.Equals(typeof(double?)))
                {
                    type = FieldType.tpNullableDouble;
                }
                else if (c.Equals(typeof(bool?)))
                {
                    type = FieldType.tpNullableBoolean;
                }
                else if (c.Equals(typeof(System.DateTime?)))
                {
                    type = FieldType.tpNullableDate;
                }
#if WINRT_NET_FRAMEWORK
                else if (c.GetTypeInfo().IsEnum) 
#else
                else if (c.IsEnum) 
#endif
                { 
                    type = FieldType.tpNullableEnum;
                }
                else if (c.Equals(typeof(decimal?)))
                { 
                    type = FieldType.tpNullableDecimal;
                }
                else if (c.Equals(typeof(Guid?))) 
                { 
                    type = FieldType.tpNullableGuid;
                }
#if WINRT_NET_FRAMEWORK
                else if (typeof(ValueType).GetTypeInfo().IsAssignableFrom(c.GenericTypeArguments[0].GetTypeInfo()))
#else
                else if (typeof(ValueType).IsAssignableFrom(c.GetGenericArguments()[0]))
#endif
               {
                    type = FieldType.tpNullableValue;
                } 
                else 
                {
                    throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_TYPE, c);
                }                   
            }
#endif
#if WINRT_NET_FRAMEWORK
            else if (c.GetTypeInfo().IsEnum) 
#else
            else if (c.IsEnum) 
#endif
            { 
                type = FieldType.tpEnum;
            }
#if WINRT_NET_FRAMEWORK
            else if (typeof(ValueType).GetTypeInfo().IsAssignableFrom(c.GetTypeInfo()))
#else
            else if (typeof(ValueType).IsAssignableFrom(c))
#endif
            {
                type = FieldType.tpValue;
            }
#if WINRT_NET_FRAMEWORK
            else if (typeof(GenericPArray).GetTypeInfo().IsAssignableFrom(c.GetTypeInfo()))
            {
                type = FieldType.tpArrayOfOid;
            }
            else if (typeof(GenericLink).GetTypeInfo().IsAssignableFrom(c.GetTypeInfo()))
            {
                type = FieldType.tpLink;
            }
            else if (typeof(CustomSerializable).GetTypeInfo().IsAssignableFrom(c.GetTypeInfo()))
            {
                type = FieldType.tpCustom;
            }
#else
            else if (typeof(GenericPArray).IsAssignableFrom(c))
            {
                type = FieldType.tpArrayOfOid;
            }
            else if (typeof(GenericLink).IsAssignableFrom(c))
            {
                type = FieldType.tpLink;
            }
            else if (typeof(CustomSerializable).IsAssignableFrom(c))
            {
                type = FieldType.tpCustom;
            }
#endif
            else if (c.IsArray)
            {
                type = getTypeCode(c.GetElementType());
                if ((int)type >= (int)FieldType.tpLink)
                {
                    throw new StorageError(StorageError.ErrorCode.UNSUPPORTED_TYPE, c);
                }
                type = (FieldType)((int)type + (int)FieldType.tpArrayOfBoolean);
            }
            else
            {
#if SUPPORT_RAW_TYPE
                type = serializeNonPersistentObjects && !typeof(IPersistent).IsAssignableFrom(c) ? FieldType.tpRaw : FieldType.tpObject;
#else
                type = FieldType.tpObject;
#endif
            }
            return type;
        }
		
        #if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
        static ClassDescriptor()
        {
            AppDomain.CurrentDomain.TypeResolve += new ResolveEventHandler(TypeResolve);
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(AssemblyResolve);
        }
        #endif

        internal ClassDescriptor()
        {
        }
		
        internal ClassDescriptor(StorageImpl storage, Type cls)
        {
            this.cls = cls;
            customSerializable = storage.serializer != null && storage.serializer.IsApplicable(cls);
#if WINRT_NET_FRAMEWORK
            isCollection = typeof(IList).GetTypeInfo().IsAssignableFrom(cls.GetTypeInfo());
            isDictionary = typeof(IDictionary).GetTypeInfo().IsAssignableFrom(cls.GetTypeInfo()); 
#else
            isCollection = typeof(IList).IsAssignableFrom(cls); 
            isDictionary = typeof(IDictionary).IsAssignableFrom(cls); 
#endif
            name = getTypeName(cls);
            ArrayList list = new ArrayList();
            buildFieldList(storage, cls, list);
            allFields = (FieldDescriptor[]) list.ToArray(typeof(FieldDescriptor));
#if WINRT_NET_FRAMEWORK
            foreach (ConstructorInfo ci in cls.GetTypeInfo().DeclaredConstructors)
            {
                if (ci.GetParameters().Length == 0)
                {
                    defaultConstructor = ci;
                    break;
                }
            }
#else
            defaultConstructor = cls.GetConstructor(BindingFlags.Instance|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly, null, defaultConstructorProfile, null);
#endif
#if COMPACT_NET_FRAMEWORK
            if (defaultConstructor == null && !cls.IsInterface && !typeof(ValueType).IsAssignableFrom(cls)) 
            { 
                throw new StorageError(StorageError.ErrorCode.DESCRIPTOR_FAILURE, cls);
            }
#endif
            resolved = true;
        }
		
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.ReflectionOnlyLoad(args.Name);
        }

        private static Assembly TypeResolve(object sender, ResolveEventArgs args)
        {
            Hashtable loadedAssemblies = new Hashtable();
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                loadedAssemblies[ass.FullName] = ass;
            } 
            Assembly entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                AssemblyName[] referencedAssemblies = entryAssembly.GetReferencedAssemblies();
                foreach (AssemblyName name in referencedAssemblies)
                {
                    // Reference already loaded?
                    if (!loadedAssemblies.Contains(name.FullName))
                    {
                        // Load reflection only
                        Assembly assembly = Assembly.ReflectionOnlyLoad(name.FullName);
                        if (assembly.GetType(args.Name, false, false) != null)
                        {
                            // Type found, load assembly in current domain, and return.
                            return AppDomain.CurrentDomain.Load(name);
                        }
                    }
                }
            }
            // Type not found
            return null;
        }
#endif

        internal static Type lookup(Storage storage, String name)
        {
            Hashtable resolvedTypes = ((StorageImpl)storage).resolvedTypes;
            lock (resolvedTypes)
            { 
                Type cls = (Type)resolvedTypes[name];
                if (cls != null)
                { 
                    return cls;
                }
                ClassLoader loader = storage.Loader;
                if (loader != null) 
                { 
                    cls = loader.LoadClass(name);
                    if (cls != null) 
                    { 
                        resolvedTypes[name] = cls;
                        return cls;
                    }
                }
#if !WINRT_NET_FRAMEWORK
                Module last = lastModule;
                if (last != null) 
                {
                    cls = last.GetType(name);
                    if (cls != null) 
                    {
                        resolvedTypes[name] = cls;
                        return cls;
                    }
                }
#endif
#if USE_GENERICS
                int p = name.IndexOf('=');
                if (p >= 0)
                { 
                    Type genericType = lookup(storage, name.Substring(0, p));
                    Type[] genericParams = new Type[genericType.GetGenericArguments().Length];
                    int nest = 0;
                    int i = p += 2; 
                    int n = 0;

                    while (true)
                    {   
                        switch (name[i++])
                        {
                        case '[': 
                            nest += 1;
                            break;
                        case ']': 
                            if (--nest < 0) 
                            { 
                                genericParams[n++] = lookup(storage, name.Substring(p, i-p-1));
                                Debug.Assert(n == genericParams.Length);
                                cls = genericType.MakeGenericType(genericParams);
                                if (cls == null)
                                { 
                                    throw new StorageError(StorageError.ErrorCode.CLASS_NOT_FOUND, name);
                                }
                                resolvedTypes[name] = cls;
                                return cls;
                            }   
                            break;
                        case ',':
                            if (nest == 0) 
                            {
                                genericParams[n++] = lookup(storage, name.Substring(p, i-p-1));
                                p = i;
                            }
                            break;
                        }
                    }
                }
#endif
#if WINRT_NET_FRAMEWORK
                cls = Type.GetType(name, false);
#else
                cls = Type.GetType(name, false, false);
#endif
                if (cls != null)
                {
                    resolvedTypes[name] = cls;
                    return cls;
                }
#if (COMPACT_NET_FRAMEWORK || SILVERLIGHT) && !WINRT_NET_FRAMEWORK
                foreach (Assembly ass in StorageImpl.assemblies) 
#else
                foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies()) 
#endif
                { 
#if WINRT_NET_FRAMEWORK
                    {
                        Type t = ass.GetType(name);
#else
                    foreach (Module mod in ass.GetModules()) 
                    { 
                        Type t = mod.GetType(name);
#endif
                        if (t != null && t != cls) 
                        {
                            if (cls != null) 
                            { 
#if __MonoCS__  || (NET_FRAMEWORK_20 && !COMPACT_NET_FRAMEWORK &&  !SILVERLIGHT)
                                if (Assembly.GetAssembly(cls) != ass)
#endif
                                {
                                    throw new StorageError(StorageError.ErrorCode.AMBIGUITY_CLASS, name);
                                }

                            } 
                            else 
                            { 
#if WINRT_NET_FRAMEWORK
                                lastModule = t.GetTypeInfo().Module;
#else
                                lastModule = mod;
#endif
                                cls = t;
                            }
                        }
                    }
                }
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
                if (cls == null && name.EndsWith("Wrapper")) 
                {
                    Type originalType = lookup(storage, name.Substring(0, name.Length-7));
                    lock (storage) 
                    {
                        cls = ((StorageImpl)storage).getWrapper(originalType);
                    }
                }
#endif
                if (cls == null && !((StorageImpl)storage).ignoreMissedClasses) 
                {
                    throw new StorageError(StorageError.ErrorCode.CLASS_NOT_FOUND, name);
                }
                resolvedTypes[name] = cls;
                return cls;
            }
        }


        internal static FieldInfo lookupField(Type type, string name) 
        {        
            if (type != null)
            {
#if WINRT_NET_FRAMEWORK
                do {
                    FieldInfo f = type.GetTypeInfo().GetDeclaredField(name);
                    if (f != null) 
                    { 
                        return f;
                    }
                } while ((type = type.GetTypeInfo().BaseType) != null);
#else
                do { 
                    FieldInfo f = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (f != null) 
                    { 
                        return f;
                    }
                } while ((type = type.BaseType) != null);
#endif
            }
            return null;
        }
             
        internal static PropertyInfo lookupProperty(Type type, string name) 
        {
            if (type != null)
            {
#if WINRT_NET_FRAMEWORK
                do { 
                    PropertyInfo prop = type.GetTypeInfo().GetDeclaredProperty(name);
                    if (prop != null) 
                    { 
                        return prop;
                    }
                } while ((type = type.GetTypeInfo().BaseType) != null);
#else
                do { 
                    PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (prop != null) 
                    { 
                        return prop;
                    }
                } while ((type = type.BaseType) != null);
#endif
            }
            return null;
        }
             
        internal static MemberInfo lookupComponent(Type cls, string name, out Type compType) 
        {
            FieldInfo fld = lookupField(cls, name);
            compType = null;
            if (fld != null) 
            {
                compType =  fld.FieldType;
                return fld;
            }
#if WINRT_NET_FRAMEWORK
            PropertyInfo prop = cls.GetTypeInfo().GetDeclaredProperty(name);
#else
            PropertyInfo prop = cls.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); 
#endif
            if (prop != null) 
            {
                compType = prop.PropertyType;
                return prop;
            } 
            return null;
        }
            

        public override void OnLoad()
        {
            StorageImpl s = (StorageImpl)Storage;
            cls = lookup(s, name);
            customSerializable = s.serializer != null && s.serializer.IsApplicable(cls);
#if WINRT_NET_FRAMEWORK
            isCollection = cls != null && typeof(IList).GetTypeInfo().IsAssignableFrom(cls.GetTypeInfo());
            isDictionary = cls != null && typeof(IDictionary).GetTypeInfo().IsAssignableFrom(cls.GetTypeInfo()); 
#else
            isCollection = cls != null && typeof(IList).IsAssignableFrom(cls);
            isDictionary = cls != null && typeof(IDictionary).IsAssignableFrom(cls); 
#endif
            int n = allFields.Length;
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
            bool hasTransparentAttribute = cls != null && cls.GetCustomAttributes(typeof(TransparentPersistenceAttribute), true).Length != 0;
#else
            bool hasTransparentAttribute = false;
#endif
            for (int i = n; --i >= 0;) 
            { 
                FieldDescriptor fd = allFields[i];
                fd.Load();
                Type fieldType = null;
                fd.field = lookupField(cls, fd.fieldName);
                if (fd.field == null)
                {
                    fd.property = lookupProperty(cls, fd.fieldName);
                    if (fd.property != null) 
                    {
                        fieldType = fd.property.PropertyType;
                    }
                }
                else
                {
                    fieldType = fd.field.FieldType;
                }
                if (hasTransparentAttribute && fd.type == FieldType.tpObject && isPerstInternalType(fieldType)) 
                {
                    fd.recursiveLoading = true;
                }                
#if USE_GENERICS
                switch (fd.type)
                {
                case FieldType.tpArrayOfOid:
                    fd.constructor = GetConstructor(fd.MemberType, "ConstructArray");
                    break;
                case FieldType.tpLink:
                    fd.constructor = GetConstructor(fd.MemberType, "ConstructLink");
                    break;
                default:
                    break;
                }
#endif
            }
            #if WINRT_NET_FRAMEWORK
            foreach (ConstructorInfo ci in cls.GetTypeInfo().DeclaredConstructors)
            {
                if (ci.GetParameters().Length == 0)
                {
                    defaultConstructor = ci;
                    break;
                }
            }
#else
            defaultConstructor = cls.GetConstructor(BindingFlags.Instance|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly, null, defaultConstructorProfile, null);
#endif
#if COMPACT_NET_FRAMEWORK
            if (defaultConstructor == null && !cls.IsInterface && !typeof(ValueType).IsAssignableFrom(cls)) 
            { 
                throw new StorageError(StorageError.ErrorCode.DESCRIPTOR_FAILURE, cls);
            }
#endif
            if (!s.classDescMap.Contains(cls)) 
            {
                s.classDescMap.Add(cls, this);
            }
        }

        internal void resolve() 
        {
            if (!resolved) 
            { 
                StorageImpl classStorage = (StorageImpl)Storage;
                ClassDescriptor desc = new ClassDescriptor(classStorage, cls);
                resolved = true;
                if (!desc.equals(this)) 
                { 
                    classStorage.registerClassDescriptor(desc);
                }
            }
        }            

        public override bool RecursiveLoading()
        {
            return false;
        }
    }
}