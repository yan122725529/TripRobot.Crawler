using System;

namespace Perst
{
    /// <summary>
    /// Listener of database events. Programmer should derive his own subclass and register
    /// it using Storage.setListener method.
    /// </summary>
    public abstract class StorageListener 
    {
        /// <summary>
        /// This metod is called during database open when database was not
        /// close normally and has to be recovered
        /// </summary>
        public virtual void DatabaseCorrupted() {}

        /// <summary>
        /// This method is called after completion of recovery
        /// </summary>
        public virtual void RecoveryCompleted() {}

        /// <summary>
        /// Method invoked by Perst affter object is loaded from the database 
        /// </summary>
        /// <param name="obj">loaded object</param>
        ///        
        public virtual void OnObjectLoad(object obj) {}
            
        /// <summary>
        /// Method invoked by Perst after object is looked up from the database
        /// </summary>
        /// <param name="obj">looked up object</param>
        ///       
        public virtual void OnObjectLookup(object obj) { }

        /// <summary>
        /// Method invoked by Perst before object is written to the database 
        /// </summary>
        /// <param name="obj">stored object</param>
        ///        
        public virtual void OnObjectStore(object obj) {}

        /// <summary>
        /// Method invoked by Perst before object is deallocated
        /// </summary>
        /// <param name="obj">deallocated object</param>
        ///        
        public virtual void OnObjectDelete(object obj) {}

        /// <summary>
        /// Method invoked by Perst after object is assigned OID (becomes persisistent)
        /// </summary>
        /// <param name="obj">object which is made persistent</param>
        ///        
        public virtual void OnObjectAssignOid(object obj) {}

        /// <summary>
        /// Method invoked by Perst when slave node receive updates from master
        /// </summary>
        ///        
        public virtual void OnMasterDatabaseUpdate() {}

        /// <summary>
        /// Method invoked by Perst when transaction is committed
        /// </summary>
        ///        
        public virtual void OnTransactionCommit() {}

        /// <summary>
        /// Method invoked by Perst when transaction is aborted
        /// </summary>
        ///        
        public virtual void OnTransactionRollback() {}

        /// <summary>
        /// This method is called when garbage collection is  started (ether explicitly
        /// by invocation of Storage.gc() method, either implicitly  after allocation
        /// of some amount of memory)).
        /// </summary>
        public virtual void GcStarted() {}

        /// <summary>
        /// This method is called  when unreferenced object is deallocated from 
        /// database during garbage collection. It is possible to get instance of the object using
        /// <see cref="M:Perst.Storage.GetObjectByOid"/> method.
        /// </summary>
        /// <param name="cls">class of deallocated object</param>
        /// <param name="oid">object identifier of deallocated object</param>
        ///
        public virtual void DeallocateObject(Type cls, int oid) {}

        /// <summary>
        /// This method is called by XMLExporter when exception happens during object export.
        /// Exception can be caused either by IO problems (failed to write data to the destination stream) 
        /// either by corruption of object.
        /// </summary>
        /// <param name="oid">object identifier of exported object</param>
        /// <param name="x">catched exception</param>
        /// <returns><b>true</b> if error should be ignored and export continued, <b>false</b> to rethrow catched exception</returns>
        ///
        public virtual bool ObjectNotExported(int oid, Exception x) 
        {
            return true;
        }

        /// <summary>
        /// This method is called when garbage collection is completed
        /// </summary>
        /// <param name="nDeallocatedObjects">number of deallocated objects</param>
        ///
        public virtual void GcCompleted(int nDeallocatedObjects) {}

        /// <summary>
        /// Handle replication error 
        /// </summary>
        /// <param name="host">address of host replication to which is failed (null if error happens at slave node)</param>
        /// <returns><b>true</b> if host should be reconnected and attempt to send data to it should be 
        /// repeated, <b>false</b> if no more attmpts to communicate with this host should be performed
        /// </returns>
        public virtual bool ReplicationError(string host) 
        {
            return false;
        }        
		
        /// <summary> This method is called when runtime error happen during execution of JSQL query
        /// </summary>
        public virtual void JSQLRuntimeError(JSQLRuntimeException x)
        {
        }

        /// <summary>
        /// This method is called when query is executed
        /// </summary>
        /// <param name="query">executed query</param>
        /// <param name="elapsedTime">time of query execution in nanoseconds (please notice that many queries are executed
        /// incrementally, so the time passed to this method is not always complete time of query execution</param>     
        /// <param  name="sequentialSearch">true if sequential scan in used for query execution, false if some index is used</param> 
        /// 
        public virtual void QueryExecution(object query, long elapsedTime, bool sequentialSearch) 
        {
        }
        
        /// <summary> 
        /// Sequential search is performed for query execution
        /// </summary>
        /// <param name="query">executed query</param>
        ///
        public virtual void SequentialSearchPerformed(object query) 
        {
        }        
    
        /// <summary> 
        /// Sort of the selected result set is performed for query execution
        /// </summary>
        /// <param name="query">executed query</param>
        ///
        public virtual void SortResultSetPerformed(object query) 
        {
        }        

        /// <summary> 
        /// Index is automaticaslly created by Database class when query is executed and autoIndices is anabled
        /// </summary> 
        /// <param name="table">table for which index is created</param>
        /// <param name="field">index key</param>
        ///
        public virtual void IndexCreated(Type table, string field) 
        {
        }        
    }
}
