using System;
using System.Collections;
using System.Diagnostics;

using Perst;

public class TestCodeGenerator 
{ 
    class Track 
    { 
        public int no;
    
        [Indexable]
        public Album album;
    
        [Indexable]
        public String name;
    
        public float duration;
    }
    
    class Album 
    { 
        [Indexable]
        public String name;
      
        [Indexable]    
        public RecordLabel label;
    
        public String genre;
    
        public DateTime release;
    }
    
    class RecordLabel 
    { 
        [Indexable]
        public String name;
    
        public String email;
        public String phone;
        public String address;
    }
    
    class QueryExecutionListener : StorageListener
    {
        public int nSequentialSearches;
        public int nSorts;
    
        override public void SequentialSearchPerformed(object query) 
        {
            nSequentialSearches += 1;
        }        
    
        override public void SortResultSetPerformed(object query) 
        {
            nSorts += 1;
        }        
    }
    

    const int nLabels = 100;
    const int nAlbums = 10000;
    const int nTracksPerAlbum = 10;
    
    public static void Main(String[] args) 
    { 
        Storage storage = StorageFactory.Instance.CreateStorage(); 
        storage.Open("testcodegenerator.dbs");
        Database db = new Database(storage);

        DateTime start = DateTime.Now;

        for (int i = 0; i < nLabels; i++) 
        { 
            RecordLabel label = new RecordLabel();
            label.name = "Label" + i;
            label.email = "contact@" + label.name + ".com";
            label.address = "Country, City, Street";
            label.phone = "+1 123-456-7890";
            db.AddRecord(label);
        }        

        for (int i = 0; i < nAlbums; i++) 
        { 
            Album album = new Album();
            album.name = "Album" + i;
            album.label = (RecordLabel)Enumerable.First(db.Select(typeof(RecordLabel), "name='Label" + (i % nLabels) + "'"));
            album.genre = "Rock";
            album.release = DateTime.Now;
            db.AddRecord(album);
            
            for (int j = 0; j < nTracksPerAlbum; j++) 
            { 
                Track track = new Track();
                track.no = j+1;
                track.name = "Track" + j;
                track.album = album;
                track.duration = 3.5f;
                db.AddRecord(track);                
            }
        }

        Console.WriteLine("Elapsed time for database initialization: " + (DateTime.Now - start));

        QueryExecutionListener listener = new QueryExecutionListener();
        storage.Listener = listener;

        Query trackQuery = db.CreateQuery(typeof(Track));
        CodeGenerator code = trackQuery.GetCodeGenerator();
        code.Predicate(code.And(code.Gt(code.Field("no"), 
                                        code.Literal(0)), 
                                code.Eq(code.Field(code.Field(code.Field("album"), "label"), "name"),
                                        code.Parameter(1, typeof(string)))));
        start = DateTime.Now;
        int nTracks = 0;
        for (int i = 0; i < nLabels; i++) 
        {
            trackQuery[1] = "Label" + i;
            foreach (Track t in trackQuery) 
            { 
                nTracks += 1;
            }
        }
        Console.WriteLine("Elapsed time for searching of " + nTracks + " tracks: " + (DateTime.Now - start));
        Debug.Assert(nTracks == nAlbums*nTracksPerAlbum);

        String prev = "";
        int n = 0;
        Query labelQuery = db.CreateQuery(typeof(RecordLabel));
        code = labelQuery.GetCodeGenerator();
        code.OrderBy("name");
        foreach (RecordLabel label in labelQuery)
        {
            Debug.Assert(prev.CompareTo(label.name) < 0);
            prev = label.name;
            n += 1;
        }
        Debug.Assert(n == nLabels);

        prev = "";
        n = 0;
        code = labelQuery.GetCodeGenerator();
        code.Predicate(code.Like(code.Field("name"), 
                                 code.Literal("Label%")));
        code.OrderBy("name");
        foreach (RecordLabel label in labelQuery)
        {
            Debug.Assert(prev.CompareTo(label.name) < 0);
            prev = label.name;
            n += 1;
        }
        Debug.Assert(n == nLabels);

        n = 0;
        code = labelQuery.GetCodeGenerator();
        code.Predicate(code.In(code.Field("name"), 
                               code.List(code.Literal("Label1"), code.Literal("Label2"), code.Literal("Label3"))));
        foreach (RecordLabel label in labelQuery)
        {
            n += 1;
        }
        Debug.Assert(n == 3);

        n = 0;
        code = labelQuery.GetCodeGenerator();
        code.Predicate(code.And(code.Or(code.Eq(code.Field("name"),
                                                code.Literal("Label1")),
                                        code.Or(code.Eq(code.Field("name"),
                                                        code.Literal("Label2")),
                                                code.Eq(code.Field("name"),
                                                        code.Literal("Label3")))),
                                code.Like(code.Field("email"),
                                          code.Literal("contact@%"))));
        foreach (RecordLabel label in labelQuery)
        {
            n += 1;
        }
        Debug.Assert(n == 3);

        code = labelQuery.GetCodeGenerator();
        code.Predicate(code.And(code.Like(code.Field("phone"),
                                          code.Literal("+1%")),
                                code.In(code.Field("name"), 
                                        code.Parameter(1, typeof(ArrayList)))));
        ArrayList list = new ArrayList(nLabels);
        for (int i = 0; i < nLabels; i++) 
        {
            list.Add("Label" + i);
        }
        n = 0;
        labelQuery[1] = list;
        foreach (RecordLabel label in labelQuery) 
        { 
            Debug.Assert(label.name == "Label" + n++);
        }
        Debug.Assert(n == nLabels);        
        
        n = 0;
        code = trackQuery.GetCodeGenerator();
        code.Predicate(code.Or(code.Eq(code.Field(code.Field(code.Field("album"), "label"), "name"),
                                       code.Literal("Label1")),
                               code.Eq(code.Field(code.Field(code.Field("album"), "label"), "name"),
                                       code.Literal("Label2"))));
        foreach (Track track in trackQuery)
        {
            Debug.Assert(track.album.label.name == "Label1" || track.album.label.name == "Label2");
            n += 1;
        }
        Debug.Assert(n == nAlbums*nTracksPerAlbum*2/nLabels);

        Debug.Assert(listener.nSequentialSearches == 0);
        Debug.Assert(listener.nSorts == 0);


        db.DropTable(typeof(Track));
        db.DropTable(typeof(Album));
        db.DropTable(typeof(RecordLabel));

        storage.Close();
    }
}        