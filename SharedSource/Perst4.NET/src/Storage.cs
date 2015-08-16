namespace Perst
{
    using System;
    using System.Collections;
    using Perst.FullText;

#if USE_GENERICS
    using System.Collections.Generic;
#endif

    public enum TransactionMode
    { 
        /// <summary>
        /// Exclusive per-thread transaction: each thread access database in exclusive mode
        /// </summary>
        Exclusive,
        ReadWrite = Exclusive,
        /// <summary>
        /// Cooperative mode; all threads share the same transaction. Commit will commit changes made
        /// by all threads. To make this schema work correctly, it is necessary to ensure (using locking)
        /// that no thread is performing update of the database while another one tries to perform commit.
        /// Also please notice that rollback will undo the work of all threads. 
        /// </summary>
        Cooperative,
        ReadOnly = Cooperative,
        /// <summary>
        /// Serializable per-thread transaction. Unlike exclusive mode, threads can concurrently access database, 
        /// but effect will be the same as them work exclusively.
        /// To provide such behavior, programmer should lock all access objects (or use hierarchical locking).
        /// When object is updated, exclusive lock should be set, otherwise shared lock is enough.
        /// Lock should be preserved until the end of transaction.
        /// </summary>
        Serializable,
        /// <summary>
        /// Read only transaction which can be started at replication slave node.
        /// It runs concurrently with receiving updates from master node.
        /// </summary>
        ReplicationSlave
    };
	
        
    /// <summary>
    /// Tune parameter for SQL optimizer
    /// </summary>
    public class SqlOptimizerParameters
    { 
        /// <summary>
        /// If optimization is enabled SQL engine will choose order of query conjuncts execution based 
        /// on the estimation of their execution cost, if optimization is disable, 
        /// then conjuncts will be executed in the same order as them are specified in the query.
        /// </summary>
        public bool enableCostBasedOptimization;

        /// <summary>
        /// Index is not applicable
        /// </summary>
        public int sequentialSearchCost;
    
        /// <summary>
        /// Cost of searching using non-unique index. It is used only of equality comparisons and is added to eqCost, eqStringConst...
        /// </summary>
        public int notUniqCost;
        
        /// <summary>
        /// Cost of index search of key of scalar, reference or date type 
        /// </summary>
        public int eqCost;
        
        /// <summary>
        /// Cost of search in boolean index
        /// </summary>
        public int eqBoolCost;
    
        /// <summary>
        /// Cost of search in string index
        /// </summary>
        public int eqStringCost;
    
        /// <summary>
        /// Cost of search in real index
        /// </summary>
        public int eqRealCost;
    
        /// <summary>
        /// Cost for the following comparison operations: &lt; &lt;= &gt; &gt;=
        /// </summary>
        public int openIntervalCost;
        
        /// <summary>
        /// Cost for BETWEEN operation
        /// </summary>
        public int closeIntervalCost;
    
        /// <summary>
        /// Cost of index search of collection elements
        /// </summary>
        public int containsCost;
    
        /// <summary>
        /// Cost of boolean OR operator
        /// </summary>
        public int orCost;
    
        /// <summary>
        /// Cost of boolean AND operator
        /// </summary>
        public int andCost;
    
        /// <summary>
        /// Cost of IS NULL operator
        /// </summary>
        public int isNullCost;
    
    
        /// <summary>
        /// Cost of LIKE operator
        /// </summary>
        public int patternMatchCost;
    
        
        /// <summary>
        /// Cost of each extra level of indirection, for example in condition (x.y.z = 1) indirection level is 2 and in condition (x = 2) it is 0.
        /// </summary>
        public int indirectionCost;
    
        /// <summary>
        /// Default constructor setting default values of parameters
        /// </summary>
        public SqlOptimizerParameters()
        {
            enableCostBasedOptimization = false;
            sequentialSearchCost = 1000;
            openIntervalCost = 100;
            containsCost = 50;
            orCost = 10;
            andCost = 10;
            isNullCost = 6;
            closeIntervalCost = 5;
            patternMatchCost = 2;
            eqCost = 1;
            eqRealCost = 2;
            eqStringCost = 3;
            eqBoolCost = 200;
            indirectionCost = 2;
             notUniqCost = 1;
        }
    }

    /// <summary> Attribute used to mark classes where properties shoudl be serialized instead of fields
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class SerializePropertiesAttribute : Attribute
    {
    }
      	
    /// <summary> Attribute used to mark field and properties which should not be serialized.
    /// The difference with standard .Net NonSerialized attribute is that the last one can be applied
    /// only to the field and Perst supports also serialization of properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
    public class TransientAttribute : Attribute
    {
    }
        

    /// <summary> Object storage
    /// </summary>
    public interface Storage
    {
        /// <summary> Get Perst version (for example 434 for release 4.34)
        /// </summary>
        int PerstVersion {get;}
    
        /// <summary> Get version of database format for this database. When new database is created it is
        /// always assigned the current database format version
        /// </summary>
        int DatabaseFormatVersion {get;}
    
    
        /// <summary> Get/set storage root. Storage can have exactly one root object. 
        /// If you need to have several root object and access them by name (as is is possible 
        /// in many other OODBMSes), you should create index and use it as root object.
        /// Previous reference to the root object is rewritten but old root is not automatically deallocated.
        /// </summary>
        object Root {get; set;}
      
        /// <summary> Open the storage
        /// </summary>
        /// <param name="filePath">path to the database file
        /// </param>
        /// <param name="pagePoolSize">size of page pool (in bytes). Page pool should contain
        /// at least ten 4kb pages, so minimal page pool size should be at least 40Kb.
        /// But larger page pool ussually leads to better performance (unless it could not fit
        /// in memory and cause swapping). If value of pagePoolSize is 0, then page pool will be
        /// unlimited - dynamically extended to conatins all database file pages.
        /// </param>
        void  Open(String filePath, long pagePoolSize);
		
        /// <summary> Open the storage with default page pool size
        /// </summary>
        /// <param name="filePath">path to the database file
        /// 
        /// </param>
        void  Open(String filePath);
		
        /// <summary> Open the storage
        /// </summary>
        /// <param name="file">user specific implementation of IFile interface
        /// </param>
        /// <param name="pagePoolSize">size of page pool (in bytes). Page pool should contain
        /// at least ten 4kb pages, so minimal page pool size should be at least 40Kb.
        /// But larger page pool ussually leads to better performance (unless it could not fit
        /// in memory and cause swapping).
        /// 
        /// </param>
        void  Open(IFile file, long pagePoolSize);
		
        /// <summary> Open the storage with default page pool size
        /// </summary>
        /// <param name="file">user specific implementation of IFile interface
        /// </param>
        void  Open(IFile file);

        /// <summary> Open the encrypted storage
        /// </summary>
        /// <param name="filePath">path to the database file
        /// </param>
        /// <param name="pagePoolSize">size of page pool (in bytes). Page pool should contain
        /// at least then 4kb pages, so minimal page pool size should be at least 40Kb.
        /// But larger page pool usually leads to better performance (unless it could not fit
        /// in memory and cause swapping).
        /// </param>
        /// <param name="cipherKey">cipher key</param>
        void  Open(String filePath, long pagePoolSize, String cipherKey);

        
        /// <summary>Check if database is opened
        /// </summary>
        /// <returns><b>true</b> if database was opened by <b>open</b> method, 
        /// <b>false</b> otherwise
        /// </returns>        
        bool IsOpened();
		
        /// <summary> Commit changes done by the last transaction. Transaction is started implcitlely with forst update
        /// opertation.
        /// </summary>
        void  Commit();
		
        /// <summary> Rollback changes made by the last transaction.
        /// By default, Perst doesn't reload modified objects after a transaction
        /// rollback. In this case, the programmer should not use references to the
        /// persistent objects stored in program variables. Instead, the application
        /// should fetch the object tree from the beginning, starting from obtaining the
        /// root object using the Storage.getRoot method.
        /// Setting the "perst.reload.objects.on.rollback" property instructs Perst to
        /// reload all objects modified by the aborted (rolled back) transaction. It
        /// takes additional processing time, but in this case it is not necessary to
        /// ignore references stored in variables, unless they point to the objects
        /// created by this transactions (which were invalidated when the transaction
        /// was rolled back). Unfortunately, there is no way to prohibit access to such
        /// objects or somehow invalidate references to them. So this option should be
        /// used with care.
        /// </summary>
        void  Rollback();
		
        /// <summary>
        /// Backup current state of database
        /// </summary>
        /// <param name="stream">output stream to which backup is done</param>
        void Backup( System.IO.Stream stream);

        /// <summary>
        /// Backup current state of database
        /// </summary>
        /// <param name="filePath">path to the written backup file</param>
        /// <param name="cipherKey">cipher key</param>
        void Backup(string filePath, string cipherKey);

        /// <summary> Create JSQL query. JSQL is object oriented subset of SQL allowing
        /// to specify arbitrary prdicates for selecting members of Perst collections
        /// </summary>
        /// <returns> created query object
        /// </returns>
#if USE_GENERICS
        Query<T> CreateQuery<T>();
#else
        Query CreateQuery();
#endif
        
#if USE_GENERICS
        /// <summary> Create new index. K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// </exception>
        Index<K,V> CreateIndex<K,V>(bool unique) where V:class;
#else
        /// <summary> Create new index
        /// </summary>
        /// <param name="type">type of the index key (you should path here <b>String.class</b>, 
        /// <b>int.class</b>, ...)
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// 
        /// </exception>
        Index CreateIndex(Type type, bool unique);
#endif
		
        /// <summary> Create new compound index.
        /// </summary>
        /// <param name="types">types of components of compund key
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// </exception>
#if USE_GENERICS
        CompoundIndex<V> CreateIndex<V>(Type[] types, bool unique) where V:class;
#else
        CompoundIndex    CreateIndex(Type[] types, bool unique);
#endif
		
#if USE_GENERICS
        /// <summary>
        /// Create new multidimensional index
        /// </summary>
        /// <param name="comparator">multidimensinal comparator</param>
        /// <returns>multidimensional index</returns>
        MultidimensionalIndex<V> CreateMultidimensionalIndex<V>(MultidimensionalComparator<V> comparator) where V : class;
#else
        /// <summary>
        /// Create new multidimensional index
        /// </summary>
        /// <param name="comparator">multidimensinal comparator</param>
        /// <returns>multidimensional index</returns>
        MultidimensionalIndex CreateMultidimensionalIndex(MultidimensionalComparator comparator);
#endif

#if USE_GENERICS
        /// <summary>
        /// Create new multidimensional index for specified fields of the class 
        /// </summary>
        /// <param name="fieldNames">name of the fields which are treated as index dimensions,
        /// if null then all declared fields of the class are used.</param>
        /// <param name="treateZeroAsUndefinedValue">if value of scalar field in QBE object is 0 
        /// (default value) then assume that condition is not defined for this field</param>
        /// <returns>multidimensional index</returns>
        MultidimensionalIndex<V> CreateMultidimensionalIndex<V>(string[] fieldNames, bool treateZeroAsUndefinedValue) where V:class;
#else
        /// <summary>
        /// Create new multidimensional index for specified fields of the class 
        /// </summary>
        /// <param name="type">class of objects included in this index</param>
        /// <param name="fieldNames">name of the fields which are treated as index dimensions,
        /// if null then all declared fields of the class are used.</param>
        /// <param name="treateZeroAsUndefinedValue">if value of scalar field in QBE object is 0 
        /// (default value) then assume that condition is not defined for this field</param>
        /// <returns>multidimensional index</returns>
        MultidimensionalIndex CreateMultidimensionalIndex(Type type, string[] fieldNames, bool treateZeroAsUndefinedValue);
#endif

#if USE_GENERICS
        /// <summary> Create new thick index (index with large number of duplicated keys).
        /// K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <returns>persistent object implementing thick index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// 
        /// </exception>
        Index<K,V> CreateThickIndex<K,V>() where V:class;
#else
        /// <summary> Create new thick index (index with large number of duplicated keys)
        /// </summary>
        /// <param name="type">type of the index key (you should path here <b>String.class</b>, 
        /// <b>int.class</b>, ...)
        /// </param>
        /// <returns>persistent object implementing thick index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// 
        /// </exception>
        Index CreateThickIndex(Type type);
#endif
	
#if USE_GENERICS
        /// <summary> 
        /// Create new field index
        /// K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex<K,V> CreateFieldIndex<K,V>(string fieldName, bool unique) where V:class;
#else
        /// <summary> 
        /// Create new field index
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex CreateFieldIndex(Type type, string fieldName, bool unique);
#endif
		
#if USE_GENERICS
        /// <summary> 
        /// Create new field index
        /// K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex<K,V> CreateFieldIndex<K,V>(string fieldName, bool unique, bool caseInsensitive) where V:class;
#else
        /// <summary> 
        /// Create new field index
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex CreateFieldIndex(Type type, string fieldName, bool unique, bool caseInsensitive);
#endif
		
#if USE_GENERICS
        /// <summary> 
        /// Create new field index
        /// K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <param name="thick">index should be optimized to handle large number of duplicate key values</param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex<K,V> CreateFieldIndex<K,V>(string fieldName, bool unique, bool caseInsensitive, bool thick) where V:class;
#else
        /// <summary> 
        /// Create new field index
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <param name="thick">index should be optimized to handle large number of duplicate key values</param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex CreateFieldIndex(Type type, string fieldName, bool unique, bool caseInsensitive, bool thick);
#endif
		
#if USE_GENERICS
        /// <summary> 
        /// Create new multi-field index
        /// </summary>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex<V> CreateFieldIndex<V>(string[] fieldNames, bool unique) where V:class;
#else
        /// <summary> 
        /// Create new multi-field index
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex CreateFieldIndex(Type type, string[] fieldNames, bool unique);
#endif
	
#if USE_GENERICS
        /// <summary> 
        /// Create new multi-field index
        /// </summary>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex<V> CreateFieldIndex<V>(string[] fieldNames, bool unique, bool caseInsensitive) where V:class;
#else
        /// <summary> 
        /// Create new multi-field index
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex CreateFieldIndex(Type type, string[] fieldNames, bool unique, bool caseInsensitive);
#endif
	
#if USE_GENERICS
        /// <summary>
        /// Create new n-gram string field index for regular expression search
        /// </summary>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <code>type</code></param>
        /// <param name="caseInsensitive">whether case of characters should be ignores</param>
        /// <param name="nGrams">number of characters used to construcrt n-grams</param>
        /// <returns>persistent object implementing n-grams field index</returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        RegexIndex<T> CreateRegexIndex<T>(string fieldName, bool caseInsensisteve, int nGrams) where T:class;

        /// <summary>
        /// Create new 3-gram case insensitive string field index for regular expression search
        /// </summary>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <code>type</code></param>
        /// <returns>persistent object implementing 3-grams field index</returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        RegexIndex<T> CreateRegexIndex<T>(string fieldName) where T:class;
#else
        /// <summary>
        /// Create new n-gram string field index for regular expression search
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index</param>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <code>type</code></param>
        /// <param name="caseInsensitive">whether case of characters should be ignores</param>
        /// <param name="nGrams">number of characters used to construcrt n-grams</param>
        /// <returns>persistent object implementing n-grams field index</returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        RegexIndex CreateRegexIndex(Type type, String fieldName, bool caseInsensisteve, int nGrams);

        /// <summary>
        /// Create new 3-gram case insensitive string field index for regular expression search
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index</param>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <code>type</code></param>
        /// <returns>persistent object implementing 3-grams field index</returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        RegexIndex CreateRegexIndex(Type type, string fieldName);
#endif

#if USE_GENERICS
        /// <summary> Create new index optimized for access by element position. 
        /// K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// </exception>
        Index<K,V> CreateRandomAccessIndex<K,V>(bool unique) where V:class;
#else
        /// <summary> Create new index optimized for access by element position. 
        /// </summary>
        /// <param name="type">type of the index key (you should path here <b>String.class</b>, 
        /// <b>int.class</b>, ...)
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// 
        /// </exception>
        Index CreateRandomAccessIndex(Type type, bool unique);
#endif
		
#if !COMPACT_NET_FRAMEWORK
        /// <summary> Create new compound index optimized for access by element position.
        /// </summary>
        /// <param name="types">types of components of compund key
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
        /// specified key type is not supported by implementation.
        /// </exception>
#if USE_GENERICS
        CompoundIndex<V> CreateRandomAccessIndex<V>(Type[] types, bool unique) where V:class;
#else
        CompoundIndex    CreateRandomAccessIndex(Type[] types, bool unique);
#endif
#endif
		
#if USE_GENERICS
        /// <summary> 
        /// Create new field index optimized for access by element position.
        /// K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex<K,V> CreateRandomAccessFieldIndex<K,V>(string fieldName, bool unique) where V:class;
#else
        /// <summary> 
        /// Create new field index optimized for access by element position.
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex CreateRandomAccessFieldIndex(Type type, string fieldName, bool unique);
#endif

#if USE_GENERICS
        /// <summary> 
        /// Create new field index optimized for access by element position.
        /// K parameter specifies key type, V - associated object type.
        /// </summary>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex<K,V> CreateRandomAccessFieldIndex<K,V>(string fieldName, bool unique, bool caseInsensitive) where V:class;
#else
        /// <summary> 
        /// Create new field index optimized for access by element position.
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldName">name of the index field. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        FieldIndex CreateRandomAccessFieldIndex(Type type, string fieldName, bool unique, bool caseInsensitive);
#endif

#if !COMPACT_NET_FRAMEWORK
#if USE_GENERICS
        /// <summary> 
        /// Create new multi-field index optimized for access by element position.
        /// </summary>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex<V> CreateRandomAccessFieldIndex<V>(string[] fieldNames, bool unique) where V:class;
#else
        /// <summary> 
        /// Create new multi-field index optimized for access by element position.
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex CreateRandomAccessFieldIndex(Type type, string[] fieldNames, bool unique);
#endif

#if USE_GENERICS
        /// <summary> 
        /// Create new multi-field index optimized for access by element position.
        /// </summary>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex<V> CreateRandomAccessFieldIndex<V>(string[] fieldNames, bool unique, bool caseInsensitive) where V:class;
#else
        /// <summary> 
        /// Create new multi-field index optimized for access by element position.
        /// </summary>
        /// <param name="type">objects of which type (or derived from which type) will be included in the index
        /// </param>
        /// <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <b>type</b>
        /// </param>
        /// <param name="unique">whether index is unique (duplicate value of keys are not allowed)
        /// </param>
        /// <param name="caseInsensitive">whether index is case insinsitive (ignored for non-string keys)
        /// </param>
        /// <returns>persistent object implementing field index
        /// </returns>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
        /// StorageError(StorageError.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
        /// </exception>
        MultiFieldIndex CreateRandomAccessFieldIndex(Type type, string[] fieldNames, bool unique, bool caseInsensitive);
#endif
#endif

        /// <summary>
        /// Create new bit index. Bit index is used to select object 
        /// with specified set of (boolean) properties.
        /// </summary>
        /// <returns>persistent object implementing bit index</returns>
#if USE_GENERICS
        BitIndex<T> CreateBitIndex<T>() where T:class;
#else
        BitIndex CreateBitIndex();
#endif

        /// <summary>
        /// Create new spatial index with integer coordinates
        /// </summary>
        /// <returns>
        /// persistent object implementing spatial index
        /// </returns>
#if USE_GENERICS
        SpatialIndex<T> CreateSpatialIndex<T>() where T:class;
#else
        SpatialIndex CreateSpatialIndex();
#endif

        /// <summary>
        /// Create new R2 spatial index
        /// </summary>
        /// <returns>
        /// persistent object implementing spatial index
        /// </returns>
#if USE_GENERICS
        SpatialIndexR2<T> CreateSpatialIndexR2<T>() where T:class;
#else
        SpatialIndexR2 CreateSpatialIndexR2();
#endif

        /// <summary>
        /// Create new Rn spatial index
        /// </summary>
        /// <returns>
        /// persistent object implementing spatial index
        /// </returns>
#if USE_GENERICS
        SpatialIndexRn<T> CreateSpatialIndexRn<T>() where T:class;
#else
        SpatialIndexRn CreateSpatialIndexRn();
#endif

        /// <summary>
        /// Create new sorted collection with specified comparator
        /// </summary>
        /// <param name="comparator">comparator class specifying order in the collection</param>
        /// <param name="unique"> whether collection is unique (members with the same key value are not allowed)</param>
        /// <returns> persistent object implementing sorted collection</returns>
#if USE_GENERICS
        SortedCollection<K,V> CreateSortedCollection<K,V>(PersistentComparator<K,V> comparator, bool unique) where V:class;
#else
        SortedCollection CreateSortedCollection(PersistentComparator comparator, bool unique);
#endif

        /// <summary>
        /// Create new sorted collection. Members of this collections should implement 
        /// <b>System.IComparable</b> interface and make it possible to compare 
        /// collection members with each other as well as with serch key.
        /// </summary>
        /// <param name="unique"> whether collection is unique (members with the same key value are not allowed)</param>
        /// <returns> persistent object implementing sorted collection</returns>
#if USE_GENERICS
        SortedCollection<K,V> CreateSortedCollection<K,V>(bool unique) where V:class,IComparable<K>,IComparable<V>;
#else
        SortedCollection CreateSortedCollection(bool unique);
#endif

        /// <summary>
        /// Create new object set
        /// </summary>
        /// <returns>
        /// empty set of persistent objects
        /// </returns>
#if USE_GENERICS
        Perst.ISet<T> CreateSet<T>() where T:class;
#else
        ISet CreateSet();
#endif

        /// <summary>
        /// Create new object multiset (bag)
        /// </summary>
        /// <returns>
        /// empty bag of persistent objects
        /// </returns>
#if USE_GENERICS
        Perst.ISet<T> CreateBag<T>() where T:class;
#else
        ISet CreateBag();
#endif

        /// <summary> Create one-to-many link.
        /// </summary>
        /// <returns>new empty link, new members can be added to the link later.
        /// 
        /// </returns>
#if USE_GENERICS
        Link<T> CreateLink<T>() where T:class;
#else
        Link CreateLink();
#endif
		
        /// <summary> Create one-to-many link with specified initial size.
        /// </summary>
        /// <param name="initialSize">initial size of the array</param>
        /// <returns>new link with specified size
        /// 
        /// </returns>
#if USE_GENERICS
        Link<T> CreateLink<T>(int initialSize) where T:class;
#else
        Link CreateLink(int initialSize);
#endif
		
        /// <summary>  Create new scalable set references to persistent objects.
        /// This container can efficiently store small number of references as well 
        /// as very large number references. When number of members is small, 
        /// Link class is used to store set members. When number of members exceed 
        /// some threshold, PersistentSet (based on B-Tree) is used instead.
        /// </summary>
        /// <returns>new empty set, new members can be added to the set later.
        /// </returns>
#if USE_GENERICS
        Perst.ISet<T> CreateScalableSet<T>() where T:class;
#else
        ISet CreateScalableSet();
#endif
		
        /// <summary>  Create new scalable set references to persistent objects.
        /// This container can efficiently store small number of references as well 
        /// as very large number references. When number of members is small, 
        /// Link class is used to store set members. When number of members exceed 
        /// some threshold, PersistentSet (based on B-Tree) is used instead.
        /// </summary>
        /// <param name="initialSize">initial size of the sety</param>
        /// <returns>new empty set, new members can be added to the set later.
        /// </returns>
#if USE_GENERICS
        Perst.ISet<T> CreateScalableSet<T>(int initialSize) where T:class;
#else
        ISet CreateScalableSet(int initialSize);
#endif
		
        /// <summary>
        /// Create new peristent list. Implementation of this list is based on B-Tree so it can efficiently
        /// handle large number of objects but in case of very small list memory overhead is too high.
        /// </summary>
        /// <returns>persistent object implementing list</returns>
        ///
#if USE_GENERICS
        IPersistentList<T> CreateList<T>() where T:class;
#else
        IPersistentList CreateList();
#endif

        /// <summary>
        /// Create new scalable list of persistent objects.
        /// This container can efficiently handle small lists as well as large lists                                                                          
        /// When number of members is small, Link class is used to store set members.
        /// When number of members exceeds some threshold, PersistentList (based on B-Tree)                                                                                                                     
        /// is used instead.
        /// </summary>
        /// <returns>scalable set implementation</returns>
        ///
#if USE_GENERICS
        IPersistentList<T> CreateScalableList<T>() where T:class;
#else
        IPersistentList CreateScalableList();
#endif

        /// <summary>
        /// Create new scalable list of persistent objects.
        /// This container can efficiently handle small lists as well as large lists 
        /// When number of members is small, Link class is used to store set members.
        /// When number of members exceeds some threshold, PersistentList (based on B-Tree)
        /// is used instead.
        /// </summary>
        /// <param name="initialSize">initial size of the list</param>
        /// <returns>scalable set implementation</returns>
        ///
#if USE_GENERICS
        IPersistentList<T> CreateScalableList<T>(int initialSize) where T:class;
#else
        IPersistentList CreateScalableList(int initialSize);
#endif

#if USE_GENERICS
        /// <summary>
        /// Create hierarhical hash table. Levels of tree are added on demand.
        /// </summary>
        /// <returns>persistent hash table</returns>
        ///
        IPersistentMap<K,V> CreateHash<K,V>() where V:class;
#else
        /// <summary>
        /// Create hierarhical hash table. Levels of tree are added on demand.
        /// </summary>
        /// <returns>scalable persistent map implementation</returns>
        ///
        IPersistentMap CreateHash();
#endif

#if USE_GENERICS
        /// <summary>
        /// Create hierarhical hash table. Levels of tree are added on demand.
        /// </summary>
        /// <param name="pageSize">Number of elements in hash tree node</param>
        /// <param name="loadFactor">Maximal collision chain lengthe</param>
        /// <returns>persistent hash table</returns>
        ///
        IPersistentMap<K,V> CreateHash<K,V>(int pageSize, int loadFactor) where V:class;
#else
        /// <summary>
        /// Create hierarhical hash table. Levels of tree are added on demand.
        /// </summary>
        /// <param name="pageSize">Number of elements in hash tree node</param>
        /// <param name="loadFactor">Maximal collision chain lengthe</param>
        /// <returns>scalable persistent map implementation</returns>
        ///
        IPersistentMap CreateHash(int pageSize, int loadFactor);
#endif

        /// <summary>
        /// Create object bitmap (each bit corresponds to OID). This bitmap can be used to merge results of multiples searches.
        /// </summary>
        /// <param name="e">persistent objects enumerator which is used to construct bitmap</param>
        /// <returns>bitmap for this selection</returns>
        Bitmap CreateBitmap(IEnumerator e);


#if USE_GENERICS
        /// <summary>
        /// Create scalable persistent map.
        /// This container can efficiently handle both small and large number of members.
        /// For small maps, implementation  uses sorted array. For large maps - B-Tree.
        /// </summary>
        /// <returns>scalable persistent map implementation</returns>
        ///
        IPersistentMap<K,V> CreateMap<K,V>() where K:IComparable where V:class;
#else
        /// <summary>
        /// Create scalable persistent map.
        /// This container can efficiently handle both small and large number of members.
        /// For small maps, implementation  uses sorted array. For large maps - B-Tree.
        /// </summary>
        /// <param name="keyType">Type of map key</param>
        /// <returns>scalable persistent map implementation</returns>
        ///
        IPersistentMap CreateMap(Type keyType);
#endif

#if USE_GENERICS
        /// <summary>
        /// Create scalable persistent map.
        /// This container can efficiently handle both small and large number of members.
        /// For small maps, implementation  uses sorted array. For large maps - B-Tree.
        /// </summary>
        /// <param name="initialSize">initial size of the list</param>
        /// <returns>scalable persistent map implementation</returns>
        ///
        IPersistentMap<K,V> CreateMap<K,V>(int initialSize) where K:IComparable where V:class;
#else
        /// <summary>
        /// Create scalable persistent map.
        /// This container can efficiently handle both small and large number of members.
        /// For small maps, implementation  uses sorted array. For large maps - B-Tree.
        /// </summary>
        /// <param name="keyType">Type of map key</param>
        /// <param name="initialSize">initial size of the list</param>
        /// <returns>scalable persistent map implementation</returns>
        ///
        IPersistentMap CreateMap(Type keyType, int initialSize);
#endif

        /// <summary> Create dynamcially extended array of reference to persistent objects.
        /// It is inteded to be used in classes using virtual properties to 
        /// access components of persistent objects.  
        /// </summary>
        /// <returns>new empty array, new members can be added to the array later.
        /// </returns>
#if USE_GENERICS
        PArray<T> CreateArray<T>() where T:class;
#else
        PArray CreateArray();
#endif
		
        /// <summary> Create dynamcially extended array of reference to persistent objects.
        /// It is inteded to be used in classes using virtual properties to 
        /// access components of persistent objects.  
        /// </summary>
        /// <param name="initialSize">initially allocated size of the array</param>
        /// <returns>new empty array, new members can be added to the array later.
        /// </returns>
#if USE_GENERICS
        PArray<T> CreateArray<T>(int initialSize) where T:class;
#else
        PArray CreateArray(int initialSize);
#endif
		
        /// <summary> Create relation object. Unlike link which represent embedded relation and stored
        /// inside owner object, this Relation object is standalone persisitent object
        /// containing references to owner and members of the relation
        /// </summary>
        /// <param name="owner">owner of the relation
        /// </param>
        /// <returns>object representing empty relation (relation with specified owner and no members), 
        /// new members can be added to the link later.
        /// 
        /// </returns>
#if USE_GENERICS
        Relation<M,O> CreateRelation<M,O>(O owner) where M:class where O:class;
#else
        Relation CreateRelation(object owner);
#endif


        /// <summary>
        /// Create new BLOB. Create object for storing large binary data.
        /// </summary>
        /// <returns>empty BLOB</returns>
        Blob CreateBlob();

        /// <summary>
        /// Create full text search index
        /// </summary>
        /// <param name="helper">helper class which provides method for scanning, stemming and tuning query</param>
        /// <returns>full text search index</returns>
        ///
        FullTextIndex CreateFullTextIndex(FullTextSearchHelper helper);

        ///
        /// Create full text search index with default helper
        /// <returns>full text search index</returns>
        ///
        FullTextIndex CreateFullTextIndex();

#if USE_GENERICS
        /// <summary>
        /// Create new time series object. 
        /// </summary>
        /// <param name="blockSize">number of elements in the block</param>
        /// <param name="maxBlockTimeInterval">maximal difference in system ticks (100 nanoseconds) between timestamps 
        /// of the first and the last elements in a block. 
        /// If value of this parameter is too small, then most blocks will contains less elements 
        /// than preallocated. 
        /// If it is too large, then searching of block will be inefficient, because index search 
        /// will select a lot of extra blocks which do not contain any element from the 
        /// specified range.
        /// Usually the value of this parameter should be set as
        /// (number of elements in block)*(tick interval)*2. 
        /// Coefficient 2 here is used to compencate possible holes in time series.
        /// For example, if we collect stocks data, we will have data only for working hours.
        /// If number of element in block is 100, time series period is 1 day, then
        /// value of maxBlockTimeInterval can be set as 100*(24*60*60*10000000L)*2
        /// </param>
        /// <returns>new empty time series</returns>
        TimeSeries<T> CreateTimeSeries<T>(int blockSize, long maxBlockTimeInterval) where T:TimeSeriesTick;
#else
        /// <summary>
        /// Create new time series object. 
        /// </summary>
        /// <param name="blockClass">class derived from TimeSeriesBlock</param>
        /// <param name="maxBlockTimeInterval">maximal difference in system ticks (100 nanoseconds) between timestamps 
        /// of the first and the last elements in a block. 
        /// If value of this parameter is too small, then most blocks will contains less elements 
        /// than preallocated. 
        /// If it is too large, then searching of block will be inefficient, because index search 
        /// will select a lot of extra blocks which do not contain any element from the 
        /// specified range.
        /// Usually the value of this parameter should be set as
        /// (number of elements in block)*(tick interval)*2. 
        /// Coefficient 2 here is used to compencate possible holes in time series.
        /// For example, if we collect stocks data, we will have data only for working hours.
        /// If number of element in block is 100, time series period is 1 day, then
        /// value of maxBlockTimeInterval can be set as 100*(24*60*60*10000000L)*2
        /// </param>
        /// <returns>new empty time series</returns>
        TimeSeries CreateTimeSeries(Type blockClass, long maxBlockTimeInterval);
#endif
		
        /// <summary>
        /// Create PATRICIA trie (Practical Algorithm To Retrieve Information Coded In Alphanumeric)
        /// Tries are a kind of tree where each node holds a common part of one or more keys. 
        /// PATRICIA trie is one of the many existing variants of the trie, which adds path compression 
        /// by grouping common sequences of nodes together.
        /// This structure provides a very efficient way of storing values while maintaining the lookup time 
        /// for a key in O(N) in the worst case, where N is the length of the longest key. 
        /// This structure has it's main use in IP routing software, but can provide an interesting alternative 
        /// to other structures such as hashtables when memory space is of concern.
        /// </summary>
        /// <returns>created PATRICIA trie</returns>
        ///
#if USE_GENERICS
        PatriciaTrie<T> CreatePatriciaTrie<T>() where T:class;
#else
        PatriciaTrie CreatePatriciaTrie();
#endif


#if USE_GENERICS
        /// <summary>
        /// Create new generic set of objects
        /// </summary>
        /// <returns>
        /// empty set of persistent objects
        /// </returns>
        Perst.ISet<object> CreateSet();
        
        /// <summary>
        /// Create new generic link
        /// </summary>
        /// <returns>
        /// link of IPersistent references
        /// </returns>
        Link<object> CreateLink();
		
        /// <summary>
        /// Create new generic link with specified initial size
        /// </summary>
        /// <param name="initialSize">Initial link size</param>
        /// <returns>
        /// link of IPersistent references
        /// </returns>
        Link<object> CreateLink(int initialSize);

        /// <summary>
		/// Create new generic array of reference
        /// </summary>
        /// <returns>
        /// array of IPersistent references
        /// </returns>
        PArray<object> CreateArray();
		
        /// <summary>
		/// Create new generic array of reference
        /// </summary>
        /// <param name="initialSize">Initial array size</param>
        /// <returns>
        /// array of IPersistent references
        /// </returns>
        PArray<object> CreateArray(int initialSize);
#endif		


        /// <summary> Commit transaction (if needed) and close the storage
        /// </summary>
        void  Close();

        /// <summary> Set threshold for initiation of garbage collection. By default garbage collection is disable (threshold is set to
        /// Int64.MaxValue). If it is set to the value different fro Long.MAX_VALUE, GC will be started each time when
        /// delta between total size of allocated and deallocated objects exceeds specified threashold OR
        /// after reaching end of allocation bitmap in allocator. 
        /// </summary>
        /// <param name="allocatedDelta"> delta between total size of allocated and deallocated object since last GC or storage opening
        /// </param>
        ///
        void SetGcThreshold(long allocatedDelta);

        /// <summary>Explicit start of garbage collector
        /// </summary>
        /// <returns>number of collected (deallocated) objects</returns>
        /// 
        int Gc();

        /// <summary> Export database in XML format 
        /// </summary>
        /// <param name="writer">writer for generated XML document
        /// 
        /// </param>
        void  ExportXML(System.IO.StreamWriter writer);
		
        /// <summary> Import data from XML file
        /// </summary>
        /// <param name="reader">XML document reader
        /// 
        /// </param>
        void  ImportXML(System.IO.TextReader reader);
		
        		
        /// <summary> 
        /// Retrieve object by OID. This method should be used with care because
        /// if object is deallocated, its OID can be reused. In this case
        /// GetObjectByOID will return reference to the new object with may be
        /// different type.
        /// </summary>
        /// <param name="oid">object oid</param>
        /// <returns>reference to the object with specified OID</returns>
        object GetObjectByOID(int oid);

        /// <summary> 
        /// Explicitely make object peristent. Usually objects are made persistent
        /// implicitlely using "persistency on reachability apporach", but this
        /// method allows to do it explicitly. If object is already persistent, execution of
        /// this method has no effect.
        /// </summary>
        /// <param name="obj">object to be made persistent</param>
        /// <returns>OID assigned to the object</returns>
        int MakePersistent(object obj);

        ///
        /// <summary>
        /// Set database property. This method should be invoked before opening database. 
        /// </summary>
        /// <remarks> 
        /// Currently the following boolean properties are supported:
        /// <TABLE><TR><TH>Property name</TH><TH>Parameter type</TH><TH>Default value</TH><TH>Description</TH></TR>
        /// <TR><TD><b>perst.serialize.transient.objects</b></TD><TD>bool</TD><TD>false</TD>
        /// <TD>Serialize any class not derived from IPersistent or IValue using standard Java serialization
        /// mechanism. Packed object closure is stored in database as byte array. Latter the same mechanism is used
        /// to unpack the objects. To be able to use this mechanism, object and all objects referenced from it
        /// should be marked with Serializable attribute and should not contain references
        /// to persistent objects. If such object is referenced from N persistent object, N instances of this object
        /// will be stored in the database and after loading there will be N instances in memory.
        /// </TD></TR>
        /// <TR><TD><b>perst.object.cache.init.size</b></TD><TD>int</TD><TD>1319</TD>
        /// <TD>Initial size of object cache
        /// </TD></TR>
        /// <TR><TD><b>perst.object.cache.kind</b></TD><TD>String</TD><TD>"lru"</TD>
        /// <TD>Kind of object cache. The following values are supported:
        /// "strong", "weak", "pinned", "lru". <B>Strong</B> cache uses strong (normal)                                                                         
        /// references to refer persistent objects. Thus none of loaded persistent objects                                                                                                                                         
        /// can be deallocated by GC. It is possible to explicitely clear object cache using 
        /// <b>Storage.ClearObjectCache()</b> method. <B>Weak</B> and <b>lru</b> caches use weak references. 
        /// But <b>lru</b> cache also pin some number of recently used objects.
        /// Pinned object cache pin in memory all modified objects while using weak referenced for 
        /// non-modified objects. This kind of cache eliminate need in finalization mechanism - all modified
        /// objects are kept in memory and are flushed to the disk only at the end of transaction. 
        /// So the size of transaction is limited by amount of main memory. Non-modified objects are accessed only 
        /// through weak references so them are not protected from GC and can be thrown away.
        /// </TD></TR>
        /// <TR><TD><b>perst.object.index.init.size</b></TD><TD>int</TD><TD>1024</TD>
        /// <TD>Initial size of object index (specifying large value increase initial size of database, but reduce
        /// number of index reallocations)
        /// </TD></TR>
        /// <TR><TD><b>perst.extension.quantum</b></TD><TD>long</TD><TD>1048576</TD>
        /// <TD>Object allocation bitmap extension quantum. Memory is allocate by scanning bitmap. If there is no
        /// large enough hole, then database is extended by the value of dbDefaultExtensionQuantum. 
        /// This parameter should not be smaller than 64Kb.
        /// </TD></TR>
        /// <TR><TD><b>perst.gc.threshold</b></TD><TD>long</TD><TD>long.MaxValue</TD>
        /// <TD>Threshold for initiation of garbage collection. 
        /// If it is set to the value different from long.MaxValue, GC will be started each time 
        /// when delta between total size of allocated and deallocated objects exceeds specified threashold OR                                                                                                                                                                                                                           
        /// after reaching end of allocation bitmap in allocator.
        /// </TD></TR>
        /// <TR><TD><b>perst.code.generation</b></TD><TD>string</TD><TD>async</TD>
        /// <TD>enable or disable dynamic generation of pack/unpack methods for persistent 
        /// classes. Such methods can be generated only for classes with public fields.
        /// Using generated methods instead of .Net reflection API increase speed of
        /// object store/fetch operations, but generation itself takes additional time at 
        /// startup. This parameter can have three values: "sync", "async" (or "true") and 
        /// "disabled" (or "false"). In case of asynchronous methods generation,
        /// it is performed by background thread with low priority. It has minimal influence on
        /// database open time, but if large of the objects are loaded from the database
        /// immediately after database open, then pack.unpack methods may not be ready and
        /// so loading of database takes a longer time.
        /// Synchronous methods inside Storage.Open method. So it can increase time of 
        /// opening database (especially if there are large number of classes in database)
        /// but in this case all pack/unpack methods will be ready after return from Open method.
        /// If runtime code generation is disabled, then no methods are generated.
        /// Runtime code generation is not supported and so is disabled at Compact.Net platform.
        /// </TD></TR>
        /// <TR><TD><b>perst.file.readonly</b></TD><TD>bool</TD><TD>false</TD>
        /// <TD>Database file should be opened in read-only mode.
        /// </TD></TR>
        /// <TR><TD><b>perst.lock.file</b></TD><TD>bool</TD><TD>true</TD>
        /// <TD>Lock database file to prevent concurrent access to the database by 
        ///  more than one application.
        /// </TD></TR>
        /// <TR><TD><b>perst.file.noflush</b></TD><TD>bool</TD><TD>false</TD>
        /// <TD>To not flush file during transaction commit. It will greatly increase performance because
        /// eliminate synchronous write to the disk (when program has to wait until all changed
        /// are actually written to the disk). But it can cause database corruption in case of 
        /// OS or power failure (but abnormal termination of application itself should not cause
        /// the problem, because all data which were written to the file, but is not yet saved to the disk is 
        /// stored in OS file buffers and sooner or later them will be written to the disk)
        /// </TD></TR>
        /// <TR><TD><b>perst.alternative.btree</b></TD><TD>bool</TD><TD>false</TD>
        /// <TD>Use aternative implementation of B-Tree (not using direct access to database
        /// file pages). This implementation should be used in case of serialized per thread transctions.
        /// New implementation of B-Tree will be used instead of old implementation
        /// if "perst.alternative.btree" property is set. New B-Tree has incompatible format with 
        /// old B-Tree, so you could not use old database or XML export file with new indices. 
        /// Alternative B-Tree is needed to provide serializable transaction (old one could not be used).
        /// Also it provides better performance (about 3 times comaring with old implementation) because
        /// of object caching. And B-Tree supports keys of user defined types. 
        /// </TD></TR>
        /// <TR><TD><b>perst.background.gc</b></TD><TD>bool</TD><TD>false</TD>
        /// <TD>Perform garbage collection in separate thread without blocking the main application.                                                 
        /// </TD></TR>
        /// <TR><TD><b>perst.string.encoding</b></TD><TD>String</TD><TD>null</TD>
        /// <TD>Specifies encoding of storing strings in the database. By default Perst stores 
        /// strings as sequence of chars (two bytes per char). If all strings in application are in 
        /// the same language, then using encoding  can significantly reduce space needed
        /// to store string (about two times). But please notice, that this option has influence
        /// on all strings  stored in database. So if you already have some data in the storage
        /// and then change encoding, then it can cause incorrect fetching of strings and even database crash.
        /// </TD></TR>
        /// <TR><TD><b>perst.replication.ack</b></TD><TD>bool</TD><TD>false</TD>
        /// <TD>Request acknowledgement from slave that it receives all data before transaction
        /// commit. If this option is not set, then replication master node just writes
        /// data to the socket not warring whether it reaches slave node or not.
        /// When this option is set to true, master not will wait during each transaction commit acknowledgement
        /// from slave node. Please notice that this option should be either set or not set both
        /// at slave and master node. If it is set only on one of this nodes then behavior of
        /// the system is unpredicted. This option can be used both in synchronous and asynchronous replication
        /// mode. The only difference is that in first case main application thread will be blocked waiting
        /// for acknowledgment, while in the asynchronous mode special replication thread will be blocked
        /// allowing thread performing commit to proceed.
        /// </TD></TR>
        /// <TR><TD><b>perst.concurrent.iterator</b></TD><TD>bool</TD><TD>false</TD>
        /// <TD>By default iterator will throw ConcurrentModificationException if iterated collection
        /// was changed outside iterator, when the value of this property is true then iterator will 
        /// try to restore current position and continue iteration
        /// </TD></TR>
        /// <TR><TD><b>perst.slave.connection.timeout</b></TD><TD>int</TD><TD>60</TD>
        /// <TD>Timeout in seconds during which master node will try to establish connection with slave node. 
        /// If connection can not be established within specified time, then master will not perform 
        /// replication to this slave node
        /// </TD></TR>
        /// <TR><TD><b>perst.page.pool.lru.limit</b></TD><TD>long</TD><TD>1L &lt;&lt; 60</TD>
        /// <TD>Set boundary for caching database pages in page pool. 
        /// By default Perst is using LRU algorithm for finding candidate for replacement.
        /// But for example for BLOBs this strategy is not optimal and fetching BLOB can
        /// cause flushing the whole page pool if LRU discipline is used. And with high
        /// probability fetched BLOB pages will no be used any more. So it is preferable not
        /// to cache BLOB pages at all (throw away such page immediately when it is not used any more).
        /// This parameter in conjunction with custom allocator allows to disable caching
        /// for BLOB objects. If you set value of "perst.page.lru.scope" property equal
        /// to base address of custom allocator (which will be used to allocate BLOBs), 
        /// then page containing objects allocated by this allocator will not be cached in page pool.
        /// </TD></TR>
        /// <TR><TD><code>perst.multiclient.support</code></TD><TD>bool</TD><TD>false</TD>
        /// <TD>Supports access to the same database file by multiple applications.
        /// In this case Perst will use file locking to synchronize access to the database file.
        /// An application MUST wrap any access to the database with BeginThreadThreansaction/EndThreadTransaction 
        /// methods. For read only access use TransactionMode.ReadOnly mode and if transaction may modify database then
        /// TransactionMode.ReadWrite mode should be used.
        /// </TD></TR>
        /// <TR><TD><code>perst.isolated.storage.init.quota</code></TD><TD>long</TD><TD>0</TD>
        /// <TD>Initial request to increased isolated storage quota for Silverlight applications.
        /// Zero value means that no quota increase is requested by Perst (application can use default quota
        /// or request to increase quote itself).
        /// </TD></TR>
        /// <TR><TD><code>perst.isolated.storage.quota.increase.quantum</code></TD><TD>long</TD><TD>0</TD>
        /// <TD>Quantum of increasing isolated storage quota for Silverlight applications.      
        /// Perst will try to increase quota when there is no enough available space in isolated storage.
        /// This parameter provides increase of isolated storage size by fixed quantum.
        /// To reduce number of user's confirmation to increase quota use 
        /// <code>perst.isolated.storage.quota.increase.percent</code> parameter which provides geometric
        /// increase of storage size. By default there is no fixed extension quantum and 
        /// requested quota is doubled.
        /// </TD></TR>
        /// <TR><TD><code>perst.isolated.storage.quota.increase.percent</code></TD><TD>int</TD><TD>100</TD>
        /// <TD>Percent of increasing isolated storage quota for Silverlight applications.      
        /// Perst will try to increase quota when there is no enough available space in isolated storage.
        /// This parameter provides geometric increase of isolated storage size on specified percent.
        /// Default value 100 cause doubling of requested quota.
        /// </TD></TR>
        /// <TR><TD><code>perst.file.extension.quantum</code></TD><TD>long</TD><TD>2Mb</TD>
        /// <TD>Quantum of increasing database file size (extension file size by quantum may help to reduce file fragmentation)
        /// </TD></TR>
        /// <TR><TD><code>perst.file.extension.percent</code></TD><TD>int</TD><TD>10</TD>
        /// <TD>Percent of increasing database file size (geometric file size extension may help to reduce file fragmentation)
        /// </TD></TR>
        /// <TR><TD><code>perst.file.buffer.size</code></TD><TD>int</TD><TD>1Mb</TD>
        /// <TD>Size of file buffer passed in FileStream constructor
        /// </TD></TR>
        /// <TR><TD><code>perst.reload.objects.on.rollback</code></TD><TD>bool</TD><TD>false</TD>
        /// <TD>By default, Perst doesn't reload modified objects after a transaction
        /// rollback. In this case, the programmer should not use references to the
        /// persistent objects stored in program variables. Instead, the application
        /// should fetch the object tree from the beginning, starting from obtaining the
        /// root object using the Storage.getRoot method.
        ///
        /// Setting the "perst.reload.objects.on.rollback" property instructs Perst to
        /// reload all objects modified by the aborted (rolled back) transaction. It
        /// takes additional processing time, but in this case it is not necessary to
        /// ignore references stored in variables, unless they point to the objects
        /// created by this transactions (which were invalidated when the transaction
        /// was rolled back). Unfortunately, there is no way to prohibit access to such
        /// objects or somehow invalidate references to them. So this option should be
        /// used with care.
        /// </TD></TR>
        /// <TR><TD><code>perst.reuse.oid</code></TD><TD>bool</TD><TD>true</TD>
        /// <TD>This parameter allows to disable reusing OID of deallocated objects.
        /// It can simplify debugging of application performing explicit deallocation of objects
        /// (not using garbage collection). Explicit object deallocation can cause "dangling references"
        /// problem - when there are live references to the deallocated object.
        /// Access to such object should cause <code>StorageError(ErrorCode.DELETED_OBJECT)</code> exception.
        /// But if OID of the object can be reused and assigned to some newly deallocated object, 
        /// then we will get type cast or field access errors when try to work with this object.
        /// In the worst case OID will be reused by the object of the same type - then application
        /// will not notice that referenced object was substituted.
        /// Disabling reuse of OID allows to eliminate such unpredictable behavior - 
        /// access to the deallocated object will always cause <code>StorageError(ErrorCode.DELETED_OBJECT)</code> exception. 
        /// But please notice that disabling reuse of OID for a long time and intensive allocation/deallocation
        /// of objects can cause exhaustion of OID space (2Gb).
        /// </TD></TR>
        /// <TR><TD><code>perst.global.class.extent</code></TD><TD>bool</TD><TD>true</TD>
        /// <TD>This parameter is used by Database class in "auto register table" mode. 
        /// In this mode Perst automatically creates indices (class extents) for all derived classes of the inserted object 
        /// if there are no such class extents yet. It include class extent for Perstistent class allowing to enumerate all
        /// objects in the storage. If such list is not needed, then this option can be set to false to 
        /// eliminate extra index maintenance overhead.
        /// </TD></TR>
        /// <TR><TD><code>perst.search.base.classes</code></TD><TD>bool</TD><TD>true</TD>
        /// <TD>This parameter is used by Database class. If there is no table (class extent) corresponding
        /// to the requested class, then Perst tries to locate class extend for base class and so on.
        /// By setting this property to false it is possible to prohibit lookup of base classes.
        /// Please notice that lookup on base classes is also not performed if "auto register table" mode is active. 
        /// </TD></TR>
        /// </TABLE>
        /// </remarks>
        /// <param name="name">name of the property</param>
        /// <param name="val">value of the property</param>
        ///
        void SetProperty(string name, object val);

        ///
        /// <summary>Set database properties. This method should be invoked before opening database. 
        /// For list of supported properties please see <see cref="SetProperty">setProperty</see>. 
        /// All not recognized properties are ignored.
        /// </summary>
        /// <param name="props">collections with storage properties</param>
        ///
        void SetProperties(Hashtable props);

        ///
        /// <summary>Get property value.
        /// </summary>
        /// <param name="name">property name</param>
        /// <returns>value of the property previously assigned by setProperty or setProperties method
        /// or <b>null</b> if property was not set
        /// </returns>
        ///
        object GetProperty(string name);

        ///
        /// <summary>
        /// Get all set properties
        /// </summary>
        /// <returns>all properties set by setProperty or setProperties method
        /// </returns>
        ///
        Hashtable GetProperties();
 
        /// <summary>
        /// Get SQL optimizer parameters.
        /// It is possible to tune these parameters by updating fields of this object.
        /// </summary>
        SqlOptimizerParameters SqlOptimizerParams
        { 
            get;
        }

        /// <summary>
        /// Merge results of several index searches. This method efficiently merge selections without loading objects themselve
        /// </summary>
        /// <param name="selections">Selections to be merged</param>
        /// <returns>Enumerator through merged result</returns>
#if USE_GENERICS
        IEnumerator<T> Merge<T>(IEnumerator<T>[] selections) where T:class;
#else
        IEnumerator Merge(IEnumerator[] selections);
#endif

        /// <summary>
        /// Join results of several index searches. This method efficiently join selections without loading objects themselve
        /// </summary>
        /// <param name="selections">Selections to be joined</param>
        /// <returns>Enumerator through joined result</returns>
#if USE_GENERICS
        IEnumerator<T> Join<T>(IEnumerator<T>[] selections) where T:class;
#else
        IEnumerator Join(IEnumerator[] selections);
#endif
        /// <summary>
        /// Storage listener.
        /// </summary>
        ///
        StorageListener Listener 
        {
            get;
            set;
        }

        /// <summary>
        /// Set class loader. This class loader will be used to locate classes for 
        /// loaded class descriptors. If class loader is not specified or
        /// it did find the class, then class will be searched in all active assemblies
        /// </summary>
        ClassLoader Loader {get; set; }


#if COMPACT_NET_FRAMEWORK || SILVERLIGHT
        /// <summary>
        /// Compact.NET framework doesn't allow to get list of assemblies loaded
        /// in application domain. Without it I do not know how to locate
        /// class from foreign assembly by name. 
        /// Assembly which creates Storage is automatically registered.
        /// Other assemblies has to explicitely registered by programmer.
        /// </summary>
        /// <param name="assembly">registered assembly</param>
        void RegisterAssembly(System.Reflection.Assembly assembly);
#else
        /// <summary>
        /// Create persistent class wrapper. This wrapper will implement virtual properties
        /// defined in specified class or interface, performing transparent loading and storing of persistent object
        /// </summary>
        /// <param name="type">Class or interface type of instantiated object</param>
        /// <returns>Wrapper for the specified class, implementing all virtual properties defined
        /// in it
        /// </returns>
        object CreateClass(Type type);
#endif

        /// <summary>
        /// Begin per-thread transaction. Three types of per-thread transactions are supported: 
        /// exclusive, cooperative and serializable. In case of exclusive transaction, only one 
        /// thread can update the database. In cooperative mode, multiple transaction can work 
        /// concurrently and commit() method will be invoked only when transactions of all threads
        /// are terminated. Serializable transactions can also work concurrently. But unlike
        /// cooperative transaction, the threads are isolated from each other. Each thread
        /// has its own associated set of modified objects and committing the transaction will cause
        /// saving only of these objects to the database.To synchronize access to the objects
        /// in case of serializable transaction programmer should use lock methods
        /// of IResource interface. Shared lock should be set before read access to any object, 
        /// and exclusive lock - before write access. Locks will be automatically released when
        /// transaction is committed (so programmer should not explicitly invoke unlock method)
        /// In this case it is guaranteed that transactions are serializable.
        /// It is not possible to use <b>IPersistent.store()</b> method in
        /// serializable transactions. That is why it is also not possible to use Index and FieldIndex
        /// containers (since them are based on B-Tree and B-Tree directly access database pages
        /// and use <b>store()</b> method to assign OID to inserted object. 
        /// You should use <b>SortedCollection</b> based on T-Tree instead or alternative
        /// B-Tree implemenataion (set "perst.alternative.btree" property).
        /// </summary>
        /// <param name="mode"><b>TransactionMode.Exclusive</b>,  <b>TransactionMode.Cooperative</b>,
        /// <b>TransactionMode.ReplicationSlave</b> or <b>TransactionMode.Serializable</b>
        /// </param>
        void BeginThreadTransaction(TransactionMode mode);
    
        /// <summary>
        /// End per-thread transaction started by beginThreadTransaction method.
        /// <ul>
        /// <li>If transaction is <i>exclusive</i>, this method commits the transaction and
        /// allows other thread to proceed.</li><li>
        /// If transaction is <i>serializable</i>, this method commits sll changes done by this thread
        /// and release all locks set by this thread.</li><li>     
        /// If transaction is <i>cooperative</i>, this method decrement counter of cooperative
        /// transactions and if it becomes zero - commit the work</li></ul>
        /// </summary>
        void EndThreadTransaction();

        /// <summary>
        /// End per-thread cooperative transaction with specified maximal delay of transaction
        /// commit. When cooperative transaction is ended, data is not immediately committed to the
        /// disk (because other cooperative transaction can be active at this moment of time).
        /// Instead of it cooperative transaction counter is decremented. Commit is performed
        /// only when this counter reaches zero value. But in case of heavy load there can be a lot of
        /// requests and so a lot of active cooperative transactions. So transaction counter never reaches zero value.
        /// If system crash happens a large amount of work will be lost in this case. 
        /// To prevent such scenario, it is possible to specify maximal delay of pending transaction commit.
        /// In this case when such timeout is expired, new cooperative transaction will be blocked until
        /// transaction is committed.
        /// </summary>
        /// <param name="maxDelay">maximal delay in milliseconds of committing transaction.  Please notice, that Perst could 
        /// not force other threads to commit their cooperative transactions when this timeout is expired. It will only
        /// block new cooperative transactions to make it possible to current transaction to complete their work.
        /// If <b>maxDelay</b> is 0, current thread will be blocked until all other cooperative trasnaction are also finished
        /// and changhes will be committed to the database.
        /// </param>
        void EndThreadTransaction(int maxDelay);
   
        /// <summary>
        /// Check if nested thread transaction is active
        /// </summary>       
        bool IsInsideThreadTransaction { get; }

        /// <summary>
        /// Rollback per-thread transaction. It is safe to use this method only for exclusive transactions.
        /// In case of cooperative transactions, this method rollback results of all transactions.
        /// </summary>
        void RollbackThreadTransaction();

        /// <summary>
        /// Start serializable transaction.
        /// This call is equivalent to <code>BeginThreadTransaction(TransactionMode.Serializable)</code>
        /// </summary>
        void BeginSerializableTransaction();

        /// <summary>
        /// Commit serializable transaction. This call is equivalent to <code>EndThreadTransaction</code>
        /// but it checks that serializable transaction was pereviously started using 
        /// BeginSerializableTransaction() method
        /// </summary>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.NOT_IN_TRANSACTION) 
        /// if this method is invoked outside serializable transaction body
        /// </exception>
        void CommitSerializableTransaction();

        /// <summary>
        /// Rollback serializable transaction. This call is equivalent to <code>RollbackThreadTransaction</code>
        /// but it checks that serializable transaction was pereviously started using 
        /// BeginSerializableTransaction() method
        /// </summary>
        /// <exception cref="Perst.StorageError">StorageError(StorageError.ErrorCode.NOT_IN_TRANSACTION) 
        /// if this method is invoked outside serializable transaction body
        /// </exception>
        void RollbackSerializableTransaction();

        /// <summary>
        /// Get database memory dump. This function returns hashmap which key is classes
        /// of stored objects and value - MemoryUsage object which specifies number of instances
        /// of particular class in the storage and total size of memory used by these instance.
        /// Size of internal database structures (object index, memory allocation bitmap) is associated with 
        /// <b>Storage</b> class. Size of class descriptors  - with <b>System.Type</b> class.
        /// <p>This method traverse the storage as garbage collection do - starting from the root object
        /// and recursively visiting all reachable objects. So it reports statistic only for visible objects.
        /// If total database size is significantly larger than total size of all instances reported
        /// by this method, it means that there is garbage in the database. You can explicitly invoke
        /// garbage collector in this case.</p> 
        /// </summary>
        Hashtable GetMemoryDump();

        /// <summary>
        /// Get total size of all allocated objects in the database
        /// </summary>
        long UsedSize {get;}

        /// <summary>
        /// Get size of the database
        /// </summary>
        long DatabaseSize {get;}

        /// <summary>
        /// Get maximal OID of object in the storage
        /// </summary>
        int MaxOid {get;}

        /// <summary>
        /// Register custom allocator for specified class. Instances of this and derived classes 
        /// will be allocated in the storage using specified allocator. 
        /// </summary>
        /// <param name="cls">class of the persistent object which instances will be allocated using this allocator</param>
        /// <param name="allocator">custom allocator</param>
        void RegisterCustomAllocator(Type cls, CustomAllocator allocator);

        /// <summary>
        /// Create bitmap custom allocator
        /// </summary>
        /// <param name="quantum">size in bytes of allocation quantum. Should be power of two.</param>
        /// <param name="baseAddr">base address for allocator (it should match offset of multifile segment)</param>
        /// <param name="extension">size by which space mapped by allocator is extended each time when 
        /// no suitable hole is found in bitmap (it should be large enough to improve allocation speed and locality                                                                                                     
        /// of references)</param>
        /// <param name="limit">maximal size of memory allocated by this allocator (pass Long.MAX_VALUE if you do not 
        /// want to limit space)</param>
        /// <returns> created allocator</returns>
        CustomAllocator CreateBitmapAllocator(int quantum, long baseAddr, long extension, long limit);

        /// <summary>
        /// Set custom serializer used fot packing/unpacking fields of persistent objects which types implemplemet 
        /// CustomSerializable interface
        /// </summary>
        void SetCustomSerializer(CustomSerializer serializer);

        /// <summary>
        /// Clear database object cache. This method can be used with "strong" object cache to avoid memory overflow.
        /// It is no valid to invoke this method when there are some uncommitted changes in the database
        /// (some modified objects). Also all variables containing references to persistent object should be reset after
        /// invocation of this method - it is not correct to accessed object directly though such variables, objects
        /// has to be reloaded from the storage
        /// </summary>
        void ClearObjectCache();

        /// <summary>
        /// Store object in storage
        /// </summary>
        /// <param name="obj">stored object</param>
        void Store(object obj);

        /// <summary>
        /// Mark object as been modified
        /// </summary>
        /// <param name="obj">modified object</param>
        void Modify(object obj);

        /// <summary>
        /// Load raw object
        /// </summary>
        /// <param name="obj">loaded object</param>
        void Load(object obj);

        /// <summary>
        /// Deallocaste object
        /// </summary>
        /// <param name="obj">deallocated object</param>
        void Deallocate(object obj);

        /// <summary>
        /// Get object identifier
        /// </summary>
        /// <param name="obj">inspected object</param>        
        int GetOid(object obj);

        /// <summary>
        /// Enable or disable recursive loading for specified class.
        /// Recursive loading can be also controlled by overriding RecursiveLoading method of
        /// Persistent class, but if class is not derived from Persistent base class and
        /// not implementing IPersistent interface, this method can be used to control 
        /// recursive loading. 
        /// </summary>
        /// <param name="type">Class for which recursive loading policy is specified.
        /// By default recursive loading is enabled for all classes. Disabling recursive loading for some
        /// class also affect all derived classes unless policy is explicitly specified for such class.
        /// </param>        
        /// <param name="enabled">Whether recursive loading is enabled or disabled for this class</param>        
        /// <returns>previous status of recursive loading policy for the specified class</returns>
        bool SetRecursiveLoading(Type type, bool enabled);
        
#region Internal methods		
        void  deallocateObject(object obj);
		
        void  storeObject(object obj);
		
        void  storeFinalizedObject(object obj);
		
        void  loadObject(object obj);
		
        void  modifyObject(object obj);

        bool  lockObject(object obj);
#endregion
    }
}