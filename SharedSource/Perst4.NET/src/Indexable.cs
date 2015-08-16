using System;

namespace Perst
{
    /// <summary>
    /// Attribute for marking indexable fields used by Database class to create table descriptors. 
    /// Indices can be unique or allow duplicates.
    /// If index is marked as unique and during transaction commit it is find out that there is already some other object
    /// with this key, NotUniqueException will be thrown
    /// Case insensitive attribute is meaningful only for string keys and if set cause ignoring case
    /// of key values.
    /// Thick index should be used for keys with small set of unique values.
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
    public class IndexableAttribute : Attribute
    {
        /// <summary>
        /// Indexable atttribute constructor
        /// </summary>
        /// <param name="unique">Index may not contain dupicates</param>
        /// <param name="caseInsensitive">String index is case insensitive</param>
        /// <param name="thick">Index is optimized to handle large number of duplicate key values</param>
        public IndexableAttribute(bool unique, bool caseInsensitive, bool thick) 
        : this(unique, caseInsensitive, thick, false, false)
        {
        }

        /// <summary>
        /// Indexable atttribute constructor
        /// </summary>
        /// <param name="unique">Index may not contain dupicates</param>
        /// <param name="caseInsensitive">String index is case insensitive</param>
        /// <param name="thick">Index is optimized to handle large number of duplicate key values</param>
        /// <param name="randomAccess">Index supports fast access to elements by position</param>
        public IndexableAttribute(bool unique, bool caseInsensitive, bool thick, bool randomAccess)
            : this(unique, caseInsensitive, thick, randomAccess, false)
        {
        }

        /// <summary>
        /// Indexable atttribute constructor
        /// </summary>
        /// <param name="unique">Index may not contain dupicates</param>
        /// <param name="caseInsensitive">String index is case insensitive</param>
        /// <param name="thick">Index is optimized to handle large number of duplicate key values</param>
        /// <param name="randomAccess">Index supports fast access to elements by position</param>
        /// <param name="regex">3-gram index for fast regular expression match</param>
        public IndexableAttribute(bool unique, bool caseInsensitive, bool thick, bool randomAccess, bool regex)
            : this(unique, caseInsensitive, thick, randomAccess, regex, false)
        {
        }

        /// <summary>
        /// Indexable atttribute constructor
        /// </summary>
        /// <param name="unique">Index may not contain dupicates</param>
        /// <param name="caseInsensitive">String index is case insensitive</param>
        /// <param name="thick">Index is optimized to handle large number of duplicate key values</param>
        /// <param name="randomAccess">Index supports fast access to elements by position</param>
        /// <param name="regex">3-gram index for fast regular expression match</param>
        /// <param name="autoincrement">Index on autoincremented key field</param>
        public IndexableAttribute(bool unique, bool caseInsensitive, bool thick, bool randomAccess, bool regex, bool autoincrement)
        {
            this.unique = unique;
            this.caseInsensitive = caseInsensitive;
            this.thick = thick;
            this.randomAccess = randomAccess;
            this.regex = regex;
            this.autoincrement = autoincrement;
        }

        public IndexableAttribute() 
        : this(false, false, false, false, false, false)
        {
        }
        
        /// <summary>
        /// Index may not contain dupicates
        /// </summary>
        public bool Unique
        {
            get { return unique; }
            set { unique = value; }
        }
        
        /// <summary>
        /// Index is optimized to handle large number of duplicate key values
        /// </summary>
        public bool Thick
        {
            get { return thick; }
            set { thick = value; }
        }
        
        /// <summary>
        /// String index is case insensitive
        /// </summary>
        public bool CaseInsensitive
        {
            get { return caseInsensitive; }
            set { caseInsensitive = value; }
        }

        /// <summary>
        /// Index supports fast access to elements by position
        /// </summary>
        public bool RandomAccess
        {
            get { return randomAccess; }
            set { randomAccess = value; }
        }

        /// <summary>
        /// 3-gram index for fast regular expression match
        /// </summary>
        public bool Regex
        {
            get { return regex; }
            set { regex = value; }
        }

        /// <summary>
        ///  Index on autoincremented key field
        /// </summary>
        public bool Autoincrement
        {
            get { return autoincrement; }
            set { autoincrement = value; }
        }

        private bool caseInsensitive;
        private bool unique;
        private bool thick;
        private bool randomAccess;
        private bool regex;
        private bool autoincrement;
    }
}
