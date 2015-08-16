using System;
using Perst;

namespace Rdf
{
    /// <summary>Class representing object (collection of its verions)</summary>
    public class VersionHistory:Persistent 
    { 
        /// <summary>Object URI</summary>   
        public string         uri;
        /// <summary>Vector of object versions (the latest version is the last element of the vector)</summary>   
        public Link           versions;    
        /// <summary>type of this object</summary>   
        public VersionHistory type;
  
        /// <summary>Get latest version in version history</summary>   
        public Thing Latest 
        { 
            get 
            { 
                return (Thing)versions[versions.Length-1];
            }
        }
    
        /// <summary>
        /// Get latest version in version history prior to the specified timestamp
        /// </summary>
        /// <param name="timestamp">timestamp</param>
        /// <returns>The latest version in version history prior to the specified timestamp 
        /// or null if no such version is found</returns>
        public Thing GetLatestBefore(DateTime timestamp) 
        {
            for (int i = versions.Count; --i >= 0;) 
            { 
                Thing v = (Thing)versions[i];
                if (v.timestamp <= timestamp) 
                { 
                    return v;
                }
            }
            return null;
        }

        /// <summary>
        /// Get oldest version in version history released after the specified timestamp
        /// </summary>
        /// <param name="timestamp">timestamp</param>
        /// <returns>The oldest version in version history released after the specified timestamp</returns>
        public Thing GetOldestAfter(DateTime timestamp) 
        {
            for (int i = 0; i < versions.Count; i++) 
            { 
                Thing v = (Thing)versions[i];
                if (v.timestamp >= timestamp) 
                { 
                    return v;
                }
            }
            return null;  
        }

        /// <summary>
        /// Get version correponding to the specified search kind and timestamp
        /// </summary>
        /// <param name="kind">One of SearchKind.LAtestVersion, SearchKind.LatestBefore and SearchKind.OldestAfter</param>
        /// <param name="timestamp"></param>
        /// <returns>Version natching time criteria or null if not found</returns>
        public Thing GetVersion(SearchKind kind, DateTime timestamp) 
        {
            switch (kind) 
            {
                case SearchKind.LatestVersion:
                    return Latest;
                case SearchKind.LatestBefore:
                    return GetLatestBefore(timestamp);
                case SearchKind.OldestAfter:
                    return GetOldestAfter(timestamp);
                default:
                    throw new InvalidOperationException("Invalid search kind " + kind + " for VersionHistory.GetVersion");
            }
        }
    }
}