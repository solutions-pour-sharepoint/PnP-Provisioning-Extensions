using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Entities;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using SoSP.PnPProvisioningExtensions.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using SPContentType = Microsoft.SharePoint.Client.ContentType;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public class DocumentSetHomePageHandler : BaseHandler<DocumentSetHomePageHandler.Data>
    {
        #region Serializable datatype (to have a more readable xml)

        [CollectionDataContract(Name = nameof(DocumentSetHomePageHandler) + "Data", KeyName = "ContentTypeId", ValueName = "WebParts", ItemName = "DocumentSetHomePage")]
        public class Data : Dictionary<string, WebPartDefinitionsList>
        {
        }

        [CollectionDataContract(Name = "WebPartDefinitionsList", ItemName = "WebPart")]
        public class WebPartDefinitionsList : List<WebPart>
        {
            public WebPartDefinitionsList(IEnumerable<WebPart> collection) : base(collection)
            {
            }

            public WebPartDefinitionsList() : base()
            {
            }
        }

        #endregion Serializable datatype (to have a more readable xml)

        public override ProvisioningTemplate Extract(
            ClientContext ctx,
            ProvisioningTemplate template,
            ProvisioningTemplateCreationInformation creationInformation,
            PnPMonitoredScope scope,
            string configurationData
            )
        {
            try
            {
                // Loop through exported ContentTypes

                if (template.ContentTypes.Count > 0)
                {
                    var web = ctx.Web;
                    var allContentTypes = web.ContentTypes;

                    web.EnsureProperty(w => w.ServerRelativeUrl);
                    ctx.Load(
                        allContentTypes,
                        col => col.Include(
                            ct => ct.Id,
                            ct => ct.SchemaXml
                            )
                        );
                    ctx.ExecuteQuery();

                    var data = new Data();
                    var tokenizer = new Tokenizer(ctx);

                    foreach (var spct in allContentTypes)
                    {
                        if (
                            spct.Id.StringValue.StartsWith(BuiltInContentTypeId.DocumentSet, StringComparison.Ordinal)
                            && template.ContentTypes.Any(tct => tct.Id == spct.Id.StringValue))
                        {
                            var docsetWelcomePage = GetWelcomePage(web, spct);

                            var dshpwp = web.GetWebParts(docsetWelcomePage);

                            var wpData = dshpwp.Select(spWp => new WebPart
                            {
                                Contents = tokenizer.Tokenize(web.GetWebPartXml(spWp.Id, docsetWelcomePage)),
                                Zone = spWp.EnsureProperty(wp => wp.ZoneId),
                                Order = (uint)spWp.WebPart.ZoneIndex,
                                Title = spWp.WebPart.Title
                            });

                            data.Add(
                                spct.Id.StringValue,
                                new WebPartDefinitionsList(wpData)
                            );
                        }
                    }

                    template.ExtensibilityHandlers.Add(GetExtensibilityHandler(data));
                }

                return template;
            }
            catch (Exception)
            {
                throw;
            }
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

            var data = SerializationHelper.DeserializeDataXml<Data>(configurationData);

            if (data.Count > 0)
            {
                var web = ctx.Web;
                var allContentTypes = web.ContentTypes;

                web.EnsureProperty(w => w.ServerRelativeUrl);
                ctx.Load(
                    allContentTypes,
                    col => col.Include(
                        ct => ct.Id,
                        ct => ct.Name,
                        ct => ct.SchemaXml
                        )
                    );
                ctx.ExecuteQuery();

                foreach (var ct in data)
                {
                    var spct = allContentTypes.FirstOrDefault(c => c.Id.StringValue == ct.Key);
                    if (spct != null)
                    {
                        scope.LogInfo($"Start provisioning webparts in the homepage of the document set content type {spct.Name}");
                        var ctHp = GetWelcomePage(web, spct);
                        var siteRelativeUrl = ctHp.Substring(web.ServerRelativeUrl.Length).TrimStart('/');

                        var existingWebParts = web.GetWebParts(ctHp).ToList();

                        // CLear the existing web parts
                        foreach (var existing in existingWebParts)
                        {
                            existing.DeleteWebPart();
                        }
                        ctx.ExecuteQueryRetry();

                        foreach (var wp in ct.Value)
                        {
                            scope.LogInfo($"  Start provisioning webpart {wp.Title} in the homepage of the document set content type {spct.Name}");
                            string webPartXml = tokenParser.ParseString(wp.Contents);
                            webPartXml = WebPartUtilities.EnsureXsltListviewWebPartView(ctx, webPartXml, tokenParser);

                            var wpEntity = new WebPartEntity
                            {
                                WebPartIndex = (int)wp.Order,
                                WebPartTitle = tokenParser.ParseString(wp.Title),
                                WebPartXml = webPartXml,
                                WebPartZone = wp.Zone
                            };

                            if (!existingWebParts.Any(w => w.WebPart.Title == wpEntity.WebPartTitle))
                            {
                                scope.LogInfo($"Provisioning webpart {wp.Title}");
                                web.AddWebPartToWebPartPage(
                                    wpEntity,
                                    siteRelativeUrl
                                    );
                            }
                            else
                            {
                                scope.LogInfo($"Ignoring webpart {wp.Title}  because there's already one with this title.");
                            }
                        }

                        scope.LogInfo($"End provisioning webparts in the homepage of the document set content type {spct.Name}");
                    }
                    else
                    {
                        // TODO: log
                    }
                }
            }
        }

        private static string GetWelcomePage(Web web, SPContentType ct)
        {
            var pagename = ct.Id.StringValue.StartsWith(VideoSet, StringComparison.Ordinal) ? "videoplayerpage.aspx" : "docsethomepage.aspx";
            var schema = XElement.Parse(ct.SchemaXml);
            var targetFolder = (string)schema.Element("Folder").Attribute("TargetName");
            return $"{web.ServerRelativeUrl}/{targetFolder}/{pagename}";
        }

        public const string VideoSet = "0x0120D520A808";
    }
}