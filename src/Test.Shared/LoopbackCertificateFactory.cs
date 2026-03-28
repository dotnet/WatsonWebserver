namespace Test.Shared
{
    using System;
    using System.Net;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Creates self-signed loopback certificates for transport tests.
    /// </summary>
    public static class LoopbackCertificateFactory
    {
        /// <summary>
        /// Create a short-lived self-signed certificate for loopback use.
        /// </summary>
        /// <param name="hostname">Certificate host name.</param>
        /// <returns>Certificate instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hostname"/> is null or empty.</exception>
        public static X509Certificate2 Create(string hostname)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest request = new CertificateRequest("CN=" + hostname, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                SubjectAlternativeNameBuilder subjectAlternativeNames = new SubjectAlternativeNameBuilder();
                OidCollection enhancedKeyUsage = new OidCollection();

                subjectAlternativeNames.AddDnsName(hostname);
                subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
                subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
                enhancedKeyUsage.Add(new Oid("1.3.6.1.5.5.7.3.1"));

                request.CertificateExtensions.Add(subjectAlternativeNames.Build());
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, true));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                using (X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7)))
                {
                    byte[] exportedCertificate = certificate.Export(X509ContentType.Pfx);
#if NET10_0_OR_GREATER
                    return X509CertificateLoader.LoadPkcs12(exportedCertificate, null);
#else
#pragma warning disable SYSLIB0057
                    return new X509Certificate2(exportedCertificate);
#pragma warning restore SYSLIB0057
#endif
                }
            }
        }
    }
}
