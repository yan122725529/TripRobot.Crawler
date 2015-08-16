using System;
using Perst;
using System.Diagnostics;
using System.Threading;

public class TestReplic2 
{ 
    class Record : Persistent { 
        public int key;
    }
    
    const int nIterations = 10000000;
    const int nRecords = 1000;
    const int transSize = 100;
    const int defaultPort = 6000;
    const int asyncBufSize = 1024*1024;
    const int pagePoolSize = 32*1024*1024;
    const int connectRetryTimeout = 2000;

    private static void usage() { 
        Console.WriteLine("Usage: java TestReplic2 (master|slave) [-port port] [-async] [-ack]");
    }

    class PerstListener : StorageListener
    {
        public override bool ReplicationError(string host) 
        {
            Console.WriteLine("Replication to node " + host + " failed");
            return true;
        }        
    }

    static public void Main(string[] args) {    
        int i;
        if (args.Length < 1) {
            usage();
            return;
        }
        int port = defaultPort;
        bool ack = false;
        bool async = false;
        String mapFile = null;
        String dbFile = null;
        String host = "localhost";
        for (i = 1; i < args.Length; i++) { 
            if (args[i] == "-async") { 
                async = true;
            } else if (args[i] == "-ack") { 
                ack = true;
            } else if (args[i] == "-map") { 
                mapFile = args[++i];
            } else if (args[i] == "-db") { 
                dbFile = args[++i];
            } else if (args[i] == "-port") { 
                port = int.Parse(args[++i]);
            } else if (args[i] == "-host") { 
                host = args[++i];
            } else { 
                usage();
            }
        }
        if ("master" == args[0]) { 
            ReplicationMasterStorage db = 
                StorageFactory.Instance.CreateReplicationMasterStorage(host, port, new string[0],
                                                                       async ? asyncBufSize : 0, mapFile);
            db.SetProperty("perst.replication.ack", ack);
            db.Listener = new PerstListener();
            if (dbFile == null) { 
                db.Open(new NullFile(), 0);
            } else { 
                db.Open(dbFile);
            }

#if USE_GENERICS
            FieldIndex<int,Record> root = (FieldIndex<int,Record>)db.Root;
            if (root == null) { 
                root = db.CreateFieldIndex<int,Record>("key", true);
#else
            FieldIndex root = (FieldIndex)db.Root;
            if (root == null) { 
                root = db.CreateFieldIndex(typeof(Record), "key", true);
#endif
                db.Root = root;
            }
            DateTime start = DateTime.Now;
            for (i = 0; i < nIterations; i++) {
                if (i >= nRecords) { 
                    object obj = root.Remove(new Key(i-nRecords));
                    db.Deallocate(obj);
                }
                Record rec = new Record();
                rec.key = i;
                root.Put(rec);
                if (i >= nRecords && i % transSize == 0) {
                    db.Commit();
                }
            }
            db.Close();
            Console.WriteLine("Elapsed time for " + nIterations + " iterations: " 
                               + (DateTime.Now - start));
        } else if ("slave" == args[0]) { 
            ReplicationSlaveStorage db = 
                StorageFactory.Instance.AddReplicationSlaveStorage(host, port, mapFile); 
            db.SetProperty("perst.replication.ack", ack);
            db.Listener = new PerstListener();
            while (true)
            {
                Console.WriteLine("Try to establish connection with master...");
                if (dbFile == null) { 
                    db.Open(new NullFile(), 0);
                } else { 
                    db.Open(dbFile);
                }
                if (!db.IsConnected())
                {
                    db.Close();
                    Console.WriteLine("Failed to connect to master...");
                    Thread.Sleep(connectRetryTimeout);
                    continue;
                }
                Console.WriteLine("Connection with master established");
                DateTime total = new DateTime(0);
                int n = 0;
                while (true) 
                { 
                    db.WaitForModification();
                    if (!db.IsConnected())
                    {
                        Console.WriteLine("Connection with master is lost");
                        break;
                    }
                    db.BeginThreadTransaction(TransactionMode.ReplicationSlave);
#if USE_GENERICS
                    FieldIndex<int,Record> root = (FieldIndex<int,Record>)db.Root;
#else
                    FieldIndex root = (FieldIndex)db.Root;
#endif
                    if (root != null && root.Count == nRecords) {
                        DateTime start = DateTime.Now;
                        int prevKey = -1;
                        i = 0;
                        foreach (Record rec in root) { 
                            int key = rec.key;
                            if (i == 0) 
                            { 
                                Console.WriteLine("First key: " + key);
                            }
                            Debug.Assert(prevKey < 0 || key == prevKey+1);
                            prevKey = key;
                            i += 1;
                        }
                        Debug.Assert(i == nRecords);
                        n += 1;
                        total += (DateTime.Now - start);
                        /*
                        if (n % 100 == 0) { 
                            Console.WriteLine("Terminate slave...");
                            Process.GetCurrentProcess().Kill();
                        }
                        */
                    }
                    db.EndThreadTransaction();
                }
                db.Close();
                Console.WriteLine("Elapsed time for " + n + " iterations: " + total);  
            }
        } else {
            usage();
        }
    }
}
