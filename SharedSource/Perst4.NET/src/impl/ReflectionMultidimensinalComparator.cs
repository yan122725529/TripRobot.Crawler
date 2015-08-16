namespace Perst.Impl
{
    using System;
    using System.Reflection;
    using Perst;

#if USE_GENERICS
    public class ReflectionMultidimensionalComparator<T> : MultidimensionalComparator<T> where T:class 
#else
    public class ReflectionMultidimensionalComparator : MultidimensionalComparator
#endif
    {
        private String   className;
        private String[] fieldNames;
        private bool     treateZeroAsUndefinedValue;

        [NonSerialized]
        private Type cls;
        [NonSerialized]
        private MemberInfo[] members;
        [NonSerialized]
        private ClassDescriptor desc;

        public override void OnLoad()
        {
            cls = ClassDescriptor.lookup(Storage, className);
#if USE_GENERICS
            if (cls != typeof(T)) 
            {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_VALUE_TYPE, cls);
            }
#endif
            locateMembers();
        }

        private void locateMembers()
        {
            if (fieldNames == null)
            {
#if WINRT_NET_FRAMEWORK
                members = Enumerable.ToArray<FieldInfo>(cls.GetRuntimeFields());
#else
                members = cls.GetMembers(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
#endif
            }
            else
            {
                members = new FieldInfo[fieldNames.Length];
                for (int i = 0; i < members.Length; i++)
                {
                    Type mbrType;
                    members[i] = ClassDescriptor.lookupComponent(cls, fieldNames[i], out mbrType);
                }
            }
        }


        private IComparable getComponentValue(object obj, int i)
        {
            MemberInfo mbr = members[i];
            return (IComparable)((mbr is FieldInfo) ? ((FieldInfo)mbr).GetValue(obj) : ((PropertyInfo)mbr).GetValue(obj, null));
        }

        private void setComponentValue(object obj, int i, object value)
        {
            MemberInfo mbr = members[i];
            if (mbr is FieldInfo)
            {
                ((FieldInfo)mbr).SetValue(obj, value);
            }
            else
            {
                ((PropertyInfo)mbr).SetValue(obj, value, null);
            }
        }

        private static bool isZero(object val)
        {
            return val is float ? (float)val == 0.0
                : val is double ? (double)val == 0.0
#if WINRT_NET_FRAMEWORK
                : val is int ? (int)val == 0
                : val is uint ? (uint)val == 0
                : val is long ? (long)val == 0
                : val is ulong ? (ulong)val == 0
                : val is short ? (short)val == 0
                : val is ushort ? (ushort)val == 0
                : val is byte ? (byte)val == 0
                : val is sbyte ? (sbyte)val == 0 : false;
#else
                : val is IConvertible && !(val is string || val is bool)
                  ? ((IConvertible)val).ToInt64(null) == 0 : false; 
#endif
        }

#if USE_GENERICS
        public override CompareResult Compare(T m1, T m2, int i)
#else
        public override CompareResult Compare(object m1, object m2, int i)
#endif
        {
            IComparable c1 = getComponentValue(m1, i);
            IComparable c2 = getComponentValue(m2, i);
            if (c1 == null && c2 == null)
            {
                return CompareResult.EQ;
            }
            else if (c1 == null || (treateZeroAsUndefinedValue && isZero(c1)))
            {
                return CompareResult.LEFT_UNDEFINED;
            }
            else if (c2 == null || (treateZeroAsUndefinedValue && isZero(c2)))
            {
                return CompareResult.RIGHT_UNDEFINED;
            }
            else
            {
                int diff = c1.CompareTo(c2);
                return diff < 0 ? CompareResult.LT : diff == 0 ? CompareResult.EQ : CompareResult.GT;
            }
        }

        public override int NumberOfDimensions
        {
            get
            {
                return members.Length;
            }
        }

#if USE_GENERICS
        public override T CloneField(T obj, int i)
#else
        public override object CloneField(object obj, int i)
#endif
        {
            if (desc == null)
            {
                desc = ((StorageImpl)Storage).findClassDescriptor(cls);
            }
#if USE_GENERICS
            T clone = (T)desc.newInstance();
#else
            object clone = desc.newInstance();
#endif
            setComponentValue(clone, i, getComponentValue(obj, i));
            return clone;
        }

#if USE_GENERICS
    public ReflectionMultidimensionalComparator(Storage storage, string[] fieldNames, bool treateZeroAsUndefinedValue) 
        : base(storage)
    { 
        this.cls = typeof(T);
#else
        public ReflectionMultidimensionalComparator(Storage storage, Type cls, string[] fieldNames, bool treateZeroAsUndefinedValue)
            : base(storage)
        {
            this.cls = cls;
#endif
            this.fieldNames = fieldNames;
            this.treateZeroAsUndefinedValue = treateZeroAsUndefinedValue;
            className = ClassDescriptor.getTypeName(cls);
            locateMembers();
        }

        protected ReflectionMultidimensionalComparator()
        {
        }
    }

}