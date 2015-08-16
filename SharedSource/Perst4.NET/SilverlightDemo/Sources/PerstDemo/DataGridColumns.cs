using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace PerstDemo
{
    public class DataGridDateTimeColumn : DataGridBoundColumn
    {
        public string DateFormat { get; set; }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var dp = new DatePicker {VerticalAlignment = VerticalAlignment.Stretch};
            if (DateFormat != null)
            {
                IValueConverter dtc = new DateTimeConverter();
                Binding.Converter = dtc;
                Binding.ConverterParameter = DateFormat;
            }
            dp.SetBinding(DatePicker.SelectedDateProperty, Binding);
            return dp;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var txt = new TextBlock {VerticalAlignment = VerticalAlignment.Center};
            if (DateFormat != null)
            {
                var dtc = new DateTimeConverter();
                Binding.Converter = dtc;
                Binding.ConverterParameter = DateFormat;
            }
            txt.SetBinding(TextBlock.TextProperty, Binding);
            return txt;
        }

        protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
        {
            var dp = editingElement as DatePicker;
            if (dp != null)
            {
                var dt = dp.SelectedDate;
                if (dt.HasValue) return dt.Value;
            }
            return DateTime.Now;
        }
    }

    public class DataGridEnumColumn : DataGridBoundColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var editElement = new ComboBox();
            editElement.SetBinding(Selector.SelectedItemProperty, Binding);

            var prop = dataItem.GetType().GetProperty(Binding.Path.Path,
                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
            Utilities.FillEnums(editElement, prop.PropertyType);
            return editElement;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var editElement = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            editElement.SetBinding(TextBlock.TextProperty, Binding);
            return editElement;
        }

        protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
        {
            return editingEventArgs.OriginalSource;
        }
    }

    public class DataGridObjectsColumn : DataGridBoundColumn
    {
        public DataGridObjectsColumn()
        {
            IsReadOnly = false;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var editElement = new ComboBox();
            editElement.SetBinding(Selector.SelectedItemProperty, Binding);

            var prop = dataItem.GetType().GetProperty(Binding.Path.Path,
                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
            Utilities.FillObjects(editElement, prop.PropertyType, new[] { dataItem });
            return editElement;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var editElement = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            editElement.SetBinding(TextBlock.TextProperty, Binding);
            return editElement;
        }

        protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
        {
            return editingEventArgs.OriginalSource;
        }
    }

    public class DateTimeConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var date = (DateTime) value;
            return date.ToString(parameter.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var strValue = value.ToString();
            DateTime resultDateTime;
            return DateTime.TryParse(strValue, out resultDateTime) ? resultDateTime : value;
        }

        #endregion
    }
}