namespace Test.BrowserInterop
{
    using System;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Locates ASP.NET Core localhost development certificates.
    /// </summary>
    internal static class BrowserDevelopmentCertificateLocator
    {
        /// <summary>
        /// Find the most recent localhost development certificate and determine whether it is trusted.
        /// </summary>
        /// <returns>Certificate info.</returns>
        public static BrowserDevelopmentCertificateInfo Find()
        {
            BrowserDevelopmentCertificateInfo result = new BrowserDevelopmentCertificateInfo();
            X509Certificate2 bestCertificate = null;

            using (X509Store personalStore = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                personalStore.Open(OpenFlags.ReadOnly);

                for (int i = 0; i < personalStore.Certificates.Count; i++)
                {
                    X509Certificate2 certificate = personalStore.Certificates[i];
                    if (!String.Equals(certificate.Subject, "CN=localhost", StringComparison.OrdinalIgnoreCase)) continue;
                    if (certificate.NotAfter <= DateTime.Now) continue;
                    if (certificate.FriendlyName == null || certificate.FriendlyName.IndexOf("ASP.NET Core HTTPS development certificate", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (bestCertificate == null || certificate.NotAfter > bestCertificate.NotAfter)
                    {
                        bestCertificate = certificate;
                    }
                }
            }

            if (bestCertificate == null)
            {
                result.IsAvailable = false;
                result.Detail = "No ASP.NET Core localhost development certificate was found.";
                return result;
            }

            result.IsAvailable = true;
            result.Certificate = bestCertificate;
            result.IsTrusted = IsTrusted(bestCertificate);
            result.Detail = bestCertificate.Thumbprint + " trusted=" + result.IsTrusted.ToString();
            return result;
        }

        private static bool IsTrusted(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            using (X509Store rootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                rootStore.Open(OpenFlags.ReadOnly);

                for (int i = 0; i < rootStore.Certificates.Count; i++)
                {
                    X509Certificate2 rootCertificate = rootStore.Certificates[i];
                    if (String.Equals(rootCertificate.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
