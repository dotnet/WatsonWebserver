namespace Test.XUnit
{
    using Xunit;

    /// <summary>
    /// Collection definition for route method parity tests. Prevents parallel execution.
    /// </summary>
    [CollectionDefinition("RouteMethodParity")]
    public class SharedRouteMethodParityCollectionDefinition
    {
    }
}
