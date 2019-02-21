using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Utilities;
using System;
using System.Net;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    internal class SharePointWebClient : WebClient
    {
        private readonly ClientRuntimeContext _ctx;

        /// <summary>
        /// Initializes a new SharePointWebClient using a given SharePoint client context
        /// </summary>
        /// <param name="ctx"></param>
        public SharePointWebClient(ClientRuntimeContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            _ctx = ctx;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var req = base.GetWebRequest(address);
            SetupWebRequest(_ctx, (HttpWebRequest)req);
            return req;
        }

        internal static void SetupWebRequest(ClientRuntimeContext ctx, HttpWebRequest req)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (req == null) throw new ArgumentNullException(nameof(req));
            ClientRuntimeContext.SetupRequestCredential(ctx, req);

            // Set a user agent to avoid 403 errors
            req.UserAgent = PnPCoreUtilities.PnPCoreUserAgent;
        }
    }
}