using System;
using System.Collections;
using Perst;
using System.Diagnostics;

public class TestKDTree
{
    class Quote : Persistent
    { 
        public int    timestamp;
        public float  low;
        public float  high;
        public float  open;
        public float  close;
        public int    volume;
        
        public bool eq(Quote q) { 
            return low == q.low && high == q.high && open == q.open && close == q.close && volume == q.volume;
        }
        
        public bool le(Quote q) { 
            return low <= q.low && high <= q.high && open <= q.open && close <= q.close && volume <= q.volume;
        }
    }

    const int nRecords = 100000;
    const int pagePoolSize = 32 * 1024 * 1024;
    const int MAX_PRICE = 100;
    const int MAX_VOLUME = 10000;
    const int EPSILON = 100;
    const int KD_TREE_OPTIMIZATION_THRESHOLD = 2;
	
    static Quote getRandomQuote(Random r) { 
        Quote q = new Quote();
        q.timestamp = (int)(DateTime.Now.Ticks/10000000);
        q.low = (float)r.Next(MAX_PRICE*10)/10;
        q.high = q.low + (float)r.Next(MAX_PRICE*10)/10;
        q.open = (float)r.Next(MAX_PRICE*10)/10;
        q.close = (float)r.Next(MAX_PRICE*10)/10;
        q.volume = r.Next(MAX_VOLUME);
        return q;
    }

    static public void Main(string[] args)
    {
        Storage db = StorageFactory.Instance.CreateStorage();
        db.Open("testkdtree.dbs", pagePoolSize);
        int n;

#if USE_GENERICS
        MultidimensionalIndex<Quote> index = (MultidimensionalIndex<Quote>)db.Root;
        if (index == null) { 
            index = db.CreateMultidimensionalIndex<Quote>(new string[] { "low", "high", "open", "close", "volume" }, false);
            db.Root = index;
        }
#else
        MultidimensionalIndex index = (MultidimensionalIndex)db.Root;
        if (index == null) { 
            index = db.CreateMultidimensionalIndex(typeof(Quote), new string[] { "low", "high", "open", "close", "volume" }, false);
            db.Root = index;
        }
#endif
        DateTime start = DateTime.Now; 
        Random r = new Random(2007);
        for (int i = 0; i < nRecords; i++) { 
            Quote q = getRandomQuote(r);
            index.Add(q);
        }
        db.Commit();
        Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));
		
        Console.WriteLine("Tree height: " + index.Height);
        if (index.Count > 1 && index.Height/Math.Log(index.Count)*Math.Log(2.0) > KD_TREE_OPTIMIZATION_THRESHOLD) { 
            start = DateTime.Now; 
            index.Optimize();
            Console.WriteLine("New tree height: " + index.Height);
            Console.WriteLine("Elapsed time for tree optimization: " + (DateTime.Now - start));
        }

        start = System.DateTime.Now;
        r = new Random(2007);
        for (int i = 0; i < nRecords; i++) {
            Quote q = getRandomQuote(r);            
#if USE_GENERICS
            Quote[] result = index.QueryByExample(q);
            Debug.Assert(result.Length >= 1);
            for (int j = 0; j < result.Length; j++) { 
                Debug.Assert(q.eq(result[j]));
            }
#else
            object[] result = index.QueryByExample(q);
            Debug.Assert(result.Length >= 1);
            for (int j = 0; j < result.Length; j++) { 
                Debug.Assert(q.eq((Quote)result[j]));
            }
#endif
        }
        Console.WriteLine("Elapsed time for performing " + nRecords + " query by example searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        r = new Random(2007);
        Random r2 = new Random(2008);
        long total = 0;
        for (int i = 0; i < nRecords; i++) {
            Quote q = getRandomQuote(r);            
            Quote min = new Quote();
            Quote max = new Quote();

            min.low = q.low - (float)MAX_PRICE/EPSILON;
            min.high = q.high - (float)MAX_PRICE/EPSILON;
            min.open = q.open - (float)MAX_PRICE/EPSILON;
            min.close = q.close - (float)MAX_PRICE/EPSILON;
            min.volume = q.volume - MAX_VOLUME/EPSILON;

            max.low = q.low + (float)MAX_PRICE/EPSILON;
            max.high = q.high + (float)MAX_PRICE/EPSILON;
            max.open = q.open + (float)MAX_PRICE/EPSILON;
            max.close = q.close + (float)MAX_PRICE/EPSILON;
            max.volume = q.volume + MAX_VOLUME/EPSILON;

            n = 0;
            foreach (Quote quote in index.Range(min, max))
            {
                Debug.Assert(min.le(quote));
                Debug.Assert(quote.le(max));
                n += 1;
            }
            Debug.Assert(n >= 1);
            total += n;
        }
        Console.WriteLine("Elapsed time for  performing " + nRecords + " range query by example searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        n = 0;
        foreach (Quote q in index)
        {
            n += 1;
        }
        Debug.Assert(n == nRecords);
        Console.WriteLine("Elapsed time for iterating through " + nRecords + " records: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        n = 0;
        foreach (Quote q in index)
        {
            bool deleted = index.Remove(q);
            Debug.Assert(deleted);
            q.Deallocate();
            n += 1;
        }
        db.Commit();
        Debug.Assert(n == nRecords);
        Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));

        Debug.Assert(!index.GetEnumerator().MoveNext());
        db.Close();
    }
}