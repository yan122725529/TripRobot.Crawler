using Perst;
using System;
using System.Collections;

public class JsqlSSD : Persistent 
{
#if USE_GENERICS
    static public void Main(string[] args) 
    {	
        Console.WriteLine("Generic version of Database class is not supported");
    }
#else
    public class Supplier
    {
        public string name;
        public string location;
        public Link   shipments;
    }

    public class Detail 
    {
        public string id;
        public float  weight;
        public Link   shipments;
    }

    public class Shipment 
    { 
        public Supplier supplier;
        public Detail   detail;
        public int      quantity;
        public long     price;
        public DateTime date;
    }


    static void skip(string prompt) 
    {
        Console.Write(prompt);
        Console.ReadLine();
    }

    static String input(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            String line = Console.ReadLine().Trim();
            if (line.Length != 0) 
            { 
                return line;
            }
        }
    }

    static long inputLong(string prompt) 
    { 
        while (true) 
        { 
            try 
            { 
                return Int32.Parse(input(prompt));
            } 
            catch (FormatException) 
            { 
                Console.WriteLine("Invalid integer constant");
            }
        }
    }

    static double inputDouble(string prompt) 
    { 
        while (true) 
        { 
            try 
            { 
                return Double.Parse(input(prompt));
            } 
            catch (FormatException) 
            { 
                Console.WriteLine("Invalid floating point constant");
            }
        }
    }

    static public void Main(String[] args) 
    {	
        Storage storage = StorageFactory.Instance.CreateStorage();
        storage.Open("testssd2.dbs");
        Database db = new Database(storage);

        db.CreateTable(typeof(Supplier));
        db.CreateIndex(typeof(Supplier), "name", true);
        db.CreateTable(typeof(Detail));
        db.CreateIndex(typeof(Detail), "id", true);
        db.CreateTable(typeof(Shipment));

        Query supplierQuery = db.Prepare(typeof(Supplier), "name like ?");
        Query detailQuery = db.Prepare(typeof(Detail), "id like ?");


        while (true) 
        { 
            try 
            { 
                switch ((int)inputLong("-------------------------------------\n" + 
                    "Menu:\n" + 
                    "1. Add supplier\n" + 
                    "2. Add detail\n" + 
                    "3. Add shipment\n" + 
                    "4. List of suppliers\n" + 
                    "5. List of details\n" + 
                    "6. Suppliers of detail\n" + 
                    "7. Details shipped by supplier\n" + 
                    "8. Find shipments for the particular date\n" + 
                    "9. Exit\n\n>>"))
                {
                    case 1:
                    {
                        Supplier supplier = new Supplier();
                        supplier.name = input("Supplier name: ");
                        supplier.location = input("Supplier location: ");
                        supplier.shipments = storage.CreateLink();
                        db.AddRecord(supplier);
                        storage.Commit();
                        continue;
                    }
                    case 2:
                    {
                        Detail detail = new Detail();
                        detail.id = input("Detail id: ");
                        detail.weight = (float)inputDouble("Detail weight: ");
                        detail.shipments = storage.CreateLink();
                        db.AddRecord(detail);
                        storage.Commit();
                        continue;
                    }
                    case 3:
                    {
                        Shipment shipment = null;
                        foreach (Supplier supplier in db.Select(typeof(Supplier), "name='" + input("Supplier name: ") + "'"))
                        {
                            foreach (Detail detail in db.Select(typeof(Detail), "id='" + input("Detail ID: ") + "'"))
                            {
                                shipment = new Shipment();
                                shipment.quantity = (int)inputLong("Shipment quantity: ");
                                shipment.price = inputLong("Shipment price: ");
                                shipment.date = DateTime.Parse(input("Date: "));
                                shipment.detail = detail;
                                shipment.supplier = supplier;
                                detail.shipments.Add(shipment);
                                supplier.shipments.Add(shipment);
                                db.AddRecord(shipment);
                                storage.Commit();
                            }
                        }
                        if (shipment == null) 
                        { 
                            Console.WriteLine("Supplier+Detail not found");
                        }
                        continue;
                    }
                    case 4:
                        foreach (Supplier supplier in db.GetRecords(typeof(Supplier))) 
                        { 
                            Console.WriteLine("Supplier name: " + supplier.name + ", supplier.location: " + supplier.location);
                        }
                        break;
                    case 5:
                        foreach (Detail detail in db.GetRecords(typeof(Detail))) 
                        {
                            Console.WriteLine("Detail ID: " + detail.id + ", detail.weight: " + detail.weight);
                        }
                        break;
                    case 6:
                    {
                        Hashtable result = new Hashtable();
                        detailQuery[1] = input("Detail ID: ");
                        foreach (Detail detail in detailQuery.Execute(db.GetRecords(typeof(Detail))))
                        {
                            foreach (Shipment shipment in detail.shipments)
                            {
                                result[shipment.supplier] = shipment;
                            }
                        }
                        foreach (Supplier supplier in result.Keys) 
                        {
                            Console.WriteLine("Suppplier name: " + supplier.name);
                        }
                        break;
                    }
                    case 7:
                    {
                        Hashtable result = new Hashtable();
                        supplierQuery[1] = input("Supplier name: ");
                        foreach (Supplier supplier in supplierQuery.Execute()) 
                        {
                            foreach (Shipment shipment in supplier.shipments)
                            {
                                result[shipment.detail] = shipment;
                            }
                        }
                        foreach (Detail detail in result.Keys) 
                        {
                            Console.WriteLine("Detail ID: " + detail.id);
                        }
                        break;
                    }
                    case 8:
                    {
                        string from = input("From: ");
                        string till = input("Till: ");
                        foreach (Shipment s in db.Select(typeof(Shipment), "date between '" + from + "' and '" + till + "'"))
                        {
                            Console.WriteLine("Shipment: " + s.date + ", Supplier: " + s.supplier.name + ", Detail: " + s.detail.id);
                        }
                        break;
                    }
                    case 9:
                        storage.Close();
                        return;
                }
                skip("Press ENTER to continue...");
            } 
            catch (StorageError x) 
            { 
                Console.WriteLine("Error: " + x.Message);
                skip("Press ENTER to continue...");
            }
        }
    }
#endif
}

