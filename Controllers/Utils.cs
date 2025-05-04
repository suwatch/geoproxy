using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

namespace geoproxy.Controllers
{
    static class Utils
    {
        static object _lockObj = new object();
        static string _defaultStamp;
        static Dictionary<string, X509Certificate2> _certificates = new Dictionary<string, X509Certificate2>();

        public static void WriteLine(object arg)
        {
            WriteLine("{0}", arg);
        }

        public static void WriteLine(string format, params object[] args)
        {
            lock (_lockObj)
            {
                Trace.TraceError(String.Format(DateTime.UtcNow.ToString("s") + " " + format, args));
            }
        }

        public static X509Certificate2 GetClientCertificate(string baseUri, string thumbprint)
        {
            if (String.IsNullOrEmpty(thumbprint))
            {
                var key = (baseUri == OperationController.PPEDFGeoUri) ? "PPEDF_STAMP_CERTIFICATE" : "PRIVATE_STAMP_CERTIFICATE";
                thumbprint = ConfigurationManager.AppSettings[key];
                if (String.IsNullOrEmpty(thumbprint))
                {
                    throw new InvalidOperationException(String.Format("AppSettings {0} is not defined!", key));
                }
            }

            return GetClientCertificateByThumbprintOrSubjectName(thumbprint);
        }

        public static X509Certificate2 GetClientCertificateByThumbprintOrSubjectName(string thumbprint)
        {
            X509Certificate2 certificate = null;
            lock (_certificates)
            {
                if (!_certificates.TryGetValue(thumbprint, out certificate))
                {
                    X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    store.Open(OpenFlags.ReadOnly);

                    try
                    {
                        foreach (var cert in store.Certificates)
                        {
                            if (cert.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase)
                                || cert.Subject.StartsWith($"CN={thumbprint}", StringComparison.OrdinalIgnoreCase))
                            {
                                certificate = _certificates[thumbprint] = cert;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        store.Close();
                    }
                }
            }

            if (certificate == null)
            {
                throw new Exception("Cannot find client cert with '" + thumbprint + "' thumbprint!");
            }

            return new X509Certificate2(certificate);
        }

        public static string GetDefaultStamp()
        {
            if (_defaultStamp == null)
            {
                _defaultStamp = ConfigurationManager.AppSettings["WEBSITE_DEFAULT_STAMP"] ?? String.Empty;
            }

            return _defaultStamp;
        }

        public static string GetHeader(this HttpHeaders headers, string name)
        {
            IEnumerable<string> values;
            if (headers.TryGetValues(name, out values))
            {
                return values.FirstOrDefault();
            }
            return null;
        }
    }
}