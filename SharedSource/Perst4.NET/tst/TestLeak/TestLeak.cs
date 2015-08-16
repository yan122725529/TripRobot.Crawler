using System;
using Perst;
using System.Diagnostics;

public class TestLeak 
{
    class SpatialObject : Persistent, ISelfSerializable
    {
        internal RectangleR2 rect;
        internal byte[] body;

        public void Pack(ObjectWriter writer)
        {
            writer.Write(rect.Top);
            writer.Write(rect.Left);
            writer.Write(rect.Bottom);
            writer.Write(rect.Right);
            writer.Write(body.Length);
            writer.Write(body);
        }

        public  void Unpack(ObjectReader reader)
        {
            rect = new RectangleR2(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
            body = reader.ReadBytes(reader.ReadInt32());
        }
                
    };


    const int nObjects = 1000;
    const int batchSize = 100;
    const int minObjectSize = 1000;
    const int maxObjectSize = 2000;
    const int nIterations = 10000;

    public static void Main(String[] args) 
    { 
        Storage db = StorageFactory.Instance.CreateStorage();
        db.Open("testleak.dbs");
#if USE_GENERICS
        SpatialIndexR2<SpatialObject> root = db.CreateSpatialIndexR2<SpatialObject>();
#else
        SpatialIndexR2 root = db.CreateSpatialIndexR2();
#endif
        RectangleR2[] rectangles = new RectangleR2[nObjects];
        Random rnd = new Random(2014);
        db.Root = root;
        for (int i = 0; i < nObjects; i++) { 
            SpatialObject so = new SpatialObject();
            double lat = rnd.NextDouble()*180;
            double lng = rnd.NextDouble()*180;
            so.rect = rectangles[i] = new RectangleR2(lat, lng, lat+10, lng+10);
            so.body = new byte[minObjectSize + rnd.Next(maxObjectSize - minObjectSize)];
            root.Put(so.rect, so);
        } 
        db.Commit();

        for (int i = 0; i < nIterations; i++) {
            if (i % 1000 == 0) { 
                Console.WriteLine("Iteration " + i);
            }
            for (int j = 0; j < batchSize; j++) { 
                int k = rnd.Next(nObjects);
                bool found = false;
                foreach (SpatialObject oldObj in root.Overlaps(rectangles[k])) { 
                    if (oldObj.rect.Equals(rectangles[k])) {
                        root.Remove(oldObj.rect, oldObj);
                        SpatialObject newObj = new SpatialObject();
                        newObj.rect = oldObj.rect;
                        newObj.body = new byte[minObjectSize + rnd.Next(maxObjectSize - minObjectSize)];
                        root.Put(newObj.rect, newObj);
                        oldObj.Deallocate();
                        found = true;
                        break;
                    }
                }
                Debug.Assert(found);
            }
            db.Commit();
        }
        db.Close();
    }
}

