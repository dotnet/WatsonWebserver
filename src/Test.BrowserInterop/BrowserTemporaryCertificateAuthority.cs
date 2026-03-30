namespace Test.BrowserInterop
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Temporary root and server certificates for browser interoperability testing.
    /// </summary>
    internal sealed class BrowserTemporaryCertificateAuthority : IDisposable
    {
        /// <summary>
        /// Instantiate the temporary certificate container.
        /// </summary>
        /// <param name="hostname">Server hostname.</param>
        public BrowserTemporaryCertificateAuthority(string hostname)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

            Hostname = hostname;
            ServerCertificate = CreateServerCertificate(hostname);
            CertificatePin = BuildCertificatePin(ServerCertificate);
        }

        /// <summary>
        /// Server hostname.
        /// </summary>
        public string Hostname { get; }

        /// <summary>
        /// Browser SPKI allowlist pin for the server certificate.
        /// </summary>
        public string CertificatePin { get; }

        /// <summary>
        /// Server certificate with private key.
        /// </summary>
        public X509Certificate2 ServerCertificate { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            ServerCertificate?.Dispose();
        }

        private static X509Certificate2 CreateServerCertificate(string hostname)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest request = new CertificateRequest("CN=" + hostname, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                SubjectAlternativeNameBuilder subjectAlternativeNames = new SubjectAlternativeNameBuilder();
                if (System.Net.IPAddress.TryParse(hostname, out System.Net.IPAddress ipAddress))
                {
                    subjectAlternativeNames.AddIpAddress(ipAddress);
                }
                else
                {
                    subjectAlternativeNames.AddDnsName(hostname);
                }
                request.CertificateExtensions.Add(subjectAlternativeNames.Build());
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                using (X509Certificate2 issuedCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7)))
                {
                    byte[] exported = issuedCertificate.Export(X509ContentType.Pfx);
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

        private static string BuildCertificatePin(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            using (RSA rsa = certificate.GetRSAPublicKey())
            {
                if (rsa == null) throw new InvalidOperationException("Temporary browser interop certificate did not contain an RSA public key.");

                byte[] subjectPublicKeyInfo = rsa.ExportSubjectPublicKeyInfo();
                byte[] hash = SHA256.HashData(subjectPublicKeyInfo);
                return Convert.ToBase64String(hash);
            }
        }
    }
}
