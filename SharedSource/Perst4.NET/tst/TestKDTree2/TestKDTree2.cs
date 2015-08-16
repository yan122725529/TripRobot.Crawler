using System;
using System.Collections;
using Perst;
using System.Diagnostics;

public class TestKdtree2
{
    class Stock : Persistent
    { 
        public string symbol;
        public float  price;
        public int    volume;

        public bool eq(Stock s) { 
            return symbol == s.symbol && price == s.price && volume == s.volume;
        }
        
        public bool le(Stock s) { 
            return price <= s.price && volume <= s.volume;
        }
    }

#if USE_GENERICS
    class StockComparator : MultidimensionalComparator<Stock>
    {
        public override CompareResult Compare(Stock s1, Stock s2, int i) { 
#else
    class StockComparator : MultidimensionalComparator
    {
        public override CompareResult Compare(object m1, object m2, int i) { 
            Stock s1 = (Stock)m1;
            Stock s2 = (Stock)m2;
#endif
            switch (i) { 
            case 0:
                if (s1.symbol == null && s2.symbol == null) { 
                    return CompareResult.EQ;
                } else if (s1.symbol == null) { 
                    return CompareResult.LEFT_UNDEFINED;
                } else if (s2.symbol == null) { 
                    return CompareResult.RIGHT_UNDEFINED;
                } else { 
                    int diff = s1.symbol.CompareTo(s2.symbol);
                    return diff < 0 ? CompareResult.LT : diff == 0 ? CompareResult.EQ : CompareResult.GT;
                }
            case 1:
                return s1.price < s2.price ? CompareResult.LT : s1.price == s2.price ? CompareResult.EQ : CompareResult.GT;
            case 2:
                return s1.volume < s2.volume ? CompareResult.LT : s1.volume == s2.volume ? CompareResult.EQ : CompareResult.GT;
            default:
                throw new InvalidOperationException();
            }
        }
                
        public override int NumberOfDimensions 
        { 
            get
            {
                return 3;
            }
        }
        
#if USE_GENERICS
        public override Stock CloneField(Stock src, int i) { 
#else
        public override object CloneField(object obj, int i) { 
            Stock src = (Stock)obj;
#endif
            Stock clone = new Stock();
            switch (i) { 
            case 0:
                clone.symbol = src.symbol;
                break;
            case 1:
                clone.price = src.price;
                break;
             case 2:
                clone.volume = src.volume;
                break;
             default:
                throw new InvalidOperationException();
            }
            return clone;
        }
    }
      
    const int nRecords = 100000;
    const int pagePoolSize = 16 * 1024 * 1024;
    const int MAX_SYMBOLS = 1000;
    const int MAX_PRICE = 100;
    const int MAX_VOLUME = 10000;
    const int EPSILON = 100;
    const int KD_TREE_OPTIMIZATION_THRESHOLD = 3;
	
    static Stock getRandomStock(Random r) { 
        Stock s = new Stock();
        s.symbol = r.Next(MAX_SYMBOLS).ToString();
        s.price = (float)r.Next(MAX_PRICE*10)/10;
        s.volume = r.Next(MAX_VOLUME);
        return s;
    }

    static public void Main(string[] args)
    {
        Storage db = StorageFactory.Instance.CreateStorage();
        db.Open("testkdtree2.dbs", pagePoolSize);
        int n;

#if USE_GENERICS
        MultidimensionalIndex<Stock> index = (MultidimensionalIndex<Stock>)db.Root;
        if (index == null) { 
            index = db.CreateMultidimensionalIndex<Stock>(new StockComparator());
            db.Root = index;
        }
#else
        MultidimensionalIndex index = (MultidimensionalIndex)db.Root;
        if (index == null) { 
            index = db.CreateMultidimensionalIndex(new StockComparator());
            db.Root = index;
        }
#endif
        DateTime start = DateTime.Now; 
        Random r = new Random(2007);
        for (int i = 0; i < nRecords; i++) { 
            Stock s = getRandomStock(r);
            index.Add(s);
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
            Stock s = getRandomStock(r);   
#if USE_GENERICS
            Stock[] result = index.QueryByExample(s);
            Debug.Assert(result.Length >= 1);
            for (int j = 0; j < result.Length; j++) { 
                Debug.Assert(s.eq(result[j]));
            }
#else
            object[] result = index.QueryByExample(s);
            Debug.Assert(result.Length >= 1);
            for (int j = 0; j < result.Length; j++) { 
                Debug.Assert(s.eq((Stock)result[j]));
            }
#endif
        }
        Console.WriteLine("Elapsed time for performing " + nRecords + " query by example searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        r = new Random(2007);
        Random r2 = new Random(2008);
        long total = 0;
        for (int i = 0; i < nRecords; i++) {
            Stock s = getRandomStock(r);            
            Stock min = new Stock();
            Stock max = new Stock();

            min.price = s.price - (float)MAX_PRICE/EPSILON;
            min.volume = s.volume - MAX_VOLUME/EPSILON;

            max.price = s.price + (float)MAX_PRICE/EPSILON;
            max.volume = s.volume + MAX_VOLUME/EPSILON;

            n = 0;
            foreach (Stock stock in index.Range(min, max))
            {
                Debug.Assert(min.le(stock));
                Debug.Assert(stock.le(max));
                n += 1;
            }
            Debug.Assert(n >= 1);
            total += n;
        }
        Console.WriteLine("Elapsed time for  performing " + nRecords + " range query by example searches: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        n = 0;
        foreach (Stock s in index)
        {
            n += 1;
        }
        Debug.Assert(n == nRecords);
        Console.WriteLine("Elapsed time for iterating through " + nRecords + " records: " + (DateTime.Now - start));

        start = System.DateTime.Now;
        n = 0;
        foreach (Stock s in index)
        {
            bool deleted = index.Remove(s);
            Debug.Assert(deleted);
            s.Deallocate();
            n += 1;
        }
        db.Commit();
        Debug.Assert(n == nRecords);
        Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));

        Debug.Assert(!index.GetEnumerator().MoveNext());
        db.Close();
    }
}