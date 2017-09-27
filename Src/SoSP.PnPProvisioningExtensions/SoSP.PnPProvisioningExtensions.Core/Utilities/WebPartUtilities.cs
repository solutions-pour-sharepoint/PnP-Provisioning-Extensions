using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    public static class WebPartUtilities
    {
        public static string EnsureXsltListviewWebPartView(ClientContext ctx, string webPartXml, TokenParser tokenParser)
        {
            var webPartXmlElement = XElement.Parse(webPartXml);
            var wp3Ns = (XNamespace)"http://schemas.microsoft.com/WebPart/v3";
            if (webPartXmlElement.Element(wp3Ns + "webPart") != null)
            {
                var xmlnsmgr = new XmlNamespaceManager(new NameTable());
                xmlnsmgr.AddNamespace("wp3", wp3Ns.NamespaceName);

                var typeName = (string)webPartXmlElement.XPathSelectElement("wp3:webPart/wp3:metaData/wp3:type", xmlnsmgr).Attribute("name");

                if (typeName.StartsWith("Microsoft.SharePoint.WebPartPages.XsltListViewWebPart"))
                {
                    var listId = Guid.Parse((string)webPartXmlElement.XPathSelectElement("wp3:webPart/wp3:data/wp3:properties/wp3:property[@name='ListId']", xmlnsmgr));
                    var viewDefinition = webPartXmlElement.XPathSelectElement("wp3:webPart/wp3:data/wp3:properties/wp3:property[@name='XmlDefinition']", xmlnsmgr);
                    var viewDefinitionXml = XElement.Parse(tokenParser.ParseXmlString((string)viewDefinition));
                    var list = ctx.Web.Lists.GetById(listId);
                    ctx.Load(list, l => l.Views.Include(v => v.Id));
                    ctx.ExecuteQueryRetry();
                    var viewId = CreateView(ctx, viewDefinitionXml, list, tokenParser);

                    viewDefinitionXml.SetAttributeValue("Name", viewId);
                    viewDefinition.SetValue(viewDefinitionXml.ToString());

                    return webPartXmlElement.ToString();
                }
            }

            return webPartXml;
        }

        /* private Guid EnsureView(ClientContext ctx, Guid listId, string viewDefinition)
         {
             var existingView = list.Views.GetById(listId);

             ctx.ExecuteQueryRetry();

             if (!(bool)existingView.ServerObjectIsNull)
             {
                 existingView.DeleteObject();
                 ctx.ExecuteQuery();
             }
         }*/

        private static Guid CreateView(
            ClientContext ctx,
            XElement viewElement,
            List list,
            TokenParser parser
            )
        {
            var web = ctx.Web;
            var viewId = Guid.Parse((string)viewElement.Attribute("Name"));

            var existingView = list.Views.AsEnumerable().FirstOrDefault(v => v.Id == viewId);



            if (existingView != null)
            {
                existingView.DeleteObject();
                web.Context.ExecuteQueryRetry();
            }

            // Type
            var viewTypeString = viewElement.Attribute("Type") != null ? viewElement.Attribute("Type").Value : "None";
            viewTypeString = viewTypeString[0].ToString().ToUpper() + viewTypeString.Substring(1).ToLower();
            var viewType = (ViewType)Enum.Parse(typeof(ViewType), viewTypeString);

            // Fix the calendar recurrence
            if (viewType == ViewType.Calendar)
            {
                viewType = ViewType.Calendar | ViewType.Recurrence;
            }

            // Fields
            string[] viewFields = null;
            var viewFieldsElement = viewElement.Descendants("ViewFields").FirstOrDefault();
            if (viewFieldsElement != null)
            {
                viewFields = (from field in viewElement.Descendants("ViewFields").Descendants("FieldRef") select field.Attribute("Name").Value).ToArray();
            }

            // Default view
            var viewDefault = viewElement.Attribute("DefaultView") != null && bool.Parse(viewElement.Attribute("DefaultView").Value);

            // Row limit
            var viewPaged = true;
            uint viewRowLimit = 30;
            var rowLimitElement = viewElement.Descendants("RowLimit").FirstOrDefault();
            if (rowLimitElement != null)
            {
                if (rowLimitElement.Attribute("Paged") != null)
                {
                    viewPaged = bool.Parse(rowLimitElement.Attribute("Paged").Value);
                }
                viewRowLimit = uint.Parse(rowLimitElement.Value);
            }

            // Query
            var viewQuery = new StringBuilder();
            foreach (var queryElement in viewElement.Descendants("Query").Elements())
            {
                viewQuery.Append(queryElement.ToString());
            }

            var viewCI = new ViewCreationInformation
            {
                ViewFields = viewFields,
                RowLimit = viewRowLimit,
                Paged = viewPaged,
                Query = viewQuery.ToString(),
                ViewTypeKind = viewType,
                PersonalView = false,
                SetAsDefaultView = viewDefault,
            };

            // Allow to specify a custom view url. View url is taken from title, so we first set title to the view url value we need,
            // create the view and then set title back to the original value
            var urlAttribute = viewElement.Attribute("Url");
            var urlHasValue = urlAttribute != null && !string.IsNullOrEmpty(urlAttribute.Value);
            if (urlHasValue)
            {
                //set Title to be equal to url (in order to generate desired url)
                viewCI.Title = Path.GetFileNameWithoutExtension(urlAttribute.Value);
            }

            var createdView = list.Views.Add(viewCI);
            createdView.EnsureProperties(v => v.Scope, v => v.Id, v => v.JSLink, v => v.Title, v => v.Aggregations, v => v.MobileView, v => v.MobileDefaultView, v => v.ViewData);
            web.Context.ExecuteQueryRetry();

            if (urlHasValue)
            {
                //restore original title
                createdView.Update();
            }

            // ContentTypeID
            var contentTypeID = viewElement.Attribute("ContentTypeID") != null ? viewElement.Attribute("ContentTypeID").Value : null;
            if (!string.IsNullOrEmpty(contentTypeID) && (contentTypeID != BuiltInContentTypeId.System))
            {
                ContentTypeId childContentTypeId = null;
                if (contentTypeID == BuiltInContentTypeId.RootOfList)
                {
                    var childContentType = web.GetContentTypeById(contentTypeID);
                    childContentTypeId = childContentType != null ? childContentType.Id : null;
                }
                else
                {
                    childContentTypeId = list.ContentTypes.BestMatch(contentTypeID);
                }
                if (childContentTypeId != null)
                {
                    createdView.ContentTypeId = childContentTypeId;
                    createdView.Update();
                }
            }

            // Default for content type
            var defaultViewForContentType = viewElement.Attribute("DefaultViewForContentType") != null ? viewElement.Attribute("DefaultViewForContentType").Value : null;
            if (!string.IsNullOrEmpty(defaultViewForContentType) && bool.TryParse(defaultViewForContentType, out bool parsedDefaultViewForContentType))
            {
                createdView.DefaultViewForContentType = parsedDefaultViewForContentType;
                createdView.Update();
            }

            // Scope
            var scope = viewElement.Attribute("Scope") != null ? viewElement.Attribute("Scope").Value : null;
            ViewScope parsedScope = ViewScope.DefaultValue;
            if (!string.IsNullOrEmpty(scope) && Enum.TryParse<ViewScope>(scope, out parsedScope))
            {
                createdView.Scope = parsedScope;
                createdView.Update();
            }

            // MobileView
            var mobileView = viewElement.Attribute("MobileView") != null && bool.Parse(viewElement.Attribute("MobileView").Value);
            if (mobileView)
            {
                createdView.MobileView = mobileView;
                createdView.Update();
            }

            // MobileDefaultView
            var mobileDefaultView = viewElement.Attribute("MobileDefaultView") != null && bool.Parse(viewElement.Attribute("MobileDefaultView").Value);
            if (mobileDefaultView)
            {
                createdView.MobileDefaultView = mobileDefaultView;
                createdView.Update();
            }

            // Aggregations
            var aggregationsElement = viewElement.Descendants("Aggregations").FirstOrDefault();
            if (aggregationsElement != null)
            {
                if (aggregationsElement.HasElements)
                {
                    var fieldRefString = "";
                    var fieldRefs = aggregationsElement.Descendants("FieldRef");
                    foreach (var fieldRef in fieldRefs)
                    {
                        fieldRefString += fieldRef.ToString();
                    }
                    if (createdView.Aggregations != fieldRefString)
                    {
                        createdView.Aggregations = fieldRefString;
                        createdView.Update();
                    }
                }
            }

            // JSLink
            var jslinkElement = viewElement.Descendants("JSLink").FirstOrDefault();
            if (jslinkElement != null)
            {
                var jslink = jslinkElement.Value;
                if (createdView.JSLink != jslink)
                {
                    createdView.JSLink = jslink;
                    createdView.Update();

                    // Only push the JSLink value to the web part as it contains a / indicating it's a custom one. So we're not pushing the OOB ones like clienttemplates.js or hierarchytaskslist.js
                    // but do push custom ones down to th web part (e.g. ~sitecollection/Style Library/JSLink-Samples/ConfidentialDocuments.js)
                    if (jslink.Contains("/"))
                    {
                        createdView.EnsureProperty(v => v.ServerRelativeUrl);
                        list.SetJSLinkCustomizations(createdView.ServerRelativeUrl, jslink);
                    }
                }
            }

            // View Data
            var viewDataElement = viewElement.Descendants("ViewData").FirstOrDefault();
            if (viewDataElement != null)
            {
                if (viewDataElement.HasElements)
                {
                    var fieldRefString = "";
                    var fieldRefs = viewDataElement.Descendants("FieldRef");
                    foreach (var fieldRef in fieldRefs)
                    {
                        fieldRefString += fieldRef.ToString();
                    }
                    if (createdView.ViewData != fieldRefString)
                    {
                        createdView.ViewData = fieldRefString;
                        createdView.Update();
                    }
                }
            }

            list.Update();
            web.Context.ExecuteQueryRetry();

            return createdView.Id;
        }

    }
}
