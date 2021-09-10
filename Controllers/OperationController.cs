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
        public const string VersionSuffix = "-privatepreview";
        public const string PPEDFGeoUri = "https://geomaster.admin-waws-ppedf.windows-int.net:444/";

        [HttpGet, HttpPost, HttpPut, HttpHead, HttpPatch, HttpOptions, HttpDelete]
        public async Task<HttpResponseMessage> Invoke(HttpRequestMessage requestMessage)
        {
            try
            {
                return await InvokeInternal(requestMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(String.Format("{0} {1}, {2}", requestMessage.Method, requestMessage.RequestUri, ex.ToString()));

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
            var baseUri = PPEDFGeoUri;
            var uri = requestMessage.RequestUri;
            var query = uri.ParseQueryString();
            var apiVersion = query["api-version"];
            if (String.IsNullOrEmpty(apiVersion))
            {
                throw new InvalidOperationException("api-version query string is missing!");
            }

            if (apiVersion.EndsWith(VersionSuffix, StringComparison.OrdinalIgnoreCase))
            {
                query["api-version"] = apiVersion.Substring(0, apiVersion.Length - VersionSuffix.Length);
            }
            else
            {
                query["api-version"] = apiVersion;
            }

            var stamp = query["stamp"];
            if (!String.IsNullOrEmpty(stamp))
            {
                baseUri = GetStampBaseUri(stamp);
                query.Remove("stamp");
            }
            else
            {
                stamp = requestMessage.Headers.GetHeader("x-geoproxy-stamp");
                if (!String.IsNullOrEmpty(stamp))
                {
                    baseUri = GetStampBaseUri(stamp);
                    requestMessage.Headers.Remove("x-geoproxy-stamp");
                }
                else
                {
                    var defaultStamp = Utils.GetDefaultStamp();
                    if (!String.IsNullOrEmpty(defaultStamp))
                    {
                        baseUri = GetStampBaseUri(defaultStamp);
                    }
                }
            }

            HttpClient client;
            var useBasicAuth = query["basicauth"];
            if (!String.IsNullOrEmpty(useBasicAuth))
            {
                query.Remove("basicauth");
                client = new HttpClient();
                var byteArray = Encoding.ASCII.GetBytes(Environment.GetEnvironmentVariable("PRIVATE_STAMP_BASICAUTH"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else
            {
                // user certificate
                var handler = new WebRequestHandler();
                var stampCert = requestMessage.Headers.GetHeader("x-geoproxy-stampcert");
                if (!String.IsNullOrEmpty(stampCert))
                {
                    handler.ClientCertificates.Add(Utils.GetClientCertificate(baseUri, stampCert));
                    requestMessage.Headers.Remove("x-geoproxy-stampcert");
                }
                else
                {
                    stampCert = query["certauth"];
                    handler.ClientCertificates.Add(Utils.GetClientCertificate(baseUri, stampCert));
                }

                query.Remove("certauth");
                client = new HttpClient(handler);
            }

            requestMessage.RequestUri = new Uri(new Uri(baseUri), uri.AbsolutePath + '?' + query);
            requestMessage.Headers.Host = null;

            var strb = new StringBuilder();
            strb.AppendLine();
            strb.AppendLine("---------- Request -----------------------");
            strb.AppendLine(string.Format("{0} {1}", requestMessage.Method, requestMessage.RequestUri));

            DumpHeaders(strb, requestMessage.Headers);
            if (requestMessage.Content != null)
            {
                DumpHeaders(strb, requestMessage.Content.Headers);
            }

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
                strb.AppendLine();
                strb.AppendLine("---------- Response -----------------------");
                strb.AppendLine(string.Format("StatusCode: {0}", response.StatusCode));

                DumpHeaders(strb, response.Headers);
                if (response.Content != null)
                {
                    DumpHeaders(strb, response.Content.Headers);
                }

                // These header is defined by client/server policy.  Since we are forwarding, 
                // it does not apply to the communication from this node to next.   Remove them.
                RemoveConnectionHeaders(response.Headers);

                Utils.WriteLine("{0} {1} {2}", requestMessage.Method, requestMessage.RequestUri, response.StatusCode);

                return response;
            }
            catch (Exception ex)
            {
                strb.AppendLine();
                strb.AppendLine("---------- Response -----------------------");
                strb.AppendLine(string.Format("Exception: {0}", ex));
                throw;
            }
            finally
            {
                strb.AppendLine();
                Utils.WriteLine(strb);
            }
        }

        private static string GetStampBaseUri(string stamp)
        {
            if (Uri.TryCreate(stamp, UriKind.Absolute, out _))
            {
                return stamp;
            }

            if (stamp.Contains(':'))
            {
                return $"https://{stamp}/";
            }

            if (stamp.Contains('.'))
            {
                return $"https://{stamp}:444/";
            }

            return $"https://{stamp}.cloudapp.net:444/";
        }

        private static void RemoveConnectionHeaders(HttpHeaders headers)
        {
            var connection = headers is HttpRequestHeaders ? ((HttpRequestHeaders)headers).Connection : ((HttpResponseHeaders)headers).Connection;
            foreach (var name in connection)
            {
                headers.Remove(name);
            }
            headers.Remove("Connection");
            headers.Remove("Transfer-Encoding");
        }

        private static void DumpHeaders(StringBuilder strb, HttpHeaders headers)
        {
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    strb.AppendLine(string.Format("{0}: {1}", header.Key, string.Join(", ", header.Value)));
                }
            }
        }
    }
}