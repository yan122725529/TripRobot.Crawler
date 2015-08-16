using System;

namespace Perst.FullText
{
    /// <summary>
    /// Attribute for marking full text indexable fields used by Database class to create table descriptors. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FullTextIndexableAttribute : Attribute
    {
        public FullTextIndexableAttribute() { }
    }
}
