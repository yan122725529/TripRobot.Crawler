#if NET_FRAMEWORK_20
namespace Perst
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// This class provide small embedded map (collection of &lt;key,value&gt; pairs).
    /// Pairs are stored in the array in the order of their insertion.
    /// Consequently operations with this map has linear ccomplexity.
    /// </summary>
    public class SmallDictionary<K,V> : PersistentResource, IDictionary<K,V>
    {
        internal Pair<K,V>[] pairs;
    
        public SmallDictionary() 
        {
            pairs = new Pair<K,V>[0];
        }
    
        public SmallDictionary(ICollection<KeyValuePair<K,V>> c) 
        {
            pairs = new Pair<K,V>[c.Count];
            int i = 0;
            foreach (KeyValuePair<K,V> pair in c)
            {
                pairs[i++] = new Pair<K,V>(pair.Key, pair.Value);
            }
        }
    
        public int Count
        {
            get
            { 
                return pairs.Length;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool ContainsKey(K key) 
        {
            V value;    
            return TryGetValue(key, out value);
        }

        public bool Contains(KeyValuePair<K,V> pair) 
        {
            V value;
            return TryGetValue(pair.Key, out value) && object.Equals(value, pair.Value);
        }

        public bool Remove(KeyValuePair<K,V> pair) 
        {
            return Contains(pair) && Remove(pair.Key);
        }

        public bool Contains(V value) 
        {
            for (int i = 0; i < pairs.Length; i++) 
            { 
                 if (object.Equals(pairs[i].value, value)) 
                 {
                     return true;
                 }
            }
            return false;
        }

        public bool TryGetValue(K key, out V val)
        {
            for (int i = 0; i < pairs.Length; i++) 
            { 
                if (object.Equals(pairs[i].key, key))
                {
                    val = pairs[i].value;
                    return true;
                }
            }
            val = default(V);
            return false;
        }

        public void Add(K key, V value)
        {
            int n = pairs.Length;
            for (int i = 0; i < n; i++) 
            { 
                if (object.Equals(key, pairs[i].key)) 
                {
                    V oldValue = pairs[i].value;
                    pairs[i].value = value;
                    Modify();
                    return;
                }
            }
            Pair<K,V>[] newPairs = new Pair<K,V>[n+1];
            Array.Copy(pairs, 0, newPairs, 0, n);
            newPairs[n] = new Pair<K,V>(key, value);
            pairs = newPairs;
            Modify();
        }

        public V this[K key] 
        {
            get
            {
                V value;    
                if (!TryGetValue(key, out value))
                {
                    throw new KeyNotFoundException();
                }                
                return value;
            }
    
            set
            {
                Add(key, value);
            }
        }
    
        public bool Remove(K key) 
        {
            for (int i = 0; i < pairs.Length; i++) 
            { 
                if (object.Equals(key, pairs[i].key)) 
                {
                    RemoveAt(i);                    
                    return true;
                }
            }
            return false;
        }

        public void RemoveAt(int i) 
        {
            Pair<K,V> pair = pairs[i];
            Pair<K,V>[] newPairs = new Pair<K,V>[pairs.Length-1];
            Array.Copy(pairs, 0, newPairs, 0, i);
            Array.Copy(pairs, i+1, newPairs, i, pairs.Length-i-1);
            pairs = newPairs;
            Modify();
        }

        public void CopyTo(KeyValuePair<K,V>[] dst, int i) 
        {
            for (int j = 0; j < pairs.Length; j++)
            {
               dst[i++] = new KeyValuePair<K,V>(pairs[j].key, pairs[j].value);
            }
        }

        public virtual bool IsSynchronized 
        {
            get 
            {
                return true;
            }
        }

        public virtual object SyncRoot 
        {
            get 
            {
                return this;
            }
        }

        public void Clear() 
        { 
            pairs = new Pair<K,V>[0];
            Modify();
        }
    
        public void Add(KeyValuePair<K,V> pair)
        {
            int n = pairs.Length;
            Pair<K,V>[] newPairs = new Pair<K,V>[n+1];
            Array.Copy(pairs, 0, newPairs, 0, n);
            newPairs[n] = new Pair<K,V>(pair.Key, pair.Value);
            pairs = newPairs;
            Modify();
        }

        public ICollection<K> Keys
        {
            get
            {
                K[] keys = new K[pairs.Length];
                for (int i = 0; i < pairs.Length; i++)
                {
                    keys[i] = pairs[i].key;
                }        
                return keys;
            }
        }

        public ICollection<V> Values
        {
            get
            {
                V[] values = new V[pairs.Length];
                for (int i = 0; i < pairs.Length; i++)
                {
                    values[i] = pairs[i].value;
                }        
                return values;
            }
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ArrayEnumerator(this);
        }
        
        public IEnumerator<KeyValuePair<K,V>> GetEnumerator()
        {
            return new ArrayEnumerator(this);
        }        
        
        class ArrayEnumerator : IEnumerator<KeyValuePair<K,V>>, IDictionaryEnumerator
        {
            SmallDictionary<K,V> dictionary;
            int current;

            internal ArrayEnumerator(SmallDictionary<K,V> dictionary) 
            {
                this.dictionary = dictionary;
                current = -1;
            }

            public bool MoveNext()
            {
                return current++ < dictionary.pairs.Length;
            }
            
            object IDictionaryEnumerator.Key
            {
                get
                {
                    return Current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    return Current.Value;
                }
            }

            
            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    return new DictionaryEntry(Current.Key, Current.Value);
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return new DictionaryEntry(Current.Key, Current.Value);
                }
            }
            
            public KeyValuePair<K,V> Current
            {
                get
                {
                    return new KeyValuePair<K,V>(dictionary.pairs[current].key, dictionary.pairs[current].value); 
                }
            }
            
            public void Dispose()
            {
            }
            
            public void Reset() 
            {
                current = -1;
            }
        }

        internal struct Pair<K,V>
        {
            internal K key;
            internal V value;

            internal Pair(K k, V v) 
            {
                key = k;
                value = v;
            }
        }
    }
}
#endif

