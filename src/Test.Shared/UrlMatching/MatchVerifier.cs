#nullable enable
namespace Test.Shared.UrlMatching
{
    using System;
    using System.Collections.Specialized;
    using System.Linq;
    using UrlMatcher;

    /// <summary>
    /// Verifies a match outcome through every public entry point, so each case also proves the static, instance,
    /// and pre-parsed <see cref="UrlPattern"/> overloads agree.
    /// </summary>
    public static class MatchVerifier
    {
        /// <summary>
        /// Verify that a URL matches or does not match a pattern through all four string URL entry points:
        /// Matcher.Match(string, string), Matcher.Match(string, UrlPattern), new Matcher(string).Match(string),
        /// and new Matcher(string).Match(UrlPattern).
        /// </summary>
        /// <param name="url">URL.</param>
        /// <param name="pattern">Pattern.</param>
        /// <param name="expectMatch">Expected result.</param>
        /// <param name="expected">Expected captured values as alternating name, value pairs.  The collection must contain exactly these keys.  Ignored when expectMatch is false, since a failed match must return an empty collection.</param>
        /// <exception cref="AssertionFailedException">Thrown when any entry point disagrees with the expectation.</exception>
        public static void Verify(string url, string pattern, bool expectMatch, params string[] expected)
        {
            ValidatePairs(expected);

            UrlPattern parsed = UrlPattern.Parse(pattern);
            Matcher instance = new Matcher(url);

            bool r1 = Matcher.Match(url, pattern, out NameValueCollection v1);
            AssertOutcome("Matcher.Match(string, string)", url, pattern, expectMatch, expected, r1, v1);

            bool r2 = Matcher.Match(url, parsed, out NameValueCollection v2);
            AssertOutcome("Matcher.Match(string, UrlPattern)", url, pattern, expectMatch, expected, r2, v2);

            bool r3 = instance.Match(pattern, out NameValueCollection v3);
            AssertOutcome("new Matcher(string).Match(string)", url, pattern, expectMatch, expected, r3, v3);

            bool r4 = instance.Match(parsed, out NameValueCollection v4);
            AssertOutcome("new Matcher(string).Match(UrlPattern)", url, pattern, expectMatch, expected, r4, v4);
        }

        /// <summary>
        /// Verify that a URI matches or does not match a pattern through all four URI entry points:
        /// Matcher.Match(Uri, string), Matcher.Match(Uri, UrlPattern), new Matcher(Uri).Match(string),
        /// and new Matcher(Uri).Match(UrlPattern).
        /// </summary>
        /// <param name="uri">URI.</param>
        /// <param name="pattern">Pattern.</param>
        /// <param name="expectMatch">Expected result.</param>
        /// <param name="expected">Expected captured values as alternating name, value pairs.</param>
        /// <exception cref="AssertionFailedException">Thrown when any entry point disagrees with the expectation.</exception>
        public static void VerifyUri(Uri uri, string pattern, bool expectMatch, params string[] expected)
        {
            ValidatePairs(expected);

            string url = uri.ToString();
            UrlPattern parsed = UrlPattern.Parse(pattern);
            Matcher instance = new Matcher(uri);

            bool r1 = Matcher.Match(uri, pattern, out NameValueCollection v1);
            AssertOutcome("Matcher.Match(Uri, string)", url, pattern, expectMatch, expected, r1, v1);

            bool r2 = Matcher.Match(uri, parsed, out NameValueCollection v2);
            AssertOutcome("Matcher.Match(Uri, UrlPattern)", url, pattern, expectMatch, expected, r2, v2);

            bool r3 = instance.Match(pattern, out NameValueCollection v3);
            AssertOutcome("new Matcher(Uri).Match(string)", url, pattern, expectMatch, expected, r3, v3);

            bool r4 = instance.Match(parsed, out NameValueCollection v4);
            AssertOutcome("new Matcher(Uri).Match(UrlPattern)", url, pattern, expectMatch, expected, r4, v4);
        }

