using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Extensibility;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using SoSP.PnPProvisioningExtensions.Core.Utilities;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public class ListContentHandler : IProvisioningExtensibilityHandler
    {
        public class Data : Collection<ListConfig>
        {
        }

        public class ListConfig
        {
            public string ListName { get; set; }
            public string KeyFieldName { get; set; }
            public List<string> FieldsToExport { get; set; } = new List<string>();
            public UpdateBehavior UpdateBehavior { get; internal set; }
        }

        public ProvisioningTemplate Extract(
            ClientContext ctx,
            ProvisioningTemplate template,
            ProvisioningTemplateCreationInformation creationInformation,
            PnPMonitoredScope scope,
            string configurationData
            )
        {
            var data = new Data
            {
                new ListConfig
                {
                    ListName = "Nature",
                    KeyFieldName = "Title",
                    FieldsToExport = { "Title", "Data1","Data2","Classement" }
                }
            };
            if (creationInformation.HandlersToProcess.HasFlag(Handlers.Lists) && template.Lists?.Count > 0)
            {
                var listsToExportContent = (from templateList in template.Lists
                                            from dataList in data
                                            where templateList.Title == dataList.ListName
                                            from siteList in GetSiteLists(ctx)
                                            where templateList.Title == siteList.Title
                                            select new
                                            {
                                                TemplateList = templateList,
                                                Config = dataList,
                                                SiteList = siteList
                                            }).ToArray();

                for (int i = 0; i < listsToExportContent.Length; i++)
                {
                    var processingItem = listsToExportContent[i];
                    creationInformation.ProgressDelegate?.Invoke(
                        $"Exporting content of list {processingItem.SiteList.Title}",
                        i,
                        listsToExportContent.Length
                        );

                    ExportData(processingItem.TemplateList, processingItem.SiteList, processingItem.Config);
                }
            }

            return template;
        }

        private static void ExportData(ListInstance templateList, List siteList, ListConfig config)
        {
            if (!config.FieldsToExport.Contains(config.KeyFieldName))
            {
                config.FieldsToExport.Add(config.KeyFieldName);
            }
            var query = new CamlQuery
            {
                ViewXml = string.Concat(
                        "<View>",
                        "<ViewFields>",
                        string.Concat(config.FieldsToExport.Select(f => $"<FieldRef Name='{f}' />")),
                        "</ViewFields>",
                        "</View>"
                        )
            };

            templateList.DataRows.UpdateBehavior = config.UpdateBehavior;
            templateList.DataRows.KeyColumn = config.KeyFieldName;
            foreach(var item in QueryHelper.GetItems(siteList, query))
            {
                var templateItem = new DataRow();

                foreach (var fieldToExport in config.FieldsToExport)
                {
                    if (item.FieldValues.ContainsKey(fieldToExport))
                    {
                        templateItem.Values.Add(fieldToExport, Convert.ToString(item[fieldToExport]));
                    }
                    else
                    {
                        // TODO: log Warning
                    }
                }

                templateList.DataRows.Add(templateItem);
            }            
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

        public IEnumerable<TokenDefinition> GetTokens(ClientContext ctx, ProvisioningTemplate template, string configurationData)
        {
            yield break;
        }

        public void Provision(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation, TokenParser tokenParser, PnPMonitoredScope scope, string configurationData)
        {
            // Nothing to do
        }
    }
}