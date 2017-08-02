using OfficeDevPnP.Core.Framework.Provisioning.Model;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public abstract class BaseHandler<TData> 
    {
        protected string SerializeData(TData data)
        {
            var serializer = new DataContractSerializer(typeof(TData));

            var sb = new StringBuilder();

            using (var xtw = XmlWriter.Create(sb))
            {
                serializer.WriteObject(xtw, data);
            }

            return sb.ToString();
        }

        protected ExtensibilityHandler GetExtensibilityHandler(string data = null)
        {
            var actualType = this.GetType();
            return new ExtensibilityHandler
            {
                Assembly = actualType.Assembly.FullName,
                Enabled = true,
                Type = actualType.FullName,
                Configuration = data
            };
        }

        protected ExtensibilityHandler GetExtensibilityHandler(TData data = default(TData))
        {
            return GetExtensibilityHandler(SerializeData(data));
        }
    }
}