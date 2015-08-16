using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Perst;

namespace PerstDemo
{
    public class DetailPanel : StackPanel, INotifyPropertyChanged
    {
        private readonly Dictionary<ItemsControl, Type> dropdowns = new Dictionary<ItemsControl, Type>();
        private IList target;

        public DetailPanel(Type typeObj, DataGrid dataGrid)
        {
            if (typeObj == null) throw new ArgumentNullException("typeObj");
            if (dataGrid == null) throw new ArgumentNullException("dataGrid");
            TypeObj = typeObj;
            DataGrid = dataGrid;
            Init();
        }

        public DataGrid DataGrid { get; private set; }

        public IList Target
        {
            get
            {
                if (target == null)
                    target = new List<object>();
                return target;
            }
            set
            {
                SetTarget(value);
                ResetControls();
                EvaluateDataContext();
                InvokePropertyChanged(new PropertyChangedEventArgs("Target"));
                InvokePropertyChanged(new PropertyChangedEventArgs("Title"));
            }
        }

        public string Title
        {
            get
            {
                if (Target == null) return "";
                switch (Target.Count)
                {
                    case 0:
                        return string.Format("No {0} Selected", TypeObj.Name);
                    case 1:
                        return string.Format("{0} Details", TypeObj.Name);
                    default:
                        return string.Format("{0} {1}s Details", Target.Count, TypeObj.Name);
                }
            }
        }

        public Type TypeObj { get; private set; }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private static string SeparateCapitalWords(IEnumerable<char> name)
        {
            var array = name.ToList();
            var res = new List<char>();
            foreach (var c in array)
            {
                if (res.Count > 1 && c >= 'A' && c <= 'Z')
                    res.Add(' ');
                res.Add(c);
            }
            return new string(res.ToArray());
        }

        public void FocusFirstTextBox()
        {
            UpdateLayout();
            foreach (var child in Children)
            {
                if (!(child is CoverControl)
                    || !(((CoverControl)child).Control is TextBox)) continue;
                ((CoverControl)child).Control.Focus();
                break;
            }
        }

