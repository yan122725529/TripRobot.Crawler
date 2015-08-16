namespace Perst
{
    using System;
    using System.Diagnostics;

    /// <summary> Base class for version of versioned object. All versions are kept in version history.
    /// </summary>
    public class Version:PersistentResource
    {
        /// <summary> Get version history containing this versioned object
        /// </summary>
        virtual public VersionHistory VersionHistory
        {
            get
            {
                lock (this)
                {
                    return history;
                }
            }
            
        }
        /// <summary> Get predecessors of this version
        /// </summary>
        /// <returns> array of predecessor versions
        /// 
        /// </returns>
        virtual public Version[] Predecessors
        {
            get
            {
                lock (this)
                {
#if USE_GENERICS
                    return predecessors.ToArray();
#else
                    return (Version[])predecessors.ToArray(typeof(Version));
#endif
                }
            }
            
        }
        /// <summary> Get successors of this version
        /// </summary>
        /// <returns> array of predecessor versions
        /// 
        /// </returns>
        virtual public Version[] Successors
        {
            get
            {
                lock (this)
                {
#if USE_GENERICS
                    return successors.ToArray();
#else
                    return (Version[])successors.ToArray(typeof(Version));
#endif
                }
            }
            
        }
        /// <summary> Check if version is checked-in
        /// </summary>
        /// <returns> <b>true</b> if version belongs to version history
        /// 
        /// </returns>
        virtual public bool IsCheckedIn
        {
            get
            {
                return id != null;
            }
            
        }
        /// <summary> Check if version is checked-out
        /// </summary>
        /// <returns> <b>true</b> if version is just created and not checked-in yet 
        /// (and so belongs to version history)
        /// 
        /// </returns>
        virtual public bool IsCheckedOut
        {
            get
            {
                return id == null;
            }
            
        }
        /// <summary> Get date of version creation 
        /// </summary>
        /// <returns> date when this version was created
        /// 
        /// </returns>
        virtual public DateTime Date
        {
            get
            {
                return date;
            }
            
        }
        /// <summary> Get labels associated with this version
        /// </summary>
        /// <returns> array of labels assigned to this version 
        /// 
        /// </returns>
        virtual public string[] Labels
        {
            get
            {
                lock (this)
                {
                    return labels;
                }
            }
            
        }

        /// <summary> Get identifier of the version 
        /// </summary>
        /// <returns> version identifier  automatically assigned by system
        /// 
        /// </returns>
        virtual public string Id
        {
            get
            {
                return id;
            }
            
        }
                       
        /// <summary> Create new version which will be direct successor of this version.
        /// This version has to be checked-in in order to be placed in version history.
        /// </summary>
        public virtual Version NewVersion()
        {
            Version newVersion = (Version)Clone();
#if USE_GENERICS
            newVersion.predecessors = Storage.CreateLink<Version>(1);
            newVersion.successors = Storage.CreateLink<Version>(1);
#else
            newVersion.predecessors = Storage.CreateLink(1);
            newVersion.successors = Storage.CreateLink(1);
#endif
            newVersion.predecessors.Add(this);
            newVersion.labels = new string[0];
            return newVersion;
        }
        
        /// <summary> Check-in new version. This method inserts in version history version created by 
        /// <b>Version.newVersion</b> or <b>VersionHistory.checkout</b> method
        /// </summary>
        public virtual void  CheckIn()
        {
            lock (history)
            {
                Debug.Assert(IsCheckedOut);
                for (int i = 0; i < predecessors.Count; i++)
                {
#if USE_GENERICS
                    Version predecessor = predecessors[i];
#else
                    Version predecessor = (Version)predecessors[i];
#endif
                    lock (predecessor)
                    {
                        if (i == 0)
                        {
                            id = predecessor.constructId();
                        }
                        predecessor.successors.Add(this);
                    }
                }
                date = System.DateTime.Now;
                history.addVersion(this);
                Modify();
            }
        }
        
        /// <summary> Make specified version predecessor of this version. 
        /// This method can be used to perfrom merge of two versions (merging of version data 
        /// should be done by application itself)
        /// </summary>
        /// <param name="predecessor">version to merged with
        /// 
        /// </param>
        public virtual void AddPredecessor(Version predecessor)
        {
            lock (predecessor)
            {
                lock (this)
                {
                    predecessors.Add(predecessor);
                    if (IsCheckedIn)
                    {
                        predecessor.successors.Add(this);
                    }
                }
            }
        }
        
        
        
        /// <summary> Add new label to this version
        /// </summary>
        /// <param name="label">label to be associated with this version
        /// 
        /// </param>
        public virtual void AddLabel(string label)
        {
            lock (this)
            {
                string[] newLabels = new string[labels.Length + 1];
                Array.Copy(labels, 0, newLabels, 0, labels.Length);
                newLabels[newLabels.Length - 1] = label;
                labels = newLabels;
                Modify();
            }
        }
        
        /// <summary> Check if version has specified label
        /// </summary>
        /// <param name="label">version label
        /// 
        /// </param>
        public virtual bool HasLabel(string label)
        {
            lock (this)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i].Equals(label))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        
        /// <summary> Constructor of roto version. All other versions should be created using 
        /// <b>Version.newVersion</b> or <b>VersionHistory.checkout</b> method
        /// </summary>
        protected internal Version(Storage storage):base(storage)
        {
#if USE_GENERICS
            successors = storage.CreateLink<Version>(1);
            predecessors = storage.CreateLink<Version>(1);
#else
            successors = storage.CreateLink(1);
            predecessors = storage.CreateLink(1);
#endif
            labels = new string[0];
            date = DateTime.Now;
            id = "1";
        }
        
        /// <summary> Default constuctor. No directly accessible.
        /// </summary>
        internal Version()
        {
        }
        
        
        private string constructId()
        {
            int suffixPos = id.LastIndexOf((System.Char) '.');
            int suffix = System.Int32.Parse(id.Substring(suffixPos + 1));
            string nextId = suffixPos < 0
                ? System.Convert.ToString(suffix + 1)
                : id.Substring(0, suffixPos) + System.Convert.ToString(suffix + 1);
            if (successors.Count != 0)
            {
                nextId += '.' + successors.Count + ".1";
            }
            return nextId;
        }
        
#if USE_GENERICS
        internal Link<Version> successors;
        internal Link<Version> predecessors;
#else
        internal Link successors;
        internal Link predecessors;
#endif
        internal string[] labels;
        internal DateTime date;
        internal string   id;
        internal VersionHistory history;
    }
}