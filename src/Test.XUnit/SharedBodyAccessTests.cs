namespace Test.XUnit
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the comprehensive body access tests.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public class SharedBodyAccessTests
    {
        #region HTTP/1.1-Body-Access-Methods

        /// <summary>
        /// HTTP/1.1 POST body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PostDataStreamRead()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PostDataStreamReadAsync();
        }

        /// <summary>
        /// HTTP/1.1 POST body read via Data.ReadAsync (asynchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PostDataStreamReadAsync()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PostDataStreamReadAsyncAsync();
        }

        /// <summary>
        /// HTTP/1.1 POST body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PostDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PostDataAsBytesAsync();
        }

        /// <summary>
        /// HTTP/1.1 POST body read via DataAsString property.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PostDataAsString()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PostDataAsStringAsync();
        }

        /// <summary>
        /// HTTP/1.1 POST body read via ReadBodyAsync.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PostReadBodyAsync()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PostReadBodyAsyncAsync();
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PutDataStreamRead()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PutDataStreamReadAsync();
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PutDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PutDataAsBytesAsync();
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via DataAsString property.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PutDataAsString()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PutDataAsStringAsync();
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via ReadBodyAsync.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PutReadBodyAsync()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PutReadBodyAsyncAsync();
        }

        /// <summary>
        /// HTTP/1.1 PATCH body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PatchDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PatchDataAsBytesAsync();
        }

        /// <summary>
        /// HTTP/1.1 PATCH body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1PatchDataStreamRead()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1PatchDataStreamReadAsync();
        }

        /// <summary>
        /// HTTP/1.1 DELETE with body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1DeleteWithBodyDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1DeleteWithBodyDataAsBytesAsync();
        }

        #endregion

        #region HTTP/1.1-Body-Sizes

        /// <summary>
        /// HTTP/1.1 POST empty body via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1EmptyBodyStream()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1EmptyBodyStreamAsync();
        }

        /// <summary>
        /// HTTP/1.1 POST single-byte body via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1SingleByteBodyStream()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1SingleByteBodyStreamAsync();
        }

        /// <summary>
        /// HTTP/1.1 POST 128KB body via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1LargeBodyStream()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1LargeBodyStreamAsync();
        }

        /// <summary>
        /// HTTP/1.1 POST 128KB body via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1LargeBodyDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1LargeBodyDataAsBytesAsync();
        }

        #endregion

        #region HTTP/1.1-Keep-Alive

        /// <summary>
        /// HTTP/1.1 keep-alive: multiple POST requests with stream-read bodies on same connection.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1KeepAliveStreamReads()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1KeepAliveStreamReadsAsync();
        }

        /// <summary>
        /// HTTP/1.1 keep-alive: alternating between stream read and DataAsBytes on same connection.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1KeepAliveAlternatingAccess()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp1KeepAliveAlternatingAccessAsync();
        }

        #endregion

        #region HTTP/2-Body-Access-Methods

        /// <summary>
        /// HTTP/2 POST body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PostDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PostDataAsBytesAsync();
        }

        /// <summary>
        /// HTTP/2 POST body read via DataAsString.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PostDataAsString()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PostDataAsStringAsync();
        }

        /// <summary>
        /// HTTP/2 POST body read via ReadBodyAsync.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PostReadBodyAsync()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PostReadBodyAsyncAsync();
        }

        /// <summary>
        /// HTTP/2 POST body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PostDataStreamRead()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PostDataStreamReadAsync();
        }

        /// <summary>
        /// HTTP/2 POST body read via Data.ReadAsync (asynchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PostDataStreamReadAsync()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PostDataStreamReadAsyncAsync();
        }

        /// <summary>
        /// HTTP/2 PUT body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PutDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PutDataAsBytesAsync();
        }

        /// <summary>
        /// HTTP/2 PUT body read via DataAsString.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PutDataAsString()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PutDataAsStringAsync();
        }

        /// <summary>
        /// HTTP/2 PATCH body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PatchDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2PatchDataAsBytesAsync();
        }

        /// <summary>
        /// HTTP/2 DELETE with body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2DeleteWithBodyDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2DeleteWithBodyDataAsBytesAsync();
        }

        #endregion

        #region HTTP/2-Body-Sizes

        /// <summary>
        /// HTTP/2 POST empty body via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2EmptyBody()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2EmptyBodyAsync();
        }

        /// <summary>
        /// HTTP/2 POST large body (32KB) via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2LargeBodyDataAsBytes()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2LargeBodyDataAsBytesAsync();
        }

        /// <summary>
        /// HTTP/2 POST large body (32KB) via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2LargeBodyStreamRead()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2LargeBodyStreamReadAsync();
        }

        /// <summary>
        /// HTTP/2 POST body spanning multiple DATA frames via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2MultiFrameBody()
        {
            await Test.Shared.SharedBodyAccessTests.TestHttp2MultiFrameBodyAsync();
        }

        #endregion

        #region WebSocket-Body-Access

        /// <summary>
        /// WebSocket text message echo with small payload.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task WebSocketTextEcho()
        {
            await Test.Shared.SharedBodyAccessTests.TestWebSocketTextEchoAsync();
        }

        /// <summary>
        /// WebSocket binary message echo with small payload.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task WebSocketBinaryEcho()
        {
            await Test.Shared.SharedBodyAccessTests.TestWebSocketBinaryEchoAsync();
        }

        /// <summary>
        /// WebSocket text message echo with 2KB payload.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task WebSocketMediumText()
        {
            await Test.Shared.SharedBodyAccessTests.TestWebSocketMediumTextAsync();
        }

        /// <summary>
        /// WebSocket binary message echo with 3KB payload covering all byte values.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task WebSocketMediumBinary()
        {
            await Test.Shared.SharedBodyAccessTests.TestWebSocketMediumBinaryAsync();
        }

        /// <summary>
        /// WebSocket fragmented text message assembly.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task WebSocketFragmentedText()
        {
            await Test.Shared.SharedBodyAccessTests.TestWebSocketFragmentedTextAsync();
        }

        /// <summary>
        /// WebSocket fragmented binary message assembly.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task WebSocketFragmentedBinary()
        {
            await Test.Shared.SharedBodyAccessTests.TestWebSocketFragmentedBinaryAsync();
        }

        /// <summary>
        /// WebSocket interleaved text and binary messages on a single session.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task WebSocketInterleavedTextAndBinary()
        {
            await Test.Shared.SharedBodyAccessTests.TestWebSocketInterleavedTextAndBinaryAsync();
        }

        #endregion
    }
}
