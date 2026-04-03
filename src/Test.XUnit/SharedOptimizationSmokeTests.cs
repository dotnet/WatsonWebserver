namespace Test.XUnit
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the shared optimization smoke-test subset.
    /// </summary>
    public class SharedOptimizationSmokeTests
    {
        /// <summary>
        /// Verify static route snapshots remain readable during concurrent mutation.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task StaticRouteSnapshotsRemainReadableDuringConcurrentMutation()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestStaticRouteSnapshotsAsync();
        }

        /// <summary>
        /// Verify the default serialization helper preserves pretty and compact JSON semantics.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task DefaultSerializationHelperPreservesPrettyAndCompactJson()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestDefaultSerializationHelperAsync();
        }

        /// <summary>
        /// Verify HTTP/1.1 cached response headers preserve dynamic fields.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1CachedResponseHeadersPreserveDynamicFields()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestHttp1CachedHeadersAsync();
        }

        /// <summary>
        /// Verify HTTP/1.1 context timing starts at request entry.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1ContextTimingStartsAtRequestEntry()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestContextTimestampStartsAtRequestEntryAsync();
        }

        /// <summary>
        /// Verify HTTP/1.1 stream send preserves direct passthrough body content.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1StreamSendPreservesDirectPassthroughBody()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestHttp1StreamSendAsync();
        }

        /// <summary>
        /// Verify HTTP/1.1 keep-alive pooling resets request state between requests.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http1KeepAlivePoolingResetsRequestState()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestHttp1KeepAlivePoolingAsync();
        }

        /// <summary>
        /// Verify HTTP/2 lazy header materialization stays coherent.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2LazyHeaderMaterializationStaysCoherent()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestHttp2LazyHeaderMaterializationAsync();
        }

        /// <summary>
        /// Verify HTTP/3 lazy header materialization stays coherent.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http3LazyHeaderMaterializationStaysCoherent()
        {
            await Test.Shared.SharedOptimizationSmokeTests.TestHttp3LazyHeaderMaterializationAsync();
        }
    }
}
