using System;
using System.Collections;
using Perst;
using System.Diagnostics;

public class Restaurant : Persistent
{                                               
    public float lat;
    public float lng; 
    public string kitchen;
    public int avgPrice;
    public int rating;
}

public class City : Persistent
{
#if USE_GENERICS
    public SpatialIndexR2<Restaurant> byLocation;
    public FieldIndex<string,Restaurant> byKitchen;
    public FieldIndex<int,Restaurant> byAvgPrice;
    public FieldIndex<int,Restaurant> byRating;
#else
    public SpatialIndexR2 byLocation;
    public FieldIndex byKitchen;
    public FieldIndex byAvgPrice;
    public FieldIndex byRating;
#endif
}

public class TestBitmap 
{ 
    const int nRecords = 1000000;
    const int nSearches = 1000;
    const int pagePoolSize = 48*1024*1024;
    static string[] kitchens = {"asian", "chines", "european", "japan", "italian", "french", "medeteranian", "nepal", "mexican", "indian", "vegetarian"};

    static public void Main(string[] args) 
    {    
        Storage db = StorageFactory.Instance.CreateStorage();
        db.Open("testbitmap.dbs", pagePoolSize);

        City city = (City)db.Root;
        if (city == null) { 
            city = new City();
#if USE_GENERICS
            city.byLocation = db.CreateSpatialIndexR2<Restaurant>();
            city.byKitchen = db.CreateFieldIndex<string,Restaurant>("kitchen", false, true, true);
            city.byAvgPrice = db.CreateFieldIndex<int,Restaurant>("avgPrice", false, true, true);
            city.byRating = db.CreateFieldIndex<int,Restaurant>("rating", false, true, true);
#else
            city.byLocation = db.CreateSpatialIndexR2();
            city.byKitchen = db.CreateFieldIndex(typeof(Restaurant), "kitchen", false, true, true);
            city.byAvgPrice = db.CreateFieldIndex(typeof(Restaurant), "avgPrice", false, true, true);
            city.byRating = db.CreateFieldIndex(typeof(Restaurant), "rating", false, true, true);
#endif
            db.Root = city;
        }
        DateTime start = DateTime.Now;
        Random rnd = new Random(2013);
        for (int i = 0; i < nRecords; i++) { 
            Restaurant rest = new Restaurant();
            rest.lat = 55 + (float)rnd.NextDouble();
            rest.lng = 37 + (float)rnd.NextDouble();
            rest.kitchen = kitchens[rnd.Next(kitchens.Length)];
            rest.avgPrice = rnd.Next(1000);
            rest.rating = rnd.Next(10);
            city.byLocation.Put(new RectangleR2(rest.lat, rest.lng, rest.lat, rest.lng), rest);
            city.byKitchen.Put(rest);
            city.byAvgPrice.Put(rest);
            city.byRating.Put(rest);
        }
        db.Commit();
        Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));

        start = DateTime.Now;              
        long total = 0;
        for (int i = 0; i  < nSearches; i++) {
            double lat = 55 + rnd.NextDouble();
            double lng = 37 + rnd.NextDouble();
            String kitchen = kitchens[rnd.Next(kitchens.Length)]; 
            int minPrice = rnd.Next(1000);
            int maxPrice = minPrice + rnd.Next(1000);
            int minRating = rnd.Next(10);
            Bitmap bitmap = db.CreateBitmap(city.byKitchen.GetEnumerator(kitchen, kitchen));
            bitmap.And(db.CreateBitmap(city.byAvgPrice.GetEnumerator(minPrice, maxPrice)));
            bitmap.And(db.CreateBitmap(city.byRating.GetEnumerator(new Key(minRating), null)));
            PersistentEnumerator enumerator = (PersistentEnumerator)city.byLocation.Neighbors(lat, lng).GetEnumerator();

            int nAlternatives = 0; 
            while (enumerator.MoveNext()) { 
                int oid = enumerator.CurrentOid;
                if (bitmap.Contains(oid)) { 
                    Restaurant rest = (Restaurant)db.GetObjectByOID(oid);
                    total += 1;
                    if (++nAlternatives == 10) { 
                        break;
                    }
                }
            }
        }
        Console.WriteLine("Elapsed time for " + nSearches + " searches of " + total + " variants among " + nRecords + " records: " 
                           + (DateTime.Now - start) + " milliseconds");
        db.Close();
    }
}
