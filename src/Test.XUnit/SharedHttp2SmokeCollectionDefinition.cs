namespace Test.XUnit
{
    using Xunit;

    /// <summary>
    /// Non-parallel xUnit collection for shared HTTP/2 smoke coverage.
    /// </summary>
    [CollectionDefinition("SharedHttp2Smoke", DisableParallelization = true)]
    public class SharedHttp2SmokeCollectionDefinition
    {
    }
}
