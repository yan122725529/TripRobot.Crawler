using System;
using System.IO;
using System.Diagnostics;
using Perst;
using Perst.FullText;

public class TestFullTextIndex
{
    internal const string LANGUAGE = "en";
    internal const int SEARCH_TIME_LIMIT = 2 * 1000; // 2 seconds
    
    [Serializable]
    internal class SourceFile:Persistent, FullTextSearchable
    {
        internal string name;

        internal string path;

        internal Blob body;
        
        public TextReader Text
        {
            get
            {
                return new StreamReader(body.GetStream(), System.Text.Encoding.Default);
            }
        }
        
        public string Language
        {
            get
            { 
                return LANGUAGE;
            }
        }
        
        internal SourceFile(Storage storage, string path):base(storage)
        {
            this.path = path;
            this.name = path.Substring(path.LastIndexOf('\\')+1);
            body = storage.CreateBlob();
            Stream outs = body.GetStream();
            Stream ins = new FileStream(path, FileMode.Open, FileAccess.Read);
            byte[] buf = new byte[64 * 1024];
            int rc;
            while ((rc = ins.Read(buf, 0, buf.Length)) > 0)
            {
                outs.Write(buf, 0, rc);
            }
            outs.Close();
            ins.Close();
        }
        
        internal SourceFile()
        {
        }
    }
    
    [Serializable]
    internal class Project:Persistent
    {
        internal FullTextIndex index;
#if USE_GENERICS
        internal FieldIndex<string, SourceFile> sources;
#else
        internal FieldIndex sources;
#endif        

        internal Project(Storage storage):base(storage)
        {
            this.index = storage.CreateFullTextIndex();
#if USE_GENERICS
            this.sources = storage.CreateFieldIndex<string, SourceFile>("path", true);
#else
            this.sources = storage.CreateFieldIndex(typeof(SourceFile), "path", true);
#endif
        }
        
        internal Project()
        {
        }
    }
    
    internal static void PrintResult(FullTextSearchResult result)
    {
        for (int i = 0; i < result.Hits.Length; i++)
        {
            Console.WriteLine(result.Hits[i].Rank + "\t" + ((SourceFile) result.Hits[i].Document).name);
        }
    }
    
    public static void  Main(string[] args)
    {
        bool reload = args.Length > 0 && "reload".Equals(args[0]);
        DateTime start;          

        Storage db = StorageFactory.Instance.CreateStorage();
        db.Open("testfulltext.dbs");
        
        Project project = (Project) db.Root;
        if (project == null)
        {
            project = new Project(db);
            db.Root = project;
        }
        if (project.sources.Count == 0 || reload)
        {
            string path = Directory.Exists("src") ? "" : "../../";
            string[] files = Directory.GetFiles(path + "src", "*.cs");
            start = DateTime.Now;
            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                SourceFile file = (SourceFile) project.sources[filePath];
                if (file == null)
                {
                    file = new SourceFile(db, filePath);
                    project.sources.Put(file);
                }
                project.index.Add(file);
            }
            System.Console.Out.WriteLine(project.sources.Count + " files are imported to the project in " + (DateTime.Now - start));
        }
        FullTextSearchResult result;
        start = DateTime.Now;

        Console.WriteLine("Total documents: " + project.index.NumberOfDocuments + " with " + project.index.NumberOfWords + " unique words");
        Debug.Assert(project.index.NumberOfDocuments == project.sources.Count);
        
        result = project.index.Search("persistent capable objects", LANGUAGE, 100, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 4 && result.Hits.Length == 4);
        
        
        result = project.index.Search("namespace", LANGUAGE, 10, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == project.sources.Count-1 && result.Hits.Length == 10);
        
        result = project.index.Search("MultidimensionalIndex OR SpatialIndex", LANGUAGE, 10, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 3 && result.Hits.Length == 3);
        
        result = project.index.Search("(MultidimensionalIndex AND ThickIndex) OR (SpatialIndex AND NOT SpatialIndexR2)", LANGUAGE, 10, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 1 && result.Hits.Length == 1 
            && ((SourceFile)result.Hits[0].Document).name == "SpatialIndex.cs");

        result = project.index.Search("(MultidimensionalIndex AND CompoundIndex) OR (SpatialIndex AND NOT SpatialIndexR2)", LANGUAGE, 10, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 2 && result.Hits.Length == 2);

        result = project.index.Search("namespace", LANGUAGE, 1000, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == result.Hits.Length && project.sources.Count-1 == result.Hits.Length);
        
        result = project.index.Search("public interface FieldIndex", LANGUAGE, 100, SEARCH_TIME_LIMIT);
        PrintResult(result);
        Debug.Assert(result.Hits.Length > 1 && result.Estimation == result.Hits.Length 
            && ((SourceFile) result.Hits[0].Document).name == "FieldIndex.cs");

        result = project.index.Search("\"public interface FieldIndex\"", LANGUAGE, 100, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Hits.Length == 1 && result.Estimation == 1
            && ((SourceFile)result.Hits[0].Document).name == "FieldIndex.cs");
        
        result = project.index.Search("Multiplatform", LANGUAGE, 100, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 0 && result.Hits.Length == 0);
        
        result = project.index.Search("namespace AND NOT perst", LANGUAGE, 100, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 0 && result.Hits.Length == 0);
        
        result = project.index.Search("to be or not to be", LANGUAGE, 100, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation > 0 && result.Hits.Length == result.Estimation);
        
        result = project.index.Search("\"to be or not to be\"", LANGUAGE, 100, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 0 && result.Hits.Length == 0);
        
        result = project.index.Search("perst", LANGUAGE, 0, 0);
        Debug.Assert(result.Estimation == project.sources.Count && result.Hits.Length == 0);

        result = project.index.Search("\"%Project Directory%\\obj\\<configuration>\"", LANGUAGE, 1, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == 1 && result.Hits.Length == 1
            && ((SourceFile)result.Hits[0].Document).name == "AssemblyInfo.cs");

        result = project.index.Search("namespace.perst{public}", LANGUAGE, 1000, SEARCH_TIME_LIMIT);
        Debug.Assert(result.Estimation == result.Hits.Length && project.sources.Count-1 == result.Hits.Length);
        Console.WriteLine("Elapsed time for full text searches: " + (DateTime.Now - start));
        
        db.Close();
    }
}