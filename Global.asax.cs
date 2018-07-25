using System;
using System.Net;
using System.Web;
using System.Web.Http;

namespace geoproxy
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            WebApiConfig.Register(GlobalConfiguration.Configuration);
        }
    }
}