#if !USE_GENERICS
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Text;
using Perst.Impl;
using Perst.FullText;
#if NET_FRAMEWORK_35
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
#endif

namespace Perst
{
    [Flags]
    public enum IndexKind
    {
        Default = 0,
        Unique = 1,
        Regex = 2,
        Thick = 4,
        RandomAccess = 8,
        CaseInsensitive = 16
    };

    /// <summary>
    /// This class emulates relational database on top of Perst storage
    /// It maintain class extends, associated indices, prepare queries.
    /// Starting from 2.72 version of Perst.Net, it supports automatic
    /// creation of table descriptors when Database class is used.
    /// So now it is not necessary to explicitly create tables and indices -
    /// the Database class will create them itself on demand.
    /// Indexable attribute should be used to mark key fields for which index should be created.
    /// Table descriptor is created when
    /// instance of the correspondent class is first time stored in the
    /// database. Perst creates table descriptors for all derived classes up
    /// to the root Perst.Persistent class.
    /// </summary>
    public class Database : IndexProvider
    { 
        /// <summary> 
        /// Constructor of database. This method initialize database if it not initialized yet.
        /// </summary>
        /// <param name="storage">opened storage. Storage should be either empty (non-initialized, either
        /// previously initialized by the this method. It is not possible to open storage with 
        /// root object other than table index created by this constructor.
        /// </param>
        /// <param name="multithreaded"><b>true</b> if database should support concurrent access
        /// to the data from multiple threads</param>
        public Database(Storage storage, bool multithreaded)
        : this(storage, multithreaded, true, null)
        {
        }
            
        /// <summary> 
        /// Constructor of database. This method initialize database if it not initialized yet.
        /// </summary>
        /// <param name="storage">opened storage. Storage should be either empty (non-initialized, either
        /// previously initialized by the this method. It is not possible to open storage with 
        /// root object other than table index created by this constructor.
        /// </param>
        /// <param name="multithreaded"><b>true</b> if database should support concurrent access</param>
        /// <param name="autoRegisterTables">automatically create tables descriptors for instances 
        /// of new classes inserted in the database</param>
        /// <param name="helper">helper for full text index</param>
        public Database(Storage storage, bool multithreaded, bool autoRegisterTables, FullTextSearchHelper helper) 
        { 
            this.storage = storage;
            this.multithreaded = multithreaded;
            this.autoRegisterTables = autoRegisterTables;       
            this.searchBaseClasses = !autoRegisterTables && !false.Equals(storage.GetProperty("perst.search.base.classes"));
            this.globalClassExtent = !false.Equals(storage.GetProperty("perst.global.class.extent"));
            if (multithreaded) 
            { 
                storage.SetProperty("perst.alternative.btree", true);
            }
            storage.SetProperty("perst.concurrent.iterator", true);
            bool schemaUpdated = false;
            object root = storage.Root;
            if (root is Index) // backward compatibility
            {
                BeginTransaction();
                metadata = new Metadata(storage, (Index)root, helper);
                storage.Root = metadata;
                schemaUpdated = true;
            } 
            else if (root == null) 
            { 
                BeginTransaction();
                metadata = new Metadata(storage, helper);
                storage.Root = metadata;
                schemaUpdated = true;
            }
            else
            {
                metadata = (Metadata)root;
            }
            schemaUpdated |= ReloadSchema();
            if (schemaUpdated) 
            {
                CommitTransaction();
            }
        }
        
        bool ReloadSchema()     
        {
            bool schemaUpdated = false;
            metadata = (Metadata)storage.Root;
            tables = new Hashtable();
            IDictionaryEnumerator e = metadata.metaclasses.GetDictionaryEnumerator();
            while (e.MoveNext())
            {
                Type type = ClassDescriptor.lookup(storage, (string)e.Key);
                Table table = (Table)e.Value;
                table.setClass(type);
                tables[type] = table;
                schemaUpdated |= addIndices(table, type);
            }                
            return schemaUpdated;
        }

        /// <summary> 
        /// Constructor of single threaded database. This method initialize database if it not initialized yet.
        /// </summary>
        /// <param name="storage">opened storage. Storage should be either empty (non-initialized, either
        /// previously initialized by the this method. It is not possible to open storage with 
        /// root object other than table index created by this constructor.
        /// </param>
        public Database(Storage storage) 
        : this(storage, false)
        {
        }

        /// <summary>
        /// Tells whether or not this database is opened in multithreaded mode
        /// </summary>
        public bool IsMultithreaded
        {
            get { return multithreaded; }
        }

        /// <summary>
        /// Enable or disable automatic creation of indices. 
        /// If this feature is enabled, Perst will try to create new index each time when it needs it during
        /// query execution
        /// </summary>
        ///
        public bool EnableAutoIndices
        { 
            set
            {
                autoIndices = value;
            }
            get
            {
                return autoIndices;
            }
        }

        /// <summary>
        /// Begin transaction
        /// </summary>
        public void BeginTransaction() 
        { 
            if (multithreaded) 
            { 
                storage.BeginSerializableTransaction();
            }
        }

        /// <summary>
        /// Commit transaction
        /// </summary>
        public void CommitTransaction() 
        { 
            if (multithreaded) 
            { 
                storage.CommitSerializableTransaction();
            }
            else  
            { 
                storage.Commit();
            }
        }

        /// <summary>
        /// Rollback transaction
        /// </summary>
        public void RollbackTransaction() 
        { 
            if (multithreaded) 
            { 
                storage.RollbackSerializableTransaction();
            } 
            else 
            { 
                storage.Rollback();
            }
            ReloadSchema();
        }

        private void CheckTransaction() 
        {
            if (!storage.IsInsideThreadTransaction) 
            { 
                throw new StorageError(StorageError.ErrorCode.NOT_IN_TRANSACTION);
            }
        }

        /// <summary>
        /// Create table for the specified class.
        /// This function does nothing if table for such class already exists.
        /// Since version 2.72 of Perst.Net it is not necessary to create table and index 
        /// descriptors explicitly: them are automatically create when object is inserted in the 
        /// database first time (to mark fields for which indices should be created, 
        /// use Indexable attribute)
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <returns> <b>true</b> if table is created, <b>false</b> if table 
        /// alreay exists
        /// </returns>
        public bool CreateTable(Type table) 
        { 
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.ExclusiveLock();
            }
            if (!tables.ContainsKey(table)) 
            { 
                Table t = new Table();
                t.extent = storage.CreateSet();
                t.indices = storage.CreateLink();
                t.indicesMap = new Hashtable();
                t.setClass(table);
                tables[table] = t;
                metadata.metaclasses[table.FullName] = t;
                addIndices(t, table);
                return true;
            }
            return false;
        }
               
