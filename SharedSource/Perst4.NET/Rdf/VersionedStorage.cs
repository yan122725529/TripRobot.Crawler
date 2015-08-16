using System;
using System.Collections;
using Perst;

namespace Rdf 
{
    /// <summary>Root class for Perst storage</summary>
    public class DatabaseRoot:PersistentResource 
    {
        /// <summary>Root object in the graph</summary>
        public VersionHistory rootObject;
        /// <summary>Index used to access object by URI prefix</summary>
        public Index          prefixUriIndex;
        /// <summary>Index used to access object by URI suffix</summary>
        public Index          suffixUriIndex;
        /// <summary>Index used to search object by string property name:value pair</summary>
        public CompoundIndex  strPropIndex;
        /// <summary>Index used to search object by numeric property name:value pair</summary>
        public CompoundIndex  numPropIndex;
        /// <summary>Index used to search object by datetime property name:value pair</summary>
        public CompoundIndex  timePropIndex;
        /// <summary>Index used to search object by reference property name:value pair</summary>
        public CompoundIndex  refPropIndex;
        /// <summary>Index used to locate property definition by property name</summary>
        public FieldIndex     propDefIndex;
        /// <summary>Index used to perform spatial search locating overlapped rectangles</summary>
        public Index          inverseIndex;
        /// <summary>Inverse keywords index</summary>
        public SpatialIndexR2 spatialIndex;
        /// <summary>Set of the latest versions</summary>
        public ISet           latest;
        /// <summary>Timestamp index</summary>
        public FieldIndex     timeIndex;
        /// <summary>Type of the types</summary>
        public VersionHistory metatype;
    }

    /// <summary>Which verions of the object should be inspected</summary>
    public enum SearchKind 
    {
        /// <summary>Latest version in version history</summary>
        LatestVersion,
        /// <summary>All versions in version history</summary>
        AllVersions,
        /// <summary>Latest version before sepcified timestamp</summary>
        LatestBefore,
        /// <summary>Oldest version after sepcified timestamp</summary>
        OldestAfter
    }

    /// <summary>
    /// Main class
    /// </summary>
    public class VersionedStorage 
    { 
        Storage      db;
        DatabaseRoot root;

        /// <summary>
        /// List of separator characters used to split string into keywords
        /// </summary>
        public static char[] keywordSeparators = 
        {
            ' ', 
            ','
        };

        /// <summary>
        /// List of most commonly used words which should be ignored andnot included in inverse index
        /// </summary>
        public static Hashtable keywordStopList = new Hashtable();

        static VersionedStorage()  
        {
            keywordStopList["the"] = true;
            keywordStopList["at"] = true;
            keywordStopList["of"] = true;
            keywordStopList["a"] = true;
            keywordStopList["to"] = true;
            keywordStopList["at"] = true;
            keywordStopList["and"] = true;
            keywordStopList["or"] = true;
            keywordStopList["i"] = true;
        }

        /// <summary>Open database</summary>
        /// <param name="filePath">path to the database file</param>    
        public void Open(string filePath) 
        { 
            db = StorageFactory.Instance.CreateStorage(); 
            db.Open(filePath);
            root = (DatabaseRoot)db.Root;
            if (root == null) 
            {
                root = new DatabaseRoot();
                root.prefixUriIndex = db.CreateIndex(typeof(string), true);
                root.suffixUriIndex = db.CreateIndex(typeof(string), true);
                root.strPropIndex = db.CreateIndex(new Type[]{typeof(PropDef), typeof(string)}, false);
                root.numPropIndex = db.CreateIndex(new Type[]{typeof(PropDef), typeof(double)}, false);
                root.refPropIndex = db.CreateIndex(new Type[]{typeof(PropDef), typeof(VersionHistory)}, false);
                root.timePropIndex = db.CreateIndex(new Type[]{typeof(PropDef), typeof(DateTime)}, false);
                root.propDefIndex = db.CreateFieldIndex(typeof(PropDef), "name", true);            
                root.timeIndex = db.CreateFieldIndex(typeof(Thing), "timestamp", false);
                root.inverseIndex = db.CreateIndex(typeof(string), false);
                root.spatialIndex = db.CreateSpatialIndexR2();
                root.latest = db.CreateSet();
                CreateMetaType();
                db.Root = root;
            }
        }
    
