namespace Test.Aot
{
    /// <summary>
    /// Type deliberately absent from <see cref="AotJsonContext"/>, used to verify the failure path.
    /// </summary>
    public class AotUnregistered
    {
        /// <summary>
        /// Value.
        /// </summary>
        public int Value { get; set; } = 1;
    }
}
