using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace geoproxy.Controllers
{
    static class Utils
    {
        static string _defaultStamp;
        static Dictionary<string, X509Certificate2> _certificates = new Dictionary<string, X509Certificate2>();

        public static void WriteLine(object arg)
        {
            WriteLine("{0}", arg);
        }

        public static void WriteLine(string format, params object[] args)
        {
            Trace.TraceError(String.Format(DateTime.UtcNow.ToString("s") + " " + format, args));
        }

        public static X509Certificate2 GetClientCertificate(string baseUri)
        {
            var key = (baseUri == OperationController.PPEDFGeoUri) ? "PPEDF_STAMP_CERTIFICATE" : "PRIVATE_STAMP_CERTIFICATE";

            X509Certificate2 certificate = null;
            lock (_certificates)
            {
                if (!_certificates.TryGetValue(key, out certificate))
                {
                    var thumbprint = ConfigurationManager.AppSettings[key];
                    if (String.IsNullOrEmpty(thumbprint))
                    {
                        // some hard coded default for testing
                        thumbprint = "AB1287A0C4CE358D46CE270AE9F8A1B8AA59F10F";
                    }

                    X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    store.Open(OpenFlags.ReadOnly);
                    try
                    {
                        var certCollection = store.Certificates.Find(
                                         X509FindType.FindByThumbprint,
                                         thumbprint,
                                         false);
                        if (certCollection.Count == 0)
                        {
                            throw new Exception("Cannot find client cert with '" + thumbprint + "' thumbprint!");
                        }

                        certificate = _certificates[key] = certCollection[0];
                    }
                    finally
                    {
                        store.Close();
                    }
                }
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
    }
}