        private bool addIndices(Table table, Type type) 
        {
            bool schemaUpdated = false;
#if WINRT_NET_FRAMEWORK
            foreach (FieldInfo fi in type.GetRuntimeFields())
#else
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
#endif
            {
                foreach (IndexableAttribute idx in fi.GetCustomAttributes(typeof(IndexableAttribute), false))
                {
                    IndexKind kind = IndexKind.Default;
                    if (idx.Unique) kind |= IndexKind.Unique;
                    if (idx.CaseInsensitive) kind |= IndexKind.CaseInsensitive;
                    if (idx.Thick) kind |= IndexKind.Thick;
                    if (idx.RandomAccess) kind |= IndexKind.RandomAccess;
                    if (idx.Regex) kind |= IndexKind.Regex;
                    schemaUpdated |= CreateIndex(table, type, fi.Name, kind);
                    if (idx.Autoincrement) 
                    { 
                        if (table.autoincrementIndex != null) 
                        { 
                            throw new InvalidOperationException("Table can have only one autoincrement field");
                        }
                        table.autoincrementIndex = (FieldIndex)table.indicesMap[fi.Name];
                    }                    
                    break;
                }
            }
#if WINRT_NET_FRAMEWORK
            foreach (PropertyInfo pi in type.GetRuntimeProperties())
#else
            foreach (PropertyInfo pi in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
#endif
            {
                foreach (IndexableAttribute idx in pi.GetCustomAttributes(typeof(IndexableAttribute), false))
                {
                    IndexKind kind = IndexKind.Default;
                    if (idx.Unique) kind |= IndexKind.Unique;
                    if (idx.CaseInsensitive) kind |= IndexKind.CaseInsensitive;
                    if (idx.Thick) kind |= IndexKind.Thick;
                    if (idx.RandomAccess) kind |= IndexKind.RandomAccess;
                    if (idx.Regex) kind |= IndexKind.Regex;
                    schemaUpdated |= CreateIndex(table, type, pi.Name, kind);
                    break;
                }
            }
            return schemaUpdated;
        }

        /// <summary>
        /// Drop table associated with this class. Do nothing if there is no such table in the database.
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <returns> <b>true</b> if table is deleted, <b>false</b> if table 
        /// is not found
        /// </returns>
        public bool DropTable(Type table) 
        { 
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.ExclusiveLock();
            }
            Table t = (Table)tables[table];
            if (t != null)
            { 
                bool savePolicy = storage.SetRecursiveLoading(table, false);
                foreach (object obj in t.extent) 
                { 
                    if (obj is FullTextSearchable || t.fullTextIndexableFields.Count != 0) 
                    { 
                        metadata.fullTextIndex.Delete(obj);
                    } 
#if WINRT_NET_FRAMEWORK
                    for (Type baseType = table; (baseType = baseType.GetTypeInfo().BaseType) != null;) 
#else
                    for (Type baseType = table; (baseType = baseType.BaseType) != null;) 
#endif
                    {
                        Table baseTable = (Table)tables[baseType];
                        if (baseTable != null && baseTable.extent.Contains(obj)) 
                        { 
                            if (multithreaded) 
                            { 
                                baseTable.extent.ExclusiveLock();
                            }
                            baseTable.extent.Remove(obj);
                            foreach (FieldIndex index in baseTable.indicesMap.Values) 
                            {
                                index.Remove(obj);
                            }
                        }
                    }    
                    storage.Deallocate(obj);
                }
                tables.Remove(table);
                metadata.metaclasses.RemoveKey(table.FullName);
                t.Deallocate();
                storage.SetRecursiveLoading(table, savePolicy);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Add new record to the table. Record is inserted in table corresponding to the class of the object.
        /// Record will be automatically added to all indices existed for this table.
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...
        /// </summary>
        /// <param name="record">object to be inserted in the table</param>
        /// <returns> <b>true</b> if record was successfully added to the table, <b>false</b>
        /// if there is already such record (object with the same ID) in the table
        /// </returns>
        /// <exception cref="StorageError"> StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// record class
        /// </exception>
        public bool AddRecord(object record) 
        { 
            return AddRecord(record.GetType(), record);        
        }

        private Table locateTable(Type cls, bool exclusive)
        { 
            return locateTable(cls, exclusive, true);
        }
 
        private Table locateTable(Type cls, bool exclusive, bool shouldExist) 
        { 
            Table table = null;
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.SharedLock();
            }
            if (searchBaseClasses) 
            {
#if WINRT_NET_FRAMEWORK
                for (Type c = cls; c != null && (table = (Table)tables[c]) == null; c = c.GetTypeInfo().BaseType);
#else
                for (Type c = cls; c != null && (table = (Table)tables[c]) == null; c = c.BaseType);
#endif
            }
            else
            {
                table = (Table)tables[cls];
            }
            if (table == null) 
            { 
                if (shouldExist) 
                { 
                    throw new StorageError(StorageError.ErrorCode.CLASS_NOT_FOUND, cls.FullName);
                }
                return null;
            }
            if (multithreaded) 
            { 
                if (exclusive)
                {
                    table.extent.ExclusiveLock();
                }
                else 
                {
                    table.extent.SharedLock();
                }            
             }
            return table;
        }

        private void registerTable(Type type) 
        { 
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.SharedLock();
            }
            if (autoRegisterTables) 
            { 
                bool exclusiveLockSet = false;     
#if WINRT_NET_FRAMEWORK
                for (Type c = type; c != typeof(object); c = c.GetTypeInfo().BaseType) 
#else
                for (Type c = type; c != typeof(object); c = c.BaseType) 
#endif
                { 
                    Table t = (Table)tables[c];
                    if (t == null && (globalClassExtent || c != typeof(Persistent))) 
                    { 
                        if (multithreaded && !exclusiveLockSet) 
                        { 
                            metadata.Unlock(); // try to avoid deadlock caused by concurrent insertion of objects
                            exclusiveLockSet = true;
                        }
                        CreateTable(c);
                    }
                }
            }
        }
        

        /// <summary> 
        /// Locate all documents containing words started with specified prefix
        /// </summary> 
        /// <param name="prefix">word prefix
        /// </param>
        /// <param name="maxResults">maximal amount of selected documents
        /// </param>
        /// <param name="timeLimit">limit for query execution time
        /// </param>
        /// <param name="sort"> whether it is necessary to sort result by rank
        /// </param>
        /// <returns> result of query execution ordered by rank or null in case of empty or incorrect query
        /// </returns>
        public FullTextSearchResult SearchPrefix(string prefix, int maxResults, int timeLimit, bool sort)
        {
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.fullTextIndex.SharedLock();
            }
            return metadata.fullTextIndex.SearchPrefix(prefix, maxResults, timeLimit, sort);
        }
        
        /// <summary> 
        /// Get enumerator through full text index keywords started with specified prefix
        /// </summary> 
        /// <param name="prefix">keyword prefix (user empty string to get list of all keywords)</param>
        /// <returns>enumerator through list of all keywords with specified prefix</returns>
#if NET_FRAMEWORK_20
        public System.Collections.Generic.IEnumerable<Keyword> GetKeywords(string prefix)
#else
        public System.Collections.IEnumerable GetKeywords(string prefix)
#endif
        {
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.fullTextIndex.SharedLock();
            }
            return metadata.fullTextIndex.GetKeywords(prefix);
        }


        /// <summary> Parse and execute full text search query</summary>
        /// <param name="query">text of the query
        /// </param>
        /// <param name="language">language if the query
        /// </param>
        /// <param name="maxResults">maximal amount of selected documents
        /// </param>
        /// <param name="timeLimit">limit for query execution time
        /// </param>
        /// <returns> result of query execution ordered by rank or null in case of empty or incorrect query
        /// </returns>
        public FullTextSearchResult Search(string query, string language, int maxResults, int timeLimit)
        {
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.fullTextIndex.SharedLock();
            }
            return metadata.fullTextIndex.Search(query, language, maxResults, timeLimit);
        }
        
        /// <summary> Execute full text search query</summary>
        /// <param name="query">prepared query
        /// </param>
        /// <param name="maxResults">maximal amount of selected documents
        /// </param>
        /// <param name="timeLimit">limit for query execution time
        /// </param>
        /// <returns> result of query execution ordered by rank or null in case of empty or incorrect query
        /// </returns>
        public FullTextSearchResult Search(FullTextQuery query, int maxResults, int timeLimit)
        {
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.fullTextIndex.SharedLock();
            }
            return metadata.fullTextIndex.Search(query, maxResults, timeLimit);
        }

        /// <summary>
        /// Update full text index for modified record
        /// </summary>
        /// <param name="record">updated record</param>
        ///
        public void UpdateFullTextIndex(object record) 
        { 
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.fullTextIndex.ExclusiveLock();
            }
            if (record is FullTextSearchable)
            {
                metadata.fullTextIndex.Add((FullTextSearchable)record);
            }
            else
            {
                StringBuilder fullText = new StringBuilder();
                Table t = locateTable(record.GetType(), true);
                foreach (FieldInfo f in t.fullTextIndexableFields)
                {
                    object text = f.GetValue(record);
                    if (text != null)
                    {
                        fullText.Append(' ');
                        fullText.Append(text.ToString());
                    }
                }
                metadata.fullTextIndex.Add(record, new System.IO.StringReader(fullText.ToString()), null);
            }
        }

        /// <summary>
        /// Add new record to the specified table. Record is inserted in table corresponding to the specified class.
        /// Record will be automatically added to all indices existed for this table.
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="record">object to be inserted in the table
        /// </param>
        /// <returns> <b>true</b> if record was successfully added to the table, <b>false</b>
        /// if there is already such record (object with the same ID) in the table
        /// </returns>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// record class
        /// </exception>
        public bool AddRecord(Type table, object record) 
        { 
            bool added = false;
            bool found = false;
            registerTable(table);
            ArrayList wasInsertedIn = new ArrayList();
#if WINRT_NET_FRAMEWORK
            for (Type c = table; c != null; c = c.GetTypeInfo().BaseType) 
#else
            for (Type c = table; c != null; c = c.BaseType) 
#endif
            { 
                Table t = (Table)tables[c];
                if (t != null) 
                { 
                    found = true;
                    if (multithreaded) 
                    { 
                        t.extent.ExclusiveLock();
                    }
                    if (!t.extent.Contains(record)) 
                    { 
                        wasInsertedIn.Add(t.extent);
                        t.extent.Add(record);
                        foreach (FieldIndex index in t.indicesMap.Values) 
                        {
                            if (index == t.autoincrementIndex) 
                            { 
                                index.Append(record);
                                storage.Modify(record);
                                wasInsertedIn.Add(index);
                            } 
                            else if (index.Put(record))
                            {
                                wasInsertedIn.Add(index);
                            } 
                            else if (index.IsUnique)
                            { 
                                foreach (object idx in wasInsertedIn)
                                {
                                    if (idx is ISet) 
                                    {
                                        ((ISet)idx).Remove(record);
                                    } 
                                    else 
                                    { 
                                         ((FieldIndex)idx).Remove(record);
                                    }
                                }
                                return false;
                            }                                         
                        }
                        added = true;
                    }
                }
            }
            if (!found) 
            { 
                throw new StorageError(StorageError.ErrorCode.CLASS_NOT_FOUND, table.FullName);
            } 
            if (record is FullTextSearchable) 
            { 
                if (multithreaded) 
                { 
                    metadata.fullTextIndex.ExclusiveLock();
                }
                metadata.fullTextIndex.Add((FullTextSearchable)record);
            } else {
                StringBuilder fullText = new StringBuilder();
                Table t = locateTable(table, true);
                foreach (FieldInfo f in t.fullTextIndexableFields)   
                { 
                    object text = f.GetValue(record);
                    if (text != null) 
                    { 
                        fullText.Append(' ');
                        fullText.Append(text.ToString());
                    }
                }
                if (fullText.Length != 0) 
                {
                    if (multithreaded) 
                    { 
                        metadata.fullTextIndex.ExclusiveLock();
                    }                
                    metadata.fullTextIndex.Add(record, new StringReader(fullText.ToString()), null);
                }
            }
            return added;
        }

        
        /// <summary> 
        /// Delete record from the table. Record is removed from the table corresponding to the class 
        /// of the object. Record will be automatically added to all indices existed for this table.
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...
        /// Object represented the record will be also deleted from the storage.
        /// </summary>
        /// <param name="record">object to be deleted from the table
        /// </param>
        /// <returns> <b>true</b> if record was successfully deleted from the table, <b>false</b>
        /// if there is not such record (object with the same ID) in the table
        /// </returns>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// record class
        /// </exception>
        public bool DeleteRecord(object record) 
        { 
            return DeleteRecord(record.GetType(), record);
        }

        /// <summary> 
        /// Delete record from the specified table. Record is removed from the table corresponding to the 
        /// specified class. Record will be automatically added to all indices existed for this table.
        /// Object represented the record will be also deleted from the storage.
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="record">object to be deleted from the table
        /// </param>
        /// <returns> <b>true</b> if record was successfully deleted from the table, <b>false</b>
        /// if there is not such record (object with the same ID) in the table
        /// </returns>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// specified class
        /// </exception>
        public bool DeleteRecord(Type table, object record) 
        { 
            bool removed = false;
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.SharedLock();
            }
            bool fullTextIndexed = false;
