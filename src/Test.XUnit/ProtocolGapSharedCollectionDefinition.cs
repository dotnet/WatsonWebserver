namespace Test.XUnit
{
    using Xunit;

    /// <summary>
    /// Serializes the live shared protocol gap coverage so transport listeners do not compete for loopback ports.
    /// </summary>
    [CollectionDefinition("ProtocolGapSharedCoverage", DisableParallelization = true)]
    public sealed class ProtocolGapSharedCollectionDefinition
    {
    }
}
