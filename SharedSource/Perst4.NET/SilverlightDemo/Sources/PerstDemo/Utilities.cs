using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Perst;

namespace PerstDemo
{
    public static class Utilities
    {
        public static void FillEnums(ItemsControl editElement, Type enumType)
        {
            if (editElement == null) throw new ArgumentNullException("editElement");
            if (enumType == null) throw new ArgumentNullException("enumType");
            if (!typeof (Enum).IsAssignableFrom(enumType))
                throw new InvalidOperationException("enumType must be of Enum type");
            foreach (var value in GetEnumValues(enumType))
                editElement.Items.Add(value);
        }

        public static void FillObjects(ItemsControl editElement, Type fillType, IList context)
        {
            if (editElement == null) throw new ArgumentNullException("editElement");
            if (fillType == null) throw new ArgumentNullException("fillType");
            if (!typeof (Persistent).IsAssignableFrom(fillType))
                throw new InvalidOperationException("fillType must be Persistent");

            IEnumerable items = null;
            var db = ((App)Application.Current).Database;
            if (typeof (Contact).IsAssignableFrom(fillType))
            {
                items = from item in db.GetTable<Contact>()
                        // Load all Contacts from DB
                        select item;
            }
            else if (typeof (Lead).IsAssignableFrom(fillType))
            {
                var contacts = new List<Contact>();
                foreach (var o in context)
                    contacts.Add(((Activity) o).Lead.Contact);
                items = from item in db.GetTable<Lead>()
                        // Load all Leads where
                        where contacts.Contains(item.Contact)
                        // Lead.Contact is in selected Contacts
                        select item;
            }
            else if (typeof (Activity).IsAssignableFrom(fillType))
            {
                var leads = context.Cast<Lead>();
                items = from item in db.GetTable<Activity>()
                        // Load all Activities where
                        where leads.Contains(item.Lead)
                        // Activity.Lead is in selected Leads
                        select item;
            }
            editElement.Items.Clear();
            if (items == null) return;
            foreach (var obj in items)
                editElement.Items.Add(obj);
        }

        public static object[] GetEnumValues(Type enumType)
        {
            if (!enumType.IsEnum)
                throw new ArgumentException("Type '" + enumType.Name + "' is not an enum");

            var values = new List<object>();

            var fields = from field in enumType.GetFields() where field.IsLiteral select field;

            foreach (var field in fields)
            {
                var value = field.GetValue(enumType);
                values.Add(value);
            }

            return values.ToArray();
        }


        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
        {
            var collection = new ObservableCollection<T>();
            foreach (var contact in source)
                collection.Add(contact);
            return collection;
        }
    }
    public class VisibilityBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (bool)value;
            return val ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}