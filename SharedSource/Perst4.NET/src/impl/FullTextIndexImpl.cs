
using System;
using System.Collections;
#if NET_FRAMEWORK_20
using System.Collections.Generic;
#endif
using System.IO;
using System.Diagnostics;

using Perst;
using Perst.FullText;

namespace Perst.Impl
{
    public class Compressor
    {
        private byte[] buf;
        private byte acc;
        private int pos;
        private int btg;

        public Compressor(byte[] buf)
        {
            this.buf = buf;
        }

        public void encodeStart()
        {
            btg = 8;
            acc = 0;
            pos = 0;
        }

        private void encodeBit(uint b)
        {
            btg -= 1;
            acc |= (byte)(b << btg);
            if (btg == 0)
            {
                buf[pos++] = acc;
                acc = 0;
                btg = 8;
            }
        }

        private int log2(uint x)
        {
            int v;
            for (v = -1; x != 0; x >>= 1, v++) ;
            return v;
        }

        public void encode(int val)
        {
            Debug.Assert(val != 0);
            uint x = (uint)val;
            int logofx = log2(x);
            int nbits = logofx + 1;
            while (logofx-- != 0)
            {
                encodeBit(0);
            }
            while (--nbits >= 0)
            {
                encodeBit((x >> nbits) & 1);
            }
        }

        public byte[] encodeStop()
        {
            if (btg != 8)
            {
                buf[pos++] = acc;
            }
            byte[] packedArray = new byte[pos];
            Array.Copy(buf, 0, packedArray, 0, pos);
            return packedArray;
        }

        public void decodeStart()
        {
            btg = 0;
            acc = 0;
            pos = 0;
        }

        private int decodeBit()
        {
            if (btg == 0)
            {
                acc = buf[pos++];
                btg = 8;
            }
            return (acc >> --btg) & 1;
        }

        public int decode()
        {
            int x = 1;
            int nbits = 0;
            while (decodeBit() == 0)
            {
                nbits += 1;
            }
            while (nbits-- > 0)
            {
                x += x + decodeBit();
            }
            return x;
        }
    }

    public class FullTextIndexImpl : PersistentResource, FullTextIndex
    {
#if USE_GENERICS
        internal Index<string,InverseList>   inverseIndex;
        internal Index<object,Document> documents;
#else
        internal Index inverseIndex;
        internal Index documents;
#endif
        protected internal FullTextSearchHelper helper;

        private const int OCC_KIND_OFFSET = 24;
        private const int OCC_POSITION_MASK = (1 << OCC_KIND_OFFSET) - 1;
        private const int COMPRESSION_OVERHEAD = 8;


        class KeywordImpl : Keyword 
        { 
             DictionaryEntry entry;

             public string NormalForm
             {
                 get
                 {
                     return (string)entry.Key;
                 }
             }
  
             public long NumberOfOccurrences
             {
                 get
                 {
                     return ((InverseList)entry.Value).Count;
                 } 
             }

             internal KeywordImpl(DictionaryEntry entry) 
             { 
                 this.entry = entry;
             }
        }

#if NET_FRAMEWORK_20
        class KeywordEnumerator : IEnumerable<Keyword>, IEnumerator<Keyword>
#else
        class KeywordEnumerator : IEnumerable, IEnumerator
#endif
        {
#if NET_FRAMEWORK_20
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            public IEnumerator<Keyword> GetEnumerator() 
#else
            public IEnumerator GetEnumerator() 
#endif
            {
                return this;
            }                            

            public bool MoveNext() 
            { 
                return enumerator.MoveNext();
            }
                
            public void Reset()
            {
                enumerator.Reset();
            }

#if NET_FRAMEWORK_20
            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public Keyword Current
#else
            public object Current
#endif
            {
                get
                {
                    return new KeywordImpl(enumerator.Entry);
                }
            }

            public void Dispose() {}

            internal KeywordEnumerator(IDictionaryEnumerator enumerator) 
            {
                this.enumerator = enumerator;
            }

            IDictionaryEnumerator enumerator;
        }

#if NET_FRAMEWORK_20
        public IEnumerable<Keyword> GetKeywords(string prefix)
#else
        public IEnumerable GetKeywords(string prefix)
#endif
        {
            return new KeywordEnumerator(inverseIndex.GetDictionaryEnumerator(new Key(prefix), 
                                                                              new Key(prefix + char.MaxValue, false), 
                                                                              IterationOrder.AscentOrder));
        }

        protected internal class DocumentOccurrences : Persistent
        {
            internal InverseList list;
            internal int nWordsInDocument;
            internal byte[] compressedOccurrences;

