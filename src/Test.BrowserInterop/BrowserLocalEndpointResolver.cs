namespace Test.BrowserInterop
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Resolves a browser-visible local endpoint.
    /// </summary>
    internal static class BrowserLocalEndpointResolver
    {
        /// <summary>
        /// Resolve the preferred browser endpoint.
        /// </summary>
        /// <returns>Endpoint selection.</returns>
        public static BrowserLocalEndpoint Resolve()
        {
            BrowserLocalEndpoint endpoint = new BrowserLocalEndpoint();

            try
            {
                string hostName = Dns.GetHostName();
                if (!String.IsNullOrEmpty(hostName))
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(hostName);
                    for (int i = 0; i < addresses.Length; i++)
                    {
                        IPAddress current = addresses[i];
                        if (current.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(current)) continue;
                        if (current.ToString().StartsWith("169.254.", StringComparison.OrdinalIgnoreCase)) continue;

                        endpoint.Hostname = current.ToString();
                        endpoint.BindAddress = current.ToString();
                        endpoint.IsNonLoopback = true;
                        return endpoint;
                    }
                }
            }
            catch (Exception)
            {
            }

            return endpoint;
        }
    }
}
