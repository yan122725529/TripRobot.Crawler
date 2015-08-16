using System;
using System.IO;
using System.Text;
using System.Management.Automation;
using Perst;

namespace Perst
{
    public class PSObjectSerializer : CustomSerializer
    {

        public void Pack(object obj, ObjectWriter writer)
        {
            PSObject ps = (PSObject)obj;
            foreach (PSPropertyInfo prop in ps.Properties)
            {
                writer.Write(prop.Name);
                writer.WriteObject(prop.Value);
            }
            writer.Write("");
        }

        public object Parse(string str)
        {
            PSObject obj = new PSObject();
            StringBuilder sb = new StringBuilder();
            string name = null;
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                switch (ch)
                {
                    case '\\':
                        sb.Append(str[++i]);
                        break;
                    case '=':
                        name = sb.ToString().Trim();
                        sb = new StringBuilder();
                        break;
                    case ';':
                        if (name == null)
                        {
                            throw new ArgumentException(str);
                        }
                        obj.Properties.Add(new PSNoteProperty(name, sb.Length == 0 ? null : sb.ToString()));
                        sb = new StringBuilder();
                        name = null;
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            if (name != null)
            {
                obj.Properties.Add(new PSNoteProperty(name, sb.Length == 0 ? null : sb.ToString()));
            }
            return obj;
        }

        private static void AppendQuotedString(StringBuilder sb, string val)
        {
            foreach (char ch in val)
            {
                if (ch == '=' || ch == '\\' || ch == ';')
                {
                    sb.Append('\\');
                }
                sb.Append(ch);
            }
        }

        public string Print(object obj)
        {
            PSObject ps = (PSObject)obj;
            StringBuilder sb = new StringBuilder();
            foreach (PSPropertyInfo prop in ps.Properties)
            {
                AppendQuotedString(sb, prop.Name);
                if (prop.Value != null)
                {
                    AppendQuotedString(sb, prop.Value.ToString());
                }
            }
            return sb.ToString();
        }

        public object Unpack(ObjectReader reader)
        {
            throw new NotImplementedException();
        }

        public object Create(Type type)
        {
            return new PSObject();
        }

        public void Unpack(object obj, ObjectReader reader)
        {
            PSObject po = (PSObject)obj;
            string name;
            while ((name = reader.ReadString()).Length != 0) 
            {
                object value = reader.ReadObject();
                po.Properties.Add(new PSNoteProperty(name, value));
            }
        }

        public bool IsApplicable(Type type)
        {
            return typeof(PSObject).IsAssignableFrom(type);
        }

        public bool IsEmbedded(object obj)
        {
            return false;
        }
    }
}