            internal int[] occurrences
            {
                set
                {
                    int i = 0;
                    int prevOcc = -1;
                    int len = value.Length;
                    Compressor compressor = new Compressor(new byte[len * 4 + COMPRESSION_OVERHEAD]);
                    compressor.encodeStart();
                    compressor.encode(len);
                    do
                    {
                        uint kind = (uint)value[i] >> OCC_KIND_OFFSET;
                        int j = i;
                        while (++j < len && ((uint)value[j] >> OCC_KIND_OFFSET) == kind) ;
                        compressor.encode(j - i);
                        compressor.encode((int)kind + 1);
                        do
                        {
                            int currOcc = value[i++] & OCC_POSITION_MASK;
                            compressor.encode(currOcc - prevOcc);
                            prevOcc = currOcc;
                        } while (i != j);
                    } while (i != len);
                    compressedOccurrences = compressor.encodeStop();
                }

                get
                {
                    Compressor compressor = new Compressor(compressedOccurrences);
                    int i = 0;
                    compressor.decodeStart();
                    int len = compressor.decode();
                    int[] buf = new int[len];
                    int pos = -1;
                    do
                    {
                        int n = compressor.decode();
                        int kind = (compressor.decode() - 1) << OCC_KIND_OFFSET;
                        do
                        {
                            pos += compressor.decode();
                            buf[i++] = kind | pos;
                        } while (--n != 0);
                    } while (i != len);
                    return buf;
                }
            }
        }

        protected internal class Document : Persistent
        {
            internal object obj;
#if USE_GENERICS
            internal Link<DocumentOccurrences> occurrences;
#else
            internal Link occurrences;
#endif

            internal Document()
            {
            }

            internal Document(Storage storage, object obj)
                : base(storage)
            {
                this.obj = obj;
#if USE_GENERICS
                occurrences = storage.CreateLink<DocumentOccurrences>();
#else
                occurrences = storage.CreateLink();
#endif
            }
        }

#if USE_GENERICS
        internal class InverseList : Btree<int,DocumentOccurrences>
#else
        internal class InverseList : Btree
#endif
        {
            internal int[] oids;
#if USE_GENERICS
            internal Link<DocumentOccurrences> docs;
#else
            internal Link docs;
#endif

            const int BTREE_THRESHOLD = 500;

            internal InverseList(Storage db, int oid, DocumentOccurrences doc)
#if USE_GENERICS
                : base(true)
#else
                : base(typeof(int), true)
#endif
            {
#if USE_GENERICS
                docs = db.CreateLink<DocumentOccurrences>(1);
#else
                docs = db.CreateLink(1);
#endif
                docs.Add(doc);
                oids = new int[1];
                oids[0] = oid;
                AssignOid(db, 0, false);
            }

            internal InverseList() { }

            public override int Count
            {
                get
                {
                    return oids != null ? oids.Length : base.Count;
                }
            }

            public int FirstKey
            {
                get
                {
                    if (oids != null)
                    {
                        return oids[0];
                    }
                    IDictionaryEnumerator e = GetDictionaryEnumerator(null, null, IterationOrder.AscentOrder);
                    e.MoveNext();
                    return (int)e.Key;
                }
            }

            public int LastKey
            {
                get
                {
                    if (oids != null)
                    {
                        return oids[oids.Length - 1];
                    }
                    IDictionaryEnumerator e = GetDictionaryEnumerator(null, null, IterationOrder.DescentOrder);
                    e.MoveNext();
                    return (int)e.Key;
                }
            }

            class InverstListEnumerator : IDictionaryEnumerator
            {
                int pos;
                int i;
                InverseList list;

                internal InverstListEnumerator(InverseList list, int pos)
                {
                    this.list = list;
                    this.pos = pos;
                    Reset();
                }

                public void Reset()
                {
                    i = pos - 1;
                }

                public bool MoveNext()
                {
                    return ++i < list.oids.Length;
                }

                public object Current
                {
                    get
                    {
                        return Entry;
                    }
                }

                public DictionaryEntry Entry
                {
                    get
                    {
                        return new DictionaryEntry(list.oids[i], list.docs[i]);
                    }
                }

                public object Key
                {
                    get
                    {
                        return list.oids[i];
                    }
                }

                public object Value
                {
                    get
                    {
                        return list.docs[i];
                    }
                }
            }


            public IDictionaryEnumerator GetDictionaryEnumerator(int oid)
            {
                int[] os = oids;
                if (os != null)
                {
                    int l = 0, r = os.Length;
                    while (l < r)
                    {
                        int m = (l + r) >> 1;
                        if (os[m] < oid)
                        {
                            l = m + 1;
                        }
                        else
                        {
                            r = m;
                        }
                    }
                    return new InverstListEnumerator(this, r);
                }
                else
                {
                    return base.GetDictionaryEnumerator(new Key(oid), null, IterationOrder.AscentOrder);
                }
            }

            public void Add(int oid, DocumentOccurrences doc)
            {
                int[] os = oids;
                if (os == null || os.Length >= BTREE_THRESHOLD)
                {
                    if (os != null)
                    {
                        for (int i = 0; i < os.Length; i++)
                        {
                            base.Put(new Key(os[i]), docs[i]);
                        }
                        oids = null;
                        docs = null;
                    }
                    base.Put(new Key(oid), doc);
                }
                else
                {
                    int l = 0, n = os.Length, r = n;
                    while (l < r)
                    {
                        int m = (l + r) >> 1;
                        if (os[m] < oid)
                        {
                            l = m + 1;
                        }
                        else
                        {
                            r = m;
                        }
                    }
                    os = new int[n + 1];
                    Array.Copy(oids, 0, os, 0, r);
                    os[r] = oid;
                    Array.Copy(oids, r, os, r + 1, n - r);
                    docs.Insert(r, doc);
                    oids = os;
                    Modify();
                }
            }

            public void Remove(int oid)
            {
                int[] os = oids;
                if (os != null)
                {
                    int l = 0, n = os.Length, r = n;
                    while (l < r)
                    {
                        int m = (l + r) >> 1;
                        if (os[m] < oid)
                        {
                            l = m + 1;
                        }
                        else
                        {
                            r = m;
                        }
                    }
                    Debug.Assert(r < n && os[r] == oid);
                    docs.Remove(r);
                    oids = new int[n - 1];
                    Array.Copy(os, 0, oids, 0, r);
                    Array.Copy(os, r + 1, oids, r, n - r - 1);
                    Modify();
                }
                else
                {
                    base.Remove(new Key(oid));
                }
            }
        }

