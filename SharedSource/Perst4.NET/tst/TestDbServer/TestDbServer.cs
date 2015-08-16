using System;
using System.Threading;
using Perst;
using Perst.FullText;
using System.Diagnostics;

public class TestDbServer 
{ 
    class Record : Persistent 
    { 
        [Indexable(Unique=true, CaseInsensitive=true)]
        public string key;

        [FullTextIndexable]
        public string value;
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
        public Database db;
        public int      id;
        
        public ClientThread(Database db, int id) 
        {
            this.db = db;
            this.id = id;
        }

        public void run() { 
            int i;
            string tid = "Thread" + id + ":";
            Storage storage = db.Storage;

            for (i = 0; i < nRecords; i++) 
            { 
                db.BeginTransaction();
                Record rec = new Record();
                rec.key = tid + toStr(i) + ".Id";
                rec.value = "Thread" + id + " Key" + i;
                db.AddRecord(rec);
                db.CommitTransaction();
            }

            db.BeginTransaction();
            i = 0;
            foreach (Record rec in db.Select(typeof(Record), "key like '" + tid + "%'"))
            {
                Debug.Assert(rec.key.Equals(tid + toStr(i) + ".Id"));
                i += 1;
            }
            Debug.Assert(i == nRecords);

            FullTextSearchResult result = db.Search("Thread" + id, null, 10, 1000);
            Debug.Assert(result.Hits.Length == 10 && result.Estimation == nRecords);
            db.CommitTransaction();

            for (i = 0; i < nRecords; i++) 
            { 
                db.BeginTransaction();
                string key = tid + toStr(i) + ".ID";
                int n = 0;
                foreach (Record rec in db.Select(typeof(Record), "key='" + key + "'"))
                {
                    Debug.Assert(String.Compare(rec.key, key, true) == 0);
                    n += 1;
                }
                Debug.Assert(n == 1);

                result = db.Search("Thread" + id + " Key" + i, null, 10, 1000);
                Debug.Assert(result.Hits.Length == 1 && result.Estimation == 1
                    && String.Compare(((Record)result.Hits[0].Document).key, key, true) == 0);

                db.CommitTransaction();
            }

            for (i = 0; i < nRecords; i++) 
            { 
                db.BeginTransaction();
                string key = tid + toStr(i) + ".id";
                int n = 0;
                foreach (Record rec in db.Select(typeof(Record), "key='" + key + "'", true))
                {
                    Debug.Assert(String.Compare(rec.key, key, true) == 0);
                    db.DeleteRecord(rec);
                    n += 1;
                    break;
                }
                Debug.Assert(n == 1);
                db.CommitTransaction();
            }
        }
    }

    static public void Main(string[] args)
    {    
        Storage storage = StorageFactory.Instance.CreateStorage();
        storage.Open(new NullFile(), 0);
        Database db = new Database(storage, true);

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
        storage.Close();
#endif
        Console.WriteLine("Elapsed time: " + (DateTime.Now - start));
    }
}