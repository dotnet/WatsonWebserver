namespace Test.Shared.Aot
{
    using System.Collections.Generic;

    /// <summary>
    /// Application type used as an OpenAPI example value.
    /// </summary>
    public class AotTestExample
    {
        /// <summary>
        /// Display name.
        /// </summary>
        public string DisplayName { get; set; } = "example";

        /// <summary>
        /// Count.
        /// </summary>
        public int Count { get; set; } = 2;

        /// <summary>
        /// Tags.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string> { "a", "b" };
    }
}
