using System.Threading;
using System.Windows;

namespace PerstDemo
{
    public partial class ClearPopup
    {
        private DataGenerator generator;

        public ClearPopup()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            OKButton.IsEnabled = false;
            CancelButton.IsEnabled = false;

            generator = new DataGenerator(((App)Application.Current).Database);
            generator.ClearComplete += (sender1, e1) => Dispatcher.BeginInvoke(() => DialogResult = true);

            ThreadPool.QueueUserWorkItem(state => generator.DropStorage());
        }
    }
}