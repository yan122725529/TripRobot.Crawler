using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace PerstDemo
{
    public partial class GeneratorPopup
    {
        private DataGenerator _generator;

        public GeneratorPopup()
        {
            InitializeComponent();
            sliderAllCount.Maximum = sliderAllCount.Maximum;
            attTextBlock.Visibility = ((App)Application.Current).Database == null ?
                Visibility.Collapsed : Visibility.Visible;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            gRes.Visibility = Visibility.Visible;
            gReq.Visibility = Visibility.Collapsed;

            OKButton.IsEnabled = false;
            OKButton.Visibility = Visibility.Collapsed;
            CancelButton.IsEnabled = true;
            CancelButton.Content = "Stop";

            if (((App)Application.Current).Database == null)
            {
                ((App)Application.Current).InitializePerstStorage();
            }
            else
            {
                _generator = new DataGenerator(((App)Application.Current).Database);
                _generator.DropStorage();
                ((App)Application.Current).InitializePerstStorage();
            }

            _generator = new DataGenerator(((App)Application.Current).Database);

            _generator.GeneratedContact +=
                (sender1, e1) => Dispatcher.BeginInvoke(ReadLabels);
            _generator.GenerationComplete +=
                (sender1, e1) =>
                {
                    _generator = null;
                    Dispatcher.BeginInvoke(() => DialogResult = true);
                };
            _generator.Committing +=
                (sender1, e1) =>
                    Dispatcher.BeginInvoke(() =>
                    {
                        ReadLabels();
                        cStatus.Text = "Committing...";
                        CancelButton.IsEnabled = false;
                    });

            var c = (int)sliderAllCount.Value;
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                var freespace = store.AvailableFreeSpace;
                var needSpace = c * DataGenerator.OneObjectAvgSize;
                if (freespace < needSpace)
                {
                    if (!store.IncreaseQuotaTo(store.Quota + (needSpace - freespace)))
                    {
                        cStatus.Text = "Data generation aborted";
                        CancelButton.Content = "Ok";
                        _generator = null;
                        return;
                    }
                }
            }
            ThreadPool.QueueUserWorkItem(state => _generator.Generate(c));
        }

        private void ReadLabels()
        {
            cContacts.Text = string.Format("Contacts: {0}", _generator.CountContacts);
            cLeads.Text = string.Format("Leads: {0}", _generator.CountLeads);
            cActivities.Text = string.Format("Activities: {0}", _generator.CountActivities);
            cStatus.Text = "";
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var v = e.NewValue;
            if (v % 100 > 0)
                ((Slider)sender).Value = ((int)(v / 100)) * 100;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_generator != null)
            {
                _generator.IsComplete = true;
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }
}