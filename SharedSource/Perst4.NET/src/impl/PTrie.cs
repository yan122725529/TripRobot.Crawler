using System;
using Perst;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst.Impl
{
#if USE_GENERICS
    internal class PTrie<T> : PersistentCollection<T>, PatriciaTrie<T>  where T:class 
#else
    internal class PTrie : PersistentCollection, PatriciaTrie 
#endif
    { 
        internal PTrieNode rootZero;
        internal PTrieNode rootOne;
        internal int       count;


#if USE_GENERICS
        public override IEnumerator<T> GetEnumerator() 
        {
            List<T> list = new List<T>();
#else
        public override IEnumerator GetEnumerator() 
        {
            ArrayList list = new ArrayList();
#endif
            fill(list, rootZero);
            fill(list, rootOne);
            return list.GetEnumerator();
        }

#if USE_GENERICS
        private static void fill(List<T> list, PTrieNode node) { 
#else
        private static void fill(ArrayList list, PTrieNode node) { 
#endif
            if (node != null) {
                list.Add(node.obj);
                fill(list, node.childZero);
                fill(list, node.childOne);
            }
        }

        public override int Count 
        { 
            get 
            {
                return count;
            }
        }

        private static int firstBit(ulong key, int keyLength)
        {
            return (int)(key >> (keyLength - 1)) & 1;
        }

        private static int getCommonPartLength(ulong keyA, int keyLengthA, ulong keyB, int keyLengthB)
        {
            if (keyLengthA > keyLengthB) 
            {
                keyA >>= keyLengthA - keyLengthB;
                keyLengthA = keyLengthB;
            } 
            else 
            {
                keyB >>= keyLengthB - keyLengthA;
                keyLengthB = keyLengthA;
            }
            ulong diff = keyA ^ keyB;
        
            int count = 0;
            while (diff != 0) 
            {
                diff >>= 1;
                count += 1;
            }
            return keyLengthA - count;
        }

#if USE_GENERICS
        public T Add(PatriciaTrieKey key, T obj) 
#else
        public object Add(PatriciaTrieKey key, object obj) 
#endif
        { 
            Modify();
            count += 1;

            if (firstBit(key.mask, key.length) == 1) 
            {
                if (rootOne != null) 
                { 
                    return rootOne.add(key.mask, key.length, obj);
                } 
                else 
                { 
                    rootOne = new PTrieNode(key.mask, key.length, obj);
                    return null;
                }
            } 
            else 
            { 
                if (rootZero != null) 
                { 
                    return rootZero.add(key.mask, key.length, obj);
                } 
                else 
                { 
                    rootZero = new PTrieNode(key.mask, key.length, obj);
                    return null;
                }
            }            
        }
    
#if USE_GENERICS
        public T FindBestMatch(PatriciaTrieKey key) 
#else
        public object FindBestMatch(PatriciaTrieKey key) 
#endif
        {
            if (firstBit(key.mask, key.length) == 1) 
            {
                if (rootOne != null) 
                { 
                    return rootOne.findBestMatch(key.mask, key.length);
                } 
            } 
            else 
            { 
                if (rootZero != null) 
                { 
                    return rootZero.findBestMatch(key.mask, key.length);
                } 
            }
            return null;
        }
    

#if USE_GENERICS
        public T FindExactMatch(PatriciaTrieKey key) 
#else
        public object FindExactMatch(PatriciaTrieKey key) 
#endif
        {
            if (firstBit(key.mask, key.length) == 1) 
            {
                if (rootOne != null) 
                { 
                    return rootOne.findExactMatch(key.mask, key.length);
                } 
            } 
            else 
            { 
                if (rootZero != null) 
                { 
                    return rootZero.findExactMatch(key.mask, key.length);
                } 
            }
            return null;
        }
    
#if USE_GENERICS
        public T Remove(PatriciaTrieKey key) 
        { 
             T obj;
#else
        public object Remove(PatriciaTrieKey key) 
        { 
            object obj;
#endif
            if (firstBit(key.mask, key.length) == 1) 
            {
                if (rootOne != null) 
                { 
                    obj = rootOne.remove(key.mask, key.length);
                    if (obj != null) 
                    { 
                        Modify();
                        count -= 1;
                        if (rootOne.isNotUsed()) 
                        { 
                            rootOne.Deallocate();
                            rootOne = null;
                        }
                        return obj;
                    }
                }  
            } 
            else 
            { 
                if (rootZero != null) 
                { 
                    obj = rootZero.remove(key.mask, key.length);
                    if (obj != null) 
                    { 
                        Modify();
                        count -= 1;
                        if (rootZero.isNotUsed()) 
                        { 
                            rootZero.Deallocate();
                            rootZero = null;
                        }
                        return obj;
                    }
                }  
            }
            return null;
        }

        public override void Clear() 
        {
            if (rootOne != null) 
            { 
                rootOne.Deallocate();
                rootOne = null;
            }
            if (rootZero != null) 
            { 
                rootZero.Deallocate();
                rootZero = null;
            }
            count = 0;
        }

        internal class PTrieNode : Persistent 
        {
            internal ulong       key;
            internal int         keyLength;
#if USE_GENERICS
            internal T           obj;
#else
            internal object obj;
#endif
            internal PTrieNode   childZero;
            internal PTrieNode   childOne;

#if USE_GENERICS
            internal PTrieNode(ulong key, int keyLength, T obj)
#else
            internal PTrieNode(ulong key, int keyLength, object obj)
#endif
            {
                this.obj = obj;
                this.key = key;
                this.keyLength = keyLength; 
            }

            internal PTrieNode() {}

#if USE_GENERICS
            internal T add(ulong key, int keyLength, T obj) 
            {
                T prevObj;
#else
            internal object add(ulong key, int keyLength, object obj) 
            {
                object prevObj;
#endif
                if (key == this.key && keyLength == this.keyLength) 
                {
                    Modify();
                    prevObj = this.obj;
                    this.obj = obj;
                    return prevObj;
                }
                int keyLengthCommon = getCommonPartLength(key, keyLength, this.key, this.keyLength);
                int keyLengthDiff = this.keyLength - keyLengthCommon;
                ulong keyCommon = key >> (keyLength - keyLengthCommon);
                ulong keyDiff = this.key - (keyCommon << keyLengthDiff);
                if (keyLengthDiff > 0) 
                {
                    Modify();
                    PTrieNode newNode = new PTrieNode(keyDiff, keyLengthDiff, this.obj);
                    newNode.childZero = childZero;
                    newNode.childOne = childOne;
                
                    this.key = keyCommon;
                    this.keyLength = keyLengthCommon;
                    this.obj = null;
                
                    if (firstBit(keyDiff, keyLengthDiff) == 1) 
                    {
                        childZero = null;
                        childOne = newNode;
                    } 
                    else 
                    {
                        childZero = newNode;
                        childOne = null;
                    }
                }
            
                if (keyLength > keyLengthCommon) 
                {
                    keyLengthDiff = keyLength - keyLengthCommon;
                    keyDiff = key - (keyCommon << keyLengthDiff);
                
                    if (firstBit(keyDiff, keyLengthDiff) == 1) 
                    {
                        if (childOne != null) 
                        {
                            return childOne.add(keyDiff, keyLengthDiff, obj);
                        } 
                        else 
                        { 
                            Modify();
                            childOne = new PTrieNode(keyDiff, keyLengthDiff, obj);
                            return null;
                        }
                    } 
                    else 
                    {
                        if (childZero != null) 
                        { 
                            return childZero.add(keyDiff, keyLengthDiff, obj);
                        } 
                        else 
                        { 
                            Modify();
                            childZero = new PTrieNode(keyDiff, keyLengthDiff, obj);
                            return null;
                        }
                    }
                } 
                else 
                {
                    prevObj = this.obj;
                    this.obj = obj;
                    return prevObj;
                }            
            }
    
        
#if USE_GENERICS
            internal T findBestMatch(ulong key, int keyLength) 
#else
            internal object findBestMatch(ulong key, int keyLength) 
#endif
            {             
                if (keyLength > this.keyLength) 
                { 
                    int keyLengthCommon = getCommonPartLength(key, keyLength, this.key, this.keyLength);
                    int keyLengthDiff = keyLength - keyLengthCommon;
                    ulong keyCommon = key >> keyLengthDiff;
                    ulong keyDiff = key - (keyCommon << keyLengthDiff);

                    if (firstBit(keyDiff, keyLengthDiff) == 1) 
                    {
                        if (childOne != null) 
                        { 
                            return childOne.findBestMatch(keyDiff, keyLengthDiff);
                        }
                    } 
                    else 
                    {
                        if (childZero != null) 
                        { 
                            return childZero.findBestMatch(keyDiff, keyLengthDiff);
                        }
                    }
                }
                return obj;
            }
				
#if USE_GENERICS
            internal T findExactMatch(ulong key, int keyLength) 
            {             
                T match = null;
#else
            internal object findExactMatch(ulong key, int keyLength) 
            {             
                object match = null;
#endif
                
                if (keyLength >= this.keyLength) 
                { 
                    if (key == this.key && keyLength == this.keyLength) 
                    { 
                        match = obj;
                    } 
                    else 
                    { 
                        int keyLengthCommon = getCommonPartLength(key, keyLength, this.key, this.keyLength);
                        if (keyLengthCommon == this.keyLength) 
                        { 
                            int keyLengthDiff = keyLength - keyLengthCommon;
                            ulong keyCommon = key >> keyLengthDiff;
                            ulong keyDiff = key - (keyCommon << keyLengthDiff);
                        
                            if (firstBit(keyDiff, keyLengthDiff) == 1) 
                            {
                                if (childOne != null) 
                                { 
                                    match = childOne.findExactMatch(keyDiff, keyLengthDiff);
                                }
                            } 
                            else 
                            {
                                if (childZero != null) 
                                { 
                                    match = childZero.findExactMatch(keyDiff, keyLengthDiff);
                                } 
                            }
                            if (match == null) 
                            { 
                               match = obj;
                            }      
                        }
                    }
                }
                return match;
            }		

            internal bool isNotUsed() 
            { 
                return obj == null && childOne == null && childZero == null;
            }

#if USE_GENERICS
            internal T remove(ulong key, int keyLength) 
            {             
                T obj;
#else
            internal object remove(ulong key, int keyLength) 
            {         
                object obj;    
#endif
                if (keyLength >= this.keyLength) 
                { 
                    if (key == this.key && keyLength == this.keyLength) 
                    { 
                        obj = this.obj;
                        this.obj = null;
                        return obj;
                    } 
                    else 
                    { 
                        int keyLengthCommon = getCommonPartLength(key, keyLength, this.key, this.keyLength);
                        int keyLengthDiff = keyLength - keyLengthCommon;
                        ulong keyCommon = key >> keyLengthDiff;
                        ulong keyDiff = key - (keyCommon << keyLengthDiff);
                    
                        if (firstBit(keyDiff, keyLengthDiff) == 1) 
                        {
                            if (childOne != null) 
                            { 
                                obj = childOne.findBestMatch(keyDiff, keyLengthDiff);
                                if (obj != null) 
                                { 
                                    if (childOne.isNotUsed()) 
                                    {
                                        Modify();
                                        childOne.Deallocate();
                                        childOne = null;
                                    }
                                    return obj;                                    
                                }
                            }
                        } 
                        else 
                        {
                            if (childZero != null) 
                            { 
                                obj = childZero.findBestMatch(keyDiff, keyLengthDiff);
                                if (obj != null) 
                                { 
                                    if (childZero.isNotUsed()) 
                                    { 
                                        Modify();
                                        childZero.Deallocate();
                                        childZero = null;
                                    }
                                    return obj;                                    
                                }
                            } 
                        }
                    }
                }
                return null;
            }		

            public override void Deallocate() 
            {
                if (childOne != null) 
                { 
                    childOne.Deallocate();
                }
                if (childZero != null) 
                { 
                    childZero.Deallocate();
                }
                base.Deallocate();
            }
        }
    }
}
