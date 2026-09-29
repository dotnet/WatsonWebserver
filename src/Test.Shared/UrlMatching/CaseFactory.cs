#nullable enable
namespace Test.Shared.UrlMatching
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Builds Touchstone descriptors for the common case shapes.
    /// </summary>
    public static class CaseFactory
    {
        /// <summary>
        /// A case whose body is synchronous.
        /// </summary>
        /// <param name="suiteId">Suite id.</param>
        /// <param name="caseId">Case id.</param>
        /// <param name="displayName">Display name.</param>
        /// <param name="body">Case body.  Throws on failure.</param>
        /// <returns>Descriptor.</returns>
        public static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Action body)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    body();
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// A case whose body is asynchronous.
        /// </summary>
        /// <param name="suiteId">Suite id.</param>
        /// <param name="caseId">Case id.</param>
        /// <param name="displayName">Display name.</param>
        /// <param name="body">Case body.  Throws on failure.</param>
        /// <returns>Descriptor.</returns>
        public static TestCaseDescriptor CaseAsync(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: body);
        }

        /// <summary>
        /// A case asserting that the URL matches the pattern with exactly the expected captured values, through every string entry point.
        /// </summary>
        /// <param name="suiteId">Suite id.</param>
        /// <param name="caseId">Case id.</param>
        /// <param name="displayName">Display name.</param>
        /// <param name="url">URL.</param>
        /// <param name="pattern">Pattern.</param>
        /// <param name="expected">Expected captured values as alternating name, value pairs.</param>
        /// <returns>Descriptor.</returns>
        public static TestCaseDescriptor Match(string suiteId, string caseId, string displayName, string url, string pattern, params string[] expected)
        {
            return Case(suiteId, caseId, displayName, () => MatchVerifier.Verify(url, pattern, true, expected));
        }

        /// <summary>
        /// A case asserting that the URL does not match the pattern and that the collection is empty, through every string entry point.
        /// </summary>
        /// <param name="suiteId">Suite id.</param>
        /// <param name="caseId">Case id.</param>
        /// <param name="displayName">Display name.</param>
        /// <param name="url">URL.</param>
        /// <param name="pattern">Pattern.</param>
        /// <returns>Descriptor.</returns>
        public static TestCaseDescriptor NoMatch(string suiteId, string caseId, string displayName, string url, string pattern)
        {
            return Case(suiteId, caseId, displayName, () => MatchVerifier.Verify(url, pattern, false));
        }

        /// <summary>
        /// A case asserting that the pattern is rejected with ArgumentException by every entry point that parses a pattern.
        /// </summary>
        /// <param name="suiteId">Suite id.</param>
        /// <param name="caseId">Case id.</param>
        /// <param name="displayName">Display name.</param>
        /// <param name="url">URL.</param>
        /// <param name="pattern">Invalid pattern.</param>
        /// <param name="fragments">Text that must appear in the exception message.</param>
        /// <returns>Descriptor.</returns>
        public static TestCaseDescriptor Throws(string suiteId, string caseId, string displayName, string url, string pattern, params string[] fragments)
        {
            return Case(suiteId, caseId, displayName, () => MatchVerifier.VerifyThrows(url, pattern, fragments));
        }
    }
}
