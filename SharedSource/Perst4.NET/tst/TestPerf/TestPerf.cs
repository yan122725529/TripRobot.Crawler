using System;
using System.Collections;
using Perst;
using Perst.Impl;
using System.Diagnostics;

public class Record:Persistent
{
    public int intKey;
}


public class Root:Persistent
{
#if USE_GENERICS
    public FieldIndex<int,Record> tree;
    public IPersistentMap<int,Record> hash;
#else
    public FieldIndex tree;
    public IPersistentMap hash;
#endif
}

public class TestPerf
{
    static public void  Main(string[] args)
    {
        int i;
        Storage db = StorageFactory.Instance.CreateStorage();
		
        bool serializableTransaction = false;
        bool compressed = false;
        bool encrypted = false;
        long pagePoolSize = 32 * 1024 * 1024; 
        int  nRecords = 100000;
        int hashPageSize = 101;
        int hashLoadFactor = 1;
        string password = "MyPassword";

        for (i = 0; i < args.Length; i++) 
        { 
            switch (args[i]) 
            { 
                case "inmemory":
                    pagePoolSize = 0;
                    break;
                case "zip": 
                    compressed = true; 
                    break;
                case "crypt": 
                    encrypted = true;
                    break;
                case "altbtree":
                    db.SetProperty("perst.alternative.btree", true);
                    break;
                case "serializable":
                    db.SetProperty("perst.alternative.btree", true);
                    serializableTransaction = true;
                    break;
                case "pool":
                    pagePoolSize = long.Parse(args[++i]);
                    break;
                case "records":
                    nRecords = int.Parse(args[++i]);
                    break;
                case "page":
                    hashPageSize = int.Parse(args[++i]);
                    break;
                case "load":
                    hashLoadFactor = int.Parse(args[++i]);
                    break;
                default:
                    Console.WriteLine("Unknown option: " + args[i]);
                    return;
            } 
        }
        if (pagePoolSize == 0)
        {
            db.Open(new NullFile(), 0);
        } 
        else
        {
#if !NET_FRAMEWORK_10 && (!COMPACT_NET_FRAMEWORK || COMPACT_NET_FRAMEWORK_35)
            if (compressed) 
            {            
                db.Open(new CompressedFile("testidx.dbs", encrypted ? password : null), pagePoolSize);
            }
            else
#endif
            if (encrypted) 
            {
                db.Open("testidx.dbs", pagePoolSize, password);
            }
            else         
            {
                db.Open("testidx.dbs", pagePoolSize);
            }
        }
        if (serializableTransaction) 
        { 
            db.BeginThreadTransaction(TransactionMode.Serializable);
        }

        Root root = (Root) db.Root;
        if (root == null)
        {
            root = new Root();
#if USE_GENERICS
            root.tree = db.CreateFieldIndex<int,Record>("intKey", true);
            root.hash = db.CreateHash<int,Record>(hashPageSize, hashLoadFactor);
#else
            root.tree = db.CreateFieldIndex(typeof(Record), "intKey", true);
            root.hash = db.CreateHash(hashPageSize, hashLoadFactor);
#endif
            db.Root = root;
        }
#if USE_GENERICS
        FieldIndex<int,Record> tree = root.tree;
        IPersistentMap<int,Record> hash = root.hash;
#else
        FieldIndex tree = root.tree;
        IPersistentMap hash = root.hash;
#endif
        DateTime start = DateTime.Now;
        for (i = 0; i < nRecords; i++)
        {
            Record rec = new Record();
            rec.intKey = i*2;
            tree.Put(rec);
            hash[rec.intKey] = rec;
        }

        if (serializableTransaction) 
        { 
            db.EndThreadTransaction();
        } 
        else 
        {
            db.Commit();
        }
        System.Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));
		
        start = System.DateTime.Now;
        for (i = 0; i < nRecords*2; i++)
        {
#if USE_GENERICS
            Record rec = tree[i];
#else
            Record rec = (Record)tree[i];
#endif
            if ((i & 1) != 0)
            {
                Debug.Assert(rec == null);
            } 
            else
            {
                Debug.Assert(rec != null && rec.intKey == i);
            }
        }     
        System.Console.WriteLine("Elapsed time for performing " + nRecords * 2 + " B-Tree searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        for (i = 0; i < nRecords*2; i++)
        {
#if USE_GENERICS
            Record rec = hash[i];
#else
            Record rec = (Record)hash[i];
#endif
            if ((i & 1) != 0)
            {
                Debug.Assert(rec == null);
            } 
            else
            {
                Debug.Assert(rec != null && rec.intKey == i);
            }
        }     
        System.Console.WriteLine("Elapsed time for performing " + nRecords * 2 + " hash searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        i = 0;
        foreach (Record rec in tree) 
        {
            Debug.Assert(rec.intKey == i*2);
            i += 1;
        }
        Debug.Assert(i == nRecords);
        System.Console.WriteLine("Elapsed time for iteration through " + nRecords + " records: " + (DateTime.Now - start));


        start = System.DateTime.Now;
        if (serializableTransaction) 
        { 
            db.BeginThreadTransaction(TransactionMode.Serializable);
        } 
#if USE_GENERICS
        HashStatistic stat = ((PersistentHashImpl<int,Record>)hash).GetStatistic();
#else
        HashStatistic stat = ((PersistentHashImpl)hash).GetStatistic();
#endif
        for (i = 0; i < nRecords*2; i++)
        {
#if USE_GENERICS
            Record rec = hash[i];
#else
            Record rec = (Record)hash[i];
#endif
            if ((i & 1) != 0)
            {
                Debug.Assert(rec == null);
            } 
            else
            {
                Debug.Assert(rec != null && rec.intKey == i);
                tree.Remove(rec);
                hash.Remove(rec.intKey);             
                rec.Deallocate();
            }
        }
        if (serializableTransaction) 
        { 
            db.EndThreadTransaction();
        } 
        Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        Console.WriteLine(stat);
        db.Close();
    }
}
