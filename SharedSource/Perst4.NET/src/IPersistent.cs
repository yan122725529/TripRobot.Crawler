namespace Perst
{
    using System;
	
    /// <summary>
    /// Interface for classes which need onLoad callback to be invoked
    /// when Perst loads object from the storage
    /// </summary>
    ///
    public interface ILoadable 
    {
        /// <summary>
        /// Method called by the database after loading of the object.
        /// It can be used to initialize transient fields of the object. 
        /// Default implementation of this method do nothing
        /// </summary>
        ///
        void OnLoad();
    }

    /// <summary>
    /// Interface for classes which need onStore callback to be invoked
    /// when Perst storess object to the storage
    /// </summary>
    ///
    public interface IStoreable 
    {
        /// <summary>
        /// Method called by the database before storing of the object.
        /// It can be used to save or close transient fields of the object. 
        /// Default implementation of this method do nothing
        /// </summary>
        ///
        void OnStore();
    }

    /// <summary> Interface of all persistent capable objects
    /// </summary>
#if SILVERLIGHT
    public interface IPersistent : ILoadable, IStoreable
#else
    public interface IPersistent : ICloneable, ILoadable, IStoreable
#endif
    {
        /// <summary> Get object identifier (OID)
        /// </summary>
        int Oid
        {
            get;
				
        }


        /// <summary> Get storage in which this object is stored
        /// </summary>
        Storage Storage
        {
            get;				
        }
        
        /// <summary> Load object from the database (if needed)
        /// </summary>
        void  Load();

        /// 
        /// <summary> Check if object is stub and has to be loaded from the database
        /// </summary>
        /// <returns><b>true</b> if object has to be loaded from the database
        /// </returns>
        bool IsRaw();

        /// <summary> Check if object is persistent 
        /// </summary>
        /// <returns><b>true</b> if object has assigned OID
        /// 
        /// </returns>
        bool IsPersistent();

        /// <summary>  Check if object is deleted by GC from process memory
        /// </summary>
        /// <returns> <b>true</b> if object is deleted by GC
        /// </returns>
        bool IsDeleted();

        /// <summary> Check if object was modified within current transaction
        /// </summary>
        /// <returns><b>true</b> if object is persistent and was modified within current transaction
        /// 
        /// </returns>
        bool IsModified();

        /// <summary> Explicitely make object peristent. Usually objects are made persistent
        /// implicitlely using "persistency on reachability apporach", but this
        /// method allows to do it explicitly 
        /// </summary>
        /// <param name="storage">storage in which object should be stored 
        /// </param>
        /// <returns>OID assigned to the object</returns>
       int MakePersistent(Storage storage);

        /// <summary> Save object in the database
        /// </summary>
        void  Store();

        /// <summary>
        /// Mark object as modified. Object will be saved to the database during transaction commit.
        /// </summary>
        void Modify();
    
        /// <summary> Deallocate persistent object from the database
        /// </summary>
        void Deallocate();

        /// <summary> Specified whether object should be automatically loaded when it is referenced
        /// by other loaded peristent object. Default implementation of this method
        /// returns <b>true</b> making all cluster of referenced objects loaded together. 
        /// To avoid main memory overflow you should stop recursive loading of all objects
        /// from the database to main memory by redefining this method in some classes and returing
        /// <b>false</b> in it. In this case object has to be loaded explicitely 
        /// using Persistent.load method.
        /// </summary>
        /// <returns><b>true</b> if object is automatically loaded
        /// 
        /// </returns>
        bool RecursiveLoading();

        /// <summary>
        /// Invalidate object. Invalidated object has to be explicitly
        /// reloaded using load() method. Attempt to store invalidated object
        /// will cause StoraegError exception.
        /// </summary>
        void Invalidate();

        /// <summary>
        /// Method used to associate object with storage.
        /// This method is used by Storage class and you should not use it explicitly.
        /// </summary>
        /// <param name="storage">storage to be assigned to</param>
        /// <param name="oid">assigned OID</param>
        /// <param name="raw">if object is already loaded</param>
        void AssignOid(Storage storage, int oid, bool raw);

        /// <summary>
        /// Method used to remove association of object with storage.
        /// </summary>
        void UnassignOid();
    }
}