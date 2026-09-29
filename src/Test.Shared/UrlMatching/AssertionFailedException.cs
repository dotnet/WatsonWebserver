#nullable enable
namespace Test.Shared.UrlMatching
{
    using System;

    /// <summary>
    /// Thrown by a test case when an assertion does not hold.
    /// </summary>
    public class AssertionFailedException : Exception
    {
        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="message">Description of the failed assertion, including the inputs that produced it.</param>
        public AssertionFailedException(string message) : base(message)
        {
        }
    }
}
