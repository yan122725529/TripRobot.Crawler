using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PerstDemo
{
    public partial class CoverControl
    {
        private Control control;
        private bool isShowingCover;
        private bool loosingFocus;

        public CoverControl()
        {
            InitializeComponent();
            var border = new Border
                             {
                                 BorderBrush = new SolidColorBrush(Colors.Black),
                                 BorderThickness = new Thickness(.5),
                                 Padding = new Thickness(2),
                                 Child =
                                     Cover =
                                     new TextBlock
                                         {
                                             VerticalAlignment = VerticalAlignment.Stretch,
                                             HorizontalAlignment = HorizontalAlignment.Stretch,
                                         }
                             };
            CoverText = "<< different >>";
            body.Children.Add(border);
            Cover.MouseLeftButtonDown += CoverOnGotFocus;
        }

        public bool AllowShowCover { get; set; }

        public Control Control
        {
            get { return control; }
            set
            {
                if (control != null)
                {
                    body.Children.Remove(control);
                    control.LostFocus -= ControlOnLostFocus;
                }

                control = value;
                if (control != null)
                    control.LostFocus += ControlOnLostFocus;
                body.Children.Add(control);
                ShowControl();
            }
        }

        public TextBlock Cover { get; private set; }

        public string CoverText
        {
            get { return Cover.Text; }
            set { Cover.Text = value; }
        }

        public bool IsShowingCover
        {
            get { return isShowingCover; }
            set
            {
                if (isShowingCover == value) return;
                isShowingCover = value;
                if (isShowingCover)
                    ShowCover();
                else
                    ShowControl();
            }
        }

        private void ControlOnLostFocus(object sender, RoutedEventArgs e)
        {
            if (loosingFocus)
            {
                loosingFocus = false;
                ((Control) sender).Focus();
                return;
            }

            if (Control is DatePicker && ((DatePicker) Control).IsDropDownOpen)
                return;
            
            if (Control is ComboBox && ((ComboBox) Control).IsDropDownOpen)
                return;
            
            if (IsShowingCover)
                ShowCover();
        }

        private void CoverOnGotFocus(object sender, RoutedEventArgs e)
        {
            if (Control == null) return;
            ShowControl();
            loosingFocus = true;
            Control.Focus();
        }

        private void ShowControl()
        {
            if (Control == null) return;
            Control.Visibility = Visibility.Visible;
            Cover.Visibility = Visibility.Collapsed;
        }

        private void ShowCover()
        {
            if (!AllowShowCover) return;
            if (Control != null)
                Control.Visibility = Visibility.Collapsed;
            Cover.Visibility = Visibility.Visible;
            UpdateLayout();
        }
    }
}