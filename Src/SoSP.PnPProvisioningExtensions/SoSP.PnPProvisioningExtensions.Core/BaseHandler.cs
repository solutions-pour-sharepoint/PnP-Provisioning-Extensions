using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Framework.Provisioning.Extensibility;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using System.Collections.Generic;
using OfficeDevPnP.Core.Extensions;
using SoSP.PnPProvisioningExtensions.Core.Utilities;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public abstract class BaseHandler<TData> : IProvisioningExtensibilityHandler
    {
        protected static string SerializeData(TData data)
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

        protected static TData ParseData(string xml)
        {
            var serializer = new DataContractSerializer(typeof(TData));

            using (var sr = new StringReader(xml))
            using (var xr = XmlReader.Create(sr))
            {
                return (TData)serializer.ReadObject(xr);
            }
        }

        public abstract void Provision(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation, TokenParser tokenParser, PnPMonitoredScope scope, string configurationData);
        public abstract ProvisioningTemplate Extract(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInformation, PnPMonitoredScope scope, string configurationData);

        public virtual IEnumerable<TokenDefinition> GetTokens(ClientContext ctx, ProvisioningTemplate template, string configurationData)
        {
            yield break;
        }
    }
}