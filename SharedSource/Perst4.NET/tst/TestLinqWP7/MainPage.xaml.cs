using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;

using System.Diagnostics;
using System.Text;
using Perst;


namespace TestLinqWP7
{
    public class Customer : Persistent
    {
        [Indexable(Unique = true, CaseInsensitive = true)]
        public string name;
        public string address;
        public string phone;
        public string contactPerson;
        [Indexable(Thick = true)]
        public bool vip;

        public override string ToString()
        {
            return "Name: " + name + "\nAddress: " + address + "\nPhone: " + phone + "\nContact: " + contactPerson;
        }
    }

    public class BugReport : Persistent
    {
        public enum Priority
        {
            Low,
            Normal,
            High
        };
        [Indexable]
        public Customer issuedBy;
        [Indexable]
        public Priority priority;
        public string description;
        public decimal version;

        public override string ToString()
        {
            return "IssuedBy: " + issuedBy.name + "\nPriority: " + priority + "\nDescription: " + description + "\nVersion: " + version;
        }
    }

    class QueryListener : StorageListener
    {
        public int nSeqSearches = 0;

        public override void SequentialSearchPerformed(object query)
        {
            nSeqSearches += 1;
        }
    }

    public class LinqTest
    {
        static public string Run(string[] args)
        {
            StringBuilder sb = new StringBuilder();
            Storage storage = StorageFactory.Instance.CreateStorage();
            storage.SetProperty("perst.file.truncate", true);
            storage.Open("testlinq.dbs");
            QueryListener listener = new QueryListener();
            storage.Listener = listener;
            Database db = new Database(storage);
            int n;

            Customer customer1 = new Customer();
            customer1.name = "Age Soft";
            customer1.address = "MT, Freen Valley, 5";
            customer1.phone = "111-1111";
            customer1.contactPerson = "John Smith";
            customer1.vip = true;
            db.AddRecord(customer1);

            Customer customer2 = new Customer();
            customer2.name = "WebAlta";
            customer2.address = "Moscow, Russia, Kolomenskay nab.,2";
            customer2.phone = "222-22222";
            customer2.contactPerson = "Piter Volokov";
            db.AddRecord(customer2);


            BugReport bug1 = new BugReport();
            bug1.issuedBy = customer1;
            bug1.priority = BugReport.Priority.Low;
            bug1.description = "It doesn't work";
            bug1.version = 1.03M;
            db.AddRecord(bug1);

            BugReport bug2 = new BugReport();
            bug2.issuedBy = customer2;
            bug2.priority = BugReport.Priority.High;
            bug2.description = "Something is definitely wrong";
            bug2.version = 2.01M;
            db.AddRecord(bug2);

            sb.AppendLine("Search customer by name");
            n = 0;
            foreach (Customer c in db.Select<Customer>(c => c.name == "WebAlta"))
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            sb.AppendLine("Locate customers issued high priority bugs for version 2.0");
            n = 0;
            var query1 = from bug in db.GetTable<BugReport>()
                         where bug.priority >= BugReport.Priority.High && bug.version >= 2.0M
                         orderby bug.priority
                         select bug.issuedBy;
            foreach (var c in query1)
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            sb.AppendLine("Select customer by name and contact person");
            n = 0;
            string name = "Age Soft";
            string person = "John Smith";
            var query2 = from c in db.GetTable<Customer>()
                         where name == c.name && c.contactPerson == person
                         select c;
            foreach (var c in query2)
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            name = "WebAlta";
            person = "Piter Volokov";
            n = 0;
            foreach (var c in query2)
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);
            Debug.Assert(listener.nSeqSearches == 0);

            sb.AppendLine("Select with index join");
            n = 0;
            foreach (BugReport bug in db.Select<BugReport>(bug => bug.issuedBy.name == "WebAlta" || bug.issuedBy.name == "Age Soft"))
            {
                sb.AppendLine(bug.ToString());
                n += 1;
            }
            Debug.Assert(n == 2);
            Debug.Assert(listener.nSeqSearches == 0);

            sb.AppendLine("Select with index prefix search");
            n = 0;
            foreach (Customer customer in db.Select<Customer>(customer => customer.phone == "222-22222" && customer.name.StartsWith("Web")))
            {
                sb.AppendLine(customer.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            Debug.Assert(listener.nSeqSearches == 0);


            sb.AppendLine("Select without search condition");
            n = 0;
            var query3 = from bug in db.GetTable<BugReport>()
                         orderby bug.priority
                         select bug;
            foreach (var b in query3)
            {
                sb.AppendLine(b.ToString());
                n += 1;
            }
            Debug.Assert(n == 2);

            sb.AppendLine("Select using sequential search");
            n = 0;
            var query4 = from bug in db.GetTable<BugReport>()
                         where bug.version >= 2.0M
                         select bug;
            foreach (var b in query4)
            {
                sb.AppendLine(b.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            n = 0;
            var query5 = from bug in db.GetTable<BugReport>()
                         where bug.issuedBy == customer1
                         select bug;
            foreach (var b in query5)
            {
                sb.AppendLine(b.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            n = 0;
            List<Customer> customers = new List<Customer>();
            customers.Add(customer1);
            customers.Add(customer2);
            var query6 = from bug in db.GetTable<BugReport>()
                         where customers.Contains(bug.issuedBy)
                         select bug;
            foreach (var b in query6)
            {
                sb.AppendLine(b.ToString());
                n += 1;
            }
            Debug.Assert(n == 2);

            n = 0;
            foreach (Customer c in db.Select<Customer>(c => c.name.CompareTo("A") >= 0))
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 2);

            n = 0;
            foreach (Customer c in db.Select<Customer>(c => String.Compare(c.name, "webalta", StringComparison.CurrentCultureIgnoreCase) == 0))
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            n = 0;
            foreach (Customer c in db.Select<Customer>(c => c.vip))
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);

            n = 0;
            foreach (Customer c in db.Select<Customer>(c => !c.vip))
            {
                sb.AppendLine(c.ToString());
                n += 1;
            }
            Debug.Assert(n == 1);


            Debug.Assert(listener.nSeqSearches == 1);
            
            storage.Close();
            return sb.ToString();
        }
    }

    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //Results.Text = PerstBench.Run(new string[1]{"zip"});
            Results.Text = LinqTest.Run(new string[0]);
        }
    }
}