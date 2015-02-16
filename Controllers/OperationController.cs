using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json.Linq;

namespace geoproxy.Controllers
{
    public class OperationController : ApiController
    {
        const string VersionSuffix = "-privatepreview";
        const string CurrentGeoUri = "https://geomaster.antdir0.antares-test.windows-int.net:444/";
        const string IntAppGeoUri = "https://geomaster.ant-intapp-admin.windows-int.net:444/";

        [HttpGet, HttpPost, HttpPut, HttpHead, HttpPatch, HttpOptions, HttpDelete]
        public async Task<HttpResponseMessage> Invoke(HttpRequestMessage requestMessage)
        {
            try
            {
                return await InvokeInternal(requestMessage);
            }
            catch (Exception ex)
            {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                var json = new JObject();
                json["code"] = (int)HttpStatusCode.InternalServerError;
                json["message"] = ex.ToString();
                response.Content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
                return response;
            }
        }

        private async Task<HttpResponseMessage> InvokeInternal(HttpRequestMessage requestMessage)
        {
            var referrer = requestMessage.Headers.Referrer;
            if (referrer == null)
            {
                throw new InvalidOperationException("referer header is missing!");
            }

            var baseUri = String.Empty;
            if (referrer.Host.Equals("api-current.resources.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                baseUri = CurrentGeoUri;
            }
            else if (referrer.Host.Equals("api-dogfood.resources.windows-int.net", StringComparison.OrdinalIgnoreCase))
            {
                baseUri = IntAppGeoUri;
            }
            else
            {
                throw new InvalidOperationException("referer '" + referrer + "' is invalid!");
            }

            IEnumerable<string> principalIds;
            if (!requestMessage.Headers.TryGetValues("x-ms-client-principal-id", out principalIds))
            {
                throw new InvalidOperationException("x-ms-client-principal-id header is missing!");
            }

            var uri = requestMessage.RequestUri;
            var query = uri.ParseQueryString();
            var apiVersion = query["api-version"];
            if (String.IsNullOrEmpty(apiVersion))
            {
                throw new InvalidOperationException("api-version query string is missing!");
            }

            var parts = apiVersion.Split('-');
            if (parts.Length < 4)
            {
                throw new InvalidOperationException("api-version query string must contains at least 4 parts!");
            }

            if (!apiVersion.EndsWith(VersionSuffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("api-version query string must end with '" + VersionSuffix + "'!");
            }

            query["api-version"] = apiVersion.Substring(0, apiVersion.Length - VersionSuffix.Length);

            var stamp = query["stamp"];
            if (!String.IsNullOrEmpty(stamp))
            {
                baseUri = String.Format("https://{0}.cloudapp.net:444/", stamp);
                query.Remove("stamp");
            }

            var handler = new WebRequestHandler();
            handler.ClientCertificates.Add(Utils.GetClientCertificate());

            var client = new HttpClient(handler);
            requestMessage.RequestUri = new Uri(new Uri(baseUri), uri.AbsolutePath + '?' + query);
            requestMessage.Headers.Host = null;

            // These header is defined by client/server policy.  Since we are forwarding, 
            // it does not apply to the communication from this node to next.   Remove them.
            RemoveConnectionHeaders(requestMessage.Headers);

            // This is to work around Server's side request message always has Content.
            // For non-null content, if we try to forward wiht say GET verb, HttpClient will fail protocol exception.
            // Workaround is to null out in such as.  Checking ContentType seems least disruptive.
            if (requestMessage.Content != null && requestMessage.Content.Headers.ContentType == null)
            {
                requestMessage.Content = null;
            }

            try
            {
                var response = await client.SendAsync(requestMessage);

                // These header is defined by client/server policy.  Since we are forwarding, 
                // it does not apply to the communication from this node to next.   Remove them.
                RemoveConnectionHeaders(response.Headers);

                Utils.WriteLine("{0} {1} {2}", requestMessage.Method, requestMessage.RequestUri, response.StatusCode);

                return response;
            }
            catch (Exception ex)
            {
                Utils.WriteLine("{0} {1} {2}", requestMessage.Method, requestMessage.RequestUri, ex);
                throw;
            }
        }

        private static void RemoveConnectionHeaders(HttpHeaders headers)
        {
            var connection = headers is HttpRequestHeaders ? ((HttpRequestHeaders)headers).Connection : ((HttpResponseHeaders)headers).Connection;
            foreach (var name in connection)
            {
                headers.Remove(name);
            }
            headers.Remove("Connection");
        }
    }
}