        public void Add(FullTextSearchable obj)
        {
            Add(obj, obj.Text, obj.Language);
        }

        public void Add(object obj, TextReader text, string language)
        {
            Occurrence[] occurrences = helper.ParseText(text);
            Delete(obj);
            if (occurrences.Length > 0)
            {
                Document doc = new Document(Storage, obj);
                documents.Put(new Key(obj), doc);
                Array.Sort(occurrences);
                string word = occurrences[0].word;
                int i = 0;
                for (int j = 1; j < occurrences.Length; j++)
                {
                    Occurrence occ = occurrences[j];
                    if (!occ.word.Equals(word))
                    {
                        addReference(doc, word, occurrences, i, j, language);
                        word = occ.word;
                        i = j;
                    }
                }
                addReference(doc, word, occurrences, i, occurrences.Length, language);
            }
        }

        private void addReference(Document doc, string word, Occurrence[] occurrences, int from, int till)
        {
            DocumentOccurrences d = new DocumentOccurrences();
            int[] occ = new int[till - from];
            d.nWordsInDocument = occurrences.Length;
            for (int i = from; i < till; i++)
            {
                occ[i - from] = occurrences[i].position | (occurrences[i].kind << OCC_KIND_OFFSET);
            }
            d.occurrences = occ;
            int oid = Storage.GetOid(doc.obj);
#if USE_GENERICS
            InverseList list = inverseIndex[word];
#else
            InverseList list = (InverseList)inverseIndex[word];
#endif
            if (list == null)
            {
                list = new InverseList(Storage, oid, d);
                inverseIndex.Put(word, list);
            }
            else
            {
                list.Add(oid, d);
            }
            d.list = list;
            d.Modify();
            doc.occurrences.Add(d);
        }

        private void addReference(Document doc, string word, Occurrence[] occurrences, int from, int till, string language)
        {
            string[] normalForms = helper.GetNormalForms(word, language);
            bool isNormalForm = false;
            for (int i = 0; i < normalForms.Length; i++)
            {
                if (word.Equals(normalForms[i]))
                {
                    isNormalForm = true;
                }
                addReference(doc, normalForms[i], occurrences, from, till);
            }
            if (!isNormalForm)
            {
                addReference(doc, word, occurrences, from, till);
            }
        }

