using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Extensibility;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using SoSP.PnPProvisioningExtensions.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SoSP.PnPProvisioningExtensions.Core
{
    internal class MetadataNavigationHandler : BaseHandler<Dictionary<string, string>>
    {
        private const string CLIENT_MOSS_METADATANAVIGATIONSETTINGS = "client_MOSS_MetadataNavigationSettings";

        public override ProvisioningTemplate Extract(
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
                Type = typeof(MetadataNavigationHandler).FullName
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

                extensibilityHandler.Configuration = SerializationHelper.SerializeDataXml(metadatanavigationSettings);
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

        public override void Provision(
            ClientContext ctx,
            ProvisioningTemplate template,
            ProvisioningTemplateApplyingInformation applyingInformation,
            TokenParser tokenParser,
            PnPMonitoredScope scope,
            string configurationData
            )
        {
            if (string.IsNullOrWhiteSpace(configurationData)) return;

            var metadataNavigationSettings = SerializationHelper.DeserializeDataXml<Dictionary<string,string>>(configurationData);

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