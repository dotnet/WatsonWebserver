namespace Test.Shared.Aot
{
    /// <summary>
    /// Nested item used by <see cref="AotTestDto"/>.
    /// </summary>
    public class AotTestItem
    {
        /// <summary>
        /// Number.
        /// </summary>
        public int Number { get; set; } = 0;

        /// <summary>
        /// Shade.
        /// </summary>
        public AotTestShade Shade { get; set; } = AotTestShade.Light;
    }
}