        /// <summary>Get verion history by URI</summary>
        /// <param name="uri">object URI</param>
        /// <returns>version history or null if no such object is found</returns>
        public VersionHistory GetObject(string uri) 
        {
            return (VersionHistory)root.prefixUriIndex[uri];
        }

        /// <summary>Get latest verion of object with specified URI</summary>
        /// <param name="uri">object URI</param>
        /// <returns>latest version of object or null if no such object is found</returns>
        public Thing GetLatestVersion(string uri) 
        {
            VersionHistory vh = (VersionHistory)root.prefixUriIndex[uri];
            return (vh != null) ? vh.Latest : null;
        }        

        /// <summary>Get verion history by URI and timestamp</summary>
        /// <param name="uri">object URI</param>
        /// <param name="kind">search kind, should be object SearchKind.LatestVersion, SearchKind.LatestBefore or 
        /// SearchKind.OldestAfter</param>
        /// <param name="timestamp">timestamp used to locate version</param>
        /// <returns>version of the object or null if no such version is found</returns>
        public Thing GetVersion(string uri, SearchKind kind, DateTime timestamp) 
        {
            VersionHistory vh = (VersionHistory)root.prefixUriIndex[uri];
            if (vh != null) 
            { 
                return vh.GetVersion(kind, timestamp);
            }
            return null;
        }

        /// <summary>Create bew object. If version history with this URI is not exists, it is created first.
        /// Then new object version is created and appended to this version history.
        /// </summary>
        /// <param name="uri">object URI</param>
        /// <param name="type">URI of object type</param>
        /// <param name="props">object properties</param>
        /// <returns>created object version</returns>
        public Thing CreateObject(string uri, string type, NameVal[] props) 
        {
            VersionHistory vh = (VersionHistory)root.prefixUriIndex[uri];
            if (vh == null) 
            {
                VersionHistory typeVh = null;
                typeVh = GetObject(type);
                if (typeVh == null) 
                { 
                    typeVh = CreateVersionHistory(type, root.metatype);
                    CreateObject(root.metatype.Latest, typeVh, new NameVal[0]);
                }
                vh = CreateVersionHistory(uri, typeVh);
            } 
            else 
            { 
                root.latest.Remove(vh.Latest);
            }
            return CreateObject(vh.type.Latest, vh, props); 
        }