        /// <summary>
        /// Verify that an invalid pattern throws ArgumentException (exactly, not a derived type) from every entry point
        /// that parses a pattern, that the message names the pattern and contains each fragment, and that
        /// UrlPattern.TryParse returns false.
        /// </summary>
        /// <param name="url">URL to match against.  The outcome must not depend on it.</param>
        /// <param name="pattern">Invalid pattern.</param>
        /// <param name="fragments">Text that must appear in the exception message.</param>
        /// <exception cref="AssertionFailedException">Thrown when any entry point does not throw as expected.</exception>
        public static void VerifyThrows(string url, string pattern, params string[] fragments)
        {
            Uri uri = new Uri("http://127.0.0.1" + (url.StartsWith("/", StringComparison.Ordinal) ? url : "/" + url));
            Matcher instance = new Matcher(url);
            string context = "pattern [" + pattern + "] url [" + url + "]";

            ArgumentException[] thrown = new ArgumentException[]
            {
                Check.Throws<ArgumentException>(() => Matcher.Match(url, pattern, out NameValueCollection _), "Matcher.Match(string, string) " + context),
                Check.Throws<ArgumentException>(() => Matcher.Match(uri, pattern, out NameValueCollection _), "Matcher.Match(Uri, string) " + context),
                Check.Throws<ArgumentException>(() => instance.Match(pattern, out NameValueCollection _), "new Matcher(string).Match(string) " + context),
                Check.Throws<ArgumentException>(() => new UrlPattern(pattern), "new UrlPattern(string) " + context),
                Check.Throws<ArgumentException>(() => UrlPattern.Parse(pattern), "UrlPattern.Parse(string) " + context)
            };

            foreach (ArgumentException e in thrown)
            {
                Check.Contains(e.Message, "'" + pattern + "'", "exception message for " + context);
                Check.Equal("pattern", e.ParamName, "exception ParamName for " + context);
                foreach (string fragment in fragments)
                    Check.Contains(e.Message, fragment, "exception message for " + context);
            }

            bool parsed = UrlPattern.TryParse(pattern, out UrlPattern result);
            Check.False(parsed, "UrlPattern.TryParse returns false for " + context);
            Check.Null(result, "UrlPattern.TryParse result for " + context);
        }

        /// <summary>
        /// Render a collection for diagnostics.
        /// </summary>
        /// <param name="vals">Collection.</param>
        /// <returns>Text such as {id=42, name=joel}.</returns>
        public static string Describe(NameValueCollection? vals)
        {
            if (vals == null) return "null";
            return "{" + String.Join(", ", vals.AllKeys.Select(k => (k ?? "null") + "=" + (vals[k] ?? "null"))) + "}";
        }

        private static void ValidatePairs(string[] expected)
        {
            if (expected.Length % 2 != 0)
                throw new ArgumentException("Expected values must be alternating name, value pairs; got " + expected.Length + " items.", nameof(expected));
        }

        private static void AssertOutcome(string entryPoint, string url, string pattern, bool expectMatch, string[] expected, bool actual, NameValueCollection vals)
        {
            string context = entryPoint + " url [" + url + "] pattern [" + pattern + "] vals " + Describe(vals);

            if (vals == null) throw new AssertionFailedException(context + ": vals must never be null");

            if (actual != expectMatch)
                throw new AssertionFailedException(context + ": expected " + (expectMatch ? "a match" : "no match") + " but got " + (actual ? "a match" : "no match"));

            if (!expectMatch)
            {
                if (vals.Count != 0) throw new AssertionFailedException(context + ": a failed match must return an empty collection");
                return;
            }

            int expectedCount = expected.Length / 2;
            if (vals.Count != expectedCount)
                throw new AssertionFailedException(context + ": expected " + expectedCount + " captured key(s) but got " + vals.Count);

            for (int i = 0; i < expected.Length; i += 2)
            {
                string name = expected[i];
                string value = expected[i + 1];
                string? actualValue = vals[name];
                if (!String.Equals(value, actualValue, StringComparison.Ordinal))
                    throw new AssertionFailedException(context + ": expected [" + name + "] = [" + value + "] but got [" + (actualValue ?? "null") + "]");
            }
        }
    }
}
