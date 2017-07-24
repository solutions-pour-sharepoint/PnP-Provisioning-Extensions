using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Extensibility;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace SoSP.PnPProvisioningExtensions.Core
{
    internal class MetadatanavigationProvider : IProvisioningExtensibilityHandler
    {
        private const string CLIENT_MOSS_METADATANAVIGATIONSETTINGS = "client_MOSS_MetadataNavigationSettings";

        public ProvisioningTemplate Extract(
            ClientContext ctx,
            ProvisioningTemplate template,
            ProvisioningTemplateCreationInformation creationInformation,
            PnPMonitoredScope scope,
            string configurationData
            )
        {
            var extensibilityHandler = new ExtensibilityHandler
            {
                Assembly = Assembly.GetExecutingAssembly().FullName,
                Enabled = true,
                Type = typeof(MetadatanavigationProvider).FullName
            };

            if (template.Lists?.Count > 0)
            {
                var web = ctx.Web;
                var allLists = web.Lists;
                var metadatanavigationSettings = new Dictionary<string, string>();
                ctx.Load(
                    allLists,
                    lists => lists.Include(
                        l => l.Title,
                        l => l.RootFolder.Properties                       
                        )
                    );
                ctx.ExecuteQuery();

                foreach (var list in allLists)
                {
                    if (list.PropertyBagContainsKey(CLIENT_MOSS_METADATANAVIGATIONSETTINGS))
                    {
                        metadatanavigationSettings.Add(list.Title, list.GetPropertyBagValueString(CLIENT_MOSS_METADATANAVIGATIONSETTINGS, null));
                    }
                }

                var serializer = new DataContractSerializer(typeof(Dictionary<string, string>));

                var sb = new StringBuilder();

                using (var xtw = XmlWriter.Create(sb))
                {
                    serializer.WriteObject(xtw, metadatanavigationSettings);
                }
                extensibilityHandler.Configuration = sb.ToString();
                template.ExtensibilityHandlers.Add(extensibilityHandler);
            }

            return template;
        }

        public IEnumerable<TokenDefinition> GetTokens(ClientContext ctx, ProvisioningTemplate template, string configurationData)
        {
            yield break;
        }

        public void Provision(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation, TokenParser tokenParser, PnPMonitoredScope scope, string configurationData)
        {
        }
    }
}