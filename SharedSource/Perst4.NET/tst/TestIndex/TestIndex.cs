using System;
using System.Collections;
using Perst;
using System.Diagnostics;

public class Record:Persistent
{
    public string strKey;
    public long intKey;
}


public class Root:Persistent
{
#if USE_GENERICS
    public Index<string,Record> strIndex;
    public Index<long,Record>   intIndex;
#else
    public Index strIndex;
    public Index intIndex;
#endif
}

public class TestIndex
{
    static public void  Main(string[] args)
    {
        int i;
        Storage db = StorageFactory.Instance.CreateStorage();
		
        bool serializableTransaction = false;
        bool compressed = false;
        bool encrypted = false;
        bool multifile = false;
        long pagePoolSize = 32 * 1024 * 1024; 
        int  nRecords = 100000;
        string password = "MyPassword";
        bool populate = false;

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
                case "multifile": 
                    multifile = true;
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
                case "populate":
                    populate = true;
                    break;    
                default:
                    Console.WriteLine("Unknown option: " + args[i]);
                    return;
            } 
        }
        string dbName = multifile ? "@testidx.mfd" : "testidx.dbs";
#if !NET_FRAMEWORK_10 && (!COMPACT_NET_FRAMEWORK || COMPACT_NET_FRAMEWORK_35)
        if (compressed) 
        {            
            db.Open(new CompressedFile(dbName, encrypted ? password : null), pagePoolSize);
        }
        else
#endif
        if (encrypted) 
        {
            db.Open(dbName, pagePoolSize, password);
        }
        else         
        {
            db.Open(dbName, pagePoolSize);
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
            root.strIndex = db.CreateIndex<string,Record>(true);
            root.intIndex = db.CreateIndex<long,Record>(true);
#else
            root.strIndex = db.CreateIndex(typeof(String), true);
            root.intIndex = db.CreateIndex(typeof(long), true);
#endif
            db.Root = root;
        }
#if USE_GENERICS
        Index<string,Record> strIndex = root.strIndex;
        Index<long,Record> intIndex = root.intIndex;
#else
        Index intIndex = root.intIndex;
        Index strIndex = root.strIndex;
#endif
        DateTime start = DateTime.Now;
        long key = 1999;
        for (i = 0; i < nRecords; i++)
        {
            Record rec = new Record();
            key = (3141592621L * key + 2718281829L) % 1000000007L;
            rec.intKey = key;
            rec.strKey = System.Convert.ToString(key);
            intIndex[rec.intKey] = rec;
            strIndex[rec.strKey] = rec;
            if (i % 100000 == 0) 
            { 
                Console.Write("Iteration " + i + "\r");
            }
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
        key = 1999;
        for (i = 0; i < nRecords; i++)
        {
            key = (3141592621L * key + 2718281829L) % 1000000007L;
#if USE_GENERICS
            Record rec1 = intIndex[key];
            Record rec2 = strIndex[Convert.ToString(key)];
#else
            Record rec1 = (Record) intIndex[key];
            Record rec2 = (Record) strIndex[Convert.ToString(key)];
#endif
            Debug.Assert(rec1 != null && rec1 == rec2);
        }     
        System.Console.WriteLine("Elapsed time for performing " + nRecords * 2 + " index searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        key = Int64.MinValue;
        i = 0;
        foreach (Record rec in intIndex) 
        {
            Debug.Assert(rec.intKey >= key);
            key = rec.intKey;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        i = 0;
        String strKey = "";
        foreach (Record rec in strIndex) 
        {
            Debug.Assert(rec.strKey.CompareTo(strKey) >= 0);
            strKey = rec.strKey;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        System.Console.WriteLine("Elapsed time for iteration through " + (nRecords * 2) + " records: " + (DateTime.Now - start));


        Hashtable map = db.GetMemoryDump();
        Console.WriteLine("Memory usage");
        start = DateTime.Now;
        foreach (MemoryUsage usage in map.Values) 
        { 
            Console.WriteLine(" " + usage.type.Name + ": instances=" + usage.nInstances + ", total size=" + usage.totalSize + ", allocated size=" + usage.allocatedSize);
        }
        Console.WriteLine("Elapsed time for memory dump: " + (DateTime.Now - start)); 

        if (!populate)
        {
            start = System.DateTime.Now;
            key = 1999;
            if (serializableTransaction) 
            { 
                db.BeginThreadTransaction(TransactionMode.Serializable);
            } 
            for (i = 0; i < nRecords; i++)
            {
                key = (3141592621L * key + 2718281829L) % 1000000007L;
#if USE_GENERICS
                Record rec = intIndex.Get(key);
                Record removed = intIndex.RemoveKey(key);
#else
                Record rec = (Record) intIndex[key];
                Record removed = (Record)intIndex.RemoveKey(key);
#endif
                Debug.Assert(removed == rec);
                strIndex.Remove(new Key(System.Convert.ToString(key)), rec);
                rec.Deallocate();
            }
            if (serializableTransaction) 
            { 
                db.EndThreadTransaction();
            } 
            System.Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        }
        db.Close();
    }
}