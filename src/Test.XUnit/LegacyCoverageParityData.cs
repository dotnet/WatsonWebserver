namespace Test.XUnit
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Dynamic xUnit member-data for comprehensive automated coverage parity cases.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public static class LegacyCoverageParityData
    {
        /// <summary>
        /// Enumerates automated coverage case names from the shared legacy suite results.
        /// </summary>
        public static IEnumerable<object[]> Cases
        {
            get
            {
                LegacyCoverageParityFixture fixture = new LegacyCoverageParityFixture();
                List<string> names = new List<string>(fixture.Results.Keys);
                names.Sort(StringComparer.Ordinal);

                for (int i = 0; i < names.Count; i++)
                {
                    yield return new object[] { names[i] };
                }
            }
        }
    }
}
