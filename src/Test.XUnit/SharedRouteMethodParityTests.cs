namespace Test.XUnit
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the shared cross-protocol route method parity suite.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    [Collection("RouteMethodParity")]
    public class SharedRouteMethodParityTests
    {
        /// <summary>
        /// GET static route returns 200 with body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task GetStaticRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunGetStaticRouteParityAsync();
        }

        /// <summary>
        /// POST static route echoes body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task PostStaticRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunPostStaticRouteParityAsync();
        }

        /// <summary>
        /// PUT static route echoes body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task PutStaticRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunPutStaticRouteParityAsync();
        }

        /// <summary>
        /// DELETE static route returns 200 across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task DeleteStaticRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunDeleteStaticRouteParityAsync();
        }

        /// <summary>
        /// PATCH static route echoes body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task PatchStaticRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunPatchStaticRouteParityAsync();
        }

        /// <summary>
        /// HEAD static route returns 200 with empty body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task HeadStaticRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunHeadStaticRouteParityAsync();
        }

        /// <summary>
        /// OPTIONS static route returns 200 with body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task OptionsStaticRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunOptionsStaticRouteParityAsync();
        }

        /// <summary>
        /// GET parameter route extracts path values across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task GetParameterRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunGetParameterRouteParityAsync();
        }

        /// <summary>
        /// POST parameter route extracts path values and echoes body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task PostParameterRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunPostParameterRouteParityAsync();
        }

        /// <summary>
        /// GET dynamic (regex) route matches across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task GetDynamicRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunGetDynamicRouteParityAsync();
        }

        /// <summary>
        /// GET content route is served across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task GetContentRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunGetContentRouteParityAsync();
        }

        /// <summary>
        /// GET API route returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task GetApiRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunGetApiRouteParityAsync();
        }

        /// <summary>
        /// POST API route with typed body returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task PostApiRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunPostApiRouteParityAsync();
        }

        /// <summary>
        /// PUT API route with typed body returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task PutApiRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunPutApiRouteParityAsync();
        }

        /// <summary>
        /// PATCH API route with typed body returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task PatchApiRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunPatchApiRouteParityAsync();
        }

        /// <summary>
        /// DELETE API route returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task DeleteApiRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunDeleteApiRouteParityAsync();
        }

        /// <summary>
        /// HEAD API route returns empty body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task HeadApiRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunHeadApiRouteParityAsync();
        }

        /// <summary>
        /// OPTIONS API route returns response across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task OptionsApiRouteParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunOptionsApiRouteParityAsync();
        }

        /// <summary>
        /// Unmatched route returns 404 across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task NotFoundParity()
        {
            await Test.Shared.SharedRouteMethodParityTests.RunNotFoundParityAsync();
        }
    }
}
