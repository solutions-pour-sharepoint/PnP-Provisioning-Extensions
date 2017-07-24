using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SoSP.PnPProvisioningExtensions.Core.Extensions
{
    public static class SerializationExtensions
    {
        public static string ToXml<T>(this T obj)
        {
            var serializer = new DataContractSerializer(typeof(T));

            var sb = new StringBuilder();

            using (var xtw = XmlWriter.Create(sb))
            {
                serializer.WriteObject(xtw, obj);
            }

            return sb.ToString();
        }

        public static T FromXml<T>(this string xml)
        {
            var serializer = new DataContractSerializer(typeof(T));

            using (var sr = new StringReader(xml))
            using (var xr = XmlReader.Create(sr))
            {
                return (T)serializer.ReadObject(xr);
            }
        }

    }
}
