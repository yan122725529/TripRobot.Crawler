namespace Perst
{
    using System;
    using System.Collections;
#if USE_GENERICS
    using System.Collections.Generic;
#endif
	
    /// <summary> 
    /// Generic interface of string field index for regular expression search.     
    /// </summary>
    public interface GenericRegexIndex 
    {
        /// <summary>
        /// Match regular expression
        /// </summary>
        /// <param name="pattern">regular expression with '%' (any substring) and '_' (any char) wildcards</param>        
        /// <returns>enumerable collection</returns>
        ///        
        IEnumerable Match(string pattern);
    }

#if USE_GENERICS
    /// <summary> 
    /// Interface of string field index for regular expression search.     
    /// </summary>
    public interface RegexIndex<T> : GenericRegexIndex, FieldIndex<string,T> where T:class
    {
        /// <summary> 
        /// Locate objects which key matches regular expression
        /// </summary>
        /// <param name="regex">regular expression with % and _ wildcards
        /// </param>
        /// <returns>matched objects
        /// </returns>
        new IEnumerable<T> Match(string regex);
    }
#else
    /// <summary> 
    /// Interface of string field index for regular expression search.     
    /// </summary>
    public interface RegexIndex : GenericRegexIndex, FieldIndex {}
#endif
}