using System;
using Perst;

namespace Rdf
{
    /// <summary>Property definition</summary>
    public class PropDef:Persistent 
    {
        /// <summary>Name of the property</summary>
        public string name;
    }    

    /// <summary>Object property</summary>
    public struct PropVal
    { 
        /// <summary>Reference to property defintion (name of the property)</summary>    
        public PropDef def;
        /// <summary>property value</summary>
        public object  val;

        /// <summary>
        /// Property value constructor
        /// </summary>
        /// <param name="def"></param>
        /// <param name="val"></param>
        public PropVal(PropDef def, object val) 
        {
            this.def = def;
            this.val = val;
        }
    }

    /// <summary>Name:value pair used to specify property value</summary>
    public struct NameVal 
    {
        /// <summary>
        /// Name of the property
        /// </summary>
        public string name;

        /// <summary>
        /// Value of the property (may be null or pattern)
        /// </summary>
        public object val;

        /// <summary>
        /// Constructor of name:valur pair
        /// </summary>
        /// <param name="name">name of the property</param>
        /// <param name="val">value of the property</param>
        public NameVal(string name, object val) 
        { 
            this.name = name;
            this.val = val;
        }
    }

    /// <summary>
    /// Class used to represent range of property values for search queries
    /// </summary>
    public class Range 
    {
        /// <summary>
        /// Low boundary
        /// </summary>
        public readonly object from;
        /// <summary>
        /// Whether low boundary is inclusive or exclusive
        /// </summary>
        public readonly bool   fromInclusive;
        /// <summary>
        /// High boundary
        /// </summary>
        public readonly object till;
        /// <summary>
        /// Whether high boundary is inclusive or exclusive
        /// </summary>
        public readonly bool   tillInclusive;

        /// <summary>
        /// Range constructor 
        /// </summary>
        /// <param name="from">low boundary</param>
        /// <param name="fromInclusive">is low boundary inclusive or exclusive</param>
        /// <param name="till">high boundary</param>
        /// <param name="tillInclusive">is high boundary inclusive or exclusive</param>
        public Range(object from, bool fromInclusive, object till, bool tillInclusive) 
        {
            this.from = from;
            this.fromInclusive = fromInclusive;
            this.till = till;
            this.tillInclusive = tillInclusive;
        }
    }
}
