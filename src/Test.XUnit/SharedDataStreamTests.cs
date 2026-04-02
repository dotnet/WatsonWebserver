namespace Test.XUnit
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the shared Data stream tests.
    /// </summary>
    public class SharedDataStreamTests
    {
        /// <summary>
        /// Verify reading Request.Data with a while-loop returns EOF after ContentLength bytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11DataStreamReadReturnsEof()
        {
            await Test.Shared.SharedDataStreamTests.TestDataStreamReadReturnsEofAsync();
        }

        /// <summary>
        /// Verify reading Request.Data asynchronously with ReadAsync returns EOF after ContentLength bytes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11DataStreamReadAsyncReturnsEof()
        {
            await Test.Shared.SharedDataStreamTests.TestDataStreamReadAsyncReturnsEofAsync();
        }

        /// <summary>
        /// Verify a large body can be read from Request.Data stream without hanging.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11DataStreamLargeBody()
        {
            await Test.Shared.SharedDataStreamTests.TestDataStreamLargeBodyAsync();
        }

        /// <summary>
        /// Verify that DataAsBytes still works correctly after the stream wrapping change.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11DataAsBytesStillWorks()
        {
            await Test.Shared.SharedDataStreamTests.TestDataAsBytesStillWorksAsync();
        }

        /// <summary>
        /// Verify that an empty POST body does not hang when reading from the Data stream.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11DataStreamEmptyBody()
        {
            await Test.Shared.SharedDataStreamTests.TestDataStreamEmptyBodyAsync();
        }

        /// <summary>
        /// Verify multiple sequential stream-read requests on a keep-alive connection succeed.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11DataStreamKeepAliveMultipleRequests()
        {
            await Test.Shared.SharedDataStreamTests.TestDataStreamKeepAliveMultipleRequestsAsync();
        }

        /// <summary>
        /// Verify ReadBodyAsync works correctly through ContentLengthStream.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ReadBodyAsyncThroughContentLengthStream()
        {
            await Test.Shared.SharedDataStreamTests.TestReadBodyAsyncThroughContentLengthStreamAsync();
        }

        /// <summary>
        /// Verify DataAsString works correctly through ContentLengthStream.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11DataAsStringThroughContentLengthStream()
        {
            await Test.Shared.SharedDataStreamTests.TestDataAsStringThroughContentLengthStreamAsync();
        }

        /// <summary>
        /// Verify WebSocket upgrade works on a server that also has stream-reading body routes.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11WebSocketUpgradeWithContentLengthStream()
        {
            await Test.Shared.SharedDataStreamTests.TestWebSocketUpgradeWithContentLengthStreamAsync();
        }

        /// <summary>
        /// Verify a POST with stream body read followed by a WebSocket upgrade on the same server.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11HttpBodyThenWebSocketOnSameServer()
        {
            await Test.Shared.SharedDataStreamTests.TestHttpBodyThenWebSocketOnSameServerAsync();
        }
    }
}
