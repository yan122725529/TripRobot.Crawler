using System;
#if USE_GENERICS
using System.Collections.Generic;
#endif
using System.Collections;
using Perst;
using System.Diagnostics;


public class TestMap
{
    public class Record:Persistent
    {
        public string strKey;
        public long   intKey;
    }


    public class Root:Persistent
    {
#if USE_GENERICS
        public IPersistentMap<string,Record> strMap;
        public IPersistentMap<long,Record>   intMap;
#else
        public IPersistentMap strMap;
        public IPersistentMap intMap;
#endif
    }

    internal static int pagePoolSize = 32 * 1024 * 1024;
	
    static public void  Main(string[] args)
    {
        int i;
        int nRecords = 100000;
        if (args.Length > 0) 
        {
            nRecords = int.Parse(args[0]);
        } 
        Storage db = StorageFactory.Instance.CreateStorage();
		
        db.Open("testmap.dbs", pagePoolSize);

        Root root = (Root) db.Root;
        if (root == null)
        {
            root = new Root();
#if USE_GENERICS
            root.strMap = db.CreateMap<string,Record>();
            root.intMap = db.CreateMap<long,Record>();
#else
            root.strMap = db.CreateMap(typeof(String));
            root.intMap = db.CreateMap(typeof(long));
#endif
            db.Root = root;
        }
#if USE_GENERICS
        IDictionary<string,Record> strMap = root.strMap;
        IDictionary<long,Record> intMap = root.intMap;
#else
        IDictionary intMap = root.intMap;
        IDictionary strMap = root.strMap;
#endif
        DateTime start = DateTime.Now;
        long key = 1999;
        for (i = 0; i < nRecords; i++)
        {
            Record rec = new Record();
            key = (3141592621L * key + 2718281829L) % 1000000007L;
            rec.intKey = key;
            rec.strKey = System.Convert.ToString(key);
            intMap[rec.intKey] = rec;
            strMap[rec.strKey] = rec;
            Debug.Assert(intMap[rec.intKey] == rec);
            Debug.Assert(strMap[rec.strKey] == rec);
        }

        db.Commit();
        System.Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));
		
        start = System.DateTime.Now;
        key = 1999;
        for (i = 0; i < nRecords; i++)
        {
            key = (3141592621L * key + 2718281829L) % 1000000007L;
#if USE_GENERICS
            Record rec1 = intMap[key];
            Record rec2 = strMap[Convert.ToString(key)];
#else
            Record rec1 = (Record) intMap[key];
            Record rec2 = (Record) strMap[Convert.ToString(key)];
#endif
            Debug.Assert(rec1 != null && rec1 == rec2);
        }     
        System.Console.WriteLine("Elapsed time for performing " + nRecords * 2 + " map searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        key = Int64.MinValue;
        i = 0;
        foreach (Record rec in intMap.Values) 
        {
            Debug.Assert(rec.intKey >= key);
            key = rec.intKey;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        key = Int64.MinValue;
        i = 0;
        foreach (long k in intMap.Keys) 
        {
            Debug.Assert(k >= key);
            key = k;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        i = 0;
        String strKey = "";
        foreach (Record rec in strMap.Values) 
        {
            Debug.Assert(rec.strKey.CompareTo(strKey) >= 0);
            strKey = rec.strKey;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        i = 0;
        strKey = "";
        foreach (String s in strMap.Keys) 
        {
            Debug.Assert(s.CompareTo(strKey) >= 0);
            strKey = s;
            i += 1;
        }
        Debug.Assert(i == nRecords);
        System.Console.WriteLine("Elapsed time for 4 iteration through " + nRecords + " records: " + (DateTime.Now - start));


        start = System.DateTime.Now;
        key = 1999;
        for (i = 0; i < nRecords; i++)
        {
            key = (3141592621L * key + 2718281829L) % 1000000007L;
#if USE_GENERICS
            Record rec = intMap[key];
            Debug.Assert(intMap.Remove(key));
            Debug.Assert(strMap.Remove(Convert.ToString(key)));            
#else
            Record rec = (Record)intMap[key];
            intMap.Remove(key);
            strMap.Remove(Convert.ToString(key));            
#endif
            rec.Deallocate();
        }
        System.Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        db.Close();
    }
}