        public void Delete(object obj)
        {
            Document doc = (Document)documents[obj];
            if (doc != null)
            {
                for (int i = 0, n = doc.occurrences.Count; i < n; i++)
                {
                    DocumentOccurrences d = (DocumentOccurrences)doc.occurrences[i];
                    d.list.Remove(Storage.GetOid(obj));
                    d.Deallocate();
                }
                documents.Remove(new Key(obj));
                doc.Deallocate();
            }
        }

        public void deallocate() 
        { 
            Clear();
            base.Deallocate();
        }

        public void Clear() 
        { 
            inverseIndex.DeallocateMembers();
            documents.DeallocateMembers();
        }
        
        public int NumberOfWords
        {
            get
            {
                return inverseIndex.Count;
            }
        }

        public int NumberOfDocuments
        {
            get
            {
                return documents.Count;
            }
        }

        public FullTextSearchResult Search(string query, string language, int maxResults, int timeLimit)
        {
            return Search(helper.ParseQuery(query, language), maxResults, timeLimit);
        }

        protected internal class KeywordList
        {
            internal InverseList list;
            internal int[] occ;
            internal string word;
            internal int sameAs;
            internal int kwdLen;
            internal int kwdOffset;
            internal int occPos;
            internal int currDoc;
            internal DictionaryEntry currEntry;
            internal IDictionaryEnumerator iterator;

            internal KeywordList(string word)
            {
                this.word = word;
                kwdLen = word.Length;
                sameAs = -1;
            }
        }

        internal class ExpressionWeight : IComparable
        {
            internal int weight;
            internal FullTextQuery expr;

            public int CompareTo(System.Object o)
            {
                return weight - ((ExpressionWeight)o).weight;
            }
        }

        protected internal class FullTextSearchEngine : FullTextQueryVisitor
        {
            public FullTextSearchEngine(FullTextIndexImpl impl)
            {
                this.impl = impl;
            }
            private FullTextIndexImpl impl;

            internal KeywordList[] kwds;
            internal ArrayList kwdList;
            internal int[] occurrences;
            internal int nOccurrences;
            internal float[] occurrenceKindWeight;

            public override void Visit(FullTextQueryMatchOp q)
            {
                q.wno = kwdList.Count;
                KeywordList list = new KeywordList(q.word);
#if USE_GENERICS
                list.list = impl.inverseIndex[q.word];
#else
                list.list = (InverseList)impl.inverseIndex[q.word];
#endif
                kwdList.Add(list);
            }

            internal const int STRICT_MATCH_BONUS = 8;

            internal virtual int calculateWeight(FullTextQuery query)
            {
                switch (query.op)
                {
                    case FullTextQuery.Operator.And:
                        {
                            return calculateWeight(((FullTextQueryBinaryOp)query).left);
                        }

                    case FullTextQuery.Operator.Near:
                        {
                            int shift = STRICT_MATCH_BONUS;
                            for (FullTextQuery q = ((FullTextQueryBinaryOp)query).right; q.op == FullTextQuery.Operator.Near; q = ((FullTextQueryBinaryOp)q).right)
                            {
                                shift += STRICT_MATCH_BONUS;
                            }
                            return shift >= 32 ? 0 : (calculateWeight(((FullTextQueryBinaryOp)query).left) >> shift);
                        }

                    case FullTextQuery.Operator.Or:
                        {
                            int leftWeight = calculateWeight(((FullTextQueryBinaryOp)query).left);
                            int rightWeight = calculateWeight(((FullTextQueryBinaryOp)query).right);
                            return leftWeight > rightWeight ? leftWeight : rightWeight;
                        }

                    case FullTextQuery.Operator.Match:
                    case FullTextQuery.Operator.StrictMatch:
                        {
                            int wno = ((FullTextQueryMatchOp)query).wno;
                            return kwds[wno].list == null ? 0 : kwds[wno].list.Count;
                        }

                    default:
                        return int.MaxValue;

                }
            }

