namespace Perst
{
    using System;
    using System.Diagnostics;
#if USE_GENERICS
        using System.Collections.Generic;
#endif
    using System.Collections;

    /// <summary> Collection of version of versioned object.
    /// Versioned object should be access through version history object.
    /// Instead of storing direct reference to Verson in some component of some other persistent object, 
    /// it is necessary to store reference to it's VersionHistory.
    /// </summary>
#if USE_GENERICS
    public abstract class VersionHistory:PersistentResource
    {    
        internal abstract void addVersion(Version v);
    }

    public class VersionHistory<V>:VersionHistory,IEnumerable,IEnumerable<V> where V:Version
#else
    public class VersionHistory:PersistentResource
#endif
    {
        /// <summary> Get/Set current version in version history.
        /// Current version can be explicitely set by setVersion or result of last checkOut
        /// is used as current version
        /// </summary>
#if USE_GENERICS
        virtual public V Current
#else
        virtual public Version Current
#endif
        {
            get
            {
                lock (this)
                {
                    return current;
                }
            }
            
            set
            {
                lock (this)
                {
                    current = value;
                    Modify();
                }
            }
            
        }
        /// <summary> Get root version
        /// </summary>
        /// <returns> root version in this version history
        /// 
        /// </returns>
#if USE_GENERICS
        virtual public V Root
        {
            get
            {
                lock (this)
                {
                    return versions[0];
                }
            }           
        }
#else
        virtual public Version Root
        {
            get
            {
                lock (this)
                {
                    return (Version)versions[0];
                }
            }           
        }
#endif

        /// <summary> Get all versions in version history
        /// </summary>
        /// <returns>array of versions sorted by date
        /// 
        /// </returns>
#if USE_GENERICS
        virtual public V[] AllVersions
        {
            get
            {
                lock (this)
                {
                    return versions.ToArray();
                }
            }           
        }
#else
        virtual public Version[] AllVersions
        {
            get
            {
                lock (this)
                {
                    return (Version[])versions.ToArray(typeof(Version));
                }
            }           
        }
#endif
        
        
        /// <summary> Checkout current version: create successor of the current version.
        /// This version has to be checked-in in order to be placed in version history
        /// </summary>
        /// <returns> checked-out version
        /// 
        /// </returns>
#if USE_GENERICS
        public virtual V CheckOut()
        {
            lock (this)
            {
                Debug.Assert(current.IsCheckedIn);
                return (V)current.NewVersion();
            }
        }
#else
        public virtual Version CheckOut()
        {
            lock (this)
            {
                Debug.Assert(current.IsCheckedIn);
                return current.NewVersion();
            }
        }
#endif
        
        /// <summary> Get latest version in version history
        /// </summary>
        /// <returns> version with the largest timestamp 
        /// 
        /// </returns>
#if USE_GENERICS
        public virtual V Latest
        {
            get 
            {
                return versions[versions.Count-1];
            }
        }
#else
        public virtual Version Latest
        {
            get 
            {
                return (Version)versions[versions.Count-1];
            }
        }
#endif                
        
        /// <summary> Get latest version before specified date
        /// </summary>
        /// <param name="timestamp">deadline
        /// </param>
        /// <returns> version with the largest timestamp less than specified <b>timestamp</b>
        /// 
        /// </returns>
#if USE_GENERICS
        public virtual V GetLatestBefore(DateTime timestamp)
        {
            lock (this)
            {
                int l = 0, n = versions.Count, r = n;
                long t = timestamp.Ticks;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (versions[m].Date.Ticks < t)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                return r > 0 ? versions[r-1] : null;
            }
        }
#else
        public virtual Version GetLatestBefore(DateTime timestamp)
        {
            lock (this)
            {
                int l = 0, n = versions.Count, r = n;
                long t = timestamp.Ticks;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (((Version)versions[m]).Date.Ticks < t)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                return r > 0 ? (Version)versions[r-1] : null;
            }
        }
#endif
        
        /// <summary> Get earliest version adter specified date
        /// </summary>
        /// <param name="timestamp">deadline
        /// </param>
        /// <returns> version with the smallest timestamp greater than specified <b>timestamp</b>
        /// 
        /// </returns>
#if USE_GENERICS
        public virtual V GetEarliestAfter(DateTime timestamp)
        {
            lock (this)
            {
                int l = 0, n = versions.Count, r = n;
                long t = timestamp.Ticks;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (versions[m].Date.Ticks < t)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                return r < n ? versions[r] : null;
            }
        }
#else
        public virtual Version GetEarliestAfter(DateTime timestamp)
        {
            lock (this)
            {
                int l = 0, n = versions.Count, r = n;
                long t = timestamp.Ticks;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (((Version)versions[m]).Date.Ticks < t)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                return r < n ? (Version)versions[r] : null;
            }
        }
#endif
        
        
        /// <summary> Get version with specified label. If there are more than one version marked with 
        /// this label, then the latest one will be returned
        /// </summary>
        /// <param name="label">version label
        /// </param>
        /// <returns> latest version with specified label
        /// 
        /// </returns>
#if USE_GENERICS
        public virtual V GetVersionByLabel(string label)
#else
        public virtual Version GetVersionByLabel(string label)
#endif
        {
            lock (this)
            {
                for (int i = versions.Count; --i >= 0;)
                {
#if USE_GENERICS
                    V v = versions[i];
#else
                    Version v = (Version)versions[i];
#endif
                    if (v.HasLabel(label))
                    {
                        return v;
                    }
                }
                return null;
            }
        }
        
        /// <summary> Get version with specified ID.
        /// </summary>
        /// <param name="id">version identifier
        /// </param>
        /// <returns> version with specified ID
        /// 
        /// </returns>
#if USE_GENERICS
        public virtual V GetVersionById(string id)
#else
        public virtual Version GetVersionById(string id)
#endif
        {
            lock (this)
            {
                for (int i = versions.Count; --i >= 0;)
                {
#if USE_GENERICS
                    V v = versions[i];
#else
                    Version v = (Version)versions[i];
#endif
                    if (v.Id == id)
                    {
                        return v;
                    }
                }
                return null;
            }
        }
        
        
        /// <summary> Get iterator through all version in version history
        /// Iteration is started from the root version and performed in direction of increaing
        /// version timestamp
        /// </summary>
        /// <returns>enumerator of all versions in version history
        /// </returns>
#if USE_GENERICS
        public virtual IEnumerator<V> GetEnumerator()
        {
            lock (this)
            {
                return versions.GetEnumerator();
            }
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (this)
            {
                return ((IEnumerable)versions).GetEnumerator();
            }
        }
#else
        public virtual IEnumerator GetEnumerator()
        {
            lock (this)
            {
                return ((IEnumerable)versions).GetEnumerator();
            }
        }       
#endif
        /// <summary> Create new version history
        /// </summary>
        /// <param name="root">root version
        /// 
        /// </param>
#if USE_GENERICS
        public VersionHistory(V root)
#else
        public VersionHistory(Version root)
#endif
        {
#if USE_GENERICS
            versions = root.Storage.CreateLink<V>(1);
#else
            versions = root.Storage.CreateLink(1);
#endif
            versions.Add(root);
            current = root;
            current.history = this;
        }
        
#if USE_GENERICS
        internal override void addVersion(Version v)
        {
            versions.Add((V)v);
            current = (V)v;
        }
#else                   
        internal void addVersion(Version v)
        {
            versions.Add(v);
            current = v;
        }
#endif

        internal VersionHistory() {}

#if USE_GENERICS
        internal Link<V>    versions;
        internal V current;
#else
        internal Link             versions;
        internal Version current;
#endif
    }
}