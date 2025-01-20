namespace WatsonWebserver.Lite
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal static class Common
    {
        internal static void ParseIpPort(string ipPort, out string ip, out int port)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ip = null;
            port = -1;

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                ip = ipPort.Substring(0, colonIndex);
                port = Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }
        }

        internal static string IpFromIpPort(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                return ipPort.Substring(0, colonIndex);
            }

            return null;
        }

        internal static int PortFromIpPort(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                return Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }

            return 0;
        }

    }
}