            internal virtual FullTextQuery optimize(FullTextQuery query)
            {
                switch (query.op)
                {

                    case FullTextQuery.Operator.And:
                    case FullTextQuery.Operator.Near:
                        {
                            FullTextQuery.Operator op = query.op;
                            int nConjuncts = 1;
                            FullTextQuery q = query;
                            while ((q = ((FullTextQueryBinaryOp)q).right).op == op)
                            {
                                nConjuncts += 1;
                            }
                            ExpressionWeight[] conjuncts = new ExpressionWeight[nConjuncts + 1];
                            q = query;
                            for (int i = 0; i < nConjuncts; i++)
                            {
                                FullTextQueryBinaryOp and = (FullTextQueryBinaryOp)q;
                                conjuncts[i] = new ExpressionWeight();
                                conjuncts[i].expr = optimize(and.left);
                                conjuncts[i].weight = calculateWeight(conjuncts[i].expr);
                                q = and.right;
                            }
                            conjuncts[nConjuncts] = new ExpressionWeight();
                            conjuncts[nConjuncts].expr = optimize(q);
                            conjuncts[nConjuncts].weight = calculateWeight(conjuncts[nConjuncts].expr);
                            Array.Sort(conjuncts);
                            if (op == FullTextQuery.Operator.And)
                            {
                                // eliminate duplicates
                                int n = 0, j = -1;
                                InverseList list = null;
                                for (int i = 0; i <= nConjuncts; i++)
                                {
                                    q = conjuncts[i].expr;
                                    if (q is FullTextQueryMatchOp)
                                    {
                                        FullTextQueryMatchOp match = (FullTextQueryMatchOp)q;
                                        if (n == 0 || kwds[match.wno].list != list)
                                        {
                                            j = match.wno;
                                            list = kwds[j].list;
                                            conjuncts[n++] = conjuncts[i];
                                        }
                                        else
                                        {
                                            kwds[match.wno].sameAs = j;
                                        }
                                    }
                                    else
                                    {
                                        conjuncts[n++] = conjuncts[i];
                                    }
                                }
                                nConjuncts = n - 1;
                            }
                            else
                            {
                                // calculate distance between keywords
                                int kwdPos = 0;
                                for (int i = 0; i <= nConjuncts; i++)
                                {
                                    q = conjuncts[i].expr;
                                    if (q is FullTextQueryMatchOp)
                                    {
                                        FullTextQueryMatchOp match = (FullTextQueryMatchOp)q;
                                        kwds[match.wno].kwdOffset = match.pos - kwdPos;
                                        kwdPos = match.pos;
                                    }
                                }
                            }
                            if (nConjuncts == 0)
                            {
                                return conjuncts[0].expr;
                            }
                            else
                            {
                                q = query;
                                int i = 0;
                                while (true)
                                {
                                    FullTextQueryBinaryOp and = (FullTextQueryBinaryOp)q;
                                    and.left = conjuncts[i].expr;
                                    if (++i < nConjuncts)
                                    {
                                        q = and.right;
                                    }
                                    else
                                    {
                                        and.right = conjuncts[i].expr;
                                        break;
                                    }
                                }
                            }
                            break;
                        }

                    case FullTextQuery.Operator.Or:
                        {
                            FullTextQueryBinaryOp or = (FullTextQueryBinaryOp)query;
                            or.left = optimize(or.left);
                            or.right = optimize(or.right);
                            break;
                        }

                    case FullTextQuery.Operator.Not:
                        {
                            FullTextQueryUnaryOp not = (FullTextQueryUnaryOp)query;
                            not.opd = optimize(not.opd);
                        }
                        break;

                    default: ;
                        break;

                }
                return query;
            }

            internal virtual int intersect(int doc, FullTextQuery query)
            {
                int left, right;

                switch (query.op)
                {

                    case FullTextQuery.Operator.And:
                    case FullTextQuery.Operator.Near:
                        do
                        {
                            left = intersect(doc, ((FullTextQueryBinaryOp)query).left);
                            if (left == int.MaxValue)
                            {
                                return left;
                            }
                            doc = intersect(left, ((FullTextQueryBinaryOp)query).right);
                        }
                        while (left != doc && doc != int.MaxValue);
                        return doc;

                    case FullTextQuery.Operator.Or:
                        left = intersect(doc, ((FullTextQueryBinaryOp)query).left);
                        right = intersect(doc, ((FullTextQueryBinaryOp)query).right);
                        return left < right ? left : right;

                    case FullTextQuery.Operator.Match:
                    case FullTextQuery.Operator.StrictMatch:
                        {
                            KeywordList kwd = kwds[((FullTextQueryMatchOp)query).wno];
                            if (kwd.currDoc >= doc)
                            {
                                return kwd.currDoc;
                            }
                            IDictionaryEnumerator iterator = kwd.iterator;
                            if (iterator != null)
                            {
                                if (iterator.MoveNext())
                                {
                                    DictionaryEntry entry = iterator.Entry;
                                    int nextDoc = (int)entry.Key;
                                    if (nextDoc >= doc)
                                    {
                                        kwd.currEntry = entry;
                                        kwd.currDoc = nextDoc;
                                        return nextDoc;
                                    }
                                }
                                else
                                {
                                    kwd.currDoc = 0;
                                    return int.MaxValue;
                                }
                            }
                            if (kwd.list != null)
                            {
                                kwd.iterator = iterator = kwd.list.GetDictionaryEnumerator(doc);
                                if (iterator.MoveNext())
                                {
                                    DictionaryEntry entry = iterator.Entry;
                                    doc = (int)entry.Key;
                                    kwd.currEntry = entry;
                                    kwd.currDoc = doc;
                                    return doc;
                                }
                            }
                            kwd.currDoc = 0;
                            return int.MaxValue;
                        }

                    case FullTextQuery.Operator.Not:
                        {
                            int nextDoc = intersect(doc, ((FullTextQueryUnaryOp)query).opd);
                            if (nextDoc == doc)
                            {
                                doc += 1;
                            }
                            return doc;
                        }

                    default:
                        return doc;

                }
            }

