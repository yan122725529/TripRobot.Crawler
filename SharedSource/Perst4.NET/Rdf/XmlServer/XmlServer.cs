using System;
using System.Xml;
using System.IO;
using System.Collections;

namespace Rdf
{
    /// <summary>
    /// Parse XML requests and store/fetch data in store database
    /// </summary>
    public class XmlServer
    {
        VersionedStorage store;
        TextWriter       writer;

        public XmlServer(VersionedStorage store, TextWriter writer) 
        {
            this.store = store;
            this.writer = writer;
        }
        
        public Thing ImportObject(XmlReader reader, String parentUri)
        {
            String localName = reader.LocalName;
            String name = reader.NamespaceURI + localName;
            String uri = null;
            bool IsEmptyElement = reader.IsEmptyElement;

            ArrayList props = new ArrayList();

            while (reader.MoveToNextAttribute())
            {
                String attrName = reader.NamespaceURI + reader.LocalName;
                if (XmlSymbols.Uri == attrName) 
                {
                    uri = reader.Value;
                } 
                else 
                { 
                    props.Add(CreateProperty(attrName, reader.Value));
                }
            }

            if (uri == null) 
            { 
                uri = parentUri + '/' + localName;
            }

            if (!IsEmptyElement) 
            {
                while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element) 
                    {
                        props.Add(ImportProperty(reader, uri));
                    }
                }
            }
            return store.CreateObject(uri, name, (NameVal[])props.ToArray(typeof(NameVal)));
        }
            
        public NameVal ImportProperty(XmlReader reader, String parentUri)
        {    
            String localName = reader.LocalName;
            String name = reader.NamespaceURI + localName;
            object val = null;
            if (reader.HasAttributes) 
            {
                String uri = reader.GetAttribute("resource", XmlSymbols.RDF);
                if (uri == null) 
                { 
                    val = ImportObject(reader, parentUri).vh;
                } 
                else 
                {             
                    val = store.GetObject(uri);
                    if (val == null) 
                    { 
                        throw new XmlException("Object with URI '" + uri + "' is not found");
                    }
                    if (!reader.IsEmptyElement)  
                    {
                        while (reader.Read() && reader.NodeType != XmlNodeType.EndElement);
                    }
                }
            } 
            else 
            {
                if (reader.IsEmptyElement) 
                {
                    throw new XmlException("Property '" + name + "' value is not defined");
                }
                while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                        {
                            ArrayList props = new ArrayList();
                            String uri = parentUri + '/' + localName;
                            do 
                            { 
                                if (reader.NodeType == XmlNodeType.Element) 
                                {
                                    props.Add(ImportProperty(reader, uri));
                                }
                            } while (reader.Read() && reader.NodeType != XmlNodeType.EndElement);
                            Thing thing = store.CreateObject(uri, name, (NameVal[])props.ToArray(typeof(NameVal)));
                            return new NameVal(name, thing.vh);
                        }
                        case XmlNodeType.Text:
                            val = ConvertValue(reader.Value);
                            continue;
                    }
                }
            }
            return new NameVal(name, val);
        }
                 
        public void ReadThingPattern(XmlReader reader)
        {
            SearchKind kind = SearchKind.LatestVersion;
            DateTime timestamp = DateTime.Now;
            String name = reader.NamespaceURI + reader.LocalName;
            String uri = null;
            String type = XmlSymbols.Thing.Equals(name) ? null : name;
            ArrayList props = new ArrayList();
            bool IsEmptyElement = reader.IsEmptyElement;
            int depth = 0;

            while(reader.MoveToNextAttribute())
            {
                string attrName = reader.NamespaceURI + reader.LocalName;
                string attrVal = reader.Value;
                switch (attrName) 
                { 
                    case XmlSymbols.Uri:
                        uri = reader.Value;
                        break;
                    case XmlSymbols.Before:
                        timestamp = DateTime.Parse(attrVal);
                        kind = SearchKind.LatestBefore;
                        break;
                    case XmlSymbols.After:
                        timestamp = DateTime.Parse(attrVal);
                        kind = SearchKind.OldestAfter;
                        break;
                    case XmlSymbols.All:
                        kind = bool.Parse(attrVal) ? SearchKind.AllVersions : SearchKind.LatestVersion;
                        break;
                    case XmlSymbols.Depth:
                        depth = int.Parse(attrVal);
                        break;
                    default:
                        props.Add(new NameVal(attrName, ConvertPattern(attrVal)));
                        break;
                }
            }

            if (!IsEmptyElement) 
            {
                while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element) 
                    {
                        props.Add(ReadPropertyPatterns(reader));
                    }
                }
            }
            foreach (Thing thing in store.Search(type, uri, (NameVal[])props.ToArray(typeof(NameVal)), kind, timestamp)) 
            {
                DumpObject(thing, writer, 1, kind, timestamp, depth);
            }
        }

        static string GetQualifiedName(string uri, TextWriter writer)
        {
            int col = uri.LastIndexOf(':');
            int hash = uri.LastIndexOf('#');
            int path = uri.LastIndexOf('/');
            int namespaceLen = Math.Max(Math.Max(col, hash), path);
                 
            writer.Write('<');
            if (namespaceLen > 0) 
            { 
                string prefix = uri.Substring(0, namespaceLen+1);
                string name = uri.Substring(namespaceLen+1);
                switch (prefix) 
                { 
                    case Symbols.RDF:
                        name = "rdf:" + name;
                        break;
                    case Symbols.RDFS:
                        name = "rdfs:" + name;
                        break;
                    case Symbols.NS:
                        name = "vr:" + name;
                        break;
                    default:
                        writer.Write(name + " xmlns=\"" + prefix + "\"");
                        return name;
                }
                uri = name;
            } 
            writer.Write(uri);
            return uri;
        }

        static void DumpObject(Thing thing, TextWriter writer, int indent, SearchKind kind, DateTime timestamp, int depth) 
        {
            WriteTab(writer, indent);
            string typeName = GetQualifiedName(thing.type.vh.uri, writer);  
            writer.WriteLine(" rdf:about=\"" + thing.vh.uri + "\" vr:timestamp=\"" + thing.timestamp + "\">");
            foreach (PropVal pv in thing.props) 
            {
                object val = pv.val;
                if (val is VersionHistory) 
                {
                    VersionHistory ptr = (VersionHistory)val;
                    if (kind != SearchKind.AllVersions) 
                    {
                        if (depth > 0 || ptr.uri.StartsWith(thing.vh.uri))
                        { 
                            Thing t = ptr.GetVersion(kind, timestamp);
                            if (t != null) 
                            {
                                DumpObject(t, writer, indent+1, kind, timestamp, depth-1);
                                continue;
                            }
                        }
                    }
                    WriteTab(writer, indent+1);
                    GetQualifiedName(pv.def.name, writer);
                    writer.WriteLine(" rdf:resource=\"" + ptr.uri + "\"/>");
                } 
                else 
                { 
                    WriteTab(writer, indent+1);
                    string propName = GetQualifiedName(pv.def.name, writer);
                    writer.WriteLine(">" + val + "</" + propName + ">");
                }
            }
            WriteTab(writer, indent);
            writer.WriteLine("</" + typeName + ">");
        }

        static void WriteTab(TextWriter writer, int ident) 
        { 
            while (--ident >= 0) writer.Write('\t');
        }
            
        public NameVal ReadPropertyPatterns(XmlReader reader)
        {    
            String name = reader.NamespaceURI + reader.LocalName;
            object val = null; 
            ArrayList props = new ArrayList();
            bool IsEmptyElement = reader.IsEmptyElement;

            while (reader.MoveToNextAttribute())
            {
                props.Add(new NameVal(reader.NamespaceURI + reader.LocalName, 
                                      ConvertPattern(reader.Value)));
            }

            if (!IsEmptyElement) 
            {
                while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            props.Add(ReadPropertyPatterns(reader));
                            continue;
                        case XmlNodeType.Text:
                            val = ConvertPattern(reader.Value);
                            continue;
                    }
                }
            }
            return new NameVal(name, val != null ? val : props.ToArray(typeof(NameVal)));
        }

        private NameVal CreateProperty(string name, string val)
        {
            if (name.Equals(XmlSymbols.Subtype)) 
            {
                VersionHistory vh = store.GetObject(val);
                if (vh == null) 
                { 
                    throw new XmlException("Object with URI '" + val + "' is not found");
                }
                return new NameVal(name, vh);
            } 
            else 
            {         
                return new NameVal(name, ConvertValue(val));
            }
        }

        private static object ConvertValue(string str) 
        {
            if (str.Length > 0) 
            {
                char ch = str[0];
                if (ch == '+' || ch == '-' || (ch >= '0' && ch <= '9'))
                {
                    try 
                    { 
                        return double.Parse(str, System.Globalization.NumberFormatInfo.InvariantInfo);
                    } 
                    catch (Exception) {}
                    try 
                    { 
                        return DateTime.Parse(str);
                    } 
                    catch (Exception) {}
                }
            }
            return str;
        }
    
        private static object ConvertPattern(string str) 
        {
            int comma;
            int len = str.Length;
            if (len > 3 && (str[0] == '[' || str[0] == '(') && (str[len-1] == ']' || str[len-1] == ')') && (comma = str.IndexOf(',', 1)) > 0)
            {
                string from = str.Substring(1, comma-1).Trim();
                object fromVal = ConvertValue(from);
                bool fromInclusive = str[0] == '[';
                string till = str.Substring(comma+1, len-comma-2).Trim();
                object tillVal = ConvertValue(till);
                bool tillInclusive = str[len-1] == ']';
                if (fromVal.GetType() != tillVal.GetType()) 
                {
                    fromVal = from;
                    tillVal = till;
                }
                return new Range(fromVal, fromInclusive, tillVal, tillInclusive);
            }
            return ConvertValue(str);
        }
   
        private void StoreObjects(XmlReader reader) 
        {
            store.BeginTransaction();
            try 
            { 
                while (reader.Read())
                {
                    switch (reader.NodeType) 
                    {
                        case XmlNodeType.Element: 
                            ImportObject(reader, XmlSymbols.NS + Guid.NewGuid().ToString());
                            break;
                        case XmlNodeType.EndElement: 
                            store.Commit();
                            return;
                    }
                }
            }
            catch (Exception x) {
                Console.WriteLine(x.StackTrace);
            }
            store.Rollback();
        }
        
        private void FindObjects(XmlReader reader)
        {
            writer.WriteLine("<?xml version='1.0'?>");
            writer.WriteLine("<vr:result xmlns:vr=\"http://www.perst.org#\" xmlns:rdfs=\"http://www.w3.org/2000/01/rdf-schema#\" xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">");
            while (reader.Read())
            {
                switch (reader.NodeType) 
                {
                    case XmlNodeType.Element: 
                        ReadThingPattern(reader);
                        break;
                    case XmlNodeType.EndElement: 
                        writer.WriteLine("</vr:result>");
                        return;
                }
            }
        }
            
        public void Parse(string xmlFilePath) 
        {
            XmlTextReader reader = new XmlTextReader(new FileStream(xmlFilePath, FileMode.Open));
            try {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element) 
                    {
                        String name = reader.NamespaceURI + reader.LocalName;
                        switch (name) 
                        {
                            case XmlSymbols.Store:
                                StoreObjects(reader);
                                break;
                            case  XmlSymbols.Find:
                                FindObjects(reader);
                                break;
                            default:
                                Console.WriteLine("Unknown operation " + name);
                                return;
                        }                             
                    }
                }
            }
            finally 
            {
                reader.Close();
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length < 2) 
            { 
                Console.WriteLine("Usage: ImportXML database-file xml-file {xml-file}");
                return;
            }
            VersionedStorage store = new VersionedStorage();
            store.Open(args[0]);
            XmlServer server = new XmlServer(store, Console.Out);
            for (int i = 1; i < args.Length; i++) 
            { 
                server.Parse(args[i]);
            }
            store.Close();
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }
    }
}
