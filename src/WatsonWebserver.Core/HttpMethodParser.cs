namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Fast HTTP method parser for common RFC method tokens.
    /// </summary>
    public static class HttpMethodParser
    {
        /// <summary>
        /// Attempt to parse an HTTP method token.
        /// </summary>
        /// <param name="methodRaw">Method token.</param>
        /// <param name="method">Parsed method.</param>
        /// <returns>True if recognized.</returns>
        public static bool TryParse(string methodRaw, out HttpMethod method)
        {
            if (String.IsNullOrEmpty(methodRaw))
            {
                method = HttpMethod.UNKNOWN;
                return false;
            }

            switch (methodRaw.Length)
            {
                case 3:
                    if (methodRaw.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.GET;
                        return true;
                    }

                    if (methodRaw.Equals("PUT", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.PUT;
                        return true;
                    }
                    break;
                case 4:
                    if (methodRaw.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.HEAD;
                        return true;
                    }

                    if (methodRaw.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.POST;
                        return true;
                    }
                    break;
                case 5:
                    if (methodRaw.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.PATCH;
                        return true;
                    }

                    if (methodRaw.Equals("TRACE", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.TRACE;
                        return true;
                    }
                    break;
                case 6:
                    if (methodRaw.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.DELETE;
                        return true;
                    }
                    break;
                case 7:
                    if (methodRaw.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.CONNECT;
                        return true;
                    }

                    if (methodRaw.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        method = HttpMethod.OPTIONS;
                        return true;
                    }
                    break;
            }

            method = HttpMethod.UNKNOWN;
            return false;
        }
    }
}