            internal virtual int calculateEstimation(FullTextQuery query, int nResults)
            {
                switch (query.op)
                {

                    case FullTextQuery.Operator.And:
                    case FullTextQuery.Operator.Near:
                        {
                            int left = calculateEstimation(((FullTextQueryBinaryOp)query).left, nResults);
                            int right = calculateEstimation(((FullTextQueryBinaryOp)query).right, nResults);
                            return left < right ? left : right;
                        }

                    case FullTextQuery.Operator.Or:
                        {
                            int left = calculateEstimation(((FullTextQueryBinaryOp)query).left, nResults);
                            int right = calculateEstimation(((FullTextQueryBinaryOp)query).right, nResults);
                            return left > right ? left : right;
                        }

                    case FullTextQuery.Operator.Match:
                    case FullTextQuery.Operator.StrictMatch:
                        {
                            KeywordList kwd = kwds[((FullTextQueryMatchOp)query).wno];
                            if (kwd.currDoc == 0)
                            {
                                return 0;
                            }
                            else
                            {
                                int curr = kwd.currDoc;
                                int first = kwd.list.FirstKey;
                                int last = kwd.list.LastKey;
                                int estimation = nResults * (last - first + 1) / (curr - first + 1);
                                if (estimation > kwd.list.Count)
                                {
                                    estimation = kwd.list.Count;
                                }
                                return estimation;
                            }
                        }

                    case FullTextQuery.Operator.Not:
                        return impl.documents.Count;
                }
                return 0;
            }

            internal const double DENSITY_MAGIC = 2;

            internal virtual double evaluate(int doc, FullTextQuery query)
            {
                double left, right;
                switch (query.op)
                {

                    case FullTextQuery.Operator.Near:
                    case FullTextQuery.Operator.And:
                        left = evaluate(doc, ((FullTextQueryBinaryOp)query).left);
                        right = evaluate(doc, ((FullTextQueryBinaryOp)query).right);
                        nOccurrences = 0;
                        return left < 0 || right < 0 ? -1 : left + right;

                    case FullTextQuery.Operator.Or:
                        left = evaluate(doc, ((FullTextQueryBinaryOp)query).left);
                        right = evaluate(doc, ((FullTextQueryBinaryOp)query).right);
                        return left > right ? left : right;

                    case FullTextQuery.Operator.Match:
                    case FullTextQuery.Operator.StrictMatch:
                        {
                            KeywordList kwd = kwds[((FullTextQueryMatchOp)query).wno];
                            if (kwd.currDoc != doc)
                            {
                                return -1;
                            }
                            DocumentOccurrences d = (DocumentOccurrences)kwd.currEntry.Value;
                            int[] occ = d.occurrences;
                            kwd.occ = occ;
                            int frequency = occ.Length;
                            if (query.op == FullTextQuery.Operator.StrictMatch)
                            {
                                if (nOccurrences == 0)
                                {
                                    nOccurrences = frequency;
                                    if (occurrences == null || occurrences.Length < frequency)
                                    {
                                        occurrences = new int[frequency];
                                    }
                                    for (int i = 0; i < frequency; i++)
                                    {
                                        occurrences[i] = occ[i] & OCC_POSITION_MASK;
                                    }
                                }
                                else
                                {
                                    int nPairs = 0;
                                    int[] dst = occurrences;
                                    int occ1 = dst[0];
                                    int occ2 = occ[0] & OCC_POSITION_MASK;
                                    int i = 0, j = 0;
                                    int offs = kwd.kwdOffset;
                                    while (true)
                                    {
                                        if (occ1 + offs <= occ2)
                                        {
                                            if (occ1 + offs + 1 >= occ2)
                                            {
                                                dst[nPairs++] = occ2;
                                            }
                                            if (++j == nOccurrences)
                                            {
                                                break;
                                            }
                                            occ1 = dst[j];
                                        }
                                        else
                                        {
                                            if (++i == frequency)
                                            {
                                                break;
                                            }
                                            occ2 = occ[i] & OCC_POSITION_MASK;
                                        }
                                    }
                                    nOccurrences = nPairs;
                                    if (nPairs == 0)
                                    {
                                        return -1;
                                    }
                                }
                            }
                            return calculateKwdRank(kwd.list, d, occ);                    
                        }

                    case FullTextQuery.Operator.Not:
                        {
                            double rank = evaluate(doc, ((FullTextQueryUnaryOp)query).opd);
                            return (rank >= 0) ? -1 : 0;
                        }

                    default:
                        return -1;

                }
            }

