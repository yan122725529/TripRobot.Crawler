using System;
using Perst;
using System.IO;
using System.Collections;

public class TestAlloc 
{ 
    public static void Main(string[] args) 
    { 
        Storage db = StorageFactory.Instance.CreateStorage();
        db.SetProperty("perst.concurrent.iterator", true);
        string path = Directory.Exists("tst") ? "./" : "../../";
        db.Open("@" + path + "tst/TestAlloc/testalloc.mfd", 128*1024);
        byte[] buf = new byte[1024];
        int rc;
        string[] files = Directory.GetFiles(path + "src/impl", "*.cs");
#if USE_GENERICS
        Index<string,Blob> root = (Index<string,Blob>)db.Root;
#else
        Index root = (Index)db.Root;
#endif
        if (root == null || root.Count == 0) 
        { 
            if (root == null)
            {
#if USE_GENERICS
                root = db.CreateIndex<string,Blob>(true);
#else
                root = db.CreateIndex(typeof(string), true);
#endif
                db.Root = root;
                db.RegisterCustomAllocator(typeof(Blob), db.CreateBitmapAllocator(1024, 0x1000000000000000L, 0x100000, long.MaxValue));
            }
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
        } else { 
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

            IDictionaryEnumerator e = root.GetDictionaryEnumerator();
            while (e.MoveNext()) 
            {
                Blob file = (Blob)e.Value;
                root.Remove((string)e.Key, file);
                file.Deallocate();
            }
            //root.Put("dummy", db.CreateBlob());
            Console.WriteLine("Cleanup completed");
        }
        db.Close();
    }
}

