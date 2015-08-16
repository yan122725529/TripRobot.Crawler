using System;
using System.IO;
using Perst;

namespace Perst.FullText
{
    
    /// <summary> Interface for classes which are able to extract text and its language themselves.</summary>
    public interface FullTextSearchable:IPersistent
    {
        /// <summary> Get document text</summary>
        TextReader Text { get; }
        
        /// <summary> Get document language (null if unknown)</summary>
        string Language { get; }
    }
}