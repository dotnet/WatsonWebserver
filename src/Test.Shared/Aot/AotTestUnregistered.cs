namespace Test.Shared.Aot
{
    /// <summary>
    /// Application type deliberately absent from <see cref="AotTestJsonContext"/>.
    /// </summary>
    public class AotTestUnregistered
    {
        /// <summary>
        /// Value.
        /// </summary>
        public int Value { get; set; } = 1;

        /// <summary>
        /// Text form, used when the type is written without metadata.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            return "unregistered:" + Value.ToString();
        }
    }
}
