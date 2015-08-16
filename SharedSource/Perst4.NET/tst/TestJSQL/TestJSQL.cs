using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif
using Perst;
using System.Diagnostics;

public class TestJSQL
{
    public class Record:Persistent
    {
        public string strKey;
        public long intKey;
        public DateTime dateKey;
    }


    public class Root:Persistent
    {
#if USE_GENERICS
        public FieldIndex<string,Record>   strIndex;
        public FieldIndex<long,Record>     intIndex;
        public FieldIndex<DateTime,Record> dateIndex;
#else
        public FieldIndex strIndex;
        public FieldIndex intIndex;
        public FieldIndex dateIndex;
#endif
    }

    internal const int nRecords = 100000;
    internal static int pagePoolSize = 32 * 1024 * 1024;
	
    static public void  Main(string[] args)
    {
        int i;
        Storage db = StorageFactory.Instance.CreateStorage();
		
        db.Open("testjsql.dbs", pagePoolSize);

  
        Root root = (Root) db.Root;
        if (root == null)
        {
            root = new Root();
#if USE_GENERICS
            root.strIndex = db.CreateFieldIndex<string,Record>("strKey", true);
            root.intIndex = db.CreateFieldIndex<long,Record>("intKey", true);
            root.dateIndex = db.CreateFieldIndex<DateTime,Record>("dateKey", false);
#else
            root.strIndex = db.CreateFieldIndex(typeof(Record), "strKey", true);
            root.intIndex = db.CreateFieldIndex(typeof(Record), "intKey", true);
            root.dateIndex = db.CreateFieldIndex(typeof(Record), "dateKey", false);
#endif
            db.Root = root;
        }
#if USE_GENERICS
        FieldIndex<string,Record> strIndex = root.strIndex;
        FieldIndex<long,Record> intIndex = root.intIndex;
        FieldIndex<DateTime,Record> dateIndex = root.dateIndex;
        IEnumerator<Record> enumerator;
#else
        FieldIndex intIndex = root.intIndex;
        FieldIndex strIndex = root.strIndex;
        FieldIndex dateIndex = root.dateIndex;
        IEnumerator enumerator;
#endif
        DateTime start = DateTime.Now;
        DateTime begin = start;
        long key = 1999;
        for (i = 0; i < nRecords; i++)
        {
            Record rec = new Record();
            key = (3141592621L * key + 2718281829L) % 1000000007L;
            rec.intKey = key;
            rec.strKey = System.Convert.ToString(key);
            rec.dateKey = DateTime.Now;
            intIndex[rec.intKey] = rec;
            strIndex[rec.strKey] = rec;
            dateIndex[rec.dateKey] = rec;
        }

        db.Commit();
        DateTime end = DateTime.Now;
        System.Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (end - start));
		
        start = System.DateTime.Now;
        key = 1999;
#if USE_GENERICS
        Query<Record> q1 = db.CreateQuery<Record>();
        q1.Prepare("strKey=?");        
        Query<Record> q2 = db.CreateQuery<Record>();
        q2.Prepare("intKey=?");        
        Query<Record> q3 = db.CreateQuery<Record>();
        q3.Prepare("dateKey between ? and ?");        
#else
        Query q1 = db.CreateQuery();
        q1.Prepare(typeof(Record), "strKey=?");        
        Query q2 = db.CreateQuery();
        q2.Prepare(typeof(Record), "intKey=?");        
        Query q3 = db.CreateQuery();
        q3.Prepare(typeof(Record), "dateKey between ? and ?");        
#endif
        q1.AddIndex("strKey",  strIndex);
        q2.AddIndex("intKey",  intIndex);
        q3.AddIndex("dateKey", dateIndex);
        for (i = 0; i < nRecords; i++)
        {
            key = (3141592621L * key + 2718281829L) % 1000000007L;
            q1[1] = Convert.ToString(key);
            enumerator = q1.Execute(intIndex).GetEnumerator();
            enumerator.MoveNext();
#if USE_GENERICS
            Record rec1 = enumerator.Current;
#else
            Record rec1 = (Record)enumerator.Current;
#endif
            Debug.Assert(!enumerator.MoveNext());

            q2[1] = key;
            enumerator = q2.Execute(strIndex).GetEnumerator();
            enumerator.MoveNext();
#if USE_GENERICS
            Record rec2 = enumerator.Current;
#else
            Record rec2 = (Record)enumerator.Current;
#endif
            Debug.Assert(rec1 != null && rec1 == rec2);
        }     
        System.Console.WriteLine("Elapsed time for performing " + nRecords * 2 + " index searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        key = Int64.MinValue;
        i = 0;
        foreach (Record rec in intIndex.Select("strKey=string(intKey)")) 
        {
            Debug.Assert(rec.intKey >= key);
            key = rec.intKey;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        System.Console.WriteLine("Elapsed time for iteration through " + nRecords + " records: " + (DateTime.Now - start));


        start = System.DateTime.Now;
        key = Int64.MinValue;
        i = 0;
        foreach (Record rec in strIndex.Select("(intKey and 1023) = 0 order by intKey")) 
        {
            Debug.Assert(rec.intKey >= key);
            key = rec.intKey;
            i += 1;
        }
        System.Console.WriteLine("Elapsed time for ordering " + i + " records: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        DateTime curr = begin;
        i = 0;
        q3[1] = begin;
        q3[2] = end;
        foreach (Record rec in q3.Execute(dateIndex)) 
        {
            Debug.Assert(rec.dateKey >= curr);
            curr = rec.dateKey;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        System.Console.WriteLine("Elapsed time for iteration through date index for " + nRecords + " records: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        key = 1999;
        foreach (Record rec in intIndex)
        {
            rec.Deallocate();
        }
        intIndex.Deallocate();
        strIndex.Deallocate();
        dateIndex.Deallocate();
        System.Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        db.Close();
    }
}
