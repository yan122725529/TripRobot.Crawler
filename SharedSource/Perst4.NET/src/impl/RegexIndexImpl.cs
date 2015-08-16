namespace Perst.Impl
{
    using System;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
    using System.Collections;
    using System.Reflection;
    using System.Diagnostics;
    using Perst;

    [Serializable]
#if USE_GENERICS
    class RegexIndexImpl<T> : AltBtreeFieldIndex<string,T>, RegexIndex<T> where T:class
#else
    class RegexIndexImpl : AltBtreeFieldIndex, RegexIndex
#endif
    {
        int nGrams;
        bool caseInsensitive;
#if USE_GENERICS
        Index<string,Perst.ISet<T>> inverseIndex;
#else
        Index inverseIndex;
#endif

#if USE_GENERICS
        internal RegexIndexImpl(StorageImpl db, string fieldName, bool caseInsensitive, int nGrams) 
        : base(fieldName, false)
#else
        internal RegexIndexImpl(StorageImpl db, Type cls, string fieldName, bool caseInsensitive, int nGrams)
            : base(cls, fieldName, false)
#endif
        {
            if (type != ClassDescriptor.FieldType.tpString)
            {
                throw new StorageError(StorageError.ErrorCode.INCOMPATIBLE_KEY_TYPE);
            }
            this.caseInsensitive = caseInsensitive;
            this.nGrams = nGrams;
            AssignOid(db, 0, false);
#if USE_GENERICS
            inverseIndex = db.CreateIndex<string, Perst.ISet<T>>(true);
#else
            inverseIndex = db.CreateIndex(typeof(string), true);
#endif
        }


        string[] splitText(string s)
        {
            int n = s.Length - nGrams + 1;
            if (n < 0)
            {
                n = 0;
            }
            string[] ngrams = new string[n];
            char[] ngram = new char[nGrams];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < nGrams; j++)
                {
                    ngram[j] = s[i + j];
                }
                ngrams[i] = new string(ngram);
            }
            return ngrams;
        }

        string[] splitPattern(string s)
        {
            ArrayList list = new ArrayList();
            int len = s.Length;
            int i, j, n = len - nGrams + 1;
            char[] ngram = new char[nGrams];
            for (i = 0; i < n; i++)
            {
                for (j = 0; j < nGrams; j++)
                {
                    char ch = s[i + j];
                    if (ch == '\\')
                    {
                        if (i + j + 1 == len)
                        {
                            return (string[])list.ToArray(typeof(string));
                        }
                        ch = s[i + j + 1];
                    }
                    else if (ch == '_' || ch == '%')
                    {
                        i += j;
                        break;
                    }
                    ngram[j] = ch;
                }
                if (j == nGrams)
                {
                    list.Add(new string(ngram));
                }
            }
            return (string[])list.ToArray(typeof(string));
        }

        protected override Key extractKey(object obj)
        {
            string text = extractText(obj);
            return text != null ? new Key(text) : null;
        }

        private string extractText(object obj)
        {
            string text = (string)(mbr is FieldInfo ? ((FieldInfo)mbr).GetValue(obj) : ((PropertyInfo)mbr).GetValue(obj, null));
            if (text != null && caseInsensitive)
            {
                text = text.ToLower();
            }
            return text;
        }

#if USE_GENERICS
        public override bool Put(T obj) 
        {
            string text = extractText(obj);
            if (text != null) { 
                base.insert(new Key(text), obj, false);
                insertInInverseIndex(text, obj);
                return true;
            }
            return false;
        }

        public override T Set(T obj) 
        {
            throw new NotSupportedException("RegexIndex.set(T obj)");
        }

        private void insertInInverseIndex(String text, T obj) 
        {
            foreach (String s in splitText(text)) 
            {
                Perst.ISet<T> set = inverseIndex[s];
                if (set == null) { 
                    set = Storage.CreateSet<T>();
                    inverseIndex[s] = set;
                }  
                set.Add(obj);
            }
        }

        public override void Remove(Key key, T obj) 
        {
            throw new NotSupportedException("RegexIndex.remove(Key key, T obj)");       
        }
        public override bool Unlink(Key key, T obj)
        {
            throw new NotSupportedException("RegexIndex.Unlink(Key key, T obj)");       
        }
        public override void  Remove(string key, T obj)
        {
            throw new NotSupportedException("RegexIndex.Remove(string key, T obj)");       
        }
 
