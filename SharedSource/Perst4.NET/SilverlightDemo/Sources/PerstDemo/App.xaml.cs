using System;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Browser;
using Perst;
using Perst.FullText;

namespace PerstDemo
{
    public partial class App
    {
        public App()
        {
            Startup += ApplicationStartup;
            Exit += ApplicationExit;
            UnhandledException += ApplicationUnhandledException;

            InitializeComponent();
        }

        public Database Database { get; internal set; }

        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            using (var stor = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (stor.FileExists(DataGenerator.StorageName))
                {
                    InitializePerstStorage();
                }
            }
            RootVisual = new MainPage { VerticalAlignment = VerticalAlignment.Stretch };
        }

        private void ApplicationExit(object sender, EventArgs e)
        {
            if (Database != null && Database.Storage != null)
                Database.Storage.Close();
        }

        private static void ApplicationUnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached) return;
            e.Handled = true;
            Deployment.Current.Dispatcher.BeginInvoke(() => ReportErrorToDom(e));
        }

        internal void InitializePerstStorage()
        {
            var storage = StorageFactory.Instance.CreateStorage(); // Creating Instance of Perst Storage
            storage.SetProperty("perst.file.extension.quantum", 512 * 1024); // Initial Size set 512KB to fit in Silverlight Isolated Storage
            storage.SetProperty("perst.extension.quantum", 256 * 1024); // Step of storage extension 256KB to have less fragmentation on disk

            storage.Open(DataGenerator.StorageName, 0); // Open Storage

            //Create Database wrapper over Perst Storage
            Database = new Database(storage, false, true, new FullTextSearchHelper(storage));
            Database.EnableAutoIndices = false; //Turn off auto-index creation (defined manually)
        }

        private static void ReportErrorToDom(ApplicationUnhandledExceptionEventArgs e)
        {
            var errorMsg = e.ExceptionObject.Message + e.ExceptionObject.StackTrace.Replace('"', '\'').Replace("\r\n", @"\n");
            HtmlPage.Window.Eval("throw new Error(\"Unhandled Error in Silverlight Application " + errorMsg + "\");");
        }
    }
}