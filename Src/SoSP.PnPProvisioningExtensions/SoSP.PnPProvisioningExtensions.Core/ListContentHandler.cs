using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Extensibility;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using SoSP.PnPProvisioningExtensions.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public class ListContentHandler : IProvisioningExtensibilityHandler
    {
        [CollectionDataContract(ItemName = nameof(ListConfig))]
        public class Data : Collection<ListConfig>
        {
            public string ToXml()
            {
                return SerializationHelper.SerializeDataXml(this);
            }
        }

        [DataContract]
        public class ListConfig
        {
            [DataMember(IsRequired = true)]
            public string ListName { get; set; }

            [DataMember(IsRequired = true)]
            public string KeyFieldName { get; set; }

            [DataMember]
            public List<string> FieldsToExport { get; set; } = new List<string>();

            [DataMember]
            public UpdateBehavior UpdateBehavior { get; set; }
        }

        public ProvisioningTemplate Extract(
            ClientContext ctx,
            ProvisioningTemplate template,
            ProvisioningTemplateCreationInformation creationInformation,
            PnPMonitoredScope scope,
            string configurationData
            )
        {
            if (string.IsNullOrWhiteSpace(configurationData)) { return template; }

            var data = SerializationHelper.DeserializeDataJson<Data>(configurationData);

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

                var tokenizer = new Tokenizer(ctx);

                for (int i = 0; i < listsToExportContent.Length; i++)
                {
                    var processingItem = listsToExportContent[i];
                    creationInformation.ProgressDelegate?.Invoke(
                        $"Exporting content of list {processingItem.SiteList.Title}",
                        i,
                        listsToExportContent.Length
                        );

                    ExportData(processingItem.TemplateList, processingItem.SiteList, processingItem.Config, tokenizer);
                }
            }

            return template;
        }

        private static void ExportData(ListInstance templateList, List siteList, ListConfig config, Tokenizer tokenizer)
        {
            if (config.FieldsToExport != null && !config.FieldsToExport.Contains(config.KeyFieldName))
            {
                config.FieldsToExport.Add(config.KeyFieldName);
            }

            templateList.DataRows.UpdateBehavior = config.UpdateBehavior;
            templateList.DataRows.KeyColumn = config.KeyFieldName;

            foreach (var item in QueryHelper.GetItemsAllFields(siteList))
            {
                var values = item.ToDictionary(i => i.Key, i => tokenizer.Tokenize(i.Value));

                var templateItem = new DataRow(values);

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
                    l => l.RootFolder.Properties,
                    l => l.Fields.Include(
                        f => f.ReadOnlyField,
                        f => f.InternalName,
                        f => f.Id,
                        f => f.Hidden,
                        f => f.FieldTypeKind
                        )
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