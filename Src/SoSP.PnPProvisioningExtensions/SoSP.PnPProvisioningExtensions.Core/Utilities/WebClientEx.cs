using System;
using System.Net;

namespace SoSP.PnPProvisioningExtensions.Core.Utilities
{
    // See https://stackoverflow.com/a/43172235/588868
    public class WebClientEx : WebClient
    {
        protected override System.Net.WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address) as HttpWebRequest;
            if (request != null)
            {
                request.ServicePoint.Expect100Continue = false;
            }
            return request;
        }
    }
}