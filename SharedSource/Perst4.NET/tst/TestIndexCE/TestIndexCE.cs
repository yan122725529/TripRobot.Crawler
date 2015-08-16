using System;
using System.Drawing;
using System.Collections;
using System.Windows.Forms;
using System.Text;
using Perst;
using System.Diagnostics;

namespace TestIndexCE
{
    /// <summary>
    /// Summary description for Form1.
    /// </summary>
    public class Form1 : System.Windows.Forms.Form
    {
        private System.Windows.Forms.TextBox label1;
        private System.Windows.Forms.Button button1;
        
        public Form1()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
        }
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Size = new System.Drawing.Size(Width, Height - 40);
            label1.Multiline = true;
            this.label1.Text = TestIndex.GetTestResults();
            
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(Width/2 - 40, Height - 38);
            this.button1.Text = "Ok";
            button1.Width = 80;
            button1.Click +=new EventHandler(button1_Click);
            // 
            // Form1
            // 
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label1);
            this.Text = "Index Test";

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        static void Main() 
        {
            Application.Run(new Form1());
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }

    public class Record:Persistent
    {
        public string strKey;
        public long intKey;
    }


    public class Root:Persistent
    {
#if USE_GENERICS
        public Index<string,Record> strIndex;
        public Index<long,Record>   intIndex;
#else
        public Index strIndex;
        public Index intIndex;
#endif
    }

    public class TestIndex
    {
        const int nRecords = 10000;
        static int pagePoolSize = 8 * 1024 * 1024;
        const string NL = "\r\n";
	
        static public string GetTestResults()
        {
            int i;
            StringBuilder results = new StringBuilder();

            Storage db = StorageFactory.Instance.CreateStorage();	
            System.IO.File.Delete("testidx.dbs");
            db.Open("testidx.dbs", pagePoolSize);

            Root root = (Root) db.Root;
            if (root == null)
            {
                root = new Root();
#if USE_GENERICS
                root.strIndex = db.CreateIndex<string,Record>(true);
                root.intIndex = db.CreateIndex<long,Record>(true);
#else
                root.strIndex = db.CreateIndex(typeof(String), true);
                root.intIndex = db.CreateIndex(typeof(long), true);
#endif
                db.Root = root;
            }
#if USE_GENERICS
            Index<string,Record> strIndex = root.strIndex;
            Index<long,Record> intIndex = root.intIndex;
#else
            Index intIndex = root.intIndex;
            Index strIndex = root.strIndex;
#endif
            DateTime start = DateTime.Now;
            long key = 1999;
            for (i = 0; i < nRecords; i++)
            {
                Record rec = new Record();
                key = (3141592621L * key + 2718281829L) % 1000000007L;
                rec.intKey = key;
                rec.strKey = System.Convert.ToString(key);
                intIndex[rec.intKey] = rec;
                strIndex[rec.strKey] = rec;
                if (i % 100000 == 0) 
                { 
                    db.Commit();
                }
            }

            db.Commit();
            results.Append("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start) + NL);
		
            start = System.DateTime.Now;
            key = 1999;
            for (i = 0; i < nRecords; i++)
            {
                key = (3141592621L * key + 2718281829L) % 1000000007L;
#if USE_GENERICS
                Record rec1 = intIndex[key];
                Record rec2 = strIndex[Convert.ToString(key)];
#else
                Record rec1 = (Record) intIndex[key];
                Record rec2 = (Record) strIndex[Convert.ToString(key)];
#endif
                Debug.Assert(rec1 != null && rec1 == rec2);
            }     
            results.Append("Elapsed time for performing " + nRecords * 2 + " index searches: " + (DateTime.Now - start) + NL);

            start = System.DateTime.Now;
            key = Int64.MinValue;
            i = 0;
            foreach (Record rec in intIndex) 
            {
                Debug.Assert(rec.intKey >= key);
                key = rec.intKey;
                i += 1;
            }
            Debug.Assert(i == nRecords);
            i = 0;
            String strKey = "";
            foreach (Record rec in strIndex) 
            {
                Debug.Assert(rec.strKey.CompareTo(strKey) >= 0);
                strKey = rec.strKey;
                i += 1;
            }
            Debug.Assert(i == nRecords);
            results.Append("Elapsed time for iteration through " + (nRecords * 2) + " records: " + (DateTime.Now - start) + NL);


            Hashtable map = db.GetMemoryDump();
            results.Append("Memory usage" + NL);
            start = DateTime.Now;
            foreach (MemoryUsage usage in db.GetMemoryDump().Values) 
            { 
                results.Append(" " + usage.type.Name + ": instances=" + usage.nInstances + ", total size=" + usage.totalSize + ", allocated size=" + usage.allocatedSize + NL);
            }
            results.Append("Elapsed time for memory dump: " + (DateTime.Now - start) + NL);
 

            start = System.DateTime.Now;
            key = 1999;
            for (i = 0; i < nRecords; i++)
            {
                key = (3141592621L * key + 2718281829L) % 1000000007L;
#if USE_GENERICS
                Record rec = intIndex.Get(key);
                Record removed = intIndex.RemoveKey(key);
#else
                Record rec = (Record) intIndex[key];
                Record removed = (Record)intIndex.RemoveKey(key);
#endif
                Debug.Assert(removed == rec);
                strIndex.Remove(new Key(System.Convert.ToString(key)), rec);
                rec.Deallocate();
            }
            results.Append("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start) + NL);
            db.Close();

            return results.ToString();
        }
    }
}

