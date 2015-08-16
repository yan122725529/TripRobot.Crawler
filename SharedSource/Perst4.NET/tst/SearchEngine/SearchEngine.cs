using System;
using System.IO;
using System.Diagnostics;
using Perst;
using Perst.FullText;

public class SearchEngine
{
    const int PAGE_POOL_SIZE = 256 * 1024 * 1024;
    const string LANGUAGE = "en";
    const int MAX_RESULTS = 1000;
    const int SEARCH_TIME_LIMIT = 2 * 1000; // 2 seconds
    static string[] INDEXED_FILE_TYPES = { "html", "htm", "txt", "c", "cpp", "java", "cs", "h" };
    const int MAX_FILE_SIZE = 1024 * 1024;

    class Workspace : Persistent
    {
        internal FullTextIndex index;
#if USE_GENERICS
        internal FieldIndex<string, TextFile> files;
#else
        internal FieldIndex files;
#endif

        internal Workspace(Storage storage)
            : base(storage)
        {
            this.index = storage.CreateFullTextIndex();
#if USE_GENERICS
            this.files = storage.CreateFieldIndex<string, TextFile>("path", true);
#else
            this.files = storage.CreateFieldIndex(typeof(TextFile), "path", true);
#endif
        }

        Workspace() { }
    }

    class TextFile : Persistent
    {
        internal String path;
        internal DateTime lastModified;

        internal TextFile(string path)
        {
            this.path = path;
            lastModified = File.GetLastWriteTime(path);
        }

        TextFile() { }
    }

    static void addFiles(Workspace ws, string dir)
    {  
        string[] files;
        try
        {       
            files = Directory.GetFiles(dir);
        } 
        catch (Exception)      
        {
            return;
        }
        foreach (string f in files)
        { 
            string fileName = f.ToLower();
            foreach (string extension in INDEXED_FILE_TYPES) 
            { 
                if (fileName.EndsWith("." + extension)) 
                { 
                    TextFile file = (TextFile)ws.files[f];
                    if (file == null) 
                    { 
                        file = new TextFile(f);
                        ws.files.Put(file);
                    }
                    FileStream stream;
                    try 
                    {
                        stream  = new FileStream(f, FileMode.Open); 
                    } 
                    catch (Exception) 
                    {
                        continue; 
                    }
                    if (stream.Length <= MAX_FILE_SIZE) 
                    { 
                        StreamReader reader = new StreamReader(stream, System.Text.Encoding.Default);
                        ws.index.Add(file, reader, LANGUAGE);
                        reader.Close();
                    }
                    stream.Close();
                    break;
                }
            }                
        }
        foreach (string subdir in Directory.GetDirectories(dir)) 
        {
             addFiles(ws, subdir);
        }
    }

    static void skip(string prompt)
    {
        Console.Write(prompt);
        Console.ReadLine();
    }

    static string input(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            String line = Console.ReadLine().Trim();
            if (line.Length != 0)
            {
                return line;
            }
        }
    }

    public static void Main(string[] args)
    {
        Storage db = StorageFactory.Instance.CreateStorage();
        db.Open("searchengine.dbs", PAGE_POOL_SIZE);
        Workspace ws = (Workspace)db.Root;
        if (ws == null)
        {
            ws = new Workspace(db);
            db.Root = ws;
        }
        if (args.Length != 0)
        {
            DateTime start = DateTime.Now;
            int nFiles = ws.files.Count;
            for (int i = 0; i < args.Length; i++)
            {
                addFiles(ws, args[i]);
            }
            db.Commit();
            Console.WriteLine((ws.files.Count - nFiles) + " files are imported to the workspace in "
                               + (DateTime.Now - start));
        }
        while (true)
        {
            switch (input("-------------------------------------\n" +
                          "Menu:\n" +
                          "1. Index files\n" +
                          "2. Search\n" +
                          "3. Statistic\n" +
                          "4. Exit\n\n>>"))
            {
                case "1":
                    {
                        DateTime start = DateTime.Now;
                        int nFiles = ws.files.Count;
                        addFiles(ws, input("Root directory: "));
                        db.Commit();
                        Console.WriteLine((ws.files.Count - nFiles) + " files are imported to the workspace in "
                            + (DateTime.Now - start));
                        break;
                    }
                case "2":
                    {
                        DateTime start = DateTime.Now;
                        FullTextSearchResult result = ws.index.Search(input("Query: "), LANGUAGE, MAX_RESULTS, SEARCH_TIME_LIMIT);
                        for (int i = 0; i < result.Hits.Length; i++)
                        {
                            Console.WriteLine(result.Hits[i].Rank + "\t" + ((TextFile)result.Hits[i].Document).path);
                        }
                        Console.WriteLine("Elapsed search time for " + result.Hits.Length + " matched resuts: " + (DateTime.Now - start));

                        break;
                    }
                case "3":
                    {
                        Console.WriteLine("Number of indexed documents: " + ws.index.NumberOfDocuments);
                        Console.WriteLine("Number total number of words: " + ws.index.NumberOfWords);
                        break;
                    }
                case "4":
                    {
                        db.Close();
                        return;
                    }
            }
            skip("Press ENTER to continue...");
        }
    }
}
