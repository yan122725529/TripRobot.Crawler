using System;
using Perst;
using Perst.Assoc;

public class Hospital
{
    void PopulateDatabase()
    {
        ReadWriteTransaction t = db.StartReadWriteTransaction();

        Item patient = t.CreateItem();
        t.Link(patient, "class", "patient");
        t.Link(patient, "name", "John Smith");
        t.Link(patient, "age", 55);
        t.Link(patient, "wight", 65.7);
        t.Link(patient, "sex", "male");
        t.Link(patient, "phone", "1234567");
        t.Link(patient, "address", "123456, CA, Dummyngton, Outlook drive, 17");

        Item doctor = t.CreateItem();
        t.Link(doctor, "class", "doctor");
        t.Link(doctor, "name", "Robby Wood");
        t.Link(doctor, "speciality", "therapeutist");

        t.Link(doctor, "patient", patient);

        Item angina = t.CreateItem();
        t.Link(angina, "class", "disease");
        t.Link(angina, "name", "angina");
        t.Link(angina, "symptoms", "throat ache");
        t.Link(angina, "symptoms", "high temperature");
        t.Link(angina, "treatment", "milk&honey");
        
        Item flu = t.CreateItem();
        t.Link(flu, "class", "disease");
        t.Link(flu, "name", "flu");
        t.Link(flu, "symptoms", "stomachache");
        t.Link(flu, "symptoms", "high temperature");
        t.Link(flu, "treatment", "theraflu");
        
        Item diagnosis = t.CreateItem();
        t.Link(diagnosis, "class", "diagnosis");
        t.Link(diagnosis, "disease", flu);
        t.Link(diagnosis, "symptoms", "high temperature");
        t.Link(diagnosis, "diagnosed-by", doctor);
        t.Link(diagnosis, "date", "2010-09-23");
        t.Link(patient, "diagnosis", diagnosis);
        
        t.Commit();
    }
    
    void SearchDatabase() 
    {
        ReadOnlyTransaction t = db.StartReadWriteTransaction();

        // Find all patients with age > 50 which are diagnosed flu in last September
        foreach (Item patient in t.Find(Predicate.Value("age") > 50 
                                       & Predicate.Value("diagnosis").In(Predicate.Value("date").Between("2010-09-01", "2010-09-30")
                                                                         & Predicate.Value("disease").In(Predicate.Value("name") == "flu"))))
        {
            Console.WriteLine("Patient " + patient.GetString("name") + ", age " +  patient.GetNumber("age"));
        }

        // Print list of diseases with high temperature symptom ordered by name
        foreach (Item disease in t.Find(Predicate.Value("class") == "disease"
                                        & Predicate.Value("symptoms") == "high temperature", 
                                        new OrderBy("name")))
        {
            Console.WriteLine("Diseas " + disease.GetString("name"));
            Object symptoms = disease.GetAttribute("symptoms");
            if (symptoms is String) { 
                Console.WriteLine("Symptom: " + symptoms);
            } else if (symptoms is String[]) { 
                Console.WriteLine("Symptoms: ");
                String[] ss = (String[])symptoms;
                for (int i = 0; i < ss.Length; i++) {
                    Console.WriteLine("{0}: {1}", i, ss[i]);
                }
            }
        }
        t.Commit();
    }

    void UpdateDatabase()
    {
        ReadWriteTransaction t = db.StartReadWriteTransaction();
        Item patient = Enumerable.First(t.Find(Predicate.Value("class") == "patient"
                                               & Predicate.Value("name") == "John Smith"));
        t.Update(patient, "age", 56);
        t.Commit();
    }

    void Shutdown()
    {
        storage.Close();
    }

    Hospital()
    {
        storage = StorageFactory.Instance.CreateStorage();
        storage.Open("hospital.dbs");
        db = new AssocDB(storage);
    }
     
    public static void Main(String[] args) 
    {
        Hospital hospital = new Hospital();
        hospital.PopulateDatabase();
        hospital.SearchDatabase();
        hospital.UpdateDatabase();
        hospital.Shutdown();
    }
   
    AssocDB db;
    Storage storage;
}