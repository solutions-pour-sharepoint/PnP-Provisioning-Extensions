using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using SoSP.PnPProvisioningExtensions.Core.Utilities;
using System.Collections.Generic;
using System.Runtime.Serialization;
using WebPart = OfficeDevPnP.Core.Framework.Provisioning.Model.WebPart;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public class WebPartPagesHandler : BaseHandler<WebPartPagesHandler.Data>
    {
        #region Serializable datatype (to have a more readable xml)

        [CollectionDataContract(
            Name = nameof(WebPartPagesHandler) + nameof(Data),
            ItemName = "WebPartPage"
            )]
        public class Data : List<PageData>
        {
        }

        [DataContract]
        public class PageData
        {
            [DataMember(IsRequired = true)]
            public int WebPartPageTemplate { get; set; }

            [DataMember(IsRequired = true)]
            public string Url { get; set; }

            [DataMember]
            public WebPartDefinitionsList WebParts { get; set; } = new WebPartDefinitionsList();
        }

        [CollectionDataContract(Name = nameof(WebPartDefinitionsList), ItemName = "WebPart")]
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
            var web = ctx.Web;
            var sitePagesLibrary = web.GetListByUrl("SitePages");
            ctx.Load(sitePagesLibrary);
            ctx.Load(web, w => w.ServerRelativeUrl);
            ctx.ExecuteQueryRetry();
            if (!sitePagesLibrary.ServerObjectIsNull.GetValueOrDefault())
            {
                var query = new CamlQuery
                {
                    ViewXml = string.Concat(
                        "<View>",
                        $"<Where><Eq><FieldRef Name='ContentTypeId'/><Value Type='ContentTypeId'>{BuiltInContentTypeId.WebPartPage}</Value></Eq></Where>",
                        "<ViewFields><FieldRef Name='Title' /></ViewFields>",
                        "</View>"
                        )
                };

                var wpPages = sitePagesLibrary.GetItems(query);

                ctx.Load(wpPages, pages => pages.Include(
                    p => p.File.ServerRelativeUrl,
                    p => p.File.CustomizedPageStatus
                    ));
                ctx.ExecuteQueryRetry();

                var data = new Data();
                var tokenizer = new Tokenizer(ctx);
                foreach (var page in wpPages)
                {
                    if (page.File.CustomizedPageStatus != CustomizedPageStatus.Uncustomized)
                    {
                        continue; // Skip customized page. Not yet supported
                    }
                    var pageData = new PageData
                    {
                        WebPartPageTemplate = 4,
                        Url = tokenizer.Tokenize(page.File.ServerRelativeUrl)
                    };

                    foreach (var webPart in web.GetWebParts(page.File.ServerRelativeUrl))
                    {
                        pageData.WebParts.Add(new WebPart
                        {
                            Contents = tokenizer.Tokenize(web.GetWebPartXml(webPart.Id, page.File.ServerRelativeUrl)),
                            Title = webPart.WebPart.Title,
                            Order = (uint)webPart.WebPart.ZoneIndex,
                            Zone = webPart.EnsureProperty(wp => wp.ZoneId)
                        });
                    }
                    data.Add(pageData);
                }

                var extHandler = GetExtensibilityHandler(data);

                template.ExtensibilityHandlers.Add(extHandler);
            }
            return template;
        }

        public override void Provision(ClientContext ctx, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation, TokenParser tokenParser, PnPMonitoredScope scope, string configurationData)
        {
            ;
        }
    }
}