        /// <summary>Get iterator through object matching specified search parameters</summary>
        /// <param name="type">String representing type of the object (direct or indirect - IsInstanceOf
        /// method will be used to check if object belongs to the specified type). It may be null, 
        /// in this case type criteria is skipped.</param>
        /// <param name="uri">Object URI pattern. It may be null, in this case URI is not inspected.</param>
        /// <param name="patterns">array of name:value pairs specifying search condition for object properties</param>
        /// <param name="kind">search kind used to select inspected versions</param>
        /// <param name="timestamp">timestamp used to select versions, if kind is SearchKind.LatestVersion
        /// or SearchKind.AllVersions this parameter is ignored</param>
        /// <returns>Enumerator through object meet search criteria.</returns>
        public IEnumerable Search(string type, string uri, NameVal[] patterns, SearchKind kind, DateTime timestamp) 
        {
            VersionHistory typeVh = null;
            root.SharedLock();
            try 
            {
                if (type != null) 
                { 
                    typeVh = GetObject(type);
                    if (typeVh == null) 
                    { 
                        return new object[0]; // empty selection
                    }
                }
                if (uri != null) 
                {
                    int wc = uri.IndexOf('*');
                    if (wc < 0) 
                    { 
                        return new SearchResult(root, typeVh, null, patterns, kind, timestamp, root.prefixUriIndex.GetEnumerator(uri, uri));
                    } 
                    else if (wc > 0) 
                    { 
                        String prefix = uri.Substring(0, wc);
                        return new SearchResult(root, typeVh, uri, patterns, kind, timestamp, root.prefixUriIndex.GetEnumerator(prefix));
                    } 
                    else if ((wc = uri.LastIndexOf('*')) < uri.Length-1) 
                    {
                        String suffix = ReverseString(uri.Substring(wc+1, uri.Length-wc-1));
                        return new SearchResult(root, typeVh, uri, patterns, kind, timestamp, root.suffixUriIndex.GetEnumerator(suffix));
                    }
                }
                if (patterns.Length > 0) 
                { 
                    NameVal prop = patterns[0];
                    object val = prop.val;
                    NameVal[] restOfPatterns = SubArray(patterns);

                    switch (prop.name) 
                    {
                        case Symbols.Timestamp: 
                  
                            if (val is Range) 
                            { 
                                Range range = (Range)val;
                                if (range.from is DateTime) 
                                {
                                    Key fromKey = new Key((DateTime)range.from, range.fromInclusive);
                                    Key tillKey = new Key((DateTime)range.till, range.tillInclusive);
                                    return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                        root.timeIndex.GetEnumerator(fromKey, tillKey));
                                    
                                }
                            } 
                            else if (val is DateTime) 
                            {
                                Key key = new Key((DateTime)val);
                                return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                    root.timeIndex.GetEnumerator(key, key));                            
                            } 
                            return new object[0]; // empty selection
                        case Symbols.Rectangle:
                            if (val is NameVal[]) 
                            {
                                NameVal[] coord = (NameVal[])val;
                                if (coord.Length == 4) 
                                {
                                    RectangleR2 r = new RectangleR2((double)coord[0].val, 
                                        (double)coord[1].val, 
                                        (double)coord[2].val, 
                                        (double)coord[3].val);
                                    return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                        root.spatialIndex.Overlaps(r).GetEnumerator());
                                }
                            }
                            break;
                        case Symbols.Point:
                            if (val is NameVal[]) 
                            {
                                NameVal[] coord = (NameVal[])val;
                                if (coord.Length == 2) 
                                {
                                    double x = (double)coord[0].val;
                                    double y = (double)coord[1].val;
                                    RectangleR2 r = new RectangleR2(x, y, x, y);
                                    return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                        root.spatialIndex.Overlaps(r).GetEnumerator());
                                }
                            }
                            break;
                        case Symbols.Keyword:
                            if (val is string) 
                            {
                                ArrayList keywords = new ArrayList();
                                foreach (string keyword in ((string)val).ToLower().Split(keywordSeparators)) 
                                {
                                    if (keyword.Length > 0 && !keywordStopList.ContainsKey(keyword))
                                    {
                                        keywords.Add(keyword);
                                    }
                                }
                                IEnumerator[] occurences = new IEnumerator[keywords.Count];
                                for (int i = 0; i < occurences.Length; i++) 
                                { 
                                    Key key = new Key((string)keywords[i]);
                                    occurences[i] = root.inverseIndex.GetEnumerator(key, key);
                                }
                                return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, db.Merge(occurences));
                            }
                            break;
                    }

                    PropDef def = (PropDef)root.propDefIndex[prop.name];
                    if (def == null) 
                    { 
                        return new object[0]; // empty selection
                    }
                    if (val is Range) 
                    { 
                        Range range = (Range)val;
                        if (range.from is double) 
                        {
                            Key fromKey = new Key(new object[]{def, range.from}, range.fromInclusive);
                            Key tillKey = new Key(new object[]{def, range.till}, range.tillInclusive);
                            return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                root.numPropIndex.GetEnumerator(fromKey, tillKey));
                        } 
                        else if (range.from is DateTime) 
                        {
                            Key fromKey = new Key(new object[]{def, range.from}, range.fromInclusive);
                            Key tillKey = new Key(new object[]{def, range.till}, range.tillInclusive);
                            return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                root.timePropIndex.GetEnumerator(fromKey, tillKey));
                        } 
                        else 
                        { 
                            Key fromKey = new Key(new object[]{def, range.from}, range.fromInclusive);
                            Key tillKey = new Key(new object[]{def, range.till}, range.tillInclusive);
                            return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                root.strPropIndex.GetEnumerator(fromKey, tillKey));
                        }
                    } 
                    else if (val is string) 
                    {
                        string str = (string)val;
                        int wc = str.IndexOf('*');
                        if (wc < 0) 
                        { 
                            Key key = new Key(new object[]{def, str});
                            return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                                root.strPropIndex.GetEnumerator(key, key));
                                
                        } 
                        else if (wc > 0) 
                        { 
                            string prefix = str.Substring(0, wc);
                            Key fromKey = new Key(new object[]{def, prefix});
                            Key tillKey = new Key(new object[]{def, prefix + Char.MaxValue}, false);                        
                            return new SearchResult(root, typeVh, uri, wc == str.Length-1 ? restOfPatterns : patterns, kind, timestamp, 
                                root.strPropIndex.GetEnumerator(fromKey, tillKey));
                        }                             
                    }
                    else if (val is double)
                    {
                        Key key = new Key(new object[]{def, val});
                        return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                            root.numPropIndex.GetEnumerator(key, key));
                    } 
                    else if (val is DateTime) 
                    {
                        Key key = new Key(new object[]{def, val});
                        return new SearchResult(root, typeVh, uri, restOfPatterns, kind, timestamp, 
                            root.timePropIndex.GetEnumerator(key, key));
                    }
                    else if (val is NameVal) 
                    { 
                        IEnumerable iterator = SearchReferenceProperty(typeVh, uri, patterns, kind, timestamp, (NameVal)val, false, def, new ArrayList());
                        if (iterator != null) 
                        {
                            return iterator;
                        }
                    }
                    else if (val is NameVal[]) 
                    { 
                        NameVal[] props = (NameVal[])val;
                        if (props.Length > 0) 
                        {
                            IEnumerable iterator = SearchReferenceProperty(typeVh, uri, patterns, kind, timestamp, props[0], props.Length > 1, def, new ArrayList());
                            if (iterator != null) 
                            {
                                return iterator;
                            }
                        }
                    }
                    
                }
                if (kind == SearchKind.LatestVersion) 
                { 
                    return new SearchResult(root, typeVh, uri, patterns, kind, timestamp, root.latest.GetEnumerator());   
                }
                return new SearchResult(root, typeVh, uri, patterns, kind, timestamp, root.timeIndex.GetEnumerator());           
            } 
            finally 
            { 
                root.Unlock();
            }
        }
                        
        class ReferenceIterator:IEnumerable,IEnumerator 
        {
            PropDef[]     defs;
            IEnumerator[] iterators;
            int           pos;
            Thing         currThing;
            SearchKind    kind;
            DateTime      timestamp; 
            DatabaseRoot  root;
            Hashtable     visited;

            public IEnumerator GetEnumerator() 
            {
                return this;
            }

            public ReferenceIterator(DatabaseRoot root, PropDef[] defs, IEnumerator iterator, SearchKind kind, DateTime timestamp) 
            {
                this.root = root;
                this.defs = defs;
                this.kind = kind;
                this.timestamp = timestamp;
                iterators = new IEnumerator[defs.Length+1];
                iterators[iterators.Length-1] = iterator;
                Reset();
            }

            public void Reset() 
            {
                visited = new Hashtable();
                currThing = null;
                pos = iterators.Length-1;
                iterators[pos].Reset();
            }

            public object Current 
            {
                get 
                {
                    if (currThing == null) 
                    { 
                        throw new InvalidOperationException("No current element");
                    }
                    return currThing;
                }
            }


            public bool MoveNext() 
            {
                while (true) 
                {
                    while (pos < iterators.Length && !iterators[pos].MoveNext()) 
                    {
                        pos += 1;
                    }
                    if (pos == iterators.Length) 
                    { 
                        currThing = null;
                        return false;
                    } 
                    Thing thing = (Thing)iterators[pos].Current;
                    switch (kind) 
                    {
                        case SearchKind.LatestVersion:
                            if (!thing.IsLatest()) 
                            {
                                continue;
                            }
                            break;
                        case SearchKind.LatestBefore:
                            if (thing.timestamp > timestamp) 
                            { 
                                continue;
                            }
                            break;
                        case SearchKind.OldestAfter:
                            if (thing.timestamp < timestamp) 
                            { 
                                continue;
                            }
                            break;
                    }
                    if (pos == 0) 
                    { 
                        if (visited.ContainsKey(thing.Oid)) 
                        {
                            continue;
                        } 
                        else 
                        {
                            visited[thing.Oid] = true;
                        }
                        currThing = thing;
                        return true;
                    }
                    pos -= 1;
                    Key key = new Key(new object[]{defs[pos], thing.vh});
                    iterators[pos] = root.refPropIndex.GetEnumerator(key, key);
                }
            }
        }
       
        private IEnumerable SearchReferenceProperty(VersionHistory type, string uri, NameVal[] patterns, SearchKind kind, DateTime timestamp, NameVal prop, bool compound, PropDef def, ArrayList refs)
        {
            refs.Add(def);

            NameVal[] restOfPatterns = compound ? patterns : SubArray(patterns);

            object val = prop.val;
            switch (prop.name) 
            {
                case Symbols.Timestamp: 
            
                    if (val is Range) 
                    { 
                        Range range = (Range)val;
                        if (range.from is DateTime) 
                        {
                            Key fromKey = new Key((DateTime)range.from, range.fromInclusive);
                            Key tillKey = new Key((DateTime)range.till, range.tillInclusive);
                            return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                                new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                                root.timeIndex.GetEnumerator(fromKey, tillKey), kind, timestamp));
                        }
                    } 
                    else if (val is DateTime) 
                    {
                        Key key = new Key((DateTime)val);
                        return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                            new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                            root.timeIndex.GetEnumerator(key, key), kind, timestamp));                            
                    } 
                    return new object[0]; // empty selection
                case Symbols.Rectangle:
                    if (val is NameVal[]) 
                    {
                        NameVal[] coord = (NameVal[])val;
                        if (coord.Length == 4) 
                        {
                            RectangleR2 r = new RectangleR2((double)coord[0].val, 
                                (double)coord[1].val, 
                                (double)coord[2].val, 
                                (double)coord[3].val);
                            return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                                new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                                root.spatialIndex.Overlaps(r).GetEnumerator(), kind, timestamp));
                        }
                    }
                    break;
                case Symbols.Point:
                    if (val is NameVal[]) 
                    {
                        NameVal[] coord = (NameVal[])val;
                        if (coord.Length == 2) 
                        {
                            double x = (double)coord[0].val;
                            double y = (double)coord[1].val;
                            RectangleR2 r = new RectangleR2(x, y, x, y);
                            return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                                new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                                root.spatialIndex.Overlaps(r).GetEnumerator(), kind, timestamp));
                        }
                    }
                    break;
                case Symbols.Keyword:
                    if (val is string) 
                    {
                        ArrayList keywords = new ArrayList();
                        foreach (string keyword in ((string)val).ToLower().Split(keywordSeparators)) 
                        {
                            if (keyword.Length > 0 && !keywordStopList.ContainsKey(keyword))
                            {
                                keywords.Add(keyword);
                            }
                        }
                        IEnumerator[] occurences = new IEnumerator[keywords.Count];
                        for (int i = 0; i < occurences.Length; i++) 
                        { 
                            Key key = new Key((string)keywords[i]);
                            occurences[i] = root.inverseIndex.GetEnumerator(key, key);
                        }
                        return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                            new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                            db.Merge(occurences), kind, timestamp));
                    }
                    break;
            }

            def = (PropDef)root.propDefIndex[prop.name];
            if (def == null) 
            { 
                return new object[0]; // empty selection
            }
            if (val is Range) 
            { 
                Range range = (Range)val;
                if (range.from is double) 
                {
                    Key fromKey = new Key(new object[]{def, range.from}, range.fromInclusive);
                    Key tillKey = new Key(new object[]{def, range.till}, range.tillInclusive);
                    return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                        new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                        root.numPropIndex.GetEnumerator(fromKey, tillKey), kind, timestamp));
                } 
                else if (range.from is DateTime) 
                {
                    Key fromKey = new Key(new object[]{def, range.from}, range.fromInclusive);
                    Key tillKey = new Key(new object[]{def, range.till}, range.tillInclusive);
                    return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                        new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                        root.timePropIndex.GetEnumerator(fromKey, tillKey), kind, timestamp));
                } 
                else 
                { 
                    Key fromKey = new Key(new object[]{def, range.from}, range.fromInclusive);
                    Key tillKey = new Key(new object[]{def, range.till}, range.tillInclusive);
                    return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                        new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                        root.strPropIndex.GetEnumerator(fromKey, tillKey), kind, timestamp));
                }
            } 
            if (val is string) 
            {
                string str = (string)prop.val;
                int wc = str.IndexOf('*');
                if (wc < 0) 
                { 
                    Key key = new Key(new object[]{def, str});
                    return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                        new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                        root.strPropIndex.GetEnumerator(key, key), kind, timestamp));
                } 
                else if (wc > 0) 
                { 
                    string prefix = str.Substring(0, wc);
                    Key fromKey = new Key(new object[]{def, prefix});
                    Key tillKey = new Key(new object[]{def, prefix + Char.MaxValue}, false);                        
                    return new SearchResult(root, type, uri, wc == str.Length-1 ? restOfPatterns : patterns, kind, timestamp, 
                        new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                        root.strPropIndex.GetEnumerator(fromKey, tillKey), kind, timestamp));
                } 
            } 
            else if (val is double) 
            {
                Key key = new Key(new object[]{def, val});
                return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                    new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                    root.numPropIndex.GetEnumerator(key, key), kind, timestamp));
            } 
            else if (val is DateTime) 
            {
                Key key = new Key(new object[]{def, (DateTime)val});
                return new SearchResult(root, type, uri, restOfPatterns, kind, timestamp, 
                    new ReferenceIterator(root, (PropDef[])refs.ToArray(typeof(PropDef)), 
                    root.timePropIndex.GetEnumerator(key, key), kind, timestamp));
            } 
            else if (val is NameVal) 
            {
                return SearchReferenceProperty(type, uri, patterns, kind, timestamp, (NameVal)val, compound, def, refs);
            }
            else if (val is NameVal[]) 
            {
                NameVal[] props = (NameVal[])val;
                if (props.Length > 0) 
                {
                    return SearchReferenceProperty(type, uri, patterns, kind, timestamp, props[0], true, def, refs);
                }
            }
            return null;
        }
        
        /// <summary>Close database</summary>
        public void Close() 
        {
            db.Close();
        }

        /// <summary>Commit current transaction</summary>
        public void Commit() 
        {
            db.Commit();
            root.Unlock();
        }

        /// <summary>Rollback current transaction</summary>
        public void Rollback() 
        {
            db.Rollback();
            root.Unlock();
        }

        /// <summary>
        /// Begin new write transction: set exclusive lock
        /// </summary>
        public void BeginTransaction() 
        {
            root.ExclusiveLock();
        }

        class SearchResult:IEnumerable,IEnumerator 
        {
            VersionHistory type;
            string         uri;
            NameVal[]      patterns;
            SearchKind     kind;
            DateTime       timestamp;
            IEnumerator    iterator;
            Thing          currThing;
            int            currVersion;
            Link           currHistory;
            DatabaseRoot   root;

            public IEnumerator GetEnumerator() 
            { 
                return this;
            }
        
            public SearchResult(DatabaseRoot root, VersionHistory type, string uri, NameVal[] patterns, SearchKind kind, DateTime timestamp, IEnumerator iterator) 
            {
                this.root = root;
                this.type = type;    
                this.uri = uri;    
                this.patterns = patterns;    
                this.kind = kind;    
                this.timestamp = timestamp;    
                this.iterator = iterator;    
            }

            public void Reset() 
            { 
                iterator.Reset();
                currThing = null;
                currHistory = null;
            }
 
            public bool MoveNext() 
            { 
                currThing = null;

            Repeat:
                if (currHistory != null) 
                { 
                    while (currVersion < currHistory.Count) 
                    { 
                        Thing thing = (Thing)currHistory[currVersion++];
                        if (Match(thing)) 
                        { 
                            return true;
                        }
                    }
                    currHistory = null;
                }              
                while (iterator.MoveNext()) 
                { 
                    object curr = iterator.Current;
                    if (curr is Thing) 
                    { 
                        if (Match((Thing)curr)) 
                        { 
                            return true;
                        }
                    } 
                    else if (curr is VersionHistory) 
                    { 
                        currHistory = ((VersionHistory)curr).versions;
                        currVersion = 0;
                        goto Repeat;
                    }                    
                }
                return false;
            }

            private static bool MatchString(string str, string pat) 
            { 

                if (pat.IndexOf('*') < 0) 
                { 
                    return  pat.Equals(str); 
                }
                int pi = 0, si = 0, pn = pat.Length, sn = str.Length; 
                int wildcard = -1, strpos = -1;
                while (true) 
                { 
                    if (pi < pn && pat[pi] == '*') 
                    { 
                        wildcard = ++pi;
                        strpos = si;
                    } 
                    else if (si == sn) 
                    { 
                        return pi == pn;
                    } 
                    else if (pi < pn && str[si] == pat[pi]) 
                    {
                        si += 1;
                        pi += 1;
                    } 
                    else if (wildcard >= 0) 
                    { 
                        si = ++strpos;
                        pi = wildcard;
                    } 
                    else 
                    { 
                        return false;
                    }
                }
            }

            private bool Match(Thing thing) 
            { 
        
                if (type != null && !thing.IsInstanceOf(type, kind, timestamp)) 
                {
                    return false;
                }
                switch (kind) 
                { 
                    case SearchKind.LatestVersion:
                        if (!thing.IsLatest()) 
                        { 
                            return false;
                        }
                        break;
                    case SearchKind.LatestBefore:
                        if (thing.timestamp > timestamp || thing.vh.GetLatestBefore(timestamp) != thing) 
                        {
                            return false;
                        }
                        break;
                    case SearchKind.OldestAfter:
                        if (thing.timestamp < timestamp || thing.vh.GetOldestAfter(timestamp) != thing)
                        {
                            return false;
                        }
                        break;
                    default:
                        break;
                }

                if (uri != null) 
                { 
                    if (!MatchString(thing.vh.uri, uri)) 
                    { 
                        return false;
                    }
                }
                for (int i = 0; i < patterns.Length; i++) 
                { 
                    if (!MatchProperty(patterns[i], thing)) 
                    { 
                        return false;
                    }
                }
                currThing = thing;
                return true;
            }

            public object Current 
            {
                get 
                {
                    if(currThing == null) 
                    { 
                        throw new InvalidOperationException("No current element");
                    }
                    return currThing;
                }
            }

            private bool MatchProperty(NameVal prop, Thing thing) 
            {
                switch (prop.name) 
                {
                    case Symbols.Point:
                        if (prop.val is NameVal[]) 
                        {
                            NameVal[] coord = (NameVal[])prop.val;
                            if (coord.Length == 2) 
                            { 
                                double x = (double)coord[0].val;
                                double y = (double)coord[1].val;
                                RectangleR2 r = new RectangleR2(x, y, x, y);
                                foreach (Thing t in root.spatialIndex.Overlaps(r)) 
                                {
                                    if (t == thing) 
                                    {
                                        return true;   
                                    }
                                }
                                return false;
                            }
                        }
                        break;
                    
                    case Symbols.Rectangle:
                        if (prop.val is NameVal[]) 
                        {
                            NameVal[] coord = (NameVal[])prop.val;
                            if (coord.Length == 4) 
                            { 
                                RectangleR2 r = new RectangleR2((double)coord[0].val,
                                        (double)coord[1].val,
                                        (double)coord[2].val,
                                        (double)coord[3].val);
                                foreach (Thing t in root.spatialIndex.Overlaps(r)) 
                                {
                                    if (t == thing) 
                                    {
                                        return true;   
                                    }
                                }
                                return false;
                            }
                        }
                        break;
                    
                    case Symbols.Keyword:
                        if (prop.val is string) 
                        {
                            Hashtable keywords = new Hashtable();
                            foreach (PropVal pv in thing.props) 
                            { 
                                object val = pv.val;
                                if (val is string) 
                                {
                                    foreach (string keyword in ((string)val).ToLower().Split(keywordSeparators)) 
                                    {
                                        if (keyword.Length > 0 && !keywordStopList.ContainsKey(keyword)) 
                                        {
                                            keywords[keyword] = this;
                                        }
                                    }
                                }
                            }
                            foreach (string keyword in ((string)prop.val).ToLower().Split(keywordSeparators)) 
                            {
                                if (keyword.Length > 0 && !keywordStopList.ContainsKey(keyword) && !keywords.ContainsKey(keyword)) 
                                {
                                    return false;
                                }
                            }
                            return true;
                        }
                        break;                 
                }
                                                                  
            NextItem:
                foreach (object val in thing[prop.name]) 
                {
                    object pattern = prop.val;
                    if (val is string && pattern is string) 
                    { 
                        if (MatchString((string)val, (string)pattern)) 
                        {
                            return true;
                        }
                    } 
                    else if (pattern is NameVal) 
                    { 
                        if (FollowReference((NameVal)pattern, val as VersionHistory)) 
                        { 
                            return true;
                        }
                    }
                    else if (pattern is NameVal[]) 
                    { 
                        foreach (NameVal p in (NameVal[])prop.val) 
                        {
                            if (!FollowReference(p, val as VersionHistory))
                            {
                                goto NextItem;
                            }
                        }
                        return true;
                    } 
                    else if (pattern is Range && val is IComparable) 
                    {
                        try 
                        {
                            Range range = (Range)pattern;
                            IComparable cmp = (IComparable)val;
                            return cmp.CompareTo(range.from) >= (range.fromInclusive ? 0 : 1) &&
                                cmp.CompareTo(range.till) <= (range.tillInclusive ? 0 : -1);
                        } 
                        catch (ArgumentException) {}
                    }
                    else if (pattern != null && pattern.Equals(val))
                    {
                        return true;
                    }
                }
                return false;
            }

            private bool FollowReference(NameVal prop, VersionHistory vh) 
            {
                if (vh != null) 
                { 
                    if (kind == SearchKind.AllVersions) 
                    { 
                        foreach (Thing v in vh.versions) 
                        {
                            if (MatchProperty(prop, v)) 
                            {
                                return true;
                            }
                        }
                    } 
                    else 
                    { 
                        Thing thing = vh.GetVersion(kind, timestamp);
                        return thing != null && MatchProperty(prop, thing); 
                    }
                }
                return false;
            }
        }
    
    
        private void CreateMetaType()
        {
            VersionHistory vh = CreateVersionHistory(Symbols.Metatype, null);
            vh.type = vh;
            Thing metatype = CreateObject(null, vh, new NameVal[0]);
            metatype.type = metatype;
            root.metatype = vh;
        }

        private static string ReverseString(string s) 
        { 
            int len = s.Length;
            char[] chars = new char[len];
            for (int i = 0; i < len; i++) 
            { 
                chars[i] = s[len-i-1];
            }
            return new string(chars);
        }

        
        private VersionHistory CreateVersionHistory(String uri, VersionHistory type) 
        {
            VersionHistory vh = new VersionHistory();
            vh.uri = uri;
            vh.type = type;
            vh.versions = db.CreateLink();
            root.prefixUriIndex.Put(uri, vh);
            root.suffixUriIndex.Put(ReverseString(uri), vh);
            return vh;
        }

        private static NameVal[] SubArray(NameVal[] arr) 
        {
            NameVal[] newArr = new NameVal[arr.Length-1];
            Array.Copy(arr, 1, newArr, 0, newArr.Length);
            return newArr;
        }

        private Thing CreateObject(Thing type, VersionHistory vh, NameVal[] props) 
        {
            Thing thing = new Thing();
            thing.vh = vh;
            thing.type = type;
            thing.timestamp = DateTime.Now;
            thing.props = new PropVal[props.Length];
            for (int i = 0; i < props.Length; i++) 
            { 
                NameVal prop = props[i];
                PropDef def = (PropDef)root.propDefIndex[prop.name];
                if (def == null) 
                {
                    def = new PropDef();
                    def.name = prop.name;
                    root.propDefIndex.Put(def);
                }
                object val = prop.val;
                PropVal pv = new PropVal(def, val);
                Key key = new Key(new object[]{def, val});
                if (val is string) 
                { 
                    root.strPropIndex.Put(key, thing);
                    foreach (string keyword in ((string)val).ToLower().Split(keywordSeparators)) 
                    {
                        if (keyword.Length > 0 && !keywordStopList.ContainsKey(keyword)) 
                        {
                            root.inverseIndex.Put(keyword, thing);
                        }
                    }
                } 
                else if (val is double) 
                { 
                    root.numPropIndex.Put(key, thing);
                } 
                else if (val is DateTime) 
                { 
                    root.timePropIndex.Put(key, thing);
                } 
                else if (val is VersionHistory || val == null) 
                { 
                    root.refPropIndex.Put(key, thing);
                    if (prop.name == Symbols.Rectangle) 
                    {
                        PropVal[] coord = ((VersionHistory)val).Latest.props;
                        RectangleR2 r = new RectangleR2((double)coord[0].val, 
                            (double)coord[1].val, 
                            (double)coord[2].val, 
                            (double)coord[3].val);
                        root.spatialIndex.Put(r, thing);   
                    }
                    else if (prop.name == Symbols.Point) 
                    {
                        PropVal[] coord = ((VersionHistory)val).Latest.props;
                        double x = (double)coord[0].val;
                        double y = (double)coord[1].val;
                        RectangleR2 r = new RectangleR2(x, y, x, y);
                        root.spatialIndex.Put(r, thing);   
                    }
                } 
                else 
                { 
                    throw new InvalidOperationException("Invalid propery value type " + prop.val.GetType());
                }
                thing.props[i] = pv;                  
            }
            thing.Modify();
            vh.versions.Add(thing);
            root.timeIndex.Put(thing);
            root.latest.Add(thing);
            return thing;
        }
    }
}