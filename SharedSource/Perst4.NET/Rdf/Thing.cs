using System;
using Perst;
using System.IO;
using System.Collections;

namespace Rdf
{
    /// <summary>Version of the object</summary>
    public class Thing:Persistent 
    {
        /// <summary>Object version history</summary>
        public VersionHistory  vh;
        /// <summary>Particular version of type of the object</summary>
        public Thing           type;
        /// <summary>Version creation timestamp</summary>
        public DateTime        timestamp;
        /// <summary>Property values</summary>
        public PropVal[]       props;

        /// <summary>Check if it is the latest version in version history</summary>
        /// <returns>true if version is the last in version history</returns>
        public bool IsLatest() 
        {
            return vh.Latest == this;
        }

        /// <summary>Get values of the property with given name</summary>
        /// <param name="propName">name of the proiperty</param>
        public object[] this[string propName]
        {
            get 
            {
                if (propName == Symbols.Timestamp) 
                {
                    return new object[]{timestamp};
                }
                ArrayList list = new ArrayList();
                foreach (PropVal prop in props) 
                {
                    if (prop.def.name.Equals(propName)) 
                    {
                        list.Add(prop.val);
                    }
                }
                return list.ToArray();
            }
        }

        
        /// <summary>Check if object belongs to the partiular type</summary>
        /// <param name="superType">version history representing object type</param>
        /// <param name="kind">search kind</param>
        /// <param name="timestamp">timestamp used to locate version</param>
        /// <returns>true if type of the object is the same or is subtype of specified type</returns>
        public bool IsInstanceOf(VersionHistory superType, SearchKind kind, DateTime timestamp) 
        {
            return type.IsSubTypeOf(superType, kind, timestamp);
        }
    
        // <summary>This method is applicable only to objects represnting types and 
        // checks if this type is the same or subtype of specified type</summary>
        /// <param name="superType">version history representing object type</param>
        /// <param name="kind">search kind</param>
        /// <param name="timestamp">timestamp used to locate version</param>
        /// <returns>true if this type is the same or is subtype of specified type</returns>
        public bool IsSubTypeOf(VersionHistory superType, SearchKind kind, DateTime timestamp) 
        {
            if (vh == superType) 
            { 
                return true;
            }
            foreach (VersionHistory subtype in this[Symbols.Subtype]) 
            {
                if (kind == SearchKind.AllVersions) 
                {
                    foreach (Thing type in subtype.versions) 
                    {
                        if (type.IsSubTypeOf(superType, kind, timestamp)) 
                        {
                            return true;
                        }
                    }
                } 
                else 
                {
                    Thing type = subtype.GetVersion(kind, timestamp);
                    if (type != null && type.IsSubTypeOf(superType, kind, timestamp)) 
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}