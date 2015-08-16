using System;
using System.Threading;
using Perst;
using System.Diagnostics;

public class TestServer 
{ 
    class Record : Persistent 
    { 
        public string key;
    }

    class Root : Persistent     
    { 
#if USE_GENERICS
        public FieldIndex<string,Record>[] indices;
#else
        public FieldIndex[] indices;
#endif
    }
    
    const int nThreads = 10;
    const int nIndices = 10;
    const int nRecords = 10000;

    static string toStr(int i) 
    { 
        string s = "000000" + i;
        return s.Substring(s.Length-6);
    }

    class ClientThread 
    {
        public Storage db;
        public int     id;
        
        public ClientThread(Storage db, int id) 
        {
            this.db = db;
            this.id = id;
        }

        public void run() { 
            int i;
            Root root = (Root)db.Root;
            string tid = "Thread" + id + ":";
#if USE_GENERICS
            FieldIndex<string,Record> index = root.indices[id % nIndices];
#else
            FieldIndex index = root.indices[id % nIndices];
#endif

            for (i = 0; i < nRecords; i++) 
            { 
                db.BeginThreadTransaction(TransactionMode.Serializable);
                index.ExclusiveLock();
                Record rec = new Record();
                rec.key = tid + toStr(i);
                index.Put(rec);
                db.EndThreadTransaction();
            }

            index.SharedLock();
            i = 0;
            foreach (Record rec in index.StartsWith(tid)) 
            {
                Debug.Assert(rec.key.Equals(tid + toStr(i)));
                i += 1;
            }
            Debug.Assert(i == nRecords);
            index.Unlock();

            for (i = 0; i < nRecords; i++) 
            { 
                index.SharedLock();
                string key = tid + toStr(i);
                Record rec = (Record)index[key];
                Debug.Assert(rec.key.Equals(key));
                index.Unlock();
            }

            for (i = 0; i < nRecords; i++) 
            { 
                db.BeginThreadTransaction(TransactionMode.Serializable);
                index.ExclusiveLock();
                Record rec = (Record)index.Remove(new Key(tid + toStr(i)));
                rec.Deallocate();
                db.EndThreadTransaction();
            }
        }
    }

    static public void Main(string[] args)
    {    
        Storage db = StorageFactory.Instance.CreateStorage();
        db.SetProperty("perst.alternative.btree", true);
        db.Open(new NullFile(), 0);
        Root root = (Root)db.Root;
        if (root == null) 
        { 
            root = new Root();
#if USE_GENERICS
            root.indices = new FieldIndex<string,Record>[nIndices];
#else
            root.indices = new FieldIndex[nIndices];
#endif
            for (int i = 0; i < nIndices; i++) 
            {
#if USE_GENERICS
                root.indices[i] = db.CreateFieldIndex<string,Record>("key", true);
#else
                root.indices[i] = db.CreateFieldIndex(typeof(Record), "key", true);
#endif
            }
            db.Root = root;
        }        
        DateTime start = DateTime.Now;
        Thread[] threads = new Thread[nThreads];
        for (int i = 0; i < nThreads; i++) 
        {
            ClientThread client = new ClientThread(db, i);  
            threads[i] = new Thread(new ThreadStart(client.run));
            threads[i].Start();
        }
#if !COMPACT_NET_FRAMEWORK
        for (int i = 0; i < nThreads; i++) 
        { 
            threads[i].Join();
        }
        db.Close();
        Console.WriteLine("Elapsed time: " + (DateTime.Now - start));
#endif
    }
}