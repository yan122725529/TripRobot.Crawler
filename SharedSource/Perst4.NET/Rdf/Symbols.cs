namespace Rdf
{
	/// <summary>
	/// Predefined names of type and properties
	/// </summary>
	public class Symbols
	{
        public const string RDFS="http://www.w3.org/2000/01/rdf-schema#";
        public const string RDF="http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public const string NS="http://www.perst.org#";

        public const string Metatype=NS+"Metaclass";
        public const string Type=RDFS+"Class";
        public const string Subtype=RDFS+"subClassOf";
        public const string Thing=NS+"thing";
        public const string Rectangle=NS+"rectangle";
        public const string Point=NS+"point";
        public const string Keyword=NS+"keyword";
        public const string Uri=RDF+"about";
        public const string Ref=RDF+"resource";
        public const string Timestamp=NS+"timestamp";
    }
}
