using System;
using System.Collections;
using Perst;
using System.Diagnostics;

public class TestList2 { 
    public class Record:Persistent
    {
        public int i;
    }

    const int nRecords = 100000;
    const int pagePoolSize = 32*1024*1024;

    static public void  Main(string[] args)
    {
        int i;
        Storage db = StorageFactory.Instance.CreateStorage();
		
        db.Open("testlist2.dbs", pagePoolSize);
            
#if USE_GENERICS
        IPersistentList<Record> root = (IPersistentList<Record>)db.Root;
        if (root == null) 
        { 
            root = db.CreateList<Record>();
            db.Root = root;
        }
#else
        IPersistentList root = (IPersistentList)db.Root;
        if (root == null) 
        { 
            root = db.CreateList();
            db.Root = root;
        }
#endif

        DateTime start = DateTime.Now;
        for (i = 0; i < nRecords/2; i++) { 
            Record rec = new Record();
            rec.i = i*2;
            root.Add(rec);
        }
        for (i = 1; i < nRecords; i+=2) { 
            Record rec = new Record();
            rec.i = i;
            root.Insert(i, rec);
        }

        db.Commit();
        Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));

        start = DateTime.Now;
        for (i = 0; i < nRecords; i++) { 
#if USE_GENERICS
            Record rec = root[i];
#else
            Record rec = (Record)root[i];
#endif
            Debug.Assert(rec.i == i);
        }
        Console.WriteLine("Elapsed time for performing " + nRecords + " gets: " + (DateTime.Now - start));

        start = DateTime.Now;
        i = 0;
        foreach (Record rec in root) 
        {
            Debug.Assert(rec.i == i);
            i += 1;
        }
        Debug.Assert(i == nRecords);
        Console.WriteLine("Elapsed time for iteration through " + nRecords + " records: " + (DateTime.Now - start));
        
        start = DateTime.Now;
        for (i = 0; i < nRecords; i++) { 
#if USE_GENERICS
            Record rec = root[0];
#else
            Record rec = (Record)root[0];
#endif
            Debug.Assert(rec.i == i);
            root.RemoveAt(0);
            rec.Deallocate();
        }
        Debug.Assert(!root.GetEnumerator().MoveNext());
        Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        db.Close();
    }
}
