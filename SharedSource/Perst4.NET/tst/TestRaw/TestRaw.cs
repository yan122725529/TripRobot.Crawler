using System;
using Perst;
using System.Collections.Generic;
using System.Diagnostics;

class L1List 
{ 
    internal L1List next;
    internal Object obj;

    internal L1List(Object val, L1List list) 
    { 
        obj = val;
        next = list;
    }
}

class ListItem 
{
    internal int id;

    internal ListItem(int id) 
    { 
        this.id = id;
    }
}

public class TestRaw : Persistent 
{ 
    L1List         list;
    List<ListItem> array;
    Object         nil;

    const int nListMembers = 100;
    const int nArrayElements = 1000;

    public static void Main(String[] args) 
    { 
        Storage db = StorageFactory.Instance.CreateStorage();
        db.Open("testraw.dbs");
        TestRaw root = (TestRaw)db.Root;
        if (root == null) 
        { 
            root = new TestRaw();
            L1List list = null;
            for (int i = 0; i < nListMembers; i++) 
            { 
                list = new L1List(i, list);
            }            
            root.list = list;
            root.array = new List<ListItem>(nArrayElements);
            for (int i = 0; i < nArrayElements; i++) 
            { 
                root.array.Add(new ListItem(i));
            }
            db.Root = root;
            Console.WriteLine("Initialization of database completed");
        } 
        L1List elem = root.list;
        for (int i = nListMembers; --i >= 0;) 
        { 
            Debug.Assert(elem.obj.Equals(i));
            elem = elem.next;
        }
        for (int i = nArrayElements; --i >= 0;) 
        { 
            Debug.Assert(root.array[i].id == i);
        }
        Console.WriteLine("Database is OK");
        db.Close();
    }
}

