using System;
using Perst;
using System.IO;

public class TestBlob 
{ 
    public static void Main(string[] args) 
    { 
        Storage db = StorageFactory.Instance.CreateStorage();
        bool large = false;
        bool compressed = false;
        bool encrypted = false;
        string password = "MyPassword";
        long pagePoolSize = 1024 * 1024;

        for (int i = 0; i < args.Length; i++) 
        {
            switch (args[i]) 
            {
                case "zip": 
                    compressed = true;
                    break;
                case "large":
                    large = true;
                    break;
                case "crypt":
                    encrypted = true;
                    break;
                case "pool":
                    pagePoolSize = long.Parse(args[++i]);
                    break;
                default:
                    Console.WriteLine("Unknown option: " + args[i]);
                    return;
             }
        }
#if !NET_FRAMEWORK_10 && (!COMPACT_NET_FRAMEWORK || COMPACT_NET_FRAMEWORK_35)
        if (compressed || encrypted) 
        {            
            db.Open(new CompressedFile("testblob.dbs", encrypted ? password : null), pagePoolSize);
        }
        else
#endif
        if (encrypted) 
        {
            db.Open("testidx.dbs", pagePoolSize, password);
        }
        else         
        {
            db.Open("testblob.dbs", pagePoolSize);
        }
        byte[] buf = new byte[1024];
        int rc;

        bool largeFile = args.Length == 0 || args[0] == "large";
        string path = Directory.Exists("src") ? "src/impl/" : "../../src/impl/";

        if (largeFile) 
        {
            FileStream fs = new FileStream(path + "dummy.cs", FileMode.Create);
            const long LargeFileSize = 128*1024*1024;
            byte bn = 0;
            for (long size = 0; size < LargeFileSize; size += buf.Length) 
            {
                for (int i = 0; i < buf.Length; i++) 
                { 
                    buf[i] = bn;
                }
                fs.Write(buf, 0, buf.Length);
                bn +=1;
            }
            fs.Close();
        }

        string[] files = Directory.GetFiles(path, "*.cs");
#if USE_GENERICS
        Index<string,Blob> root = (Index<string,Blob>)db.Root;
#else
        Index root = (Index)db.Root;
#endif
        if (root == null) 
        { 
#if USE_GENERICS
            root = db.CreateIndex<string,Blob>(true);
#else
            root = db.CreateIndex(typeof(string), true);
#endif
            db.Root = root;
            foreach (string file in files) 
            { 
                FileStream fin = new FileStream(file, FileMode.Open, FileAccess.Read);
                Blob blob = db.CreateBlob();                    
                Stream bout = blob.GetStream();
                while ((rc = fin.Read(buf, 0, buf.Length)) > 0) 
                { 
                    bout.Write(buf, 0, rc);
                }
                root[file] = blob; 
                fin.Close();
                bout.Close();   
            }
            Console.WriteLine("Database is initialized");
        } 
        foreach (string file in files) 
        {
            byte[] buf2 = new byte[1024];
#if USE_GENERICS
            Blob blob = root[file];
#else
            Blob blob = (Blob)root[file];
#endif
            if (blob == null) 
            {
                Console.WriteLine("File " + file + " not found in database");
                continue;
            }
            Stream bin = blob.GetStream();
            FileStream fin = new FileStream(file, FileMode.Open, FileAccess.Read);
            while ((rc = fin.Read(buf, 0, buf.Length)) > 0) 
            { 
                int rc2 = bin.Read(buf2, 0, buf2.Length);
                if (rc != rc2) 
                {
                    Console.WriteLine("Different file size: " + rc + " .vs. " + rc2);
                    break;
                }
                while (--rc >= 0 && buf[rc] == buf2[rc]);
                if (rc >= 0) 
                { 
                    Console.WriteLine("Content of the files is different: " + buf[rc] + " .vs. " + buf2[rc]);
                    break;
                }
            }
            fin.Close();
            bin.Close();
        }            
        Console.WriteLine("Verification completed");
        db.Close();
        if (largeFile) 
        {
            File.Delete(path + "dummy.cs"); 
        }
    }
}

