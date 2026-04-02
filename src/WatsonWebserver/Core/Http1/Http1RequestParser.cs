namespace WatsonWebserver.Core.Http1
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Parses HTTP/1.1 request lines and headers.
    /// </summary>
    public static class Http1RequestParser
    {
        /// <summary>
        /// Parse an HTTP/1.1 request header block.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="sourceIp">Source IP address.</param>
        /// <param name="sourcePort">Source port.</param>
        /// <param name="destinationIp">Destination IP address.</param>
        /// <param name="destinationPort">Destination port.</param>
        /// <param name="requestHeaderBytes">Request header bytes.</param>
        /// <returns>Parsed request metadata.</returns>
        public static Http1RequestMetadata Parse(WebserverSettings settings, string sourceIp, int sourcePort, string destinationIp, int destinationPort, byte[] requestHeaderBytes)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrEmpty(sourceIp)) throw new ArgumentNullException(nameof(sourceIp));
            if (sourcePort < 0 || sourcePort > 65535) throw new ArgumentOutOfRangeException(nameof(sourcePort));
            if (String.IsNullOrEmpty(destinationIp)) throw new ArgumentNullException(nameof(destinationIp));
            if (destinationPort < 0 || destinationPort > 65535) throw new ArgumentOutOfRangeException(nameof(destinationPort));
            if (requestHeaderBytes == null || requestHeaderBytes.Length < 1) throw new ArgumentNullException(nameof(requestHeaderBytes));

            Http1RequestMetadata metadata = new Http1RequestMetadata();
            metadata.Source = new SourceDetails(sourceIp, sourcePort);
            metadata.Destination = new DestinationDetails(destinationIp, destinationPort, settings.Hostname);
            metadata.SetUrlParts(settings.Ssl.Enable ? "https" : "http", settings.Hostname, settings.Port);

            List<Http1RequestMetadata.HeaderSlice> headerSlices = new List<Http1RequestMetadata.HeaderSlice>(16);
            ReadOnlySpan<byte> data = requestHeaderBytes;
            int position = 0;
            int lineNumber = 0;
            bool hostSeen = false;
            string contentLengthValue = null;

            while (TryReadLine(data, ref position, out int lineOffset, out int lineLength))
            {
                if (lineLength < 1)
                {
                    continue;
                }

                ReadOnlySpan<byte> line = data.Slice(lineOffset, lineLength);
                if (lineNumber == 0)
                {
                    ParseRequestLine(line, metadata);
                }
                else
                {
                    ParseHeaderLine(settings, requestHeaderBytes, lineOffset, line, metadata, headerSlices, ref hostSeen, ref contentLengthValue);
                    if (settings.IO.MaxHeaderCount > 0 && headerSlices.Count > settings.IO.MaxHeaderCount)
                    {
                        throw new IOException("Header count " + headerSlices.Count + " exceeds maximum allowed count " + settings.IO.MaxHeaderCount + ".");
                    }
                }

                lineNumber++;
            }

            ValidateParsedHeaders(metadata, hostSeen, contentLengthValue != null);
            metadata.InitializeParsedHeaders(requestHeaderBytes, headerSlices.ToArray());
            return metadata;
        }

        private static void ParseRequestLine(ReadOnlySpan<byte> requestLine, Http1RequestMetadata metadata)
        {
            requestLine = TrimSpacesAndNulls(requestLine);

            int methodSeparatorIndex = requestLine.IndexOf((byte)' ');
            if (methodSeparatorIndex < 1) throw new MalformedHttpRequestException("Request line does not contain at least three parts (method, raw URL, protocol/version).");

            int urlStartIndex = SkipSpaces(requestLine, methodSeparatorIndex + 1);
            if (urlStartIndex >= requestLine.Length) throw new MalformedHttpRequestException("Request line does not contain at least three parts (method, raw URL, protocol/version).");

            int urlSeparatorRelativeIndex = requestLine.Slice(urlStartIndex).IndexOf((byte)' ');
            if (urlSeparatorRelativeIndex < 0) throw new MalformedHttpRequestException("Request line does not contain at least three parts (method, raw URL, protocol/version).");

            int urlSeparatorIndex = urlStartIndex + urlSeparatorRelativeIndex;
            int protocolStartIndex = SkipSpaces(requestLine, urlSeparatorIndex + 1);
            if (protocolStartIndex >= requestLine.Length) throw new MalformedHttpRequestException("Request line does not contain at least three parts (method, raw URL, protocol/version).");

            ReadOnlySpan<byte> methodSpan = requestLine.Slice(0, methodSeparatorIndex);
            ReadOnlySpan<byte> urlSpan = requestLine.Slice(urlStartIndex, urlSeparatorIndex - urlStartIndex);
            ReadOnlySpan<byte> protocolSpan = requestLine.Slice(protocolStartIndex);

            string methodRaw = Encoding.ASCII.GetString(methodSpan);
            if (!HttpMethodParser.TryParse(methodRaw, out HttpMethod parsedMethod))
            {
                throw new MalformedHttpRequestException("HTTP method '" + methodRaw + "' is not recognized.");
            }

            metadata.Method = parsedMethod;
            metadata.MethodRaw = methodRaw;
            metadata.RawUrl = Encoding.ASCII.GetString(urlSpan);
            metadata.ProtocolVersion = Encoding.ASCII.GetString(protocolSpan);
            metadata.Keepalive = metadata.ProtocolVersion.Equals("HTTP/1.1", StringComparison.OrdinalIgnoreCase);
        }

        private static void ParseHeaderLine(
            WebserverSettings settings,
            byte[] requestHeaderBytes,
            int lineOffset,
            ReadOnlySpan<byte> headerLine,
            Http1RequestMetadata metadata,
            List<Http1RequestMetadata.HeaderSlice> headerSlices,
            ref bool hostSeen,
            ref string contentLengthValue)
        {
            if (IsWhitespace(headerLine[0]))
            {
                throw new MalformedHttpRequestException("Obsolete folded header lines are not supported.");
            }

            int separatorIndex = headerLine.IndexOf((byte)':');
            if (separatorIndex < 0)
            {
                throw new MalformedHttpRequestException("Header line '" + Encoding.ASCII.GetString(headerLine) + "' does not contain a valid name/value separator.");
            }

            ReadOnlySpan<byte> nameSpan = TrimSpacesAndNulls(headerLine.Slice(0, separatorIndex));
            ReadOnlySpan<byte> valueSpan = separatorIndex < (headerLine.Length - 1)
                ? TrimSpacesAndNulls(headerLine.Slice(separatorIndex + 1))
                : ReadOnlySpan<byte>.Empty;

            if (nameSpan.Length < 1)
            {
                throw new MalformedHttpRequestException("Header line '" + Encoding.ASCII.GetString(headerLine) + "' does not contain a header name.");
            }

            int nameOffset = lineOffset + GetTrimmedStartOffset(headerLine.Slice(0, separatorIndex));
            int valueOffset = separatorIndex < (headerLine.Length - 1)
                ? lineOffset + separatorIndex + 1 + GetTrimmedStartOffset(headerLine.Slice(separatorIndex + 1))
                : lineOffset + headerLine.Length;

            headerSlices.Add(new Http1RequestMetadata.HeaderSlice(nameOffset, nameSpan.Length, valueOffset, valueSpan.Length));

            if (ByteSpanEqualsIgnoreCase(nameSpan, "connection"))
            {
                if (ContainsTokenIgnoreCase(valueSpan, "close")) metadata.Keepalive = false;
                else if (ContainsTokenIgnoreCase(valueSpan, "keep-alive")) metadata.Keepalive = true;
            }
            else if (ByteSpanEqualsIgnoreCase(nameSpan, "keep-alive"))
            {
                metadata.Keepalive = true;
            }
            else if (ByteSpanEqualsIgnoreCase(nameSpan, "user-agent"))
            {
                metadata.Useragent = DecodeAscii(valueSpan);
            }
            else if (ByteSpanEqualsIgnoreCase(nameSpan, "host"))
            {
                if (hostSeen)
                {
                    throw new MalformedHttpRequestException("Multiple Host header values were received.");
                }

                hostSeen = true;
            }
            else if (ByteSpanEqualsIgnoreCase(nameSpan, "content-length"))
            {
                string value = DecodeAscii(valueSpan);
                long parsedContentLength = 0;

                try
                {
                    parsedContentLength = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    throw new MalformedHttpRequestException("Content-Length header value is invalid.", e);
                }

                if (parsedContentLength < 0)
                {
                    throw new MalformedHttpRequestException("Content-Length header value must not be negative.");
                }

                if (!String.IsNullOrEmpty(contentLengthValue)
                    && !String.Equals(contentLengthValue.Trim(), value, StringComparison.Ordinal))
                {
                    throw new MalformedHttpRequestException("Conflicting Content-Length header values were received.");
                }

                contentLengthValue = value;
                metadata.ContentLength = parsedContentLength;

                if (settings.IO.MaxRequestBodySize > 0 && metadata.ContentLength > settings.IO.MaxRequestBodySize)
                {
                    throw new IOException("Request body size " + metadata.ContentLength + " exceeds maximum allowed size " + settings.IO.MaxRequestBodySize + ".");
                }
            }
            else if (ByteSpanEqualsIgnoreCase(nameSpan, "content-type"))
            {
                metadata.ContentType = DecodeAscii(valueSpan);
            }
            else if (ByteSpanEqualsIgnoreCase(nameSpan, "transfer-encoding"))
            {
                if (ContainsTokenIgnoreCase(valueSpan, "chunked")) metadata.ChunkedTransfer = true;
                if (ContainsTokenIgnoreCase(valueSpan, "gzip")) metadata.Gzip = true;
                if (ContainsTokenIgnoreCase(valueSpan, "deflate")) metadata.Deflate = true;
            }
            else if (ByteSpanEqualsIgnoreCase(nameSpan, "expect"))
            {
                if (ContainsTokenIgnoreCase(valueSpan, "100-continue")) metadata.ExpectContinue = true;
            }
        }

        private static void ValidateParsedHeaders(Http1RequestMetadata metadata, bool hostSeen, bool contentLengthSeen)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            if (String.Equals(metadata.ProtocolVersion, "HTTP/1.1", StringComparison.OrdinalIgnoreCase)
                && !hostSeen)
            {
                throw new MalformedHttpRequestException("HTTP/1.1 requests must include exactly one Host header.");
            }

            if (metadata.ChunkedTransfer && contentLengthSeen)
            {
                throw new MalformedHttpRequestException("Transfer-Encoding: chunked must not be combined with Content-Length.");
            }
        }

        private static bool TryReadLine(ReadOnlySpan<byte> data, ref int startIndex, out int lineOffset, out int lineLength)
        {
            if (startIndex >= data.Length)
            {
                lineOffset = 0;
                lineLength = 0;
                return false;
            }

            int index = startIndex;
            while (index < data.Length && data[index] != (byte)'\r' && data[index] != (byte)'\n')
            {
                index++;
            }

            lineOffset = startIndex;
            lineLength = index - startIndex;

            if (index < data.Length && data[index] == (byte)'\r') index++;
            if (index < data.Length && data[index] == (byte)'\n') index++;

            startIndex = index;
            return true;
        }

        private static int SkipSpaces(ReadOnlySpan<byte> data, int startIndex)
        {
            int index = startIndex;
            while (index < data.Length && data[index] == (byte)' ')
            {
                index++;
            }

            return index;
        }

        private static ReadOnlySpan<byte> TrimSpacesAndNulls(ReadOnlySpan<byte> data)
        {
            int start = 0;
            int end = data.Length - 1;

            while (start <= end && (data[start] == (byte)' ' || data[start] == (byte)'\0'))
            {
                start++;
            }

            while (end >= start && (data[end] == (byte)' ' || data[end] == (byte)'\0'))
            {
                end--;
            }

            return start > end ? ReadOnlySpan<byte>.Empty : data.Slice(start, end - start + 1);
        }

        private static int GetTrimmedStartOffset(ReadOnlySpan<byte> data)
        {
            int offset = 0;
            while (offset < data.Length && (data[offset] == (byte)' ' || data[offset] == (byte)'\0'))
            {
                offset++;
            }

            return offset;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static string DecodeAscii(ReadOnlySpan<byte> data)
        {
            return data.Length > 0 ? Encoding.ASCII.GetString(data) : String.Empty;
        }

        private static bool ByteSpanEqualsIgnoreCase(ReadOnlySpan<byte> span, string value)
        {
            if (span.Length != value.Length) return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte current = span[i];
                char expected = value[i];

                if (current >= (byte)'A' && current <= (byte)'Z') current = (byte)(current + 32);
                if (expected >= 'A' && expected <= 'Z') expected = (char)(expected + 32);
                if (current != (byte)expected) return false;
            }

            return true;
        }

        private static bool ContainsTokenIgnoreCase(ReadOnlySpan<byte> span, string value)
        {
            if (span.Length < value.Length) return false;

            for (int i = 0; i <= (span.Length - value.Length); i++)
            {
                if (ByteSpanEqualsIgnoreCase(span.Slice(i, value.Length), value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