#if WINRT_NET_FRAMEWORK
            for (Type c = table; c != null; c = c.GetTypeInfo().BaseType) 
#else
            for (Type c = table; c != null; c = c.BaseType) 
#endif
            {
                Table t = (Table)tables[c];
                if (t != null) 
                { 
                    if (t.extent.Contains(record)) 
                    { 
                        if (multithreaded) 
                        { 
                            t.extent.ExclusiveLock();
                        }
                        t.extent.Remove(record);
                        foreach (FieldIndex index in t.indicesMap.Values) 
                        {
                            index.Remove(record);
                        }
                        if (t.fullTextIndexableFields.Count != 0) 
                        { 
                            fullTextIndexed = true;
                        }
                        removed = true;
                    }
                }
            }
            if (removed) 
            {
                if (record is FullTextSearchable || fullTextIndexed) 
                {
                    if (multithreaded) 
                    { 
                        metadata.fullTextIndex.ExclusiveLock();
                    }
                    metadata.fullTextIndex.Delete(record);
                }
                storage.Deallocate(record);
            }
            return removed;
        }
    
        /// <summary>
        /// Add new index to the table. If such index already exists this method does nothing.
        /// Since version 2.72 of Perst.Net it is not necessary to create table and index 
        /// descriptors explicitly: them are automatically create when object is inserted in the 
        /// database first time (to mark fields for which indices should be created, 
        /// use Indexable attribute)
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="key">field of the class to be indexed
        /// </param>
        /// <param name="unique">if index is unique or not
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if index is created, <b>false</b> if index
        /// already exists
        /// </returns>
        public bool CreateIndex(Type table, string key, bool unique) 
        { 
            return CreateIndex(locateTable(table, true), table, key, unique ? IndexKind.Unique : IndexKind.Default);
        }

        /// <summary>
        /// Add new index to the table. If such index already exists this method does nothing.
        /// Since version 2.72 of Perst.Net it is not necessary to create table and index 
        /// descriptors explicitly: them are automatically create when object is inserted in the 
        /// database first time (to mark fields for which indices should be created, 
        /// use Indexable attribute)
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="key">field of the class to be indexed
        /// </param>
        /// <param name="unique">if index is unique or not
        /// </param>
        /// <param name="caseInsensitive">if string index is case insensitive
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if index is created, <b>false</b> if index
        /// already exists
        /// </returns>
        public bool CreateIndex(Type table, string key, bool unique, bool caseInsensitive) 
        {
            IndexKind kind = IndexKind.Default;
            if (unique) kind |= IndexKind.Unique;
            if (caseInsensitive) kind |= IndexKind.CaseInsensitive;
            return CreateIndex(locateTable(table, true), table, key, kind);
        }

        /// <summary>
        /// Add new index to the table. If such index already exists this method does nothing.
        /// Since version 2.72 of Perst.Net it is not necessary to create table and index 
        /// descriptors explicitly: them are automatically create when object is inserted in the 
        /// database first time (to mark fields for which indices should be created, 
        /// use Indexable attribute)
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="key">field of the class to be indexed
        /// </param>
        /// <param name="unique">if index is unique or not
        /// </param>
        /// <param name="caseInsensitive">if string index is case insensitive
        /// </param>
        /// <param name="thick">if index contains a lot of duplicates
        /// </param>
        /// <param name="randomAccess">if index supports efficent access to elements by position
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if index is created, <b>false</b> if index
        /// already exists
        /// </returns>
        public bool CreateIndex(Type table, string key, bool unique, bool caseInsensitive, bool thick, bool randomAccess) 
        {
            IndexKind kind = IndexKind.Default;
            if (unique) kind |= IndexKind.Unique;
            if (caseInsensitive) kind |= IndexKind.CaseInsensitive;
            if (thick) kind |= IndexKind.Thick;
            if (randomAccess) kind |= IndexKind.RandomAccess;
            return CreateIndex(locateTable(table, true), table, key, kind);
        }

        /// <summary>
        /// Add new index to the table. If such index already exists this method does nothing.
        /// Since version 2.72 of Perst.Net it is not necessary to create table and index 
        /// descriptors explicitly: them are automatically create when object is inserted in the 
        /// database first time (to mark fields for which indices should be created, 
        /// use Indexable attribute)
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="key">field of the class to be indexed
        /// </param>
        /// <param name="kind">bitmask of index kinds
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if index is created, <b>false</b> if index
        /// already exists
        /// </returns>
        public bool CreateIndex(Type table, string key, IndexKind kind)
        {
            return CreateIndex(locateTable(table, true), table, key, kind);
        }

        private bool CreateIndex(Table t, Type type, string key, IndexKind kind) 
        {
            bool unique = (kind & IndexKind.Unique) != 0;
            bool caseInsensitive = (kind & IndexKind.CaseInsensitive) != 0;

            if (!t.indicesMap.ContainsKey(key)) 
            { 
                FieldIndex index = (kind & IndexKind.Regex) != 0
                    ? storage.CreateRegexIndex(type, key, caseInsensitive, 3)
                    : (kind & IndexKind.RandomAccess) != 0
                        ? storage.CreateRandomAccessFieldIndex(type, key, unique, caseInsensitive)
                        : storage.CreateFieldIndex(type, key, unique, caseInsensitive, (kind & IndexKind.Thick) != 0);
                t.indicesMap[key] = index;
                t.indices.Add(index);
                foreach (object o in t.extent)
                {
                    if (!index.Put(o) && index.IsUnique)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Drop index for the specified table and key.
        /// Does nothing if there is no such index.
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="key">field of the class to be indexed
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if index is deleted, <b>false</b> if index
        /// is not found
        /// </returns>
        public bool DropIndex(Type table, string key) 
        { 
            Table t = locateTable(table, true);
            FieldIndex index = (FieldIndex)t.indicesMap[key];
            if (index != null)
            { 
                t.indicesMap.Remove(key);
                t.indices.Remove(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get index for the specified field of the class
        /// </summary>
        /// <param name="table">class where index is located</param>
        /// <param name="key">field of the class</param>
        /// <returns>Index for this field or null if index doesn't exist</returns>
        ///
        public GenericIndex GetIndex(Type table, string key)
        {
#if WINRT_NET_FRAMEWORK
            for (Type c = table; c != null; c = c.GetTypeInfo().BaseType) 
#else
            for (Type c = table; c != null; c = c.BaseType) 
#endif
            { 
                Table t = locateTable(c, false, false);
                if (t != null) 
                { 
                    lock (t.indicesMap) 
                    { 
                        GenericIndex index = (GenericIndex)t.indicesMap[key];
                        if (index != null) 
                        {
                            return index;
                        }
#if WINRT_NET_FRAMEWORK
                        if (autoIndices && key.IndexOf('.') < 0 
                            && (c.GetTypeInfo().GetDeclaredField(key) != null
                                || c.GetTypeInfo().GetDeclaredProperty(key) != null))
#else
                        if (autoIndices && key.IndexOf('.') < 0 
                            && (c.GetField(key, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) != null
                                || c.GetProperty(key, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly) != null))
#endif
                        { 
                             StorageListener listener = storage.Listener;
                             if (listener != null) 
                             { 
                                 listener.IndexCreated(c, key);
                             }
                             CreateIndex(t, c, key, IndexKind.Default); 
                             return (GenericIndex)t.indicesMap[key];
                        }
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// Get indices for the specified table
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <returns>hash table of indices
        /// </returns>
        public Hashtable GetIndices(Type table)
        {
             Table t = locateTable(table, true, false);
             return t != null ? t.indicesMap : new Hashtable();
        }

        /// <summary>
        /// <p>
        /// Exclude record from specified index. This method is needed to perform update of indexed
        /// field (key). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected. After updating the field, record should be reinserted in these indices
        /// using includeInIndex method.</p><p>
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...</p><p>
        /// This method does nothing if there is no index for the specified field.
        /// </p></summary>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <param name="key">name of the indexed field
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// record class
        /// </exception>
        /// <returns> <b>true</b> if record is excluded from index, <b>false</b> if 
        /// there is no such index
        /// </returns>
        public bool ExcludeFromIndex(object record, string key) 
        {
            return ExcludeFromIndex(record.GetType(), record, key);
        }



        /// <summary>
        /// <p>
        /// Exclude record from all indices. This method is needed to perform update of indexed
        /// field (key). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected. After updating the field, record should be reinserted in these indices
        /// using IncludeInIndex method. If your know which fields will be updated and which indices
        /// exist for this table, it is more efficient to use ExcludeFromIndex method to exclude
        /// object only from affected indices.</p><p>
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...</p><p>
        /// This method does nothing if there is no index for the specified field.
        /// </p></summary>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// record class
        /// </exception>
        public void ExcludeFromAllIndices(object record) 
        {
            ExcludeFromAllIndices(record.GetType(), record);
        }

        /// <summary>
        /// <p>
        /// Exclude record from specified index. This method is needed to perform update of indexed
        /// field (key). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected. After updating the field, record should be reinserted in these indices
        /// using includeInIndex method.</p><p>
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...</p><p>
        /// This method does nothing if there is no index for the specified field.
        /// </p></summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <param name="key">name of the indexed field
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if record is excluded from index, <b>false</b> if 
        /// there is no such index
        /// </returns>
        public bool ExcludeFromIndex(Type table, object record, string key) 
        {
            Table t = locateTable(table, true);
            FieldIndex index = (FieldIndex)t.indicesMap[key];
            if (index != null) 
            { 
                index.Remove(record);
                return true;
            }
            return false;
        }

        /// <summary>
        /// <p>
        /// Exclude record from all indices. This method is needed to perform update of indexed
        /// field (key). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected. After updating the field, record should be reinserted in these indices
        /// using IncludeInIndex method. If your know which fields will be updated and which indices
        /// exist for this table, it is more efficient to use ExcludeFromIndex method to exclude
        /// object only from affected indices.</p><p>
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...</p><p>
        /// This method does nothing if there is no index for the specified field.
        /// </p></summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// record class
        /// </exception>
        public void ExcludeFromAllIndices(Type table, object record) 
        {
            if (multithreaded) 
            { 
                CheckTransaction();
                metadata.SharedLock();
            }
            bool fullTextIndexed = false;
#if WINRT_NET_FRAMEWORK
            for (Type c = table; c != null; c = c.GetTypeInfo().BaseType) 
#else
            for (Type c = table; c != null; c = c.BaseType) 
#endif
            { 
                Table t = (Table)tables[c];
                if (t != null) 
                { 
                    if (t.extent.Contains(record)) 
                    { 
                        if (multithreaded) 
                        { 
                            t.extent.ExclusiveLock();
                        }
                        foreach (FieldIndex index in t.indicesMap.Values) 
                        {
                            index.Remove(record);
                        }
                        if (t.fullTextIndexableFields.Count != 0) 
                        { 
                            fullTextIndexed = true;
                        }
                    }
                }
            }
            if (record is FullTextSearchable || fullTextIndexed) 
            {
                if (multithreaded) 
                { 
                    metadata.fullTextIndex.ExclusiveLock();
                }
                metadata.fullTextIndex.Delete(record);
            }
        }

        /// <summary>
        /// <p>
        /// Include record in the specified index. This method is needed to perform update of indexed
        /// field (key). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected using excludeFromIndex method. After updating the field, record should be 
        /// reinserted in these indices using this method.</p><p>
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...</p><p>
        /// This method does nothing if there is no index for the specified field.
        /// </p></summary>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <param name="key">name of the indexed field
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if record is included in index, <b>false</b> if 
        /// there is no such index or unique constraint is violated
        /// </returns>
        public bool IncludeInIndex(object record, string key) 
        { 
            return IncludeInIndex(record.GetType(), record, key);
        }

        /// <summary>
        /// <p>
        /// Include record in all indices. This method is needed to perform update of indexed
        /// fields (keys). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected using excludeFromIndices method. After updating the field, record should be 
        /// reinserted in these indices using this method. If your know which fields will be updated and which indices
        /// exist for this table, it is more efficient to use excludeFromIndex/includeInIndex methods to touch
        /// only affected indices.
        /// </p></summary>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if record is included in indices, <b>false</b> if unique unique constraint is violated
        /// </returns>
        public bool IncludeInAllIndices(object record) 
        { 
            return IncludeInAllIndices(record.GetType(), record);
        }

        /// <summary>
        /// <p>
        /// Include record in the specified index. This method is needed to perform update of indexed
        /// field (key). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected using excludeFromIndex method. After updating the field, record should be 
        /// reinserted in these indices using this method.</p><p>
        /// If there is not table associated with class of this object, then 
        /// database will search for table associated with superclass and so on...</p><p>
        /// This method does nothing if there is no index for the specified field.
        /// </p></summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <param name="key">name of the indexed field
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if record is included in index, <b>false</b> if 
        /// there is no such index or unique constraint is violated
        /// </returns>
        public bool IncludeInIndex(Type table, object record, string key) 
        { 
            Table t = locateTable(table, true);
            FieldIndex index = (FieldIndex)t.indicesMap[key];
            if (index != null) 
            { 
                return index.Put(record) || !index.IsUnique;
            }
            return false;
        }

        /// <summary>
        /// <p>
        /// Include record in all indices. This method is needed to perform update of indexed
        /// fields (keys). Before updating the record, it is necessary to exclude it from indices
        /// which keys are affected using excludeFromIndices method. After updating the field, record should be 
        /// reinserted in these indices using this method. If your know which fields will be updated and which indices
        /// exist for this table, it is more efficient to use excludeFromIndex/includeInIndex methods to touch
        /// only affected indices.
        /// </p></summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="record">object to be excluded from the specified index
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns> <b>true</b> if record is included in indices, <b>false</b> if unique unique constraint is violated
        /// </returns>
        public bool IncludeInAllIndices(Type table, object record) 
        { 
            if (multithreaded) 
            { 
                CheckTransaction();
            }
            ArrayList wasInsertedIn = new ArrayList();
#if WINRT_NET_FRAMEWORK
            for (Type c = table; c != null; c = c.GetTypeInfo().BaseType) 
#else
            for (Type c = table; c != null; c = c.BaseType) 
#endif
            { 
                Table t = (Table)tables[c];
                if (t != null) 
                { 
                    if (multithreaded) 
                    { 
                        t.extent.ExclusiveLock();
                    }
                    foreach (FieldIndex index in t.indicesMap.Values) 
                    {
                        if (index.Put(record))
                        {
                            wasInsertedIn.Add(index);
                        } 
                        else if (index.IsUnique)
                        { 
                            foreach (object idx in wasInsertedIn)
                            {
                                if (idx is ISet) 
                                {
                                    ((ISet)idx).Remove(record);
                                } 
                                else 
                                { 
                                     ((FieldIndex)idx).Remove(record);
                                }
                            }
                            return false;
                        }                                         
                    }
                }
            }
            if (record is FullTextSearchable) 
            { 
                if (multithreaded) 
                { 
                    metadata.fullTextIndex.ExclusiveLock();
                }
                metadata.fullTextIndex.Add((FullTextSearchable)record);
            } else {
                StringBuilder fullText = new StringBuilder();
                Table t = locateTable(table, true);
                foreach (FieldInfo f in t.fullTextIndexableFields)   
                { 
                    object text = f.GetValue(record);
                    if (text != null) 
                    { 
                        fullText.Append(' ');
                        fullText.Append(text.ToString());
                    }
                }
                if (fullText.Length != 0) 
                {
                    if (multithreaded) 
                    { 
                        metadata.fullTextIndex.ExclusiveLock();
                    }                
                    metadata.fullTextIndex.Add(record, new StringReader(fullText.ToString()), null);
                }
            }
            return true;
        }

        /// <summary>
        /// This method can be used to update a key field. It is responsibility of programmer in Perst
        /// to maintain consistency of indices: before updating key field it is necessary to exclude 
        /// the object from the index and after assigning new value to the key field - reinsert it in the index.
        /// It can be done using excludeFromIndex/includeInIndex methods, but updateKey combines all this steps:
        /// exclude from index, update, mark object as been modified and reinsert in index.
        /// It is safe to call updateKey method for fields which are actually not used in any index - 
        /// in this case excludeFromIndex/includeInIndex do nothing.
        /// </summary>
        /// <param name="record">updated object</param>
        /// <param name="key">name of the indexed field</param>
        /// <param name="value">new value of indexed field</param>
        /// <exception cref="StorageError">StorageError(INDEXED_FIELD_NOT_FOUND) exception is thrown if 
        /// specified field is not found</exception>
        ///
        public void UpdateKey(object record, string key, object value) 
        {
            UpdateKey(record.GetType(), record, key, value);
        }
                
        /// <summary>
        /// This method can be used to update a key field. It is responsibility of programmer in Perst
        /// to maintain consistency of indices: before updating key field it is necessary to exclude 
        /// the object from the index and after assigning new value to the key field - reinsert it in the index.
        /// It can be done using excludeFromIndex/includeInIndex methods, but updateKey combines all this steps:
        /// exclude from index, update, mark object as been modified and reinsert in index.
        /// It is safe to call updateKey method for fields which are actually not used in any index - 
        /// in this case excludeFromIndex/includeInIndex do nothing.
        /// </summary>
        /// <param name="record">updated object</param>
        /// <param name="key">name of the indexed field</param>
        /// <param name="value">new value of indexed field</param>
        /// <exception cref="StorageError">StorageError(INDEXED_FIELD_NOT_FOUND) exception is thrown if 
        /// specified field is not found</exception>
        ///
        public void UpdateKey(Type table, object record, string key, object value) 
        {
            ExcludeFromIndex(table, record, key);
            FieldInfo f = QueryImpl.lookupField(table, key);
            if (f == null) 
            { 
                throw new StorageError(StorageError.ErrorCode.INDEXED_FIELD_NOT_FOUND, table.Name);
            } 
            f.SetValue(record, value);
            storage.Modify(record);
            IncludeInIndex(table, record, key);
        }

        /// <summary>
        /// Select record from specified table
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="predicate">search predicate
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <exception cref="CompileError"> exception is thrown if predicate is not valid JSQL exception
        /// </exception>
        /// <exception cref="JSQLRuntimeException"> exception is thrown if there is runtime error during query execution
        /// </exception>
        public IEnumerable Select(Type table, string predicate) 
        { 
            return Select(table, predicate, false);
        }

        /// <summary>
        /// Select record from specified table
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="predicate">search predicate
        /// </param>
        /// <param name="forUpdate"><b>true</b> if records are selected for update - 
        /// in this case exclusive lock is set for the table to avoid deadlock.
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <exception cref="CompileError"> exception is thrown if predicate is not valid JSQL exception
        /// </exception>
        /// <exception cref="JSQLRuntimeException"> exception is thrown if there is runtime error during query execution
        /// </exception>
        public IEnumerable Select(Type table, string predicate, bool forUpdate) 
        { 
            Query q = Prepare(table, predicate, forUpdate);
            return q.Execute(GetRecords(table));
        }

#if NET_FRAMEWORK_35
        /// <summary>
        /// Select record from specified table
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="predicate">search predicate
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <exception cref="CompileError"> exception is thrown if predicate is not valid JSQL exception
        /// </exception>
        /// <exception cref="JSQLRuntimeException"> exception is thrown if there is runtime error during query execution
        /// </exception>
        public IEnumerable<T> Select<T>(string predicate) where T:class
        { 
            return Select<T>(predicate, false);
        }

        /// <summary>
        /// Select record from specified table
        /// </summary>
        /// <param name="predicate">search predicate
        /// </param>
        /// <param name="forUpdate"><b>true</b> if records are selected for update - 
        /// in this case exclusive lock is set for the table to avoid deadlock.
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <exception cref="CompileError"> exception is thrown if predicate is not valid JSQL exception
        /// </exception>
        /// <exception cref="JSQLRuntimeException"> exception is thrown if there is runtime error during query execution
        /// </exception>
        public IEnumerable<T> Select<T>(string predicate, bool forUpdate) where T:class
        { 
            Type table = typeof(T);      
            Query q = Prepare(table, predicate, forUpdate);
            return q.Execute<T>(GetRecords(table));
        }

        private Key ConstructKey(object value, FieldIndex index, bool inclusive)
        {
            return new Key(value, index.KeyType, inclusive);
        }

        JoinIterator Join(MemberExpression deref, JoinIterator parent, bool forUpdate)
        {
            if (deref.Expression.NodeType != ExpressionType.MemberAccess) 
            { 
                return null;
            }
            deref = (MemberExpression)deref.Expression;
            Table table = locateTable(deref.Member.DeclaringType, forUpdate, false);
            if (table == null) 
            {
                return null;
            }
            GenericIndex joinIndex = (GenericIndex)table.indicesMap[deref.Member.Name];
            if (joinIndex == null) 
            { 
                return null;
            }
            parent.joinIndex = joinIndex;
            if (deref.Expression.NodeType == ExpressionType.Parameter) 
            { 
                return parent;
            }
            JoinIterator child = new JoinIterator();
            child.iterator = parent;
            return Join(deref, child, forUpdate);
        }       

        static bool IsLiteral(Expression expr, out object value)
        {
            if (expr.NodeType == ExpressionType.Constant)
            {
                value = ((ConstantExpression)expr).Value;
                return true;
            }
           else if (expr.NodeType == ExpressionType.MemberAccess
                && IsLiteral(((MemberExpression)expr).Expression, out value))
           {
                MemberInfo member = ((MemberExpression)expr).Member;
#if SILVERLIGHT
                Expression<Func<object>> g = (Expression<Func<object>>)Expression.Lambda(Expression.Convert(expr, typeof(object)));
                try
                {
                    value = g.Compile()();
                    return true;
                } 
                catch (Exception) 
                {
                    return false;
                }  
#else
                if (member is FieldInfo)
                {
                    value = ((FieldInfo)member).GetValue(value);
                    return true;
                } 
                else if (member is PropertyInfo)
                {
                    value = ((PropertyInfo)member).GetValue(value, null);
                    return true;
                } 
#endif
            } 
            value = null;
            return false;
        }

        static bool EqualExpressions(Expression e1, Expression e2)
        {
            if (e1 == e2)
            { 
                return true;
            }
            if (e1 == null || e2 == null || e1.NodeType != e2.NodeType)
            {
                return false;
            }
            switch (e1.NodeType)
            {
                case ExpressionType.Convert:
                    return e1.Type.Equals(e2.Type) && EqualExpressions(((UnaryExpression)e1).Operand, ((UnaryExpression)e2).Operand);
                case ExpressionType.MemberAccess:
                    return ((MemberExpression)e1).Member.Equals(((MemberExpression)e2).Member) && EqualExpressions(((MemberExpression)e1).Expression, ((MemberExpression)e2).Expression); 
                default:
                    return false;      
            }
        }

        static int GetIndirectionLevel(Expression expr)
        {
            return (expr.NodeType == ExpressionType.MemberAccess) 
                ? GetIndirectionLevel(((MemberExpression)expr).Expression) + 1
                : 0;
        }

        int CalculateCost(Expression expr) 
        {
            SqlOptimizerParameters p = storage.SqlOptimizerParams;
            switch (expr.NodeType) 
            {
            case ExpressionType.AndAlso:
                return p.andCost + Math.Min(CalculateCost(((BinaryExpression)expr).Left), CalculateCost(((BinaryExpression)expr).Right));
            case ExpressionType.OrElse:
                return p.orCost + CalculateCost(((BinaryExpression)expr).Left) + CalculateCost(((BinaryExpression)expr).Right);            
            case ExpressionType.Equal:
            {
                Type type = ((BinaryExpression)expr).Left.Type; 
                return (type.Equals(typeof(bool)) ? p.eqBoolCost 
                        : type.Equals(typeof(string)) ? p.eqStringCost
                        : type.Equals(typeof(double)) || type.Equals(typeof(float)) ? p.eqRealCost
                        : p.eqCost)
                       + Math.Max(GetIndirectionLevel(((BinaryExpression)expr).Left),
                                  GetIndirectionLevel(((BinaryExpression)expr).Right))*p.indirectionCost;
            }
            case ExpressionType.GreaterThan:
            case ExpressionType.LessThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThanOrEqual:
                return  p.openIntervalCost + Math.Max(GetIndirectionLevel(((BinaryExpression)expr).Left),
                                                      GetIndirectionLevel(((BinaryExpression)expr).Right))*p.indirectionCost;
            case ExpressionType.Call:
            {
                MethodCallExpression call = (MethodCallExpression)expr;
                object value;
                if (call.Method.DeclaringType == typeof(string) && call.Method.Name == "StartsWith")
                {
                    return p.patternMatchCost + GetIndirectionLevel(call.Object)*p.indirectionCost;
                }
                else if (call.Method.DeclaringType != typeof(string) && call.Method.Name == "Contains" 
                    && ((call.Arguments.Count == 1 && IsLiteral(call.Object, out value)) ||
                        (call.Arguments.Count == 2 && null == call.Object && IsLiteral(call.Arguments[0], out value)))
                    && value is IEnumerable)
                {
                    return p.containsCost + GetIndirectionLevel(call.Object)*p.indirectionCost;
                }
                break;
            }
            case ExpressionType.Not:
                return p.eqBoolCost + GetIndirectionLevel(((UnaryExpression)expr).Operand)*p.indirectionCost;

            case ExpressionType.MemberAccess:
                return p.eqBoolCost + GetIndirectionLevel(expr)*p.indirectionCost;
            
            default:
                break;
            }
            return p.sequentialSearchCost;
        }    
            
        IEnumerable ApplyIndex(Expression expr, Table table, bool forUpdate)
        {
            object value;
            if (expr.NodeType == ExpressionType.Call)
            {
                MethodCallExpression call = (MethodCallExpression)expr;
                if (call.Method.DeclaringType == typeof(string) && (call.Method.Name == "StartsWith" || call.Method.Name == "Contains"))
                {
                    if (call.Object.NodeType == ExpressionType.MemberAccess)
                    {
                        JoinIterator lastJoinIterator = null;
                        JoinIterator firstJoinIterator = null;
                        MemberExpression deref = (MemberExpression)call.Object;
                        if (deref.Expression.NodeType != ExpressionType.Parameter)
                        {
                            lastJoinIterator = new JoinIterator();
                            firstJoinIterator = Join(deref, lastJoinIterator, forUpdate);
                            if (firstJoinIterator == null)
                            {
                                return null;
                            }
                            table = locateTable(deref.Member.DeclaringType, forUpdate, false);
                        }
                        string prefix;
                        Expression arg = call.Arguments[0];
                        if (arg.NodeType == ExpressionType.Constant)
                        {
                            prefix = (string)((ConstantExpression)arg).Value;
                        }
                        else if (arg.NodeType == ExpressionType.MemberAccess
                                 && ((MemberExpression)arg).Expression.NodeType == ExpressionType.Constant
                                 && ((MemberExpression)arg).Member is FieldInfo)
                        {
#if SILVERLIGHT
                            Expression<Func<object>> g = (Expression<Func<object>>)Expression.Lambda(Expression.Convert(arg, typeof(object)));
                            prefix = (string)g.Compile()();
#else
                            object target = ((ConstantExpression)((MemberExpression)arg).Expression).Value;
                            prefix = (string)((FieldInfo)((MemberExpression)arg).Member).GetValue(target);
#endif
                        }
                        else
                        {
                            return null;
                        }
                        string name = deref.Member.Name;
                        while (table != null)
                        {
                            FieldIndex index = (FieldIndex)table.indicesMap[name];
                            if (index != null)
                            {
                                IEnumerable enumerable;
                                if (call.Method.Name == "Contains") { 
                                    if (index is RegexIndex) {
                                        enumerable = ((RegexIndex)index).Match("%" + prefix + "%");
                                    } else { 
                                        return null;
                                    }
                                } else { 
                                    enumerable = index.StartsWith(prefix);
                                }
                                if (firstJoinIterator != null)
                                {
                                    lastJoinIterator.iterator = enumerable.GetEnumerator();
                                    enumerable = firstJoinIterator;
                                }
                                return enumerable;
                            }
#if WINRT_NET_FRAMEWORK
                            table = locateTable(table.type.GetTypeInfo().BaseType, forUpdate, false);
#else
                            table = locateTable(table.type.BaseType, forUpdate, false);
#endif
                        }    
                    }
                } 
                else if (call.Method.DeclaringType != typeof(string) && call.Method.Name == "Contains" 
                    && ((call.Arguments.Count == 1 && IsLiteral(call.Object, out value)) ||
                        (call.Arguments.Count == 2 && null == call.Object && IsLiteral(call.Arguments[0], out value)))
                    && value is IEnumerable)
                {
                    Expression arg = call.Arguments[call.Arguments.Count-1]; 
                    if (arg.NodeType == ExpressionType.MemberAccess)
                    {
                        JoinIterator lastJoinIterator = null;
                        JoinIterator firstJoinIterator = null;
                        MemberExpression deref = (MemberExpression)arg;
                        if (deref.Expression.NodeType != ExpressionType.Parameter)
                        {
                            lastJoinIterator = new JoinIterator();
                            firstJoinIterator = Join(deref, lastJoinIterator, forUpdate);
                            if (firstJoinIterator == null)
                            {
                                return null;
                            }
                            table = locateTable(deref.Member.DeclaringType, forUpdate, false);
                        }
                        string name = deref.Member.Name;
                        while (table != null)
                        {
                            FieldIndex index = (FieldIndex)table.indicesMap[name];
                            if (index != null)
                            {
                                JoinIterator union = new JoinIterator();
                                union.joinIndex = index;
                                union.iterator = ((IEnumerable)value).GetEnumerator();
                                IEnumerable enumerable = union;
                                if (firstJoinIterator != null)
                                {
                                    lastJoinIterator.iterator = enumerable.GetEnumerator();
                                    enumerable = firstJoinIterator;
                                }
                                return enumerable;
                            }
#if WINRT_NET_FRAMEWORK
                            table = locateTable(table.type.GetTypeInfo().BaseType, forUpdate, false);
#else
                            table = locateTable(table.type.BaseType, forUpdate, false);
#endif
                        }
                    }
                }
            } 
            else if (expr.NodeType == ExpressionType.Not)
            {
                if (expr.Type == typeof(bool))
                {
                    Expression opd = ((UnaryExpression)expr).Operand;            
                    if (opd.NodeType == ExpressionType.MemberAccess)
                    {
                        JoinIterator lastJoinIterator = null;
                        JoinIterator firstJoinIterator = null;
                        MemberExpression deref = (MemberExpression)opd;
                        if (deref.Expression.NodeType != ExpressionType.Parameter)
                        {
                            lastJoinIterator = new JoinIterator();
                            firstJoinIterator = Join(deref, lastJoinIterator, forUpdate);
                            if (firstJoinIterator == null)
                            {
                                return null;
                            }
                            table = locateTable(deref.Member.DeclaringType, forUpdate, false);
                        }
    
                        string name = deref.Member.Name;
                        while (table != null)
                        {
                            FieldIndex index = (FieldIndex)table.indicesMap[name];
                            if (index != null)
                            {
                                Key f = new Key(false);
                                IEnumerable enumerable = index.Range(f, f, IterationOrder.AscentOrder);
                                if (firstJoinIterator != null)
                                {
                                    lastJoinIterator.iterator = enumerable.GetEnumerator();
                                    enumerable = firstJoinIterator;
                                }
                                return enumerable;
                            }
#if WINRT_NET_FRAMEWORK
                            table = locateTable(table.type.GetTypeInfo().BaseType, forUpdate, false);
#else
                            table = locateTable(table.type.BaseType, forUpdate, false);
#endif
                        }
                    }
                }
                return null;                
            }
            else if (expr.NodeType == ExpressionType.MemberAccess)
            {
                if (expr.Type == typeof(bool))
                {
                    JoinIterator lastJoinIterator = null;
                    JoinIterator firstJoinIterator = null;
                    MemberExpression deref = (MemberExpression)expr;
                    if (deref.Expression.NodeType != ExpressionType.Parameter)
                    {
                        lastJoinIterator = new JoinIterator();
                        firstJoinIterator = Join(deref, lastJoinIterator, forUpdate);
                        if (firstJoinIterator == null)
                        {
                            return null;
                        }
                        table = locateTable(deref.Member.DeclaringType, forUpdate, false);
                    }

                    string name = deref.Member.Name;
                    while (table != null)
                    {
                        FieldIndex index = (FieldIndex)table.indicesMap[name];
                        if (index != null)
                        {
                            Key t = new Key(true);
                            IEnumerable enumerable = index.Range(t, t, IterationOrder.AscentOrder);
                            if (firstJoinIterator != null)
                            {
                                lastJoinIterator.iterator = enumerable.GetEnumerator();
                                enumerable = firstJoinIterator;
                            }
                            return enumerable;
                        }
#if WINRT_NET_FRAMEWORK
                        table = locateTable(table.type.GetTypeInfo().BaseType, forUpdate, false);
#else
                        table = locateTable(table.type.BaseType, forUpdate, false);
#endif
                    }
                }
                return null;                
            }
            else if (expr is BinaryExpression)
            {
                ArrayList alternatives = null;
                Expression left = ((BinaryExpression)expr).Left;
                Expression right = ((BinaryExpression)expr).Right;
    
                if (expr.NodeType == ExpressionType.AndAlso)
                {
                    if (storage.SqlOptimizerParams.enableCostBasedOptimization 
                        && CalculateCost(left) > CalculateCost(right))
                    {
                        IEnumerable result = ApplyIndex(right, table, forUpdate);
                        if (result != null)
                        {
                            return result;
                        }
                        return ApplyIndex(left, table, forUpdate);
                    }
                    else
                    {
                        IEnumerable result = ApplyIndex(left, table, forUpdate);
                        if (result != null)
                        {
                            return result;
                        }
                        return ApplyIndex(right, table, forUpdate);
                    }
                }
                if (expr.NodeType == ExpressionType.OrElse)
                { 
                    if (left.NodeType == ExpressionType.Equal && IsLiteral(((BinaryExpression)left).Right, out value))
                    {
                        Expression baseExpr = ((BinaryExpression)left).Left;
                        alternatives = new ArrayList();
                        while (right is BinaryExpression)
                        {
                            Expression cmp;
                            if (right.NodeType ==  ExpressionType.OrElse)
                            {
                                BinaryExpression or = (BinaryExpression)right;
                                right = or.Right;
                                cmp = or.Left;
                            }
                            else
                            {
                                cmp = right;
                                right = null;
                            }
                            if (cmp.NodeType != ExpressionType.Equal
                                || !EqualExpressions(baseExpr, ((BinaryExpression)cmp).Left)
                                || !IsLiteral(((BinaryExpression)cmp).Right, out value))
                            {
                                return null;
                            }
                            alternatives.Add(value);
                        }
                        if (right != null)
                        {
                            return null;
                        }   
                        expr = left;
                        left = ((BinaryExpression)expr).Left;
                        right = ((BinaryExpression)expr).Right;
                    }
                    else
                    { 
                        return null;
                    }
                }
                if (left.NodeType == ExpressionType.Convert
#if WINRT_NET_FRAMEWORK
                    && ((UnaryExpression)left).Operand.Type.GetTypeInfo().IsEnum
#else
                    && ((UnaryExpression)left).Operand.Type.IsEnum
#endif
                    && left.Type == typeof(int))
                {
                    left = ((UnaryExpression)left).Operand;
                }
                bool inverse = false;
                if (!IsLiteral(right, out value))
                {
                    if (!IsLiteral(left, out value))   
                    {
                        return null;
                    }
                    Expression tmp = left;
                    left = right;
                    right = tmp;
                    inverse = true;
                }   
                if (left.NodeType == ExpressionType.Call && value is int && (int)value == 0) 
                { 
                    MethodCallExpression call = (MethodCallExpression)left;
                    if (call.Method.DeclaringType != typeof(string))
                    {
                        return null;
                    }
                    if (call.Method.Name == "CompareTo")
                    {   
                        left = call.Object;
                        right = call.Arguments[0];
                    } 
                    else if (call.Method.Name == "Compare")
                    {   
                        left = call.Arguments[0];
                        right = call.Arguments[1];
                    } 
                    else
                    {
                        return null;
                    }
                    if (!IsLiteral(right, out value))
                    {
                        if (!IsLiteral(left, out value))   
                        {
                            return null;
                        }
                        Expression tmp = left;
                        left = right;
                        right = tmp;
                        inverse = !inverse;
                    }
                }
                if (left.NodeType == ExpressionType.MemberAccess)
                {
                    JoinIterator lastJoinIterator = null;
                    JoinIterator firstJoinIterator = null;
                    MemberExpression deref = (MemberExpression)left;
                    if (deref.Expression.NodeType != ExpressionType.Parameter)
                    {
                        lastJoinIterator = new JoinIterator();
                        firstJoinIterator = Join(deref, lastJoinIterator, forUpdate);
                        if (firstJoinIterator == null)
                        {
                            return null;
                        }
                        table = locateTable(deref.Member.DeclaringType, forUpdate, false);
                    }

                    string name = deref.Member.Name;
                    while (table != null)
                    {
                        FieldIndex index = (FieldIndex)table.indicesMap[name];
                        if (index != null)
                        {
                            Key min = null;
                            Key max = null;
                            switch (expr.NodeType)
                            {
                                case ExpressionType.Equal:
                                    min = max = ConstructKey(value, index, true);
                                    break;
                                case ExpressionType.GreaterThan:
                                    min = ConstructKey(value, index, false);
                                    break;
                                case ExpressionType.GreaterThanOrEqual:
                                    min = ConstructKey(value, index, true);
                                    break;
                                case ExpressionType.LessThan:
                                    max = ConstructKey(value, index, false);
                                    break;
                                case ExpressionType.LessThanOrEqual:
                                    max = ConstructKey(value, index, true);
                                    break;
                                default:
                                    return null;
                            }
                            if (inverse)
                            {
                                Key tmp = min;
                                min = max;
                                max = tmp;
                            }
                            IEnumerable enumerable = index.Range(min, max, IterationOrder.AscentOrder);
                            if (alternatives != null)
                            {
                                enumerable = new UnionIterator(index, enumerable.GetEnumerator(), alternatives);
                            }
                            if (firstJoinIterator != null)
                            {
                                lastJoinIterator.iterator = enumerable.GetEnumerator();
                                enumerable = firstJoinIterator;
                            }
                            return enumerable;
                        }
#if WINRT_NET_FRAMEWORK
                        table = locateTable(table.type.GetTypeInfo().BaseType, forUpdate, false);
#else
                        table = locateTable(table.type.BaseType, forUpdate, false);
#endif
                    }
                }
            }
            return null;
        }
                
                

        public IEnumerable<T> Select<T>(Expression<Func<T,bool>> predicate) where T:class
        {
            return Select<T>(predicate, false);
        }

        class LinqFilter<T> : IEnumerable<T>, IEnumerable where T:class
        {
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new LinqEnumerator<T>(enumerable.GetEnumerator(), predicate);
            }

            public IEnumerator<T> GetEnumerator() 
            {
                return new LinqEnumerator<T>(enumerable.GetEnumerator(), predicate);
            }

            public LinqFilter(IEnumerable e, Func<T,bool> p) 
            {
                enumerable = e;
                predicate = p;
            }

            IEnumerable enumerable;
            Func<T, bool> predicate;    
       }

        
        
        class LinqEnumerator<T> : IEnumerator<T>, IEnumerator where T:class
        {
            IEnumerator enumerator;
            Func<T, bool> predicate;    
            T current;

            public bool MoveNext() 
            {
                while (enumerator.MoveNext()) 
                {
                    T obj = enumerator.Current as T;
                    if (obj != null && (predicate == null || predicate(obj)))
                    {
                        current = obj;
                        return true;
                    }
                }
                current = null;
                return false;
            }

            public T Current
            {
                get
                {
                    return current;
                }
            }
             
            public void Dispose()
            {
            }

            public virtual void Reset() 
            {
                enumerator.Reset();
                current = null;
            }
                
            object IEnumerator.Current
            {
                get
                {
                    return current;
                }
            }

            public LinqEnumerator(IEnumerator e, Func<T,bool> p) 
            {
                enumerator = e;
                predicate = p;
            }
        }

        public IEnumerable<T> Select<T>(Expression<Func<T,bool>> predicate, bool forUpdate) where T:class
        {
            Table t = locateTable(typeof(T), forUpdate, false);
            if (t != null)
            {
                IEnumerable result = ApplyIndex(predicate.Body, t, forUpdate);
                bool filterNeeded;
                if (result == null) 
                {
                    result = (IEnumerable)t.extent;
                    filterNeeded = true;
                    if (storage.Listener != null) 
                    {
                        storage.Listener.SequentialSearchPerformed(predicate);
                    } 
                }
                else
                {
                    filterNeeded = predicate.Body.NodeType == ExpressionType.AndAlso;
                }
                Func<T,bool> filter = null;
                if (filterNeeded)
                {
                    filter = predicate.Compile();
                }
                return new LinqFilter<T>(result, filter);
            }
            return (IEnumerable<T>)new T[0];
        }

        public IEnumerable<T> GetRecords<T>() where T : class
        {
            return GetRecords<T>(false);
        }


        public IEnumerable<T> GetRecords<T>(bool forUpdate) where T:class
        {
            return new LinqFilter<T>(GetRecords(typeof(T), forUpdate), null);
        }

        public int CountRecords<T>() where T : class
        {
            return CountRecords<T>(false);
        }


        public int CountRecords<T>(bool forUpdate) where T:class
        {
            return CountRecords(typeof(T), forUpdate);
        }

        public class TableData<T> : IOrderedQueryable<T> where T:class
        {   
            Database db;
            Expression<Func<T, bool>> predicate;

            public TableData(Database db)
            {
                this.db = db;
            }

            public IQueryable<T> Where(Expression<Func<T, bool>> predicate)
            {
                this.predicate = predicate;
                return this;
            }

            IEnumerable<T> GetEnumerable()
            {
                return predicate == null ? db.GetRecords<T>() : db.Select<T>(predicate);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return GetEnumerable().GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)GetEnumerable()).GetEnumerator();
            }

        #region IQueryable Members

 
            public Type ElementType
            {
                get { return (typeof(T)); }
            }

            public Expression Expression
            {
                get
                {
                    return Expression.Constant(GetEnumerable().AsQueryable<T>());
                }
            }

            public IQueryProvider Provider
            {
                get
                {
                   return GetEnumerable().AsQueryable<T>().Provider;
                }
            }
        #endregion
        }

        public TableData<T> GetTable<T>() where T : class
        {
            return new TableData<T>(this);
        }
#endif

        /// <summary>
        /// <p>
        /// Prepare JSQL query. Prepare is needed for queries with parameters. Also
        /// preparing query can improve speed if query will be executed multiple times
        /// (using prepare, it is compiled only once).</p><p>
        /// To execute prepared query, you should use Query.Execute()
        /// or Query.Execute(db.GetRecords(typeof(XYZ))) method
        /// </p></summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="predicate">search predicate
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <exception cref="CompileError"> exception is thrown if predicate is not valid JSQL exception
        /// </exception>
        /// <returns>prepared query</returns>
        public Query Prepare(Type table, string predicate) 
        { 
            return Prepare(table, predicate, false);
        }

        /// <summary>
        /// <p>
        /// Prepare JSQL query. Prepare is needed for queries with parameters. Also
        /// preparing query can improve speed if query will be executed multiple times
        /// (using prepare, it is compiled only once).</p><p>
        /// To execute prepared query, you should use Query.Execute(db.GetRecords(typeof(XYZ))) method
        /// </p></summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="predicate">search predicate
        /// </param>
        /// <param name="forUpdate"><b>true</b> if records are selected for update - 
        /// in this case exclusive lock is set for the table to avoid deadlock.
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <exception cref="CompileError"> exception is thrown if predicate is not valid JSQL exception
        /// </exception>
        /// <returns>prepared query</returns>
        public Query Prepare(Type table, string predicate, bool forUpdate) 
        { 
            Query q = CreateQuery(table, forUpdate);
            q.Prepare(table, predicate);      
            return q;
        }

        /// <summary>
        /// Create query for the specified class. You can use Query.getCodeGenerator method to generate 
        /// query code.
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns>query without predicate</returns>
        ///
        public Query CreateQuery(Type table)
        {
            return CreateQuery(table, false);
        }

        /// <summary>
        /// Create query for the specified class. You can use Query.getCodeGenerator method to generate 
        /// query code.
        /// </summary>
        /// <param name="table">class corresponding to the table
        /// </param>
        /// <param name="forUpdate"><b>true</b> if records are selected for update - 
        /// in this case exclusive lock is set for the table to avoid deadlock.
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        /// <returns>query without predicate</returns>
        ///
        public Query CreateQuery(Type table, bool forUpdate)
        {
            Table t = locateTable(table, forUpdate, false);
            Query q = storage.CreateQuery();
            q.SetIndexProvider(this);
            q.SetClass(table);
            while (t != null)
            {
                q.SetClassExtent(t.extent, multithreaded ? forUpdate ? ClassExtentLockType.Exclusive : ClassExtentLockType.Shared : ClassExtentLockType.None);
#if SILVERLIGHT
                foreach (System.Collections.Generic.KeyValuePair<object, object> entry in t.indicesMap)
#else
                foreach (DictionaryEntry entry in t.indicesMap) 
#endif
                {
                    FieldIndex index = (FieldIndex)entry.Value;
                    string key = (string)entry.Key;
                    q.AddIndex(key, index);
                }
#if WINRT_NET_FRAMEWORK
                t = locateTable(t.type.GetTypeInfo().BaseType, forUpdate, false);
#else
                t = locateTable(t.type.BaseType, forUpdate, false);
#endif
            }
            return q;
        }
        
        /// <summary> 
        /// Get iterator through all table records
        /// </summary>
        /// <param name="table">class corresponding to the table</param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        public IEnumerable GetRecords(Type table) 
        { 
            return GetRecords(table, false);
        }

        /// <summary> 
        /// Get iterator through all table records
        /// </summary>
        /// <param name="table">class corresponding to the table</param>
        /// <param name="forUpdate"><b>true</b> if records are selected for update - 
        /// in this case exclusive lock is set for the table to avoid deadlock.
        /// </param>
        /// <exception cref="StorageError">StorageError(CLASS_NOT_FOUND) exception is thrown if there is no table corresponding to 
        /// the specified class
        /// </exception>
        public IEnumerable GetRecords(Type table, bool forUpdate) 
        { 
            Table t = locateTable(table, forUpdate, false);
            return t == null ? (IEnumerable)new object[0] : (IEnumerable)new TypeFilter(table, t.extent);
        }
    
        /// <summary> 
        /// Get number of records in the table
        /// </summary>
        /// <param name="table">class corresponding to the table</param>
        /// <returns>number of records in the table associated with specified class</returns>
        public int CountRecords(Type table) 
        {
            return CountRecords(table, false);
        }

        /// <summary> 
        /// Get number of records in the table 
        /// </summary>
        /// <param name="table">class corresponding to the table</param>
        /// <param name="forUpdate"><b>true</b> if you are going to update the table - 
        /// in this case exclusive lock is set for the table to avoid deadlock.</param>
        /// <returns>number of records in the table associated with specified class</returns>
        public int CountRecords(Type table, bool forUpdate) 
        { 
            Table t = locateTable(table, forUpdate, false);
            return t == null ? 0 : t.extent.Count;        
        }

        /// <summary>
        /// Get storage associated with this database
        /// </summary>
        /// <returns> underlying storage</returns>
        public Storage Storage 
        { 
            get 
            {
                return storage;
            }
        }

        /// <summary>
        /// Get full text index
        /// </summary>
        /// <returns>used full text index</returns>
        public FullTextIndex  FullTextIndex
        {
            get
            {
                return metadata.fullTextIndex;
            }
        }

        internal class Metadata : PersistentResource 
        {
            internal Index metaclasses;
            internal FullTextIndex fullTextIndex;

            internal Metadata(Storage storage, Index index, FullTextSearchHelper helper) 
               : base(storage) 
            { 
                metaclasses = index;
                fullTextIndex = (helper != null)
                    ? storage.CreateFullTextIndex(helper)
                    : storage.CreateFullTextIndex();
            }

            internal Metadata(Storage storage, FullTextSearchHelper helper) 
               : base(storage) 
            { 
                metaclasses = storage.CreateIndex(typeof(string), true);
                fullTextIndex = (helper != null) 
                    ? storage.CreateFullTextIndex(helper)
                    : storage.CreateFullTextIndex();
            }

            internal Metadata() {}
        }
        
        internal class Table : Persistent 
        { 
            internal ISet extent;
            internal Link indices;

            [NonSerialized()]
            internal Hashtable indicesMap = new Hashtable();

            [NonSerialized()]
            internal Type type;

            [NonSerialized()]
            internal ArrayList fullTextIndexableFields;

            [NonSerialized()]
            internal FieldIndex autoincrementIndex;

            internal void setClass(Type cls) 
            {
                type = cls;
                fullTextIndexableFields = new ArrayList();
#if WINRT_NET_FRAMEWORK
                foreach (FieldInfo fi in cls.GetRuntimeFields())
#else
                foreach (FieldInfo fi in cls.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
#endif
                {
#if WINRT_NET_FRAMEWORK
                    if (fi.GetCustomAttributes(typeof(FullTextIndexableAttribute), false).GetEnumerator().MoveNext())
#else
                    if (fi.GetCustomAttributes(typeof(FullTextIndexableAttribute), false).Length > 0)
#endif
                    {
                        fullTextIndexableFields.Add(fi);
                    }
                }
            }

            public override void OnLoad() 
            { 
                indicesMap = new Hashtable();
                for (int i = indices.Count; --i >= 0;) 
                { 
                    FieldIndex index = (FieldIndex)indices[i];
                    indicesMap[index.KeyField.Name] = index;
                    foreach (IndexableAttribute idx in index.KeyField.GetCustomAttributes(typeof(IndexableAttribute), false))
                    {
                        if (idx.Autoincrement)
                        {
                            autoincrementIndex = index;
                        }   
                    }
                }
            }

            public override void Deallocate()
            {
                extent.Deallocate();
                foreach (FieldIndex index in indicesMap.Values) 
                { 
                    index.Deallocate();
                }
                base.Deallocate();
            }        
        }                        

        Hashtable tables;
        Storage   storage;
        Metadata  metadata;
        bool      multithreaded;
        bool      autoRegisterTables;
        bool      autoIndices;
        bool      globalClassExtent;
        bool      searchBaseClasses;
    }

    public class TypeFilterEnumerator : IEnumerator
    {
        public bool MoveNext()
        {
            while (enumerator.MoveNext())
            {
                obj = enumerator.Current;
#if WINRT_NET_FRAMEWORK
                if (obj != null && type.GetTypeInfo().IsAssignableFrom(obj.GetType().GetTypeInfo())) 
#else
                if (type.IsInstanceOfType(obj)) 
#endif
                {
                    return true;
                }
            }
            return false;
        } 
                    
        public object Current
        {
            get 
            {
                return obj;
            }
        }                        
    
        public void Reset() 
        {
            enumerator.Reset();
            obj = null;
        }
    
        public TypeFilterEnumerator(Type t, IEnumerator e)
        {
            type = t;
            enumerator = e;
        }
    
        Type type;
        IEnumerator enumerator;
        object obj;
    }
    
    public class TypeFilter : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            return new TypeFilterEnumerator(type, enumerable.GetEnumerator());
        }

        public TypeFilter(Type t, IEnumerable e) 
        {
            type = t;
            enumerable = e;
        }

        Type type;
        IEnumerable enumerable;
    }
}

#endif
