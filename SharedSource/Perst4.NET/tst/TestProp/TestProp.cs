using System;
using Perst;

[SerializeProperties]
public class Person:Persistent
{
    private string firstName;
    public string FirstName
    {
        get
        {
            return firstName;
        }
        
        set
        {
            NotifyChange("FirstName");
            firstName = value;
        }
    }

    private string lastName;
    public string LastName
    {
        get
        {
            return lastName;
        }
        
        set
        {
            NotifyChange("LastName");
            lastName = value;
        }
    }

    protected virtual void NotifyChange(string field)
    {
        Console.WriteLine("Field {0} is changed", field);
    }

    public Person() {}

    public Person(string firstName, string lastName)
    {
        this.firstName = firstName;
        this.lastName = lastName;
    }
}


public class TestProp
{
    static public void  Main(string[] args)
    {
        Storage db = StorageFactory.Instance.CreateStorage();
		
        db.Open("testprop.dbs");
#if USE_GENERICS
        MultiFieldIndex<Person> root = (MultiFieldIndex<Person>)db.Root;
        if (root == null) 
        {         
            root = db.CreateFieldIndex<Person>(new string[]{"LastName", "FirstName"}, true, true);
            db.Root = root;
            db.Commit(); 
        } 
#else
        MultiFieldIndex root = (MultiFieldIndex)db.Root;
        if (root == null) 
        {         
            root = db.CreateFieldIndex(typeof(Person), new string[]{"LastName", "FirstName"}, true, true);
            db.Root = root;
            db.Commit(); 
        } 
#endif
        while (true) 
        {
            Console.WriteLine("Last name: ");
            string lastName = Console.ReadLine().Trim();
            if (lastName.Length == 0)
            {
                break;
            }
#if USE_GENERICS
            Person[] result = root.Get(new Key(new object[]{lastName}), 
                                       new Key(new object[]{lastName + "_"}));
#else
            object[] result = root.Get(new Key(new object[]{lastName}), 
                                       new Key(new object[]{lastName + "_"}));
#endif
            foreach (Person p in result)
            {
                Console.WriteLine("{0} {1}", p.FirstName, p.LastName);
            } 
            Console.WriteLine("First name: ");
            string firstName = Console.ReadLine().Trim();
#if USE_GENERICS
            Person person = root.Get(new Key(new object[]{lastName, firstName}));
#else
            Person person = (Person)root.Get(new Key(new object[]{lastName, firstName}));
#endif
            if (person != null)
            {
                Console.WriteLine("Person exists in the database");
            }                        
            else 
            {
                Console.WriteLine("Add new person to the database");
                root.Put(new Person(firstName, lastName));
                db.Commit();
            }
        }
        Console.WriteLine("Close session");
        db.Close();
    }
}
