using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Perst;
using Perst.FullText;

namespace PerstDemo
{
    public partial class MainPage
    {
        private readonly Dictionary<DataGrid, DetailPanel> cacheDetailPanels = new Dictionary<DataGrid, DetailPanel>();
        private readonly Delayer detailDelayer;
        private object currentDataGrid;

        public MainPage()
        {
            InitializeComponent();
            SizeChanged += OnThisSizeChanged;

            gridContact.MouseEnter += GridOnMouseEnter;
            gridLead.MouseEnter += GridOnMouseEnter;
            gridActivity.MouseEnter += GridOnMouseEnter;

            gridContact.GotFocus += DataGridGotFocus;
            gridLead.GotFocus += DataGridGotFocus;
            gridActivity.GotFocus += DataGridGotFocus;

            if (Database != null)
                gridContact.ItemsSource = Database.GetTable<Contact>().ToObservableCollection(); // Load all contacts

            Delayer.DelayMilliseconds = 300;
            var contactDelayer = new Delayer();
            gridContact.SelectionChanged += (sender, e) => contactDelayer.Action = RefreshLeads;
            var activityDelayer = new Delayer();
            gridLead.SelectionChanged += (sender, e) => activityDelayer.Action = RefreshActivities;

            gridContact.SelectionChanged += (sender, e) => CheckButtons();
            gridLead.SelectionChanged += (sender, e) => CheckButtons();
            CheckButtons();

            detailDelayer = new Delayer();
        }

        private static Database Database
        {
            get { return ((App)Application.Current).Database; }
        }

        private bool IsSearchableState
        {
            get { return !tbSearch.IsEmpty; }
        }

        private static Storage Storage
        {
            get { return Database.Storage; }
        }

        private void AddItem(IPersistent obj, DataGrid dataGrid)
        {
            Database.AddRecord(obj); // Adding new record to Database
            ((IList)dataGrid.ItemsSource).Insert(0, obj);
            dataGrid.SelectedItem = obj;
            ShowDetailPanel(dataGrid);
            ((DetailPanel)swDetail.Content).FocusFirstTextBox();
        }

        private void bClearSearch_Click(object sender, RoutedEventArgs e)
        {
            tbSearch.Clear();
        }

        private void bNewActivity_Click(object sender, RoutedEventArgs e)
        {
            var currentLead = (Lead)gridLead.SelectedItem;
            AddItem(new Activity { Lead = currentLead }, gridActivity);
        }

        private void bNewContact_Click(object sender, RoutedEventArgs e)
        {
            AddItem(new Contact(), gridContact);
        }

        private void bNewLead_Click(object sender, RoutedEventArgs e)
        {
            var currentContact = (Contact)gridContact.SelectedItem;
            AddItem(new Lead { Contact = currentContact }, gridLead);
        }

        private void CheckButtons()
        {
            bClearDB.IsEnabled = Database != null;
            bNewContact.IsEnabled = Database != null;
            bNewLead.IsEnabled = gridContact.SelectedItem != null;
            bNewActivity.IsEnabled = gridLead.SelectedItem != null;
        }

        private void ClearDBClick(object sender, RoutedEventArgs e)
        {
            var clearPopup = new ClearPopup();
            clearPopup.Closed += (sender1, e1) =>
                 {
                     if (clearPopup.DialogResult == true)
                         RefreshContact();
                     CheckButtons();
                 };
            clearPopup.Show();
        }

        private void DataGridGotFocus(object sender, RoutedEventArgs e)
        {
            if (currentDataGrid == sender)
                ShowDetailPanel((DataGrid)sender);
        }

        private void DataGrid_RowEditEnded(object sender, DataGridRowEditEndedEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            var persistent = e.Row.DataContext as Base;
            if (persistent == null) return;
            persistent.Save(); // Saving Item to Storage
            Storage.Commit(); // Commiting changes
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            detailDelayer.Action = () =>
               {
                   if (!cacheDetailPanels.ContainsKey((DataGrid)sender)) return;
                   cacheDetailPanels[(DataGrid)sender].Target =
                       ((DataGrid)sender).SelectedItems;
               };
        }