        private void DeleteOnClick(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItems == null) return;
            if (MessageBox.Show(
                    string.Format("Delete record - {0}?",
                                  (DataGrid.SelectedItems.Count == 1
                                       ? DataGrid.SelectedItems[0]
                                       : string.Format("{0} items", DataGrid.SelectedItems.Count))), "Delete",
                    MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            var selected = new ArrayList();
            foreach (var item in DataGrid.SelectedItems)
                selected.Add(item);
            foreach (var item in selected)
            {
                ((IList)DataGrid.ItemsSource).Remove(item);
                ((Persistent)item).Deallocate(); // Removing Deleted object from Database
            }
            ((App)Application.Current).Database.Storage.Commit(); // Commiting Changes
        }

        private void EvaluateDataContext()
        {
            Base context = null;
            if (Target.Count == 1)
                context = (Base)Target[0];
            else if (Target.Count > 1)
            {
                var type = Target[0].GetType();
                context = (Base)Activator.CreateInstance(type);
                context.IsTemp = true;
            }
            if (context is INotifyPropertyChanged)
            {
                ((INotifyPropertyChanged)context).PropertyChanged -= OnPropertyChanged;
                ((INotifyPropertyChanged)context).PropertyChanged += OnPropertyChanged;
            }
            if (DataContext is INotifyPropertyChanged)
                ((INotifyPropertyChanged)DataContext).PropertyChanged -= OnPropertyChanged;

            DataContext = null;
            if (context != null)
                RefreshDropDowns();
            else
                ResetDropDowns();

            DataContext = context;
            if (DataContext is Base && ((Base)DataContext).IsTemp)
                IntersectProperties(context);
        }

        private void Init()
        {
            var detail = this;
            foreach (var propertyInfo in TypeObj.GetProperties(BindingFlags.DeclaredOnly
                    | BindingFlags.Public | BindingFlags.Instance))
            {
                detail.Children.Add(new TextBlock { Text = SeparateCapitalWords(propertyInfo.Name) });
                FrameworkElement element;
                var binding = new Binding(propertyInfo.Name);
                if (propertyInfo.CanWrite)
                {
                    binding.Mode = BindingMode.TwoWay;
                    if (typeof(DateTime).IsAssignableFrom(propertyInfo.PropertyType))
                    {
                        var dtp = new DatePicker();
                        dtp.SetBinding(DatePicker.SelectedDateProperty, binding);
                        element = new CoverControl { Control = dtp, Name = propertyInfo.Name };
                    }
                    else if (typeof(Enum).IsAssignableFrom(propertyInfo.PropertyType))
                    {
                        var cb = new ComboBox();
                        cb.SetBinding(Selector.SelectedItemProperty, binding);
                        var propertyInfo1 = propertyInfo;
                        Utilities.FillEnums(cb, propertyInfo1.PropertyType);
                        element = new CoverControl { Control = cb, Name = propertyInfo.Name };
                    }
                    else if (typeof(Persistent).IsAssignableFrom(propertyInfo.PropertyType))
                    {
                        var cb = new ComboBox();
                        cb.SetBinding(Selector.SelectedItemProperty, binding);
                        var propertyInfo1 = propertyInfo;
                        dropdowns[cb] = propertyInfo1.PropertyType;
                        element = new CoverControl { Control = cb, Name = propertyInfo.Name };
                    }
                    else
                    {
                        var tb = new TextBox();
                        tb.SetBinding(TextBox.TextProperty, binding);
                        element = new CoverControl { Control = tb, Name = propertyInfo.Name };
                    }
                }
                else
                {
                    binding.Mode = BindingMode.OneWay;
                    var tb = new TextBlock();
                    tb.SetBinding(TextBlock.TextProperty, binding);
                    element = tb;
                }
                detail.Children.Add(element);
            }

            var grid = new Grid { Margin = new Thickness(0, 50, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var b = new Button { Content = "Delete" };
            b.Click += DeleteOnClick;
            b.SetValue(Grid.ColumnProperty, 2);
            grid.Children.Add(b);

            detail.Children.Add(grid);
        }

        private void IntersectProperties(Base context)
        {
            foreach (var c in Children)
            {
                var child = c as CoverControl;
                if (child == null) continue;
                var propInfo = target[0].GetType().GetProperty(child.Name);
                if (propInfo == null)
                    throw new ArgumentNullException(string.Format("Property with name {0} not found.", child.Name));
                if (!propInfo.CanWrite) continue;
                var firstObject = true;
                object val = null;
                foreach (var o in target)
                {
                    var nextVal = propInfo.GetValue(o, null);
                    if (firstObject)
                    {
                        val = nextVal;
                        firstObject = false;
                        continue;
                    }
                    if (Equals(nextVal, val)) continue;
                    child.IsShowingCover = true;
                    break;
                }
                if (!child.IsShowingCover)
                    propInfo.SetValue(context, val, null);
            }
        }

        private void InvokePropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, e);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var baseSender = (Base)sender;
            var coverControl =
                (CoverControl)
                (from child in Children
                 where child is CoverControl && ((CoverControl)child).Name == e.PropertyName
                 select child).First();
            coverControl.IsShowingCover = false;
            if (baseSender.IsTemp)
                WriteValueToTargets(baseSender, e.PropertyName);
            else
                baseSender.Save();
        }

        private void RefreshDropDowns()
        {
            foreach (var dd in dropdowns)
                Utilities.FillObjects(dd.Key, dd.Value, Target);
        }

        private void ResetControls()
        {
            foreach (var c in Children)
            {
                var child = c as CoverControl;
                if (child == null) continue;
                child.IsShowingCover = false;
                child.AllowShowCover = Target.Count > 1;
            }
        }

        private void ResetDropDowns()
        {
            foreach (var dropdown in dropdowns)
                dropdown.Key.Items.Clear();
        }

        private void SetTarget(IList value)
        {
            target.Clear();
            foreach (var o in value)
                target.Add(o);
        }

        private void WriteValueToTargets(object sender, string paramName)
        {
            var pInfo = sender.GetType().GetProperty(paramName);
            if (pInfo == null) return;
            var val = pInfo.GetValue(sender, null);
            foreach (Base obj in target)
            {
                pInfo.SetValue(obj, val, null);
                obj.Save();
            }
        }
    }
}