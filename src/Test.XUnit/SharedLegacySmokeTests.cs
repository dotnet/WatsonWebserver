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
    }
}
