using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    public static class SerializationHelper
    {
        public static string SerializeDataXml<TData>(TData data)
        {
            var serializer = new DataContractSerializer(typeof(TData));

            var sb = new StringBuilder();

            using (var xtw = XmlWriter.Create(sb, new XmlWriterSettings {
                Indent = true,
                IndentChars = "  "
            }))
            {
                serializer.WriteObject(xtw, data);
            }

            return sb.ToString();
        }

        public static TData DeserializeDataXml<TData>(string xml)
        {
            var serializer = new DataContractSerializer(typeof(TData));

            using (var sr = new StringReader(xml))
            using (var xr = XmlReader.Create(sr))
            {
                return (TData)serializer.ReadObject(xr);
            }
        }

        public static TData DeserializeDataJson<TData>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(TData));

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (TData)serializer.ReadObject(stream);
            }
        }
    }
}
