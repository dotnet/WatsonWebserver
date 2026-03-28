namespace Test.XUnit
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the shared legacy smoke-test subset.
    /// </summary>
    public class SharedLegacySmokeTests
    {
        /// <summary>
        /// Verify a basic HTTP/1.1 GET request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11BasicGetRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11BasicGetAsync();
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 POST request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11BasicPostRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11BasicPostAsync();
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 POST body can be read and echoed by the route.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11BodyEchoRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11BodyEchoAsync();
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 PUT request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11BasicPutRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11BasicPutAsync();
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 DELETE request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11BasicDeleteRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11BasicDeleteAsync();
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 parameter route resolves and returns the parameterized value.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ParameterRouteRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11ParameterRouteAsync();
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 query-string route resolves and returns the query value.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11QueryStringRouteRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11QueryStringRouteAsync();
        }

        /// <summary>
        /// Verify a static content route returns the expected content.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11StaticContentRoute()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11StaticContentRouteAsync();
        }

        /// <summary>
        /// Verify request headers are echoed back with values preserved.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11HeaderEchoRequest()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11HeaderEchoAsync();
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 response delivers all chunks and advertises chunked transfer encoding.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ChunkedTransferResponse()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11ChunkedTransferEncodingAsync();
        }

        /// <summary>
        /// Verify chunked edge-case responses succeed.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ChunkedEdgeCaseResponse()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11ChunkedEdgeCasesAsync();
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>DataAsBytes</c>.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ChunkedRequestBodyViaDataAsBytes()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsBytesAsync();
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>DataAsString</c>.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ChunkedRequestBodyViaDataAsString()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsStringAsync();
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>ReadBodyAsync</c>.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ChunkedRequestBodyViaReadBodyAsync()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyReadBodyAsync();
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through manual chunk reads.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11ChunkedRequestBodyViaManualChunkRead()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyManualReadChunkAsync();
        }

        /// <summary>
        /// Verify a large binary chunked HTTP/1.1 request body round-trips successfully.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11LargeChunkedRequestBody()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11LargeChunkedRequestBodyAsync();
        }

        /// <summary>
        /// Verify an unmatched HTTP/1.1 route returns the default 404 response.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http11UnmatchedRouteReturnsNotFound()
        {
            await Test.Shared.SharedLegacySmokeTests.TestHttp11NotFoundRouteAsync();
        }
    }
}