#else
        public override bool Put(object obj)
        {
            string text = extractText(obj);
            if (text != null)
            {
                base.insert(new Key(text), obj, false);
                insertInInverseIndex(text, obj);
                return true;
            }
            return false;
        }

        public override object Set(object obj)
        {
            throw new NotSupportedException("RegexIndex.set(object obj)");
        }

        private void insertInInverseIndex(String text, object obj)
        {
            foreach (String s in splitText(text))
            {
                ISet set = (ISet)inverseIndex[s];
                if (set == null)
                {
                    set = Storage.CreateSet();
                    inverseIndex[s] = set;
                }
                set.Add(obj);
            }
        }

        public override void Remove(Key key, object obj)
        {
            throw new NotSupportedException("RegexIndex.remove(Key key, object obj)");
        }
        public override bool Unlink(Key key, object obj)
        {
            throw new NotSupportedException("RegexIndex.Unlink(Key key, object obj)");
        }
        public override void Remove(object key, object obj)
        {
            throw new NotSupportedException("RegexIndex.Remove(object key, object obj)");
        }
#endif


        public override bool IsCaseInsensitive
        {
            get
            {
                return caseInsensitive;
            }
        }

        public override void Deallocate()
        {
            inverseIndex.DeallocateMembers();
            inverseIndex.Deallocate();
            base.Deallocate();
        }

        public override void Clear()
        {
            base.Clear();
            inverseIndex.DeallocateMembers();
        }

        internal override Key checkKey(Key key)
        {
            if (key != null && caseInsensitive)
            {
                key = new Key(((string)key.oval).ToLower(), key.inclusion != 0);
            }
            return key;
        }

#if USE_GENERICS
        public override bool Remove(T obj) 
#else
        public override bool Remove(object obj) 
#endif
        {
            string text = extractText(obj);
            if (text != null)
            {
                if (base.removeIfExists(new Key(text), obj))
                {
                    removeFromInverseIndex(text, obj);
                    return true;
                }
            }
            return false;
        }

        private void removeFromInverseIndex(string text, object obj)
        {
            foreach (string s in splitText(text))
            {
#if USE_GENERICS
                Perst.ISet<T> set = inverseIndex[s];
                Debug.Assert(set != null);
                set.Remove((T)obj);
#else
                ISet set = (ISet)inverseIndex[s];
                Debug.Assert(set != null);
                set.Remove(obj);
#endif
            }
        }

        static int findWildcard(string pattern)
        {
            int i, n = pattern.Length;
            for (i = 0; i < n; i++)
            {
                char ch = pattern[i];
                if (ch == '\\')
                {
                    i += 1;
                }
                else if (ch == '%' || ch == '_')
                {
                    return i;
                }
            }
            return -1;
        }

#if USE_GENERICS
        IEnumerable GenericRegexIndex.Match(string pattern) 
        {
            return (IEnumerable)Match(pattern);
        }

        public IEnumerable<T> Match(string pattern) 
#else
        public IEnumerable Match(string pattern)