            internal double calculateKwdRank(InverseList list, DocumentOccurrences d, int[] occ)
            {
                int frequency = occ.Length;
                int totalNumberOfDocuments = impl.documents.Count;
                int nRelevantDocuments = list.Count;
                int totalNumberOfWords = impl.inverseIndex.Count;
                double idf = System.Math.Log((double)totalNumberOfDocuments / nRelevantDocuments);
                double averageWords = (double)totalNumberOfWords / totalNumberOfDocuments;
                double density = frequency * System.Math.Log(1 + (DENSITY_MAGIC * averageWords / d.nWordsInDocument));
                double wordWeight = (density * idf);
                double wordScore = 1;
                for (int i = 0; i < frequency; i++)
                {
                    wordScore += wordWeight * occurrenceKindWeight[(uint)occ[i] >> OCC_KIND_OFFSET];
                }
                return System.Math.Log(wordScore);
            }

            internal virtual void buildOccurrenceKindWeightTable()
            {
                occurrenceKindWeight = new float[256];
                float[] weights = impl.helper.OccurrenceKindWeights;
                occurrenceKindWeight[0] = 1.0f;
                for (int i = 1; i < 256; i++)
                {
                    float weight = 0;
                    for (int j = 0; j < weights.Length; j++)
                    {
                        if ((i & (1 << j)) != 0)
                        {
                            weight += weights[j];
                        }
                        occurrenceKindWeight[i] = weight;
                    }
                }
            }

            internal virtual double calculateNearness()
            {
                KeywordList[] kwds = this.kwds;
                int nKwds = kwds.Length;
                if (nKwds < 2)
                {
                    return 0;
                }
                for (int i = 0; i < nKwds; i++)
                {
                    if (kwds[i].occ == null)
                    {
                        int j = kwds[i].sameAs;
                        if (j >= 0 && kwds[j].occ != null)
                        {
                            kwds[i].occ = kwds[j].occ;
                        }
                        else
                        {
                            return 0;
                        }
                    }
                    kwds[i].occPos = 0;
                }
                double maxNearness = 0;
                int swapPenalty = impl.helper.WordSwapPenalty;
                while (true)
                {
                    int minPos = int.MaxValue;
                    double nearness = 0;
                    KeywordList first = null;
                    KeywordList prev = null;
                    for (int i = 0; i < nKwds; i++)
                    {
                        KeywordList curr = kwds[i];
                        if (curr.occPos < curr.occ.Length)
                        {
                            if (prev != null)
                            {
                                int offset = curr.occ[curr.occPos] - prev.occ[prev.occPos];
                                if (offset < 0)
                                {
                                    offset = (-offset - curr.kwdLen) * swapPenalty;
                                }
                                else
                                {
                                    offset -= prev.kwdLen;
                                }
                                if (offset <= 2)
                                {
                                    offset = 1;
                                }
                                nearness += 1 / System.Math.Sqrt(offset);
                            }
                            if (curr.occ[curr.occPos] < minPos)
                            {
                                minPos = curr.occ[curr.occPos];
                                first = curr;
                            }
                            prev = curr;
                        }
                    }
                    if (first == null)
                    {
                        break;
                    }
                    first.occPos += 1;

                    if (nearness > maxNearness)
                    {
                        maxNearness = nearness;
                    }
                }
                return maxNearness;
            }

            internal virtual void reset()
            {
                nOccurrences = 0;
                for (int i = 0; i < kwds.Length; i++)
                {
                    kwds[i].occ = null;
                }
            }


