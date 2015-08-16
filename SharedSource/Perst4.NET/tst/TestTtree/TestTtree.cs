using System;
using System.Collections;
using Perst;
using System.Diagnostics;


class Name 
{ 
    public String first;
    public String last;
}


class Person : Persistent 
{ 
    public String firstName;
    public String lastName;
    public int    age;

    private Person() {}

    public Person(String firstName, String lastName, int age) 
    { 
        this.firstName = firstName;
        this.lastName = lastName;
        this.age = age; 
    }
}

class PersonList : Persistent 
{
#if USE_GENERICS
    public SortedCollection<Name,Person> list;
#else
    public SortedCollection list;
#endif
}

#if USE_GENERICS
class NameComparator : PersistentComparator<Name,Person>
{ 
    public override int CompareMembers(Person p1, Person p2) 
    { 
        int diff = p1.firstName.CompareTo(p2.firstName);
        if (diff != 0) 
        { 
            return diff;
        }
        return p1.lastName.CompareTo(p2.lastName);
    }

    public override int CompareMemberWithKey(Person p, Name name) 
    { 
        int diff = p.firstName.CompareTo(name.first);
        if (diff != 0) 
        { 
            return diff;
        }
        return p.lastName.CompareTo(name.last);
    }
}
#else
class NameComparator : PersistentComparator 
{ 
    public override int CompareMembers(object m1, object m2) 
    { 
        Person p1 = (Person)m1;
        Person p2 = (Person)m2;
        int diff = p1.firstName.CompareTo(p2.firstName);
        if (diff != 0) 
        { 
            return diff;
        }
        return p1.lastName.CompareTo(p2.lastName);
    }

    public override int CompareMemberWithKey(object mbr, Object key) 
    { 
        Person p = (Person)mbr;
        Name name = (Name)key;
        int diff = p.firstName.CompareTo(name.first);
        if (diff != 0) 
        { 
            return diff;
        }
        return p.lastName.CompareTo(name.last);
    }
}
#endif

public class TestTtree 
{ 
    const int nRecords = 100000;
    const int pagePoolSize = 32*1024*1024;

    static public void Main(String[] args) 
    {	
        Storage db = StorageFactory.Instance.CreateStorage();

        db.Open("testtree.dbs", pagePoolSize);
        PersonList root = (PersonList)db.Root;
        if (root == null) 
        { 
            root = new PersonList();
#if USE_GENERICS
            root.list = db.CreateSortedCollection<Name,Person>(new NameComparator(), true);
#else
            root.list = db.CreateSortedCollection(new NameComparator(), true);
#endif
            db.Root = root;
        }
#if USE_GENERICS
        SortedCollection<Name,Person> list = root.list;
#else
        SortedCollection list = root.list;
#endif
        long key = 1999;
        int i;
        DateTime start = DateTime.Now;
        for (i = 0; i < nRecords; i++) 
        { 
            key = (3141592621L*key + 2718281829L) % 1000000007L;
            String str = Convert.ToString(key);
            int m = str.Length / 2;
            String firstName = str.Substring(0, m);
            String lastName = str.Substring(m);
            int age = (int)key % 100;
            Person p = new Person(firstName, lastName, age);
            list.Add(p);
        }
        db.Commit();
        Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " 
            + (DateTime.Now - start) + " milliseconds");
        
        start = DateTime.Now;
        key = 1999;
        for (i = 0; i < nRecords; i++) 
        { 
            key = (3141592621L*key + 2718281829L) % 1000000007L;
            String str = Convert.ToString(key);
            int m = str.Length / 2;
            Name name = new Name();
            int age = (int)key % 100;
            name.first = str.Substring(0, m);
            name.last = str.Substring(m);
            
#if USE_GENERICS
            Person p = list[name];
#else
            Person p = (Person)list[name];
#endif
            Debug.Assert(p != null);
            Debug.Assert(list.Contains(p));
            Debug.Assert(p.age == age);
        }
        Console.WriteLine("Elapsed time for performing " + nRecords + " index searches: " 
            + (DateTime.Now - start) + " milliseconds");
        
        start = DateTime.Now;
        Name nm = new Name();
        nm.first = nm.last = "";
#if USE_GENERICS
        PersistentComparator<Name,Person> comparator = list.GetComparator();
#else
        PersistentComparator comparator = list.GetComparator();
#endif
        i = 0; 
        foreach (Person p in list) 
        { 
            Debug.Assert(comparator.CompareMemberWithKey(p, nm) > 0);
            nm.first = p.firstName;
            nm.last = p.lastName;
            list.Remove(p);
            i += 1;
        }
        Debug.Assert(i == nRecords);
        Console.WriteLine("Elapsed time for removing " + nRecords + " records: " 
            + (DateTime.Now - start) + " milliseconds");
        Debug.Assert(list.Count == 0);
        db.Close();
    }
}
