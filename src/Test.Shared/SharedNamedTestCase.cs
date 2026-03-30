namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a named shared test case that can execute in either runner.
    /// </summary>
    public sealed class SharedNamedTestCase
    {
        private readonly Func<Task> _ExecuteAsync;

        /// <summary>
        /// Instantiate the test case.
        /// </summary>
        /// <param name="name">Test case name.</param>
        /// <param name="executeAsync">Asynchronous test delegate.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public SharedNamedTestCase(string name, Func<Task> executeAsync)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (executeAsync == null) throw new ArgumentNullException(nameof(executeAsync));

            Name = name;
            _ExecuteAsync = executeAsync;
        }

        /// <summary>
        /// Test case name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Execute the test case.
        /// </summary>
        /// <returns>Task.</returns>
        public Task ExecuteAsync()
        {
            return _ExecuteAsync();
        }
    }
}
