using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using SoSP.PnPProvisioningExtensions.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using File = Microsoft.SharePoint.Client.File;
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
            if(sitePagesLibrary == null)
            {
                // No site pages library, skip the handler
                return template;
            }
            ctx.Load(sitePagesLibrary);
            ctx.Load(web, w => w.ServerRelativeUrl, w => w.Url);
            ctx.ExecuteQueryRetry();
            if (!sitePagesLibrary.ServerObjectIsNull.GetValueOrDefault())
            {
                var query = new CamlQuery
                {
                    ViewXml = string.Concat(
                        "<View>",
                        "<Query>",
                        $"<Where><BeginsWith><FieldRef Name='ContentTypeId'/><Value Type='ContentTypeId'>{BuiltInContentTypeId.WebPartPage}</Value></BeginsWith></Where>",
                        "</Query>",
                        "<ViewFields><FieldRef Name='Title' /></ViewFields>",
                        "</View>"
                        )
                };

                var wpPages = sitePagesLibrary.GetItems(query);

                ctx.Load(wpPages, pages => pages.IncludeWithDefaultProperties(
                    p => p.File.ServerRelativeUrl,
                    p => p.File.CustomizedPageStatus,
                    p => p.File
                    ));
                ctx.ExecuteQueryRetry();

                var data = new Data();
                var tokenizer = new Tokenizer(ctx);
                foreach (var page in wpPages)
                {
                    var propertyBag = GetPropertyBag(page.File);
                    if (propertyBag.ContainsKey("vti_setuppath")) // Unghosted pages are not yet supported
                    {
                        var setupPath = (string)propertyBag["vti_setuppath"];
                        var webPartPageTemplateStr = Path.GetFileNameWithoutExtension(setupPath).Substring(5); // 5 for spstd in spstdXX.aspx filename
                        if (page.File.CustomizedPageStatus != CustomizedPageStatus.Uncustomized)
                        {
                            continue; // Skip customized page. Not yet supported
                        }
                        var webRelativeFileUrl = page.File.ServerRelativeUrl.Replace(web.ServerRelativeUrl.TrimEnd('/') + '/', "");
                        var pageData = new PageData
                        {
                            WebPartPageTemplate = int.Parse(webPartPageTemplateStr),
                            Url = webRelativeFileUrl
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
                }

                if (data.Count == 0) { return template; }

                var extHandler = GetExtensibilityHandler(data);

                template.ExtensibilityHandlers.Add(extHandler);
            }
            return template;
        }

        private static IDictionary<string, object> GetPropertyBag(File file)
        {
            var ctx = (ClientContext)file.Context;
            var web = ctx.Web;
            var webRelativeFileUrl = file.ServerRelativeUrl.Replace(web.ServerRelativeUrl.TrimEnd('/') + '/', "");

            using (var wc = new WebClientEx())
            {
                if (file.Context.Credentials != null)
                {
                    wc.Credentials = file.Context.Credentials;
                }
                else
                {
                    wc.UseDefaultCredentials = true;
                }

                var requestUrl = web.Url.TrimEnd('/') + "/_vti_bin/_vti_aut/author.dll";

                wc.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
                wc.Headers.Add("X-Vermeer-Content-Type", "application/x-www-form-urlencoded");

                var query = HttpUtility.ParseQueryString(string.Empty);
                query.Add("method", "getDocsMetaInfo");
                query.Add("url_list", $"[{webRelativeFileUrl}]");

                var rpcResult = Encoding.UTF8.GetString(
                    wc.UploadData(requestUrl, "POST", Encoding.UTF8.GetBytes(query.ToString()))
                    );

                return ParseRpcResult(rpcResult);
            }
        }

        private static Dictionary<string, object> ParseRpcResult(string rpcResult)
        {
            var result = new Dictionary<string, object>();

            using (var sr = new StringReader(rpcResult))
            {
                string currentLine;
                var hasReachedMetadata = false;
                while ((currentLine = sr.ReadLine()) != null)
                {
                    if (!hasReachedMetadata)
                    {
                        if (currentLine == "<li>meta_info=")
                        {
                            // SKip the next ul line
                            sr.ReadLine();
                            hasReachedMetadata = true;
                        }
                        // else nothing to do
                    }
                    else
                    {
                        if (currentLine == "</ul>")
                        {
                            break; // end of data has been reached
                        }
                        else
                        {
                            var key = currentLine.Substring(4);
                            var rawValue = sr.ReadLine().Substring(4);
                            var typeInfo = rawValue.Substring(0, 1);
                            var strValue = HttpUtility.HtmlDecode(rawValue.Substring(3));
                            object value = null;
                            switch (typeInfo)
                            {
                                case "B":
                                    value = Convert.ToBoolean(strValue);
                                    break;

                                case "I":
                                    value = Convert.ToInt32(strValue);
                                    break;

                                case "F":
                                case "T":
                                    value = DateTime.Parse(strValue);
                                    break;

                                case "S":
                                case "V":
                                    value = strValue;
                                    break;

                                default:
                                    throw new InvalidOperationException("Unknown RPC type");
                            }
                            result.Add(key, value);
                        }
                    }
                }
                return result;
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
            if (string.IsNullOrWhiteSpace(configurationData)) { return; }

            var web = ctx.Web;
            var sitePagesLibrary = web.GetListByUrl("SitePages");
            ctx.Load(web, w => w.Url);
            ctx.Load(sitePagesLibrary, list => list.Id, list => list.RootFolder.ServerRelativeUrl);
            ctx.ExecuteQueryRetry();

            var data =SerializationHelper.DeserializeDataXml<Data>(configurationData);

            for (int i = 0; i < data.Count; i++)
            {
                var webPartPage = data[i];
                applyingInformation.ProgressDelegate?.Invoke($"Creating page {webPartPage.Url}", i, data.Count);

                var batchMessage = GetCreatePageBatchMessage(webPartPage, sitePagesLibrary);

                var result = ExecuteBatchMessage(web, batchMessage);

                var resultXml = XElement.Parse(result);

                if (!(int.TryParse((string)resultXml.Element("Result")?.Attribute("Code"), out int resultCode) && resultCode == 0))
                {
                    throw new ApplicationException($"Erreur when adding page : {resultXml}");
                }

                foreach (var webPart in webPartPage.WebParts)
                {
                    var webPartXml = tokenParser.ParseString(webPart.Contents);

                    webPartXml = EnsureXsltListviewWebPartView(ctx, webPartXml, tokenParser);
                    
                    web.AddWebPartToWebPartPage(
                        new OfficeDevPnP.Core.Entities.WebPartEntity
                        {
                            WebPartIndex = (int)webPart.Order,
                            WebPartTitle = webPart.Title,
                            WebPartXml = webPartXml,
                            WebPartZone = webPart.Zone
                        },
                        webPartPage.Url
                    );
                }
            }
        }

        private string EnsureXsltListviewWebPartView(ClientContext ctx, string webPartXml, TokenParser tokenParser)
        {
            var webPartXmlElement = XElement.Parse(webPartXml);
            var wp3Ns = (XNamespace)"http://schemas.microsoft.com/WebPart/v3";
            if (webPartXmlElement.Element(wp3Ns + "webPart")!= null)
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

        private Guid CreateView(
            ClientContext ctx,
            XElement viewElement,
            List list,
            TokenParser parser
            )
        {
            var web = ctx.Web;
            var viewId = Guid.Parse((string)viewElement.Attribute("Name"));

            var existingView = list.Views.AsEnumerable().FirstOrDefault(v=>v.Id ==viewId);



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
            createdView.EnsureProperties(v => v.Scope, v=>v.Id, v => v.JSLink, v => v.Title, v => v.Aggregations, v => v.MobileView, v => v.MobileDefaultView, v => v.ViewData);
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

        private static string ExecuteBatchMessage(Web web, string batchMessage)
        {
            var requestUrl = web.Url.TrimEnd('/') + "/_vti_bin/owssvr.dll?Cmd=DisplayPost";

            using (var wc = new WebClientEx())
            {
                if (web.Context.Credentials != null)
                {
                    wc.Credentials = web.Context.Credentials;
                }
                else
                {
                    wc.UseDefaultCredentials = true;
                }
                wc.Headers.Add(HttpRequestHeader.Accept, "auth/sicily");
                wc.Headers.Add(HttpRequestHeader.ContentType, "application/xml");
                wc.Headers.Add("X-Vermeer-Content-Type", "application/xml");
                return Encoding.UTF8.GetString(
                    wc.UploadData(requestUrl, "POST", Encoding.UTF8.GetBytes(batchMessage))
                    );
            }
        }

        private static string GetCreatePageBatchMessage(PageData webPartPage, List pageList)
        {
            // Warning: the server side Xml Parser is shamelessly buggy.
            // Xml must have a very precise syntax, especially, only " can be used for attributes, and line breaks are required.
            var xmlBatch = XElement.Parse(CREATE_PAGE_TEMPLATE);
            xmlBatch.XPathSelectElement("SetList").Value = pageList.Id.ToString("B").ToUpper();
            xmlBatch.XPathSelectElement("SetVar[@Name='List']").Value = pageList.Id.ToString("B").ToUpper();
            xmlBatch.XPathSelectElement("SetVar[@Name='Title']").Value = Path.GetFileNameWithoutExtension(webPartPage.Url);
            xmlBatch.XPathSelectElement("SetVar[@Name='WebPartPageTemplate']").Value = webPartPage.WebPartPageTemplate.ToString();
            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ows:Batch OnError=\"Return\" Version=\"15.00.0.000\">\n{xmlBatch}\n</ows:Batch>\n";
        }

        private const string CREATE_PAGE_TEMPLATE = @"<Method ID=""0,NewWebPage"">
  <SetList Scope=""Request""></SetList>
  <SetVar Name=""Cmd"">NewWebPage</SetVar>
  <SetVar Name=""List""></SetVar>
  <SetVar Name=""RelativeFolderPath"" />
  <SetVar Name=""Title""></SetVar>
  <SetVar Name=""WebPartPageTemplate""></SetVar>
  <SetVar Name=""Overwrite"">true</SetVar>
  <SetVar Name=""ID"">New</SetVar>
  <SetVar Name=""Cmd"">NewWebPage</SetVar>
  <SetVar Name=""Type"">WebPartPage</SetVar>
 </Method>";
    }
}