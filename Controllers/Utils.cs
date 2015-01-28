using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Web;

namespace geoproxy.Controllers
{
    static class Utils
    {
        static X509Certificate2 _certificate;
        const string LogFile = @"d:\home\logFiles\temp.txt";

        public static void ClearLog()
        {
            if (File.Exists(LogFile))
            {
                File.Delete(LogFile);
            }
        }

        public static void WriteLine(object arg)
        {
            WriteLine("{0}", arg);
        }

        public static void WriteLine(string format, params object[] args)
        {
            File.AppendAllLines(@"d:\home\logFiles\temp.txt", new[] { String.Format(DateTime.UtcNow.ToString("s") + " " + format, args) });
        }

        public static X509Certificate2 GetClientCertificate()
        {
            if (_certificate == null)
            {
                var thumbprint = ConfigurationManager.AppSettings["WEBSITE_LOAD_CERTIFICATES"];
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

                    _certificate = certCollection[0];
                }
                finally
                {
                    store.Close();
                }
            }

            return new X509Certificate2(_certificate);
        }
    }
}