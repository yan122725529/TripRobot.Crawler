namespace Perst
{
    /// <summary>
    /// Class encapsulating native .Net string. System.String is not persistent object
    /// so it can not be stored in Perst as independent persistent object. 
    /// But sometimes it is needed. This class sole this problem providing implcit conversion
    /// operator from System.String to PerisstentString.
    /// Also PersistentString class is mutable, allowing to change it's values.
    /// </summary>
    public class PersistentString : PersistentResource
    {
        /// <summary>
        /// Consutrctor of perisstent string
        /// </summary>
        /// <param name="str">.Net string</param>
        public PersistentString(string str)
        {
            this.str = str;
        }

        /// <summary>
        /// Get .Net string
        /// </summary>
        /// <returns>.Net string</returns>
        public override string ToString() 
        {
            return str;
        }

        /// <summary>
        /// Append string to the current string value of PersistentString
        /// </summary>
        /// <param name="tail">appended string</param>
        public void Append(string tail) 
        {
            str = str + tail;
            Modify();
        }

        /// <summary>
        /// Assign new string value to the PersistentString
        /// </summary>
        /// <param name="str">new string value</param>
        public void Set(string str)
        {
            this.str = str;
            Modify();
        }
    
        /// <summary>
        /// Get current string value
        /// </summary>
        /// <returns>.Net string</returns>
        public string Get() 
        {
            return str;
        }

        /// <summary>
        /// Operator for implicit convertsion from System.String to PersistentString
        /// </summary>
        /// <param name="str">.Net string</param>
        /// <returns>PersistentString</returns>
        public static implicit operator PersistentString(string str) 
        {
            return new PersistentString(str);
        }

        /// <summary>
        /// Operator for implicit convertsion from PersistentString to System.String
        /// </summary>
        /// <param name="str">PersistentString</param>
        /// <returns>.Net string</returns>
        public static implicit operator string(PersistentString str) 
        {
            return str.ToString();
        }

        internal PersistentString() {}

        internal string str;
    }
}