#endif
        {
            if (caseInsensitive)
            {
                pattern = pattern.ToLower();
            }
            int firstWildcard = findWildcard(pattern);
            if (firstWildcard < 0)
            { // exact match
                return Range(pattern, pattern, IterationOrder.AscentOrder);
            }
            else if (firstWildcard == pattern.Length - 1 && pattern[firstWildcard] == '%')
            { // pattern like 'XYZ%': use prefix search 
                return StartsWith(pattern.Substring(0, firstWildcard));
            }
            else if (firstWildcard >= nGrams * 2 || firstWildcard > pattern.Length - nGrams)
            { // better to use prefix  search
                return new RegexEnumerable(this, StartsWith(pattern.Substring(0, firstWildcard)), pattern);
            }
            else
            {
                string[] ngrams = splitPattern(pattern);
                if (ngrams.Length == 0)
                { // no n-grams: have to use sequential scan 
                    return new RegexEnumerable(this, this, pattern);
                }
#if USE_GENERICS
                Perst.ISet<T>[] sets = new Perst.ISet<T>[ngrams.Length];
                for (int i = 0; i < sets.Length; i++) { 
                    Perst.ISet<T> s = inverseIndex[ngrams[i]];
                    if (s == null) { 
                        return new List<T>();
                    } 
                    sets[i] = s;
                }        
#else
                ISet[] sets = new ISet[ngrams.Length];
                for (int i = 0; i < sets.Length; i++)
                {
                    ISet s = (ISet)inverseIndex[ngrams[i]];
                    if (s == null)
                    {
                        return new ArrayList();
                    }
                    sets[i] = s;
                }
#endif
                return new JoinRegexEnumerable(this, sets, pattern);
            }
        }

        static bool match(string text, string pattern)
        {
            int ti = 0, tn = text.Length;
            int pi = 0, pn = pattern.Length;
            int any = -1;
            int pos = -1;
            while (true)
            {
                char ch = pi < pn ? pattern[pi] : '\0';
                if (ch == '%')
                {
                    any = ++pi;
                    pos = ti;
                }
                else if (ti == tn)
                {
                    return pi == pn;
                }
                else if (ch == '\\' && pi + 1 < pn && pattern[pi + 1] == text[ti])
                {
                    ti += 1;
                    pi += 2;
                }
                else if (ch != '\\' && (ch == text[ti] || ch == '_'))
                {
                    ti += 1;
                    pi += 1;
                }
                else if (any >= 0)
                {
                    ti = ++pos;
                    pi = any;
                }
                else
                {
                    return false;
                }
            }
        }

#if USE_GENERICS
        class RegexEnumerable : IEnumerable<T>, IEnumerable
#else
        class RegexEnumerable : IEnumerable
#endif
        {
#if USE_GENERICS
            private RegexIndexImpl<T> index;
            private IEnumerable<T> iterator;
#else
            private RegexIndexImpl index;
            private IEnumerable iterator;
#endif
            private string pattern;

#if USE_GENERICS
            internal RegexEnumerable(RegexIndexImpl<T> index, IEnumerable<T> iterator, string pattern) 
#else
            internal RegexEnumerable(RegexIndexImpl index, IEnumerable iterator, string pattern)
#endif
            {
                this.index = index;
                this.iterator = iterator;
                this.pattern = pattern;
            }

#if USE_GENERICS        
            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)GetEnumerator();
            }

            public IEnumerator<T> GetEnumerator() 
#else
            public IEnumerator GetEnumerator()
#endif
            {
                return new RegexEnumerator(index, iterator.GetEnumerator(), pattern);
            }
        }

#if USE_GENERICS
        class RegexEnumerator : IEnumerator<T>, PersistentEnumerator
#else
        class RegexEnumerator : IEnumerator
#endif
        {
#if USE_GENERICS
            private RegexIndexImpl<T> index;
            private IEnumerator<T> iterator;
            private T currObj;
#else
            private RegexIndexImpl index;
            private IEnumerator iterator;
            private object currObj;
#endif
            private string pattern;

#if USE_GENERICS
            internal RegexEnumerator(RegexIndexImpl<T> index, IEnumerator<T> iterator, string pattern) 
#else
            internal RegexEnumerator(RegexIndexImpl index, IEnumerator iterator, string pattern)
#endif
            {
                this.index = index;
                this.iterator = iterator;
                this.pattern = pattern;
            }

            public virtual void Reset()
            {
                iterator.Reset();
                currObj = null;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                while (iterator.MoveNext())
                {
#if USE_GENERICS
                    T obj = iterator.Current;
#else
                    object obj = iterator.Current;
#endif
                    string text = index.extractText(obj);
                    if (match(text, pattern))
                    {
                        currObj = obj;
                        return true;
                    }
                }
                return false;
            }

#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public T Current 
#else
            public object Current
#endif
            {
                get
                {
                    return currObj;
                }
            }

            public int CurrentOid
            {
                get
                {
                    return index.Storage.GetOid(currObj);
                }
            }
        }

