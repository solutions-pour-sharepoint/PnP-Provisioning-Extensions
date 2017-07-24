using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Extensibility;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using SoSP.PnPProvisioningExtensions.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
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
                var metadatanavigationSettings = new Dictionary<string, string>();
                var allLists = GetSiteLists(ctx);

                foreach (var list in allLists)
                {
                    if (list.PropertyBagContainsKey(CLIENT_MOSS_METADATANAVIGATIONSETTINGS))
                    {
                        scope.LogInfo("Exporting MetadataNavigationSettings from list " + list.Title);
                        metadatanavigationSettings.Add(list.Title, list.GetPropertyBagValueString(CLIENT_MOSS_METADATANAVIGATIONSETTINGS, null));
                    }
                }

                extensibilityHandler.Configuration = SerializeData(metadatanavigationSettings);
                template.ExtensibilityHandlers.Add(extensibilityHandler);
            }

            return template;
        }

        private static ListCollection GetSiteLists(ClientContext ctx)
        {
            var web = ctx.Web;
            var allLists = web.Lists;
            ctx.Load(
                allLists,
                lists => lists.Include(
                    l => l.Title,
                    l => l.RootFolder.Properties
                    )
                );
            ctx.ExecuteQuery();
            return allLists;
        }

        private static string SerializeData(Dictionary<string, string> metadatanavigationSettings)
        {
            var serializer = new DataContractSerializer(typeof(Dictionary<string, string>));

            var sb = new StringBuilder();

            using (var xtw = XmlWriter.Create(sb))
            {
                serializer.WriteObject(xtw, metadatanavigationSettings);
            }

            return sb.ToString();
        }

        public IEnumerable<TokenDefinition> GetTokens(ClientContext ctx, ProvisioningTemplate template, string configurationData)
        {
            yield break;
        }

        public void Provision(
            ClientContext ctx,
            ProvisioningTemplate template,
            ProvisioningTemplateApplyingInformation applyingInformation,
            TokenParser tokenParser,
            PnPMonitoredScope scope,
            string configurationData
            )
        {
            if (string.IsNullOrWhiteSpace(configurationData)) return;

            var metadataNavigationSettings = configurationData.FromXml<Dictionary<string, string>>();

            if (metadataNavigationSettings.Count > 0)
            {
                var allLists = GetSiteLists(ctx);
                foreach (var listName in metadataNavigationSettings.Keys)
                {
                    var propertyValue = tokenParser.ParseString(metadataNavigationSettings[listName]);
                    var list = allLists.FirstOrDefault(l => l.Title == listName);
                    list.SetPropertyBagValue(CLIENT_MOSS_METADATANAVIGATIONSETTINGS, propertyValue);
                    list.Update();
                    scope.LogInfo("Imported MetadataNavigationSettings to list " + list.Title);

                }
                ctx.ExecuteQuery();
            }
        }
    }
}