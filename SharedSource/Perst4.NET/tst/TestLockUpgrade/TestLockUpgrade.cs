using System;
using System.Threading;
using Perst;
using System.Diagnostics;


class L2List : PersistentResource 
{
    internal L2Elem head;
}

class L2Elem : Persistent { 
    internal L2Elem next;
    internal L2Elem prev;
    internal int    count;

    public override bool RecursiveLoading() { 
        return false;
    }

    internal void unlink() { 
        next.prev = prev;
        prev.next = next;
        next.Store();
        prev.Store();
    }

    internal void linkAfter(L2Elem elem) {         
        elem.next.prev = this;
        next = elem.next;
        elem.next = this;
        prev = elem;
        Store();
        next.Store();
        prev.Store();
    }
}

public class TestConcur 
{ 
    const int nElements = 100000;
    const int nIterations = 100;
    const int nThreads = 4;

    static Storage db;
#if COMPACT_NET_FRAMEWORK
    static int nFinishedThreads;
#endif

    static void TraverseList(L2List list)
    {
        using (SharedLock sl = new SharedLock(list)) 
        {
            L2Elem head = list.head;             
            L2Elem elem = head;
            long sum = 0, n = 0;
            do { 
                elem.Load();
                sum += elem.count;
                n += 1;
            } while ((elem = elem.next) != head);
            Debug.Assert(n == nElements && sum == (long)nElements*(nElements-1)/2);
        }
    }

    static void RotateList(L2List list)
    {
        using (ExclusiveLock xl = new ExclusiveLock(list)) 
        {
            L2Elem last = list.head.prev;
            last.unlink();
            last.linkAfter(list.head);
        }
    }    

    static void WorkWithList(L2List list, bool rotate)
    {
        using (UpdateLock ul = new UpdateLock(list)) 
        {
            TraverseList(list);
            if (rotate)
            {
                RotateList(list);
            }
        }
    }

    static void Run() 
    { 
        L2List list = (L2List)db.Root;
        for (int i = 0; i < nIterations; i++) { 
            WorkWithList(list, (i & 1) == 0);
        }
#if COMPACT_NET_FRAMEWORK
        lock (typeof(TestConcur)) 
        {
            if (++nFinishedThreads == nThreads) 
            {
                db.Close();
            }
        }
#endif
    }

    public static void Main(String[] args) {
        db = StorageFactory.Instance.CreateStorage();
        if (args.Length > 0) 
        { 
            db.SetProperty("perst.object.cache.kind", args[0]);
        }
	    db.Open("testconcur.dbs");
        L2List list = (L2List)db.Root;
        if (list == null) { 
            list = new L2List();
            list.head = new L2Elem();
            list.head.next = list.head.prev = list.head;
            db.Root = list;
            for (int i = 1; i < nElements; i++) { 
                L2Elem elem = new L2Elem();
                elem.count = i;
                elem.linkAfter(list.head); 
            }
        }
        Thread[] threads = new Thread[nThreads];
        for (int i = 0; i < nThreads; i++) { 
            threads[i] = new Thread(new ThreadStart(Run));
            threads[i].Start();
        }
#if !COMPACT_NET_FRAMEWORK
        for (int i = 0; i < nThreads; i++) 
        { 
            threads[i].Join();
        }
        db.Close();
#endif
    }
 }