#if USE_GENERICS
        class JoinRegexEnumerable : IEnumerable<T>, IEnumerable
#else
        class JoinRegexEnumerable : IEnumerable
#endif
        {
#if USE_GENERICS
            private Perst.ISet<T>[] sets;
            private RegexIndexImpl<T> index;            
#else
            private ISet[] sets;
            private RegexIndexImpl index;
#endif
            private string pattern;
            private Storage storage;

#if USE_GENERICS
            internal JoinRegexEnumerable(RegexIndexImpl<T> index, Perst.ISet<T>[] sets, string pattern) 
#else
            internal JoinRegexEnumerable(RegexIndexImpl index, ISet[] sets, string pattern)
#endif
            {
                this.index = index;
                this.sets = sets;
                this.pattern = pattern;
            }

#if USE_GENERICS        
            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)GetEnumerator();
            }

            public IEnumerator<T> GetEnumerator() 
#else
            public IEnumerator GetEnumerator()
#endif
            {
                return new JoinRegexEnumerator(index, sets, pattern);
            }
        }

#if USE_GENERICS
        class JoinRegexEnumerator : IEnumerator<T>, PersistentEnumerator
#else
        class JoinRegexEnumerator : IEnumerator
#endif
        {
            private PersistentEnumerator[] iterators;
            private object currObj;
            private string pattern;
#if USE_GENERICS
            private RegexIndexImpl<T> index;
#else
            private RegexIndexImpl index;
#endif
            private int currOid;

#if USE_GENERICS
            internal JoinRegexEnumerator(RegexIndexImpl<T> index, Perst.ISet<T>[] sets, string pattern) 
#else
            internal JoinRegexEnumerator(RegexIndexImpl index, ISet[] sets, string pattern)
#endif
            {
                this.index = index;
                iterators = new PersistentEnumerator[sets.Length];
                for (int i = 0; i < sets.Length; i++)
                {
                    iterators[i] = (PersistentEnumerator)sets[i].GetEnumerator();
                }
                this.pattern = pattern;
            }

            public virtual void Reset()
            {
                for (int i = 0; i < iterators.Length; i++)
                {
                    iterators[i].Reset();
                }
                currObj = null;
                currOid = 0;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                int oid1 = 0, oid2;
                int n = iterators.Length;
                while (true)
                {
                    for (int i = 0, j = 0; i < n; j++, i++)
                    {
                        do
                        {
                            if (!iterators[j % n].MoveNext())
                            {
                                return false;
                            }
                            oid2 = iterators[j % n].CurrentOid;
                        } while (oid2 < oid1);

                        if (oid2 > oid1)
                        {
                            oid1 = oid2;
                            i = 0;
                        }
                    }
                    object obj = index.Storage.GetObjectByOID(oid1);
                    string text = index.extractText(obj);
                    if (match(text, pattern))
                    {
                        currObj = obj;
                        currOid = oid1;
                        return true;
                    }
                }
            }
#if USE_GENERICS
            object IEnumerator.Current
            {
                get
                {
                    return currObj;
                }
            }
            public T Current 
            {
                get 
                {
                    return (T)currObj;
                }
            }
#else
            public object Current
            {
                get
                {
                    return currObj;
                }
            }
#endif

            public int CurrentOid
            {
                get
                {
                    return currOid;
                }
            }
        }
    }
}
