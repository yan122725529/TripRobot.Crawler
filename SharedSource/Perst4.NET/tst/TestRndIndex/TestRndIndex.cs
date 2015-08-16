using System;
using System.Collections;
using Perst;
using System.Diagnostics;

public class TestRndIndex { 
    public class Record:Persistent
    {
        public int i;
    }

    const int nRecords = 100003; // should be prime
    const int step = 5;
    const int pagePoolSize = 32*1024*1024;

    static public void  Main(string[] args)
    {
        int i, j;
        Storage db = StorageFactory.Instance.CreateStorage();
        db.SetProperty("perst.concurrent.iterator", true);
        db.Open("testrnd.dbs", pagePoolSize);
            
#if USE_GENERICS
        FieldIndex<int,Record> root = (FieldIndex<int,Record>)db.Root;
        if (root == null) 
        { 
            root = db.CreateRandomAccessFieldIndex<int,Record>("i", true);
            db.Root = root;
        }
#else
        FieldIndex root = (FieldIndex)db.Root;
        if (root == null) 
        { 
            root = db.CreateRandomAccessFieldIndex(typeof(Record), "i", true);
            db.Root = root;
        }
#endif

        DateTime start = DateTime.Now;
        for (i = 0, j = 0; i < nRecords; i++, j += step) { 
            Record rec = new Record();
            rec.i = j % nRecords;
            root.Put(rec);
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
            Debug.Assert(root.IndexOf(new Key(i)) == i);
        }
        Console.WriteLine("Elapsed time for performing " + nRecords + " Get operations: " + (DateTime.Now - start));

        start = DateTime.Now;
        for (i = 0; i < nRecords; i++) { 
#if USE_GENERICS
            Record rec = root.GetAt(i);
#else
            Record rec = (Record)root.GetAt(i);
#endif
            Debug.Assert(rec.i == i);
        }
        Console.WriteLine("Elapsed time for performing " + nRecords + " GetAt operations: " + (DateTime.Now - start));

        start = DateTime.Now;
        i = nRecords/2;
        IDictionaryEnumerator e = root.GetDictionaryEnumerator(nRecords/2, IterationOrder.AscentOrder);
        while (e.MoveNext())  
        {
            Debug.Assert((int)e.Key == i && ((Record)e.Value).i == i);
            i += 1;
        }
        Debug.Assert(i == nRecords);
        i = nRecords/2-1;
        e = root.GetDictionaryEnumerator(nRecords/2-1, IterationOrder.DescentOrder);
        while (e.MoveNext())  
        {
            Debug.Assert((int)e.Key == i && ((Record)e.Value).i == i);
            i -= 1;
        }
        Debug.Assert(i == -1);
        Console.WriteLine("Elapsed time for iteration through " + nRecords + " records: " + (DateTime.Now - start));
        
        start = DateTime.Now;
        for (i = 0, j = 0; i < nRecords; i += step, j++) { 
#if USE_GENERICS
            Record rec = root.GetAt(i-j);
#else
            Record rec = (Record)root.GetAt(i-j);
#endif
            Debug.Assert(rec.i == i);
            root.Remove(rec);
            rec.Deallocate();
        }
        Console.WriteLine("Elapsed time for deleting " + nRecords/step + " records: " + (DateTime.Now - start));
        root.Clear();
        Debug.Assert(!root.GetEnumerator().MoveNext());
        db.Close();
    }
}
