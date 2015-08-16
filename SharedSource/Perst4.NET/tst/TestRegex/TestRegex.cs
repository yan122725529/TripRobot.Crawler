using System;
using System.Collections;
using Perst;
using System.Diagnostics;


public class TestRegex
{
    public class Record:Persistent
    {
        public string key;
    }

    static public void  Main(string[] args)
    {
    	const int pagePoolSize = 256*1024*1024;
        const int nRecords = 1024*1024;

        Storage db = StorageFactory.Instance.CreateStorage();
        db.SetProperty("perst.concurrent.iterator", true);
        db.Open("testregex.dbs", pagePoolSize);

#if USE_GENERICS
        RegexIndex<Record> index = (RegexIndex<Record>)db.Root;
        if (index == null) { 
            index = db.CreateRegexIndex<Record>("key");
            db.Root = index;
        }
#else
        RegexIndex index = (RegexIndex)db.Root;
        if (index == null) { 
            index = db.CreateRegexIndex(typeof(Record), "key");
            db.Root = index;
        }
#endif

        DateTime start = DateTime.Now;
        for (int i = 0; i < nRecords; i++) { 
            Record rec = new Record();
            rec.key = i.ToString("x");
            index.Put(rec);
        }
        db.Commit();
        Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));

        start = DateTime.Now;
        int n = 0;
        foreach (Record rec in index.Match("%Abcd%")) { 
            n += 1;
            Debug.Assert(rec.key.IndexOf("abcd") >= 0);
        }
        Console.WriteLine("Elapsed time for query LIKE '%abcd%': "  + (DateTime.Now - start));
        Debug.Assert(n == 16*2);
        
        start = DateTime.Now;
        n = 0;
        foreach (Record rec in index.Match("1_2_3")) { 
            n += 1;
        }
        Console.WriteLine("Elapsed time for query LIKE '1_2_3': "  + (DateTime.Now - start));
        Debug.Assert(n == 16*16);
        
        start = DateTime.Now;
        foreach (Record rec in index) { 
            index.Remove(rec);
            rec.Deallocate();
        }
        Debug.Assert(!index.GetEnumerator().MoveNext());
        Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        db.Close();
    }
}