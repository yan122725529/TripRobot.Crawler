using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Perst.FullText;

namespace PerstDemo
{
    public class AutosuggestTextBox : AutoCompleteBox, INotifyPropertyChanged
    {
        private bool criteriaChanged;
        private bool isEmpty;
        private bool isInternalTextChanged;
        private string oldText;

        public AutosuggestTextBox()
        {
            IsEmpty = true;
            PerformLostFocus();
            TextChanged += (sender, e) => OnTextChanged();
        }

        public bool IsEmpty
        {
            get { return isEmpty; }
            private set
            {
                isEmpty = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("IsEmpty"));
                InvokePropertyChanged(new PropertyChangedEventArgs("IsSearch"));
            }
        }

        public bool IsSearch
        {
            get { return !IsEmpty; }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public void Clear()
        {
            IsEmpty = true;
            PerformLostFocus();
            InvokeSearchStringChanged(EventArgs.Empty);
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            PerformGotFocus();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            PerformLostFocus();
        }

        protected override void OnPopulating(PopulatingEventArgs e)
        {
            if (((App)Application.Current).Database == null) return;
            var kwrds = new List<string>();
            foreach (Keyword keyword in ((App)Application.Current).Database.GetKeywords(Text))
                kwrds.Add(keyword.NormalForm);
            ItemsSource = kwrds;
        }

        private void InvokePropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, e);
        }

        private void InvokeSearchStringChanged(EventArgs e)
        {
            criteriaChanged = false;
            var searchStringChangedHandler = SearchStringChanged;
            if (searchStringChangedHandler != null) searchStringChangedHandler(this, e);
        }

        private void OnTextChanged()
        {
            if (oldText == Text) return;
            oldText = Text;
            if (isInternalTextChanged)
            {
                isInternalTextChanged = false;
                return;
            }
            IsEmpty = Text.Length == 0;
            criteriaChanged = true;
            InvokeSearchStringChanged(EventArgs.Empty);
        }

        private void PerformGotFocus()
        {
            if (!IsEmpty) return;
            isInternalTextChanged = true;
            Text = "";
            FontStyle = FontStyles.Normal;
            FontWeight = FontWeights.Normal;
            Foreground = new SolidColorBrush(Colors.Black);
            Opacity = 1;
        }

        private void PerformLostFocus()
        {
            if (!IsEmpty) return;
            isInternalTextChanged = true;
            Text = "Search";
            FontStyle = FontStyles.Italic;
            FontWeight = FontWeights.Thin;
            Foreground = new SolidColorBrush(Colors.LightGray);
            if (!criteriaChanged) return;
            InvokeSearchStringChanged(EventArgs.Empty);
        }

        public event EventHandler SearchStringChanged;
    }
}