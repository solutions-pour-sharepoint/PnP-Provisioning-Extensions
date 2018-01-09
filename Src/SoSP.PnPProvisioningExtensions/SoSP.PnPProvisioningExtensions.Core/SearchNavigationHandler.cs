using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using SoSP.PnPProvisioningExtensions.Core.Utilities;
using System.Collections.Generic;
using System.Linq;
using NavigationNode = OfficeDevPnP.Core.Framework.Provisioning.Model.NavigationNode;
using NavigationNodeCollection = OfficeDevPnP.Core.Framework.Provisioning.Model.NavigationNodeCollection;
using System;
using OfficeDevPnP.Core.Enums;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.Extensions;

namespace SoSP.PnPProvisioningExtensions.Core
{
    public class SearchNavigationHandler : BaseHandler<List<NavigationNode>>
    {
        public override ProvisioningTemplate Extract(
            ClientContext ctx,
            ProvisioningTemplate template,
            ProvisioningTemplateCreationInformation creationInformation,
            PnPMonitoredScope scope,
            string configurationData
            )
        {
            var web = ctx.Web;
            var searchNav = web.Navigation.GetNodeById(1040);

            ctx.Load(web, w => w.ServerRelativeUrl);
            ctx.Load(searchNav, sn=>sn.Children.Include(childNode=>childNode.Title, childNode => childNode.Url, childNode => childNode.IsExternal));
            ctx.ExecuteQueryRetry();
            var data = searchNav.Children.AsEnumerable().Select(n => ToDomainModelNavigationNode(n, web)).ToList();

            template.ExtensibilityHandlers.Add(GetExtensibilityHandler(data));

            return template;
        }

        internal static NavigationNode ToDomainModelNavigationNode(Microsoft.SharePoint.Client.NavigationNode node, Web web)
        {
            var result = new NavigationNode
            {
                Title = node.Title,
                IsExternal = node.IsExternal,
                Url = web.ServerRelativeUrl != "/" ? node.Url.Replace(web.ServerRelativeUrl, "{site}") : $"{{site}}{node.Url}"
            };

            node.Context.Load(node.Children);
            node.Context.ExecuteQueryRetry();

            result.NavigationNodes.AddRange(from n in node.Children.AsEnumerable()
                                            select ToDomainModelNavigationNode(n, web));

            return result;
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

            var data = SerializationHelper.DeserializeDataXml<List<NavigationNode>>(configurationData);

            var web = ctx.Web;
            var searchNav = web.Navigation.GetNodeById(1040);

            ctx.Load(web, w => w.ServerRelativeUrl);
            ctx.Load(searchNav);

            ProvisionNodes(web, tokenParser, NavigationType.SearchNav, data, scope);
        }

        private void ProvisionNodes(Web web, TokenParser parser, NavigationType navigationType, IEnumerable<NavigationNode> nodes, PnPMonitoredScope scope, string parentNodeTitle = null)
        {
            foreach (var node in nodes)
            {
                var navNode = web.AddNavigationNode(
                    parser.ParseString(node.Title),
                    new Uri(parser.ParseString(node.Url), UriKind.RelativeOrAbsolute),
                    parser.ParseString(parentNodeTitle),
                    navigationType,
                    node.IsExternal);

                ProvisionNodes(
                    web,
                    parser,
                    navigationType,
                    node.NavigationNodes,
                    scope,
                    parser.ParseString(node.Title)
                    );

            }
        }
    }
}