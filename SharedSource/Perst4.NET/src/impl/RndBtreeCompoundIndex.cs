namespace Perst.Impl
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
    using System.Diagnostics;
    using Perst;
    
    [Serializable]
#if USE_GENERICS
    class RndBtreeCompoundIndex<T>:RndBtree<object[],T>, CompoundIndex<T> where T:class
#else
    class RndBtreeCompoundIndex:RndBtree, CompoundIndex
#endif
    {
        ClassDescriptor.FieldType[]    types;

        internal RndBtreeCompoundIndex() 
        {
        }
    
        internal RndBtreeCompoundIndex(Type[] keyTypes, bool unique) 
        {
            this.unique = unique;
            type = ClassDescriptor.FieldType.tpValue;        
            types = new ClassDescriptor.FieldType[keyTypes.Length];
            for (int i = 0; i < keyTypes.Length; i++) 
            {
                types[i] = checkType(keyTypes[i]);
            }
        }

        public Type[] KeyTypes
        {
            get
            { 
                Type[] keyTypes = new Type[types.Length];
                for (int i = 0; i < keyTypes.Length; i++) 
                { 
                     keyTypes[i] = mapKeyType(types[i]);
                }
                return keyTypes;
            }
        }

        internal struct CompoundKey : IComparable
        {
            internal object[] keys;
            
            public int CompareTo(object o)
            {
                CompoundKey c = (CompoundKey) o;
                int n = keys.Length < c.keys.Length?keys.Length:c.keys.Length;
                for (int i = 0; i < n; i++)
                {
                    int diff = ((IComparable) keys[i]).CompareTo(c.keys[i]);
                    if (diff != 0)
                    {
                        return diff;
                    }
                }
                return  0; // allow to compare part of the compound key
            }
            
            internal CompoundKey(object[] keys)
            {
                this.keys = keys;
            }
        }
        
        private Key convertKey(Key key) 
        { 
            return convertKey(key, true);
        }

        private Key convertKey(Key key, bool prefix) 
        {
            if (key == null)
            {
                return null;
            }
            if (key.type != ClassDescriptor.FieldType.tpArrayOfObject)
            {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            Object[] keyComponents = (Object[])key.oval;
            if ((!prefix && keyComponents.Length != types.Length) || keyComponents.Length > types.Length)
            { 
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            return new Key(new CompoundKey(keyComponents), key.inclusion != 0);
        }
        
#if USE_GENERICS
        public override T Remove(Key key) 
#else
        public override object Remove(Key key) 
#endif
        {
            return base.Remove(convertKey(key, false));
        }       

#if USE_GENERICS
        public override void Remove(Key key, T obj)
#else
        public override void Remove(Key key, object obj)
#endif
        {

            base.Remove(convertKey(key, false), obj);
        }       

#if !USE_GENERICS
        public override object[] Get(Key from, Key till)
#else 
        public override T[] Get(Key from, Key till)
#endif
        {
            return base.Get(convertKey(from), convertKey(till));
        }

#if USE_GENERICS
        public override T Get(Key key) 
#else
        public override object Get(Key key) 
#endif
        {
            return base.Get(convertKey(key));
        }

#if USE_GENERICS
        public override bool Put(Key key, T obj)
#else
        public override bool Put(Key key, object obj)
#endif
        {
            return base.Put(convertKey(key, false), obj);
        }

#if USE_GENERICS
        public override T Set(Key key, T obj)
#else
        public override object Set(Key key, object obj)
#endif
        {
            return base.Set(convertKey(key, false), obj);
        }

#if USE_GENERICS
        public override IEnumerable<T> Range(Key from, Key till, IterationOrder order) 
#else
        public override IEnumerable Range(Key from, Key till, IterationOrder order) 
#endif
        { 
            return base.Range(convertKey(from), convertKey(till), order);
        }


        public override IDictionaryEnumerator GetDictionaryEnumerator(Key from, Key till, IterationOrder order) 
        {
            return base.GetDictionaryEnumerator(convertKey(from), convertKey(till), order);
        }
    }
}