using System;
using Perst;
using System.Diagnostics;


public class TestR2 : Persistent 
{ 
    class SpatialObject : Persistent 
    { 
        public RectangleR2 rect;
    }
    
#if USE_GENERICS
    SpatialIndexR2<SpatialObject> index;
#else
    SpatialIndexR2 index;
#endif

    const int nObjectsInTree = 1000;
    const int nIterations = 100000;

    public static void Main(String[] args) 
    { 
        Storage db = StorageFactory.Instance.CreateStorage();
        SpatialObject so;
        RectangleR2 r;
        DateTime start = DateTime.Now;
        if (args.Length > 0 && "noflush" == args[0]) 
        { 
            db.SetProperty("perst.file.noflush", true);
        }
        db.Open("testr2.dbs");
        TestR2 root = (TestR2)db.Root;
        if (root == null) 
        { 
            root = new TestR2();
#if USE_GENERICS
            root.index = db.CreateSpatialIndexR2<SpatialObject>();
#else
            root.index = db.CreateSpatialIndexR2();
#endif
            db.Root = root;
        }

        RectangleR2[] rectangles = new RectangleR2[nObjectsInTree];
        long key = 1999;
        for (int i = 0; i < nIterations; i++) 
        { 
            int j = i % nObjectsInTree;
            if (i >= nObjectsInTree) 
            { 
                r = rectangles[j];
#if USE_GENERICS
                SpatialObject[] sos = root.index.Get(r);
                SpatialObject   po = null;
#else
                object[] sos = root.index.Get(r);
                object po = null;
#endif
                int n = 0;
                for (int k = 0; k < sos.Length; k++) 
                { 
#if USE_GENERICS
                    so = sos[k];
#else
                    so = (SpatialObject)sos[k];
#endif
                    if (r.Equals(so.rect)) 
                    { 
                        po = so;
                    } 
                    else 
                    { 
                        Debug.Assert(r.Intersects(so.rect));
                    }
                }    
                Debug.Assert(po != null);
                for (int k = 0; k < nObjectsInTree; k++) 
                { 
                    if (r.Intersects(rectangles[k])) 
                    {
                        n += 1;
                    }
                }
                Debug.Assert(n == sos.Length);

                n = 0;
                foreach (SpatialObject o in root.index.Overlaps(r)) 
                {
                    Debug.Assert(o == sos[n++]);
                }
                Debug.Assert(n == sos.Length);

                root.index.Remove(r, po);
                db.Deallocate(po);
            }
            key = (3141592621L*key + 2718281829L) % 1000000007L;
            int top = (int)(key % 1000);
            int left = (int)(key / 1000 % 1000);            
            key = (3141592621L*key + 2718281829L) % 1000000007L;
            int bottom = top + (int)(key % 100);
            int right = left + (int)(key / 100 % 100);
            so = new SpatialObject();
            r = new RectangleR2(top, left, bottom, right);
            so.rect = r;
            rectangles[j] = r;
            root.index.Put(r, so);

            if (i % 100 == 0) 
            { 
                double minDistance = 0;
                j = 0;
                foreach (SpatialObject obj in root.index.Neighbors(0, 0)) 
                {
                    double distance = Math.Sqrt(obj.rect.Left*obj.rect.Left + obj.rect.Top*obj.rect.Top);
                    Debug.Assert(distance >= minDistance);
                    minDistance = distance;
                    j += 1;
                }
                Debug.Assert((i >= nObjectsInTree && j == nObjectsInTree) || (i < nObjectsInTree && j == i+1));
                Console.Write("Iteration " + i + "\r");
                db.Commit();
            }
        }        
        root.index.Clear();
        Console.WriteLine();
        Console.WriteLine("Elapsed time " + (DateTime.Now - start));
        db.Close();
    }
}
