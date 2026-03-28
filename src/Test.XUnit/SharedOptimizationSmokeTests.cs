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
    }
}
