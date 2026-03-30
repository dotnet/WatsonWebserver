namespace Test.Benchmark
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Creates self-signed certificates for local benchmark servers.
    /// </summary>
    internal static class BenchmarkCertificateFactory
    {
        /// <summary>
        /// Create a self-signed certificate for localhost usage.
        /// </summary>
        /// <returns>Certificate.</returns>
        public static X509Certificate2 Create()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest request = new CertificateRequest(
                    "CN=localhost",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                SubjectAlternativeNameBuilder subjectAlternativeName = new SubjectAlternativeNameBuilder();
                subjectAlternativeName.AddDnsName("localhost");
                subjectAlternativeName.AddIpAddress(System.Net.IPAddress.Loopback);
                request.CertificateExtensions.Add(subjectAlternativeName.Build());

                using (X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)))
                {
                    byte[] exported = certificate.Export(X509ContentType.Pfx);
#if NET10_0_OR_GREATER
                    return X509CertificateLoader.LoadPkcs12(exported, null);
#else
#pragma warning disable SYSLIB0057
                    return new X509Certificate2(exported);
#pragma warning restore SYSLIB0057
#endif
                }
            }
        }
    }
}
