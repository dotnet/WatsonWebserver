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
