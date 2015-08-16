namespace Perst.Impl
{
    using System;
    using Perst;

#if USE_GENERICS
    public class DefaultPersistentComparator<K,V> : PersistentComparator<K,V> where V:class,IComparable<V>,IComparable<K> 
    { 
        public override int CompareMembers(V m1, V m2) {
            return m1.CompareTo(m2);
        }
        
        public override int CompareMemberWithKey(V mbr, K key) { 
            return mbr.CompareTo(key);
        }
    }
#else
    public class DefaultPersistentComparator : PersistentComparator { 
        public override int CompareMembers(object m1, object m2) {
            return ((IComparable)m1).CompareTo(m2);
        }
        
        public override int CompareMemberWithKey(object mbr, object key) { 
            return ((IComparable)mbr).CompareTo(key);
        }
    }
#endif
}