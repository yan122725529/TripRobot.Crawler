using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst
{
    /// <summary> 
    /// PATRICIA trie (Practical Algorithm To Retrieve Information Coded In Alphanumeric).
    /// Tries are a kind of tree where each node holds a common part of one or more keys. 
    /// PATRICIA trie is one of the many existing variants of the trie, which adds path compression 
    /// by grouping common sequences of nodes together.
    /// This structure provides a very efficient way of storing values while maintaining the lookup time 
    /// for a key in O(N) in the worst case, where N is the length of the longest key. 
    /// This structure has it's main use in IP routing software, but can provide an interesting alternative 
    /// to other structures such as hashtables when memory space is of concern.
    /// </summary>
#if USE_GENERICS
    public interface PatriciaTrie<T> : IPersistent, IResource, ITable<T> where T:class
#else
    public interface PatriciaTrie : IPersistent, IResource, ITable
#endif
    { 
        /// <summary> 
        /// Add new key to the trie
        /// </summary>
        /// <param name="key">bit vector</param>
        /// <param name="obj">persistent object associated with this key</param>
        /// <returns>previous object associtated with this key or <b>null</b> if there
        /// was no such object</returns>
        ///
#if USE_GENERICS
        T Add(PatriciaTrieKey key, T obj);
#else
        object Add(PatriciaTrieKey key, object obj);
#endif
    
        /// <summary>
        /// Find best match with specified key
        /// </summary>
        /// <param name="key">bit vector</param>
        /// <returns>object associated with this deepest possible match with specified key</returns>
        ///
#if USE_GENERICS
        T FindBestMatch(PatriciaTrieKey key);
#else
        object FindBestMatch(PatriciaTrieKey key);
#endif
    
        /// <summary>
        /// Find exact match with specified key
        /// </summary>
        /// <param name="key">bit vector</param>
        /// <returns>object associated with this key or NULL if match is not found</returns>
        ///
#if USE_GENERICS
        T FindExactMatch(PatriciaTrieKey key);
#else
        object FindExactMatch(PatriciaTrieKey key);
#endif
    
        /// <summary>
        /// Removes key from the triesKFind exact match with specified key
        /// </summary>
        /// <param name="key">bit vector</param>
        /// <returns>object associated with removed key or <b>null</b> if such key is not found</returns>
        ///
#if USE_GENERICS
        T Remove(PatriciaTrieKey key);
#else
        object Remove(PatriciaTrieKey key);
#endif
    }
}
