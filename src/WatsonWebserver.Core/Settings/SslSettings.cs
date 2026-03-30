namespace WatsonWebserver.Core.Settings
{
    using System.IO;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// SSL settings.
    /// </summary>
    public class SslSettings
    {
        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Certificate for SSL.
        /// If not set directly, the certificate can be loaded from the configured PFX file.
        /// </summary>
        public X509Certificate2 SslCertificate
        {
            get
            {
                if (_SslCertificate == null)
                {
                    if (!string.IsNullOrEmpty(PfxCertificateFile))
                    {
                        if (!string.IsNullOrEmpty(PfxCertificatePassword))
                        {
#if NET9_0_OR_GREATER
                            _SslCertificate = X509CertificateLoader.LoadPkcs12FromFile(PfxCertificateFile, PfxCertificatePassword);
#else
                            _SslCertificate = new X509Certificate2(File.ReadAllBytes(PfxCertificateFile), PfxCertificatePassword);
#endif
                        }
                        else
                        {
#if NET9_0_OR_GREATER
                            _SslCertificate = X509CertificateLoader.LoadPkcs12FromFile(PfxCertificateFile, null);
#else
                            _SslCertificate = new X509Certificate2(File.ReadAllBytes(PfxCertificateFile));
#endif
                        }
                    }
                }

                return _SslCertificate;
            }
            set
            {
                _SslCertificate = value;
            }
        }

        /// <summary>
        /// PFX certificate filename.
        /// </summary>
        public string PfxCertificateFile { get; set; } = null;

        /// <summary>
        /// PFX certificate password.
        /// </summary>
        public string PfxCertificatePassword { get; set; } = null;

        /// <summary>
        /// Require mutual authentication.
        /// </summary>
        public bool MutuallyAuthenticate { get; set; } = false;

        /// <summary>
        /// Accept invalid certificates including self-signed and those that are unable to be verified.
        /// </summary>
        public bool AcceptInvalidAcertificates { get; set; } = true;

        private X509Certificate2 _SslCertificate = null;

        /// <summary>
        /// SSL settings.
        /// </summary>
        public SslSettings()
        {
        }
    }
}
