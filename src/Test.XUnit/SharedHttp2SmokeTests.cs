namespace Test.XUnit
{
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the shared HTTP/2 smoke-test subset.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    [Collection("SharedHttp2Smoke")]
    public class SharedHttp2SmokeTests
    {
        /// <summary>
        /// Verify a basic cleartext HTTP/2 GET request routes and returns a normal response.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2BasicGetRequest()
        {
            await Test.Shared.SharedHttp2SmokeTests.TestHttp2BasicGetAsync();
        }

        /// <summary>
        /// Verify a continuation header block routes and returns the expected response.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2ContinuationHeaderBlockRequest()
        {
            await Test.Shared.SharedHttp2SmokeTests.TestHttp2ContinuationHeaderBlockAsync();
        }

        /// <summary>
        /// Verify padded priority headers and padded DATA frames are accepted and routed correctly.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2PaddedPriorityHeadersAndDataRequest()
        {
            await Test.Shared.SharedHttp2SmokeTests.TestHttp2PaddedPriorityHeadersAndDataAsync();
        }

        /// <summary>
        /// Verify HTTP/2 response trailers are emitted and decoded correctly.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2ResponseTrailers()
        {
            await Test.Shared.SharedHttp2SmokeTests.TestHttp2ResponseTrailersAsync();
        }

        /// <summary>
        /// Verify an HTTP/2 chunked-style API response is surfaced as a normal streamed body.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2ChunkedApiResponse()
        {
            await Test.Shared.SharedHttp2SmokeTests.TestHttp2ChunkedApiResponseAsync();
        }

        /// <summary>
        /// Verify an HTTP/2 SSE API response is surfaced with the correct content type and event payload.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2ServerSentEventsResponse()
        {
            await Test.Shared.SharedHttp2SmokeTests.TestHttp2ServerSentEventsResponseAsync();
        }
    }
}
