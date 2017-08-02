using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Extensibility;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public class DocumentSetHomePageHandler : BaseHandler<DocumentSetHomePageHandler.Data>, IProvisioningExtensibilityHandler
    {
        #region Serializable datatype (to have a more readable xml)
        [CollectionDataContract(Name = nameof(DocumentSetHomePageHandler) + "Data", KeyName = "ContentTypeId", ValueName = "WebParts", ItemName = "DocumentSetHomePage")]
        public class Data : Dictionary<ContentTypeId, WebPartDefinitionsList>
        {
        }

        [CollectionDataContract(Name = "WebPartDefinitionsList",ItemName = "WebPart")]
        public class WebPartDefinitionsList : List<WebPart>
        {
            public WebPartDefinitionsList(IEnumerable<WebPart> collection) : base(collection) { }
            public WebPartDefinitionsList() : base() { }
        }

        #endregion

        public ProvisioningTemplate Extract(
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

                    foreach (var ct in allContentTypes)
                    {
                        if (template.ContentTypes.Any(tct => tct.Id == ct.Id.StringValue))
                        {
                            var docsetWelcomePage = GetWelcomePage(web, ct);

                            var dshpwp = web.GetWebParts(docsetWelcomePage);

                            var wpData = dshpwp.Select(spWp => new WebPart
                            {
                                Contents = web.GetWebPartXml(spWp.Id, docsetWelcomePage),
                                Zone = spWp.EnsureProperty(wp => wp.ZoneId)
                            });

                            data.Add(
                                ct.Id,
                                new WebPartDefinitionsList(wpData)
                            );
                        }
                    }

                    template.ExtensibilityHandlers.Add(GetExtensibilityHandler(data));
                }

                return template;
            }
            catch (Exception exc)
            {
                throw;
            }
        }

        public IEnumerable<TokenDefinition> GetTokens(ClientContext ctx, ProvisioningTemplate template, string configurationData)
        {
            yield break;
        }

        public void Provision(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation, TokenParser tokenParser, PnPMonitoredScope scope, string configurationData)
        {
        }

        private static string GetWelcomePage(Web web, Microsoft.SharePoint.Client.ContentType ct)
        {
            var pagename = ct.Id.StringValue.StartsWith(VideoSet, StringComparison.Ordinal) ? "videoplayerpage.aspx" : "docsethomepage.aspx";
            var schema = XElement.Parse(ct.SchemaXml);
            var targetFolder = (string)schema.Element("Folder").Attribute("TargetName");
            return $"{web.ServerRelativeUrl}/{targetFolder}/{pagename}";
        }

        public const string VideoSet = "0x0120D520A808";
    }
}