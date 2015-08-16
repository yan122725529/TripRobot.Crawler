using System;
using System.Collections;
using System.Diagnostics;

using Perst;

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
    [Indexable(Unique=true, CaseInsensitive=true)]
    public String name;
  
    [Indexable]    
    public RecordLabel label;

    [Indexable(Thick=true, CaseInsensitive=true)]    
    public String genre;

    public DateTime release;
}

class RecordLabel
{ 
    [Indexable(Unique=true)]
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

public class TestJsqlJoin 
{ 
    const int nLabels = 100;
    const int nAlbums = 10000;
    const int nTracksPerAlbum = 10;
    
    static string[] GENRES = {"Rock", "Pop", "Jazz", "R&B", "Folk", "Classic"};

    public static void Main(String[] args) 
    { 
        Storage storage = StorageFactory.Instance.CreateStorage(); 
        storage.Open("testjsqljoin.dbs");
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
            album.genre = GENRES[i % GENRES.Length].ToLower();
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

        prev = "zzz";
        n = 0;
        foreach (RecordLabel label in db.Select(typeof(RecordLabel), "name like 'Label%' order by name desc"))
        {
            Debug.Assert(prev.CompareTo(label.name) > 0);
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
        string labelName = "Label2";
        foreach (Track track in db.Select(typeof(Track), "album.label.name='Label1' or album.label.name='Label2' order by album.label.name desc"))
        {
            Debug.Assert(track.album.label.name == labelName || track.album.label.name == (labelName = "Label1"));
            n += 1;
        }
        Debug.Assert(n == nAlbums*nTracksPerAlbum*2/nLabels);

        Query query3 = db.Prepare(typeof(Album), "label=?");
        n = 0;
        foreach (RecordLabel label in db.GetRecords(typeof(RecordLabel))) 
        {
            query3[1] = label;
            foreach (Album album in query3) 
            { 
                n += 1;
            }
        }
        Debug.Assert(n == nAlbums);

        Query query4 = db.Prepare(typeof(Album), "genre in ?");
        query4[1] = GENRES;
        n = 0;
        foreach (Album album in query4) 
        { 
            n += 1;
        }
        Debug.Assert(n == nAlbums);

        Debug.Assert(listener.nSequentialSearches == 0);
        Debug.Assert(listener.nSorts == 1);


        db.DropTable(typeof(Track));
        db.DropTable(typeof(Album));
        db.DropTable(typeof(RecordLabel));

        storage.Close();
    }
}        