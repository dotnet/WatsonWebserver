namespace Test.XUnit
{
    using Xunit;

    /// <summary>
    /// Non-parallel xUnit collection for shared legacy coverage parity execution.
    /// </summary>
    [CollectionDefinition("LegacyCoverageParity", DisableParallelization = true)]
    public class LegacyCoverageParityCollectionDefinition : ICollectionFixture<LegacyCoverageParityFixture>
    {
    }
}
