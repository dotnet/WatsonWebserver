namespace Test.BrowserInterop
{
    using System;
    using System.IO;

    /// <summary>
    /// Detects local Chromium browser availability.
    /// </summary>
    internal static class BrowserCapabilitiesDetector
    {
        /// <summary>
        /// Detect browser availability.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Browser capabilities.</returns>
        public static BrowserCapabilities Detect(string[] args)
        {
            BrowserCapabilities capabilities = new BrowserCapabilities();
            string explicitPath = GetExplicitPath(args);

            if (!String.IsNullOrEmpty(explicitPath))
            {
                if (File.Exists(explicitPath))
                {
                    capabilities.IsAvailable = true;
                    capabilities.ExecutablePath = explicitPath;
                    capabilities.BrowserName = Path.GetFileNameWithoutExtension(explicitPath);
                    capabilities.Detail = "Using explicit browser path.";
                    return capabilities;
                }

                capabilities.IsAvailable = false;
                capabilities.Detail = "Specified browser executable was not found: " + explicitPath;
                return capabilities;
            }

            string[] browserPaths = new string[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe"
            };

            for (int i = 0; i < browserPaths.Length; i++)
            {
                if (File.Exists(browserPaths[i]))
                {
                    capabilities.IsAvailable = true;
                    capabilities.ExecutablePath = browserPaths[i];
                    capabilities.BrowserName = Path.GetFileNameWithoutExtension(browserPaths[i]);
                    capabilities.Detail = "Detected installed Chromium browser.";
                    PopulateCertificateCapabilities(capabilities);
                    return capabilities;
                }
            }

            capabilities.IsAvailable = false;
            capabilities.Detail = "No supported Chromium browser was found. Install Microsoft Edge or Google Chrome, or pass --browser <path>.";
            PopulateCertificateCapabilities(capabilities);
            return capabilities;
        }

        private static void PopulateCertificateCapabilities(BrowserCapabilities capabilities)
        {
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));

            BrowserDevelopmentCertificateInfo certificateInfo = BrowserDevelopmentCertificateLocator.Find();
            capabilities.HasTrustedDevelopmentCertificate = certificateInfo.IsAvailable && certificateInfo.IsTrusted;
            capabilities.CertificateDetail = certificateInfo.Detail;
        }

        private static string GetExplicitPath(string[] args)
        {
            if (args == null || args.Length < 1) return null;

            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--browser", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
