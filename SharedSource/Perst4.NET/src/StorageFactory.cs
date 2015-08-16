namespace Perst
{
    using System;
    using Perst.Impl;
	
    /// <summary> Storage factory
    /// </summary>
    public class StorageFactory
    {
        /// <summary> Get instance of storage factory.
        /// So new storages should be create in application in the following way:
        /// <see cref="M:Perst.StorageFactory.Instance.CreateStorage"/>
        /// </summary>
        public static StorageFactory Instance
        {
            get
            {
                return instance;
            }
			
        }

        /// <summary> Create new instance of the storage
        /// </summary>
        /// <returns>instance of the storage (unopened,you should explicitely invoke open method)
        /// </returns>
        public virtual Storage CreateStorage()
        {
#if (COMPACT_NET_FRAMEWORK || SILVERLIGHT) && !WINRT_NET_FRAMEWORK
            return new StorageImpl(System.Reflection.Assembly.GetCallingAssembly());
#else
            return new StorageImpl();
#endif
        }
		
#if !COMPACT_NET_FRAMEWORK && !SILVERLIGHT
        /// <summary>
        /// Create new instance of the master node of replicated storage. There are two kinds of replication slave nodes:
        /// statically defined and dynamically added. First one are specified by replicationSlaveNodes parameter.
        /// When replication master is started it tries to eastablish connection with all of the specified nodes. 
        /// It is expected that state of each such node is synchronized with state of the master node.
        /// It is not possible to add or remove static replication slave node without stopping master node.
        /// Dynamic slave nodes can be added at any moment of time. Replication master will send to such node complete 
        /// snapshot of the database.
        /// </summary>
        /// <param name="port">socket port at which replication master will listen for dynamic slave nodes connections. 
        /// If this parameter is -1, then no dynamic slave node conenctions are accepted.</param> 
        /// <param name="replicationSlaveNodes">addresses of hosts to which replication will be performed. 
        /// Address as specified as NAME:PORT</param>
        /// <param name="asyncBufSize">if value of this parameter is greater than zero then replication will be 
        /// asynchronous, done by separate thread and not blocking main application. 
        /// Otherwise data is send to the slave nodes by the same thread which updates the database.
        /// If space asynchronous buffer is exhausted, then main thread willbe also blocked until the
        /// data is send.</param>
        /// <returns>new instance of the master storage (unopened, you should explicitely invoke open method)</returns>
        ///
        public virtual ReplicationMasterStorage CreateReplicationMasterStorage(int port, string[] replicationSlaveNodes, int asyncBufSize) 
        {
            return new ReplicationMasterStorageImpl(null, port, replicationSlaveNodes, asyncBufSize, null);
        }

        /// <summary>
        /// Create new instance of the master node of replicated storage. There are two kinds of replication slave nodes:
        /// statically defined and dynamically added. First one are specified by replicationSlaveNodes parameter.
        /// When replication master is started it tries to eastablish connection with all of the specified nodes. 
        /// It is expected that state of each such node is synchronized with state of the master node.
        /// It is not possible to add or remove static replication slave node without stopping master node.
        /// Dynamic slave nodes can be added at any moment of time. Replication master will send to such node complete 
        /// snapshot of the database.
        /// </summary>
        /// <param name="port">socket port at which replication master will listen for dynamic slave nodes connections. 
        /// If this parameter is -1, then no dynamic slave node conenctions are accepted.</param> 
        /// <param name="replicationSlaveNodes">addresses of hosts to which replication will be performed. 
        /// Address as specified as NAME:PORT</param>
        /// <param name="asyncBufSize">if value of this parameter is greater than zero then replication will be 
        /// asynchronous, done by separate thread and not blocking main application. 
        /// Otherwise data is send to the slave nodes by the same thread which updates the database.
        /// If space asynchronous buffer is exhausted, then main thread willbe also blocked until the
        /// data is send.</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        /// <returns>new instance of the master storage (unopened, you should explicitely invoke open method)</returns>
        ///
        public virtual ReplicationMasterStorage CreateReplicationMasterStorage(int port, string[] replicationSlaveNodes, int asyncBufSize, String pageTimestampFile) 
        {
            return new ReplicationMasterStorageImpl(null, port, replicationSlaveNodes, asyncBufSize, pageTimestampFile);
        }

        /// <summary>
        /// Create new instance of the master node of replicated storage. There are two kinds of replication slave nodes:
        /// statically defined and dynamically added. First one are specified by replicationSlaveNodes parameter.
        /// When replication master is started it tries to eastablish connection with all of the specified nodes. 
        /// It is expected that state of each such node is synchronized with state of the master node.
        /// It is not possible to add or remove static replication slave node without stopping master node.
        /// Dynamic slave nodes can be added at any moment of time. Replication master will send to such node complete 
        /// snapshot of the database.
        /// </summary>
        /// <param name="localhost">network interface address</param> 
        /// <param name="port">socket port at which replication master will listen for dynamic slave nodes connections. 
        /// If this parameter is -1, then no dynamic slave node conenctions are accepted.</param> 
        /// <param name="replicationSlaveNodes">addresses of hosts to which replication will be performed. 
        /// Address as specified as NAME:PORT</param>
        /// <param name="asyncBufSize">if value of this parameter is greater than zero then replication will be 
        /// asynchronous, done by separate thread and not blocking main application. 
        /// Otherwise data is send to the slave nodes by the same thread which updates the database.
        /// If space asynchronous buffer is exhausted, then main thread willbe also blocked until the
        /// data is send.</param>
        /// <returns>new instance of the master storage (unopened, you should explicitely invoke open method)</returns>
        ///
        public virtual ReplicationMasterStorage CreateReplicationMasterStorage(string localhost, int port, string[] replicationSlaveNodes, int asyncBufSize) 
        {
            return new ReplicationMasterStorageImpl(localhost, port, replicationSlaveNodes, asyncBufSize, null);
        }

        /// <summary>
        /// Create new instance of the master node of replicated storage. There are two kinds of replication slave nodes:
        /// statically defined and dynamically added. First one are specified by replicationSlaveNodes parameter.
        /// When replication master is started it tries to eastablish connection with all of the specified nodes. 
        /// It is expected that state of each such node is synchronized with state of the master node.
        /// It is not possible to add or remove static replication slave node without stopping master node.
        /// Dynamic slave nodes can be added at any moment of time. Replication master will send to such node complete 
        /// snapshot of the database.
        /// </summary>
        /// <param name="localhost">network interface address</param> 
        /// <param name="port">socket port at which replication master will listen for dynamic slave nodes connections. 
        /// If this parameter is -1, then no dynamic slave node conenctions are accepted.</param> 
        /// <param name="replicationSlaveNodes">addresses of hosts to which replication will be performed. 
        /// Address as specified as NAME:PORT</param>
        /// <param name="asyncBufSize">if value of this parameter is greater than zero then replication will be 
        /// asynchronous, done by separate thread and not blocking main application. 
        /// Otherwise data is send to the slave nodes by the same thread which updates the database.
        /// If space asynchronous buffer is exhausted, then main thread willbe also blocked until the
        /// data is send.</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        /// <returns>new instance of the master storage (unopened, you should explicitely invoke open method)</returns>
        ///
        public virtual ReplicationMasterStorage CreateReplicationMasterStorage(string localhost, int port, string[] replicationSlaveNodes, int asyncBufSize, String pageTimestampFile) 
        {
            return new ReplicationMasterStorageImpl(localhost, port, replicationSlaveNodes, asyncBufSize, pageTimestampFile);
        }

        /// <summary>
        /// Create new instance of the static slave node of replicated storage.
        /// The address of this host should be sepecified in the replicationSlaveNodes
        /// parameter of createReplicationMasterStorage method. When replication master
        /// is started it tries to eastablish connection with all of the specified nodes. 
        /// </summary>
        /// <param name="slavePort">socket port at which connection from master will be established</param>
        /// <returns>new instance of the slave storage (unopened, you should explicitely invoke open method)</returns>
        ////
        public virtual ReplicationSlaveStorage CreateReplicationSlaveStorage(int slavePort) 
        {
            return new ReplicationStaticSlaveStorageImpl(slavePort, null);
        }

        /// <summary>
        /// Create new instance of the static slave node of replicated storage.
        /// The address of this host should be sepecified in the replicationSlaveNodes
        /// parameter of createReplicationMasterStorage method. When replication master
        /// is started it tries to eastablish connection with all of the specified nodes. 
        /// </summary>
        /// <param name="slavePort">socket port at which connection from master will be established</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        /// <returns>new instance of the slave storage (unopened, you should explicitely invoke open method)</returns>
        ////
        public virtual ReplicationSlaveStorage CreateReplicationSlaveStorage(int slavePort, String pageTimestampFile) 
        {
            return new ReplicationStaticSlaveStorageImpl(slavePort, pageTimestampFile);
        }

        /// <summary>
        /// Add new instance of the dynamic slave node of replicated storage. 
        /// </summary>
        /// <param name="replicationMasterNode">name of the host where replication master is running</param>
        /// <param name="masterPort">replication master socket port to which connection should be established</param>
        /// <returns>new instance of the slave storage (unopened, you should explicitely invoke open method)</returns>
        ///
        public virtual ReplicationSlaveStorage AddReplicationSlaveStorage(String replicationMasterNode, int masterPort) 
        {
            return new ReplicationDynamicSlaveStorageImpl(replicationMasterNode, masterPort, null);
        }

        /// <summary>
        /// Add new instance of the dynamic slave node of replicated storage. 
        /// </summary>
        /// <param name="replicationMasterNode">name of the host where replication master is running</param>
        /// <param name="masterPort">replication master socket port to which connection should be established</param>
        /// <param name="pageTimestampFile">path to the file with pages timestamps. This file is used for synchronizing
        /// with master content of newly attached node</param>
        /// <returns>new instance of the slave storage (unopened, you should explicitely invoke open method)</returns>
        ///
        public virtual ReplicationSlaveStorage AddReplicationSlaveStorage(String replicationMasterNode, int masterPort, String pageTimestampFile) 
        {
            return new ReplicationDynamicSlaveStorageImpl(replicationMasterNode, masterPort, pageTimestampFile);
        }
#endif

		
        protected internal static StorageFactory instance = new StorageFactory();
    }	
}