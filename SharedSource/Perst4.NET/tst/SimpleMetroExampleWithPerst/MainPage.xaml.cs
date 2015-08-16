using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;

using System.Diagnostics;
using System.Text;
using Perst;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SimpleMetroExampleWithPerst
{
    public class EmployeeTable : Persistent 
    {
	    [Indexable(Unique=true, CaseInsensitive=true)]
   	    public String EmpId;

 	    public String Name;
    }


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
    	Storage  storage;
	    Database db;
	    String   databasePath = "test.dbs";

        void CreateStorage()
        {
            storage = StorageFactory.Instance.CreateStorage();
        	// create in-memory storage
        	storage.Open(databasePath, 0);
            db = new Database(storage, true);
            db.BeginTransaction();
        	if (db.CreateTable(typeof(EmployeeTable))){
    			Results.Text = "Table Created";
    		} else {
    			Results.Text = "Table Already Exits";
    		}
        	db.CommitTransaction();        	
        }


        void InsertRecords()
        {
            int count = 0;
            DateTime start = DateTime.Now;
            try
            {
           	    db.BeginTransaction();
           	    for (int i=0; i<20000; i++)
                {    		
           	   	    EmployeeTable tblEmpRec = new EmployeeTable();
        		    tblEmpRec.EmpId = "Emp"+i;
        		    tblEmpRec.Name = "Siranjeevi";
        		
        		     bool result = db.AddRecord(tblEmpRec);
           		     if (result) 
                     {
                         count += 1;
                     }        			         		
        		} 
          	    db.CommitTransaction();	
        	}
      	    catch (Exception x) 
            {
    		   db.RollbackTransaction();
    		}
            Results.Text = "Elapsed time for storing " + count + " records: " + (DateTime.Now - start);
        }

        void GetRecords()
        { 
            int totalRecords = 0;
            DateTime start = DateTime.Now; 
       	    try 
            {
                db.BeginTransaction();
                foreach (EmployeeTable rec in db.GetRecords<EmployeeTable>()) 
                {
       			    totalRecords++;    		
    		    }    	
    			db.CommitTransaction();
    		}	
            catch (Exception x) 
            {
    			db.RollbackTransaction();
    		}
            Results.Text = "Elapsed time for loading " + totalRecords + " records: " + (DateTime.Now - start);
        }

        void CloseStorage()
        {
            storage.Close();
        }

        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            CreateStorage();
        }

        private void Button_Insert(object sender, RoutedEventArgs e)
        {
            InsertRecords();
        }

        private void Button_Get(object sender, RoutedEventArgs e)
        {
            GetRecords();
        }

        private void Button_Close(object sender, RoutedEventArgs e)
        {
            CloseStorage();
            App.Current.Exit();
        }
    }


}