            internal virtual FullTextSearchResult SearchPrefix(string prefix, int maxResults, int timeLimit, bool sort) 
            { 
                const int TICKS_PER_MSEC = 10000;
                FullTextSearchHit[] hits = new FullTextSearchHit[maxResults];
                int nResults = 0;
                int estimation = 0;
                long stop = DateTime.Now.Ticks + (long)timeLimit * TICKS_PER_MSEC;

                foreach (InverseList list in impl.inverseIndex.StartsWith(prefix)) 
                { 
                    IDictionaryEnumerator occurrences = list.GetDictionaryEnumerator(0);
                    estimation += list.Count;
                    while (occurrences.MoveNext()) 
                    { 
                        int doc = (int)occurrences.Key;
                        float rank = 1.0f;
                        if (sort) 
                        { 
                            DocumentOccurrences d = (DocumentOccurrences)occurrences.Value;
                            rank = (float)calculateKwdRank(list, d, d.occurrences);
                        }
                        hits[nResults] = new FullTextSearchHit(impl.Storage, doc, rank);
                        if (++nResults >= maxResults || DateTime.Now.Ticks >= stop) 
                        { 
                            goto Done;
                        }
                    }
                }
              Done:
                if (nResults < maxResults) 
                { 
                    FullTextSearchHit[] realHits = new FullTextSearchHit[nResults];
                    Array.Copy(hits, 0, realHits, 0, nResults);
                    hits = realHits;
                }
                if (sort) 
                { 
                    Array.Sort(hits);
                }
                return new FullTextSearchResult(hits, estimation);
            }

            internal virtual FullTextSearchResult Search(FullTextQuery query, int maxResults, int timeLimit)
            {
                const int TICKS_PER_MSEC = 10000;
                if (query == null || !query.IsConstrained)
                {
                    return null;
                }

                long stop = DateTime.Now.Ticks + (long)timeLimit * TICKS_PER_MSEC;

                buildOccurrenceKindWeightTable();
                kwdList = new ArrayList();
                query.Visit(this);
                kwds = (KeywordList[])kwdList.ToArray(typeof(KeywordList));
                query = optimize(query);
                //Console.WriteLine(query.ToString());
                FullTextSearchHit[] hits = new FullTextSearchHit[maxResults];
                int currDoc = 1;
                int nResults = 0;
                float nearnessWeight = impl.helper.NearnessWeight;
                bool noMoreMatches = false;
                while (nResults < maxResults && DateTime.Now.Ticks < stop)
                {
                    currDoc = intersect(currDoc, query);
                    if (currDoc == int.MaxValue)
                    {
                        noMoreMatches = true;
                        break;
                    }
                    reset();
                    double kwdRank = evaluate(currDoc, query);
                    if (kwdRank >= 0)
                    {
                        double nearness = calculateNearness();
                        float rank = (float)(kwdRank * (1 + calculateNearness() * nearnessWeight));
                        //Console.WriteLine("kwdRank=" + kwdRank + ", nearness=" + nearness + ", total rank=" + rank);
                        hits[nResults++] = new FullTextSearchHit(impl.Storage, currDoc, rank);
                    }
                    currDoc += 1;
                }
                int estimation;
                if (nResults < maxResults)
                {
                    FullTextSearchHit[] realHits = new FullTextSearchHit[nResults];
                    Array.Copy(hits, 0, realHits, 0, nResults);
                    hits = realHits;
                }
                if (noMoreMatches)
                {
                    estimation = nResults;
                }
                else if (query is FullTextQueryMatchOp)
                {
                    estimation = kwds[0].list.Count;
                }
                else
                {
                    estimation = calculateEstimation(query, nResults);
                }
                Array.Sort(hits);
                return new FullTextSearchResult(hits, estimation);
            }
        }

        public FullTextSearchResult Search(FullTextQuery query, int maxResults, int timeLimit)
        {
            FullTextSearchEngine engine = new FullTextSearchEngine(this);
            return engine.Search(query, maxResults, timeLimit);
        }

        public FullTextSearchResult SearchPrefix(string prefix, int maxResults, int timeLimit, bool sort)
        {
            FullTextSearchEngine engine = new FullTextSearchEngine(this);
            return engine.SearchPrefix(prefix, maxResults, timeLimit, sort);
        }

        public virtual FullTextSearchHelper Helper
        {
            get
            {
                return helper;
            }
        }

        public FullTextIndexImpl(Storage storage, FullTextSearchHelper helper)
            : base(storage)
        {
            this.helper = helper;
#if USE_GENERICS
            inverseIndex = storage.CreateIndex<string, InverseList>(true);
            documents = storage.CreateIndex<object,Document>(true);
#else
            inverseIndex = storage.CreateIndex(typeof(string), true);
            documents = storage.CreateIndex(typeof(object), true);
#endif
        }

        internal FullTextIndexImpl()
        {
        }
    }
}