        private void GenerateDBClick(object sender, RoutedEventArgs e)
        {
            var generatorPopup = new GeneratorPopup();
            generatorPopup.Closed += (sender1, e1) =>
                {
                    if (generatorPopup.DialogResult == true)
                        RefreshContact();
                    CheckButtons();
                };
            generatorPopup.Show();
        }

        private void GridOnMouseEnter(object sender, MouseEventArgs e)
        {
            currentDataGrid = sender;
        }

        private void RefreshActivities()
        {
            if (IsSearchableState) return;
            gridActivity.ItemsSource = null;
            IEnumerable<Activity> res = null;
            if (Database != null)
            {
                if (gridLead.SelectedItem != null)
                {
                    var leads = gridLead.SelectedItems.Cast<Lead>();
                    res = (from activity in Database.GetTable<Activity>()
                           // Load activities
                           where leads.Contains(activity.Lead)
                           // by selected lead
                           select activity);
                }
                if (res == null && gridContact.SelectedItem != null)
                {
                    var contacts = gridContact.SelectedItems.Cast<Contact>();
                    res = (from activity in Database.GetTable<Activity>()
                           // Load activities
                           where activity.Lead != null && contacts.Contains(activity.Lead.Contact)
                           // by selected Contact
                           select activity);
                }
            }
            if (res != null)
            {
                var result = res.ToObservableCollection();
                gridActivity.ItemsSource = result;
            }
            else
            {
                gridActivity.ItemsSource = null;
            }
        }

        private void RefreshContact()
        {
            if (IsSearchableState) return;
            var contacts = Database != null ?
                Database.GetTable<Contact>().ToObservableCollection() : // Reload all contacts
                null;
            gridContact.ItemsSource = contacts;
            RefreshLeads();
        }

        private void RefreshLeads()
        {
            if (IsSearchableState) return;
            var contacts = gridContact.SelectedItems.Cast<Contact>();
            gridLead.ItemsSource = null;

            var res = Database != null ?
                (from lead in Database.GetTable<Lead>()
                 // Loading Leads
                 where contacts.Contains(lead.Contact)
                 // by selected Contact
                 select lead).ToObservableCollection() :
                 null;
            gridLead.ItemsSource = res;
            RefreshActivities();
        }

        private void Search()
        {
            if (Database == null) return;
            // Make full-text search in DB limited to 1000 items and 2 seconds
            // without results sorting
            var prefixes = Database.SearchPrefix(tbSearch.Text, 1000, 2000, false);

            var contacts = new ObservableCollection<Contact>();
            var leads = new ObservableCollection<Lead>();
            var activities = new ObservableCollection<Activity>();

            var arrayRes = new List<FullTextSearchHit>();
            if (prefixes != null) arrayRes.AddRange(prefixes.Hits);
            foreach (var hit in arrayRes)
            {
                if (hit.Document is Contact)
                {
                    if (!contacts.Contains((Contact)hit.Document))
                        contacts.Add((Contact)hit.Document);
                }
                else if (hit.Document is Lead)
                {
                    if (!leads.Contains((Lead)hit.Document))
                        leads.Add((Lead)hit.Document);
                }
                else if (hit.Document is Activity)
                {
                    if (!activities.Contains((Activity)hit.Document))
                        activities.Add((Activity)hit.Document);
                }
            }
            gridContact.ItemsSource = contacts;
            gridLead.ItemsSource = leads;
            gridActivity.ItemsSource = activities;
        }

        private void ShowDetailPanel(DataGrid dataGrid)
        {
            if (cacheDetailPanels.ContainsKey(dataGrid) && swDetail.Content == cacheDetailPanels[dataGrid]) return;

            DetailPanel detail;
            if (!cacheDetailPanels.TryGetValue(dataGrid, out detail))
            {
                Type typeObj;
                if (dataGrid == gridContact)
                    typeObj = typeof(Contact);
                else if (dataGrid == gridLead)
                    typeObj = typeof(Lead);
                else if (dataGrid == gridActivity)
                    typeObj = typeof(Activity);
                else
                    throw new ArgumentOutOfRangeException("dataGrid");
                cacheDetailPanels[dataGrid] = detail = new DetailPanel(typeObj, dataGrid);
            }

            if (detail.Target != dataGrid.SelectedItems)
                detail.Target = dataGrid.SelectedItems;

            swDetail.Content = detail;
        }

        private void tbSearch_SearchChanged(object sender, EventArgs e)
        {
            if (!tbSearch.IsEmpty)
                Search();
            else
                RefreshContact();
        }

        #region Animation

        private static readonly DependencyProperty ActivityWidthProperty
            = DependencyProperty.Register("ActivityWidth", typeof(double), typeof(MainPage),
            new PropertyMetadata(0.0, ActivityWidthChanged));

        private static readonly DependencyProperty ContactWidthProperty
            = DependencyProperty.Register("ContactWidth", typeof(double), typeof(MainPage),
            new PropertyMetadata(0.0, ContactWidthChanged));

        private static readonly DependencyProperty LeadWidthProperty
            = DependencyProperty.Register("LeadWidth", typeof(double), typeof(MainPage),
            new PropertyMetadata(0.0, LeadWidthChanged));

        private readonly string[] props = { "ContactWidth", "LeadWidth", "ActivityWidth" };
        private double startWidth;

        public Double ActivityWidth
        {
            get { return (double)GetValue(ActivityWidthProperty); }
            set { SetValue(ActivityWidthProperty, value); }
        }

        public Double ContactWidth
        {
            get { return (double)GetValue(ContactWidthProperty); }
            set { SetValue(ContactWidthProperty, value); }
        }

        public Double LeadWidth
        {
            get { return (double)GetValue(LeadWidthProperty); }
            set { SetValue(LeadWidthProperty, value); }
        }

        private static void ActivityWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainPage)d).cdActivity.Width = new GridLength((double)e.NewValue, GridUnitType.Star);
        }

        private static void ContactWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainPage)d).cdContact.Width = new GridLength((double)e.NewValue, GridUnitType.Star);
        }

        private static void LeadWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainPage)d).cdLead.Width = new GridLength((double)e.NewValue, GridUnitType.Star);
        }

        private void DataGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            var sb = new Storyboard { Duration = TimeSpan.FromSeconds(0.3) };

            foreach (var propName in props)
            {
                if (propName == (string)((DataGrid)sender).Tag)
                {
                    var increase = new DoubleAnimation { To = startWidth * 2, Duration = sb.Duration };

                    Storyboard.SetTarget(increase, this);
                    Storyboard.SetTargetProperty(increase, new PropertyPath("(MainPage." + propName + ")"));
                    sb.Children.Add(increase);
                }
                else
                {
                    var decrease = new DoubleAnimation { To = startWidth / 2, Duration = sb.Duration };

                    Storyboard.SetTarget(decrease, this);
                    Storyboard.SetTargetProperty(decrease, new PropertyPath("(MainPage." + propName + ")"));
                    sb.Children.Add(decrease);
                }
            }

            sb.Begin();
        }

        private void OnThisSizeChanged(object sender, SizeChangedEventArgs e)
        {
            startWidth = e.NewSize.Width / 4;

            ContactWidth = LeadWidth = ActivityWidth = startWidth;
            cdDetail.Width = new GridLength(startWidth, GridUnitType.Pixel);
        }

        #endregion

        private class Delayer
        {
            private Action action;

            public Delayer()
            {
                Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DelayMilliseconds < 0 ? 150 : DelayMilliseconds) };
                Timer.Tick += Tick;
            }

            public static int DelayMilliseconds { get; set; }

            public Action Action
            {
                set
                {
                    action = null;
                    if (Timer.IsEnabled)
                        Timer.Stop();
                    action = value;
                    Timer.Start();
                }
            }

            private DispatcherTimer Timer { get; set; }

            private void Tick(object sender, EventArgs e)
            {
                Timer.Stop();
                if (action != null)
                    action();
            }
        }
    }
}