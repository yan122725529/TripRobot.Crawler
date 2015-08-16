using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using Perst;
using Perst.FullText;

namespace PerstDemo
{
    public class Base : Persistent
    {
        [NonSerialized]
        private bool isTemp;

        protected static Database Database
        {
            get { return ((App)Application.Current).Database; }
        }

        public bool IsTemp
        {
            get { return isTemp; }
            set { isTemp = value; }
        }

        public override void Deallocate()
        {
            Database.DeleteRecord(this);
        }

        public void Save()
        {
            Store();
            // Manually updating index for all fields marked with
            // [FullTextIndexable] attribute
            Database.UpdateFullTextIndex(this);
        }
    }

    public class Contact : Base, INotifyPropertyChanged
    {
        [FullTextIndexable]
        public string address;
        [FullTextIndexable]
        public string company;
        public string email;
        [FullTextIndexable]
        public string firstName;
        [FullTextIndexable]
        public string lastName;

        public string Address
        {
            get { return address; }
            set
            {
                address = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Address"));
            }
        }

        public string Company
        {
            get { return company; }
            set
            {
                company = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Company"));
            }
        }

        public string Email
        {
            get { return email; }
            set
            {
                email = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Email"));
            }
        }

        public string FirstName
        {
            get { return firstName; }
            set
            {
                firstName = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("FirstName"));
            }
        }

        public Activity LastActivity
        {
            get
            {
                return
                    Database.GetTable<Activity>()
                        .Where(activity => activity.Lead != null
                                           && activity.Lead.Contact == this).FirstOrDefault();
            }
        }

        public string LastName
        {
            get { return lastName; }
            set
            {
                lastName = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("LastName"));
            }
        }

        internal IList<Lead> Leads
        {
            get { return Database.Select<Lead>(l => l.Contact == this).ToObservableCollection(); }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public override void Deallocate()
        {
            foreach (var lead in Leads)
            {
                lead.Deallocate();
            }
            base.Deallocate();
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} {1}", FirstName, LastName);
        }

        private void InvokePropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, e);
            Save();
        }
    }

    public class Lead : Base, INotifyPropertyChanged
    {
        public int amount;
        [Indexable]
        public Contact contact;
        public DateTime expectedClose;
        [FullTextIndexable]
        public string name;
        public Activity nextStep;
        public double probability;

        internal IEnumerable<Activity> Activities
        {
            get { return Database.Select<Activity>(a => a.Lead == this).ToObservableCollection(); }
        }

        public int Amount
        {
            get { return amount; }
            set
            {
                amount = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Amount"));
            }
        }

        public Contact Contact
        {
            get { return contact; }
            set
            {
                contact = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Contact"));
            }
        }

        public DateTime ExpectedClose
        {
            get { return expectedClose; }
            set
            {
                expectedClose = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("ExpectedClose"));
            }
        }

        public string Name
        {
            get { return name ?? ""; }
            set
            {
                name = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Name"));
            }
        }

        public Activity NextStep
        {
            get { return nextStep; }
            set
            {
                nextStep = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("NextStep"));
            }
        }

        public double Probability
        {
            get { return probability; }
            set
            {
                probability = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Probability"));
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public override void Deallocate()
        {
            foreach (var activity in Activities)
            {
                activity.Deallocate();
            }
            base.Deallocate();
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", Contact != null ? Contact.LastName : "", Name);
        }

        private void InvokePropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, e);
            Save();
        }
    }

    public enum ActivityPriority
    {
        Low,
        Normal,
        High
    }

    public enum ActivityStatus
    {
        Complete,
        Incomplete
    }

    public enum ActivityType
    {
        Meeting,
        Call
    }

    public class Activity : Base, INotifyPropertyChanged
    {
        public ActivityType activityType;
        public DateTime due;
        [Indexable]
        public Lead lead;
        public ActivityPriority priority;
        public ActivityStatus status;
        [FullTextIndexable]
        public string subject;

        public DateTime Due
        {
            get { return due; }
            set
            {
                due = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Due"));
            }
        }

        public Lead Lead
        {
            get { return lead; }
            set
            {
                lead = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Lead"));
            }
        }

        public ActivityPriority Priority
        {
            get { return priority; }
            set
            {
                priority = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Priority"));
            }
        }

        public ActivityStatus Status
        {
            get { return status; }
            set
            {
                status = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Status"));
            }
        }

        public string Subject
        {
            get { return subject ?? ""; }
            set
            {
                subject = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("Subject"));
            }
        }

        public ActivityType ActivityType
        {
            get { return activityType; }
            set
            {
                activityType = value;
                InvokePropertyChanged(new PropertyChangedEventArgs("ActivityType"));
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public override void Deallocate()
        {
            var leads = Database.GetTable<Lead>().Where(l => l.NextStep == this);
            foreach (var l in leads)
            {
                l.NextStep = null;
                l.Store();
            }
            base.Deallocate();
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} {1}. {2}. {3}", Due, Subject,
                                 Lead != null ? Lead.Name : "",
                                 Lead != null && Lead.Contact != null ? Lead.Contact.LastName : "");
        }

        private void InvokePropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, e);
            Save();
        }
    }
}