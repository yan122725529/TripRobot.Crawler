using System;
using System.Collections;
using System.Diagnostics;

using Perst;

public class TestAutoIndices 
{ 
    class Track 
    { 
        public int no;
        public Album album;
        public String name;
        public float duration;
    }
    
    class Album 
    { 
        public String name;
        public RecordLabel label;
        public String genre;
        public DateTime release;
    }
    
    class RecordLabel 
    { 
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
        storage.Open("testautoindices.dbs");
        Database db = new Database(storage);

        db.EnableAutoIndices = true;

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

        Query query = db.Prepare(typeof(Track), "album.label.name=?");
        start = DateTime.Now;
        int nTracks = 0;
        for (int i = 0; i < nLabels; i++) 
        {
            query[1] = "Label" + i;
            foreach (Track t in query) 
            { 
                nTracks += 1;
            }
        }
        Console.WriteLine("Elapsed time for searching of " + nTracks + " tracks: " + (DateTime.Now - start));
        Debug.Assert(nTracks == nAlbums*nTracksPerAlbum);

        String prev = "";
        int n = 0;
        foreach (RecordLabel label in db.Select(typeof(RecordLabel), "order by name"))
        {
            Debug.Assert(prev.CompareTo(label.name) < 0);
            prev = label.name;
            n += 1;
        }
        Debug.Assert(n == nLabels);

        prev = "";
        n = 0;
        foreach (RecordLabel label in db.Select(typeof(RecordLabel), "name like 'Label%' order by name"))
        {
            Debug.Assert(prev.CompareTo(label.name) < 0);
            prev = label.name;
            n += 1;
        }
        Debug.Assert(n == nLabels);

        n = 0;
        foreach (RecordLabel label in db.Select(typeof(RecordLabel), "name in ('Label1', 'Label2', 'Label3')"))
        {
            n += 1;
        }
        Debug.Assert(n == 3);

        n = 0;
        foreach (RecordLabel label in db.Select(typeof(RecordLabel), "(name = 'Label1' or name = 'Label2' or name = 'Label3') and email like 'contact@%'"))
        {
            n += 1;
        }
        Debug.Assert(n == 3);

        Query query2 = db.Prepare(typeof(RecordLabel), "phone like '+1%' and name in ?");
        ArrayList list = new ArrayList(nLabels);
        for (int i = 0; i < nLabels; i++) 
        {
            list.Add("Label" + i);
        }
        n = 0;
        query2[1] = list;
        foreach (RecordLabel label in query2) 
        { 
            Debug.Assert(label.name == "Label" + n++);
        }
        Debug.Assert(n == nLabels);        
        
        n = 0;
        foreach (Track track in db.Select(typeof(Track), "album.label.name='Label1' or album.label.name='Label2'"))
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