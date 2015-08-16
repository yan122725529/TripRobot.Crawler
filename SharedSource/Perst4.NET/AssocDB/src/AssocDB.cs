namespace Perst.Assoc
{
    using System;
    using Perst;
    using Perst.FullText;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Main class of this package. Emulation of associative database on top of Perst. 
    /// Strictly speaking AssocDB doesn't implement pure associative data model, although
    /// it provides some notions of this model (items, links,...)
    /// The main goal of this database is to efficient storage for object with statically known format and complex relations between
    /// them. It can be used to store XML data, objects with user-defined fields, ...
    /// AssocDB allows to fetch all attributes of an objects in one read operation without some kind of joins.
    /// It automatically index all object attributes to provide fast search (simple query language is used).
    /// All kind of relation between objects ore provided: one-to-one, one-to-many, many-to-many.
    /// AssocDB supports small relations (tens of members) as well as very large relation (millions of members).
    /// Small relations are embedded inside object to reduce storage overhead and their increase access time.
    /// Large relations are implemented using B-Tree. AssocDB automatically choose proper representation.
    /// Inverse links are automatically maintained, enforcing consistency of references.
    /// AssocDB provides MURSIW (multiple readers single writer) isolation model.
    /// It means that only one transaction can update the database at each moment of time, but multiple transactions
    /// can concurrently read it.
    /// </summary>
    public class AssocDB
    {
        /// <summary>
        /// Start read-only transactions. All access to the database (read or write) should be performed within transaction body.
        /// For write access it is enforced by placing all update methods in ReadWriteTransaction class.
        /// But for convenience reasons read-only access methods are left in Item class. It is responsibility of programmer to check that 
        /// them are not invoked outside transaction body.
        /// </summary>
        /// <returns>transaction object</returns>
        public virtual ReadOnlyTransaction StartReadOnlyTransaction()
        {
            root.SharedLock();
            return new ReadOnlyTransaction(this);
        }

        /// <summary>
        /// Start read write transaction
        /// </summary> 
        /// <returns>transaction object</returns>
        public virtual ReadWriteTransaction StartReadWriteTransaction()
        {
            root.ExclusiveLock();
            return new ReadWriteTransaction(this);
        }

        /// <summary>
        /// Set threshold for embedded relations.
        /// AssocDB supports small relations (tens of members) as well as very large relation (millions of members).
        /// Small relation are embedded inside object to reduce storage overhead and their increase access time.
        /// Large relations are implemented using B-Tree. AssocDB automatically choose proper representation.
        /// Initially relations are stored inside object.
        /// When number of links from an object exceeds this threshold value, AssocDB 
        /// removes links from the object and store it in external B-Tree index.
        /// </summary>
        public int EmbeddedRelationThreshold
        {
            get
            {
                return embeddedRelationThreshold;
            }
            set
            {
                embeddedRelationThreshold = value;
            }
        }

        /// <summary>
        /// AssocDB constructor. You should open and close storage yourself. You are free to set some storage properties, 
        /// storage listener and use some other storage administrative methods  like backup.
        /// But you should <b>not</b>: <ol>
        /// <li>specify your own root object for the storage</li>
        /// <li>use Storage commit or rollback methods directly</li>
        /// <li>modify storage using Perst classes not belonging to this package</li>
        /// </ol>
        /// </summary>
        /// <param name="storage">opened Perst storage</param>
        public AssocDB(Storage storage)
        {
            this.storage = storage;
            storage.SetProperty("perst.concurrent.iterator", true);
            embeddedRelationThreshold = DEFAULT_EMBEDDED_RELATION_THRESHOLD;
            language = DEFAULT_DOCUMENT_LANGUAGE;
            root = (Root)storage.Root;
            name2id = new Dictionary<string, int>();
            id2name = new Dictionary<int, string>();
            if (root == null)
            {
                root = CreateRoot();
                storage.Root = root;
                storage.Commit();
            }
            else
            {
                root.db = this;
                IDictionaryEnumerator e = root.attributes.GetDictionaryEnumerator();
                while (e.MoveNext())
                {
                    int id = ((IPersistent)e.Value).Oid;
                    String name = (String)e.Key;
                    name2id[name] = id;
                    id2name[id] = name;
                }
            }
        }

        /// <summary>
        /// Set document's languages: used in full text search index
        /// </summary>
        public String Language
        {
            get
            {
                return language;
            }
            set
            {
                language = value;
            }
        }


        internal protected void Unlock()
        {
            root.Unlock();
        }

        /// <summary>
        /// Storage root class - used internally by AssocDB.
        /// You can provide your own root class derived from Root by overriding AssocDB.createRoot method
        /// </summary>
        public class Root : PersistentResource
        {
            internal protected Index relations;
            internal protected Index attributes;
            internal protected FullTextIndex fullTextIndex;

            [NonSerialized()]
            internal protected AssocDB db;

            internal protected Root() { }

            internal protected Root(AssocDB db)
                : base(db.storage)
            {
                this.db = db;
                attributes = db.storage.CreateIndex(typeof(string), true);
                relations = db.storage.CreateIndex(typeof(long), false);
                fullTextIndex = db.CreateFullTextIndex();
            }
        }

        /// <summary>
        /// Create index for the particular attribute. Override this method to create some custom indices..
        /// </summary>
        /// <param name="name">attribute name</param>
        /// <param name="type">attribute type</param>
        /// <returns>new index</returns>
        internal protected virtual Index CreateIndex(String name, Type type)
        {
            return storage.CreateThickIndex(type);
        }

        /// <summary>
        /// Create root object. Override this method to create your own root derived from Root class.
        /// </summary>
        /// <returns>created root</returns>
        internal protected virtual Root CreateRoot()
        {
            return new Root(this);
        }

        /// <summary>
        /// Create full text index. Override this method to customize full text search.
        /// </summary>
        /// <returns>created full text index</returns>
        internal protected virtual FullTextIndex CreateFullTextIndex()
        {
            return storage.CreateFullTextIndex();
        }

        /// <summary>
        /// Create item. Override this method to create instance of your own class derived from Item. In such way 
        /// you can statically add some application specific fields to each class.
        /// </summary>
        /// <returns>created full text index</returns>
        internal protected virtual Item CreateItem()
        {
            return new Item(this);
        }

        /// <summary>
        /// Default value for embedded relations threshold
        /// </summary>
        public static int DEFAULT_EMBEDDED_RELATION_THRESHOLD = 100;

        /// <summary>
        /// Default document's language (used for full text search)
        /// </summary>
        public static String DEFAULT_DOCUMENT_LANGUAGE = "en";

        internal protected Storage storage;
        internal protected Root root;
        internal protected Dictionary<string, int> name2id;
        internal protected Dictionary<int, string> id2name;
        internal protected int embeddedRelationThreshold;
        internal protected String language;
    }
}
