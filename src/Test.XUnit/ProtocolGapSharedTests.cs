namespace Test.XUnit
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the shared live protocol gap suite.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public class ProtocolGapSharedCoverageTests
    {
        /// <summary>
        /// Verify HTTP/2 writer serialization correctness.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http2WriterSerializationCorrectness()
        {
            await Test.Shared.ProtocolGapSharedTests.RunHttp2WriterSerializationCorrectnessAsync();
        }

        /// <summary>
        /// Verify HTTP/3 transport backpressure behavior.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http3TransportBackpressureBehavior()
        {
            await Test.Shared.ProtocolGapSharedTests.RunHttp3TransportBackpressureAsync();
        }

        /// <summary>
        /// Verify HTTP/3 sibling stream survival after abort.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task Http3SiblingStreamSurvivalAfterAbort()
        {
            await Test.Shared.ProtocolGapSharedTests.RunHttp3SiblingStreamSurvivalAsync();
        }

        /// <summary>
        /// Verify cross-protocol auth, session, and middleware parity.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task CrossProtocolAuthSessionEventParity()
        {
            await Test.Shared.ProtocolGapSharedTests.RunCrossProtocolAuthSessionEventParityAsync();
        }

        /// <summary>
        /// Verify mixed-version client interoperability.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task MixedVersionClientInteroperability()
        {
            await Test.Shared.ProtocolGapSharedTests.RunMixedVersionClientInteroperabilityAsync();
        }
    }
}
