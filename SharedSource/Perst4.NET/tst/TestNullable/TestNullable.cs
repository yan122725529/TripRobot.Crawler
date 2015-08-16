using System;
using System.Collections;
using Perst;
using System.Diagnostics;

public class TestNullable
{

    public class Record:Persistent
    {
        public int  pk;
        public int? sk;
    }

    public class Root:Persistent 
    {   
#if USE_GENERICS
        public FieldIndex<int,Record> pki;
        public FieldIndex<int?,Record> ski;
#else
        public FieldIndex pki;
        public FieldIndex ski;
#endif
    }

    internal const int nRecords = 100000;
    internal static int pagePoolSize = 32 * 1024 * 1024;
	
    static public void  Main(string[] args)
    {
        int i;
        Storage db = StorageFactory.Instance.CreateStorage();

        db.Open("testnullable.dbs", pagePoolSize);

        Root root = (Root) db.Root;
        if (root == null)
        {
            root = new Root();
#if USE_GENERICS
            root.pki = db.CreateFieldIndex<int,Record>("pk", true);
            root.ski = db.CreateFieldIndex<int?,Record>("sk", true);
#else
            root.pki = db.CreateFieldIndex(typeof(Record), "pk", true);
            root.ski = db.CreateFieldIndex(typeof(Record), "sk", true);
#endif
            db.Root = root;
        }
#if USE_GENERICS
        FieldIndex<int,Record> pki = root.pki;
        FieldIndex<int?,Record> ski = root.ski;
#else
        FieldIndex pki = root.pki;
        FieldIndex ski = root.ski;
#endif
        DateTime start = DateTime.Now;
        for (i = 0; i < nRecords; i++)
        {
            Record rec = new Record();
            rec.pk = i;
            if ((i & 1) != 0) 
            { 
                rec.sk = i;
            }
            pki.Put(rec);
            ski.Put(rec);
        }
        db.Commit();
        System.Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));
		
        start = System.DateTime.Now;
        for (i = 0; i < nRecords; i++)
        {
#if USE_GENERICS
            Record rec1 = pki[i];
            Record rec2 = ski[i];            
#else
            Record rec1 = (Record)pki[i];
            Record rec2 = (Record)ski[i]; 
#endif
            Debug.Assert(rec1 != null);
            if ((i & 1) != 0) 
            { 
                Debug.Assert(rec2 == rec1);
            }
            else
            { 
                Debug.Assert(rec2 == null);
            }
        }     
        System.Console.WriteLine("Elapsed time for performing " + nRecords * 2 + " index searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        i = 0;
        foreach (Record rec in pki) 
        {
            Debug.Assert(rec.pk == i);
            if ((i & 1) != 0) 
            { 
                Debug.Assert(rec.sk == i);
            }
            else
            { 
                Debug.Assert(rec.sk == null);
            }
            i += 1;
        }
        Debug.Assert(i == nRecords);
        i = 1;
        foreach (Record rec in ski) 
        {
            Debug.Assert(rec.pk == i && rec.sk == i);
            i += 2;
        }
        Debug.Assert(i == nRecords+1);
        System.Console.WriteLine("Elapsed time for iteration through " + nRecords*3/2 + " records: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        i = 1;
        foreach (Record rec in pki.Select("sk = pk")) 
        {
            Debug.Assert(rec.pk == i && rec.sk == i);
            i += 2;
        }        
        Debug.Assert(i == nRecords+1);
        System.Console.WriteLine("Elapsed time for first sequential SubSQL search in " + nRecords + " records: " + (DateTime.Now - start));

        i = 1;
        foreach (Record rec in pki.Select("sk+1 = pk+1 and (sk and 1) <> 0")) 
        {
            Debug.Assert(rec.pk == i && rec.sk == i);
            i += 2;
        }        
        Debug.Assert(i == nRecords+1);
        System.Console.WriteLine("Elapsed time for second sequential SubSQL search in " + nRecords + " records: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        for (i = 0; i < nRecords; i++)
        {
#if USE_GENERICS
            Record rec = pki[i];
#else
            Record rec = (Record)pki[i];
#endif
            bool removed = pki.Remove(rec);
            Debug.Assert(removed);
            removed = ski.Remove(rec);
            if ((i & 1) != 0) 
            { 
                Debug.Assert(removed);
            }
            else 
            {
                Debug.Assert(!removed);
            }
            rec.Deallocate();
        }
        System.Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        db.Close();
    }
}