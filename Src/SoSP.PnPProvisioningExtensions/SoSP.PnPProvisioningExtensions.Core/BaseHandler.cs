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

        protected static string Tokenize(string input, ClientContext context)
        {
            var web = context.Web;
            var fields = web.Fields;
            var lists = web.Lists;

            context.Load(web, w => w.Id, w => w.ServerRelativeUrl);
            context.Load(
                fields,
                col => col.Include(f => f.InternalName, f => f.Id)
                );
            context.Load(
                lists,
                col => col.Include(
                    l => l.Title,
                    l => l.Id,
                    l => l.Views.Include(
                        v => v.Title,
                        v => v.Id
                    )));
            context.ExecuteQueryRetry();

            foreach (var list in lists)
            {
                input = input.ReplaceCaseInsensitive(list.Id.ToString(), "{listid:" + Regex.Escape(list.Title) + "}");
                foreach (var view in list.Views)
                {
                    input = input.ReplaceCaseInsensitive(view.Id.ToString(), "{viewid:" + Regex.Escape(view.Title) + "}");

                }
            }
            foreach (var field in fields)
            {
                input = input.ReplaceCaseInsensitive(field.Id.ToString(), "{fieldtitle:" + field.Id + "}");
            }
            input = input.ReplaceCaseInsensitive(web.ServerRelativeUrl, "{siteurl}");
            input = input.ReplaceCaseInsensitive(web.Id.ToString(), "{siteid}");

            return input;
        }

        public abstract void Provision(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation, TokenParser tokenParser, PnPMonitoredScope scope, string configurationData);
        public abstract ProvisioningTemplate Extract(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInformation, PnPMonitoredScope scope, string configurationData);

        public virtual IEnumerable<TokenDefinition> GetTokens(ClientContext ctx, ProvisioningTemplate template, string configurationData)
        {
            yield break;
        }
    }
}