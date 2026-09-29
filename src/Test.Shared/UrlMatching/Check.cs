#nullable enable
namespace Test.Shared.UrlMatching
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Assertion helpers.  Every failure throws <see cref="AssertionFailedException"/> with a descriptive message.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// Assert that a condition is true.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="message">Message describing the expectation.</param>
        /// <exception cref="AssertionFailedException">Thrown when the condition is false.</exception>
        public static void True(bool condition, string message)
        {
            if (!condition) throw new AssertionFailedException("Expected true: " + message);
        }

        /// <summary>
        /// Assert that a condition is false.
        /// </summary>
        /// <param name="condition">Condition.</param>
        /// <param name="message">Message describing the expectation.</param>
        /// <exception cref="AssertionFailedException">Thrown when the condition is true.</exception>
        public static void False(bool condition, string message)
        {
            if (condition) throw new AssertionFailedException("Expected false: " + message);
        }

        /// <summary>
        /// Assert that two values are equal.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <param name="message">Message describing the value being compared.</param>
        /// <exception cref="AssertionFailedException">Thrown when the values differ.</exception>
        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new AssertionFailedException(message + ": expected [" + Format(expected) + "] but got [" + Format(actual) + "]");
        }

        /// <summary>
        /// Assert that a value is null.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="message">Message describing the value.</param>
        /// <exception cref="AssertionFailedException">Thrown when the value is not null.</exception>
        public static void Null(object? value, string message)
        {
            if (value != null) throw new AssertionFailedException(message + ": expected null but got [" + Format(value) + "]");
        }

        /// <summary>
        /// Assert that a value is not null.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="message">Message describing the value.</param>
        /// <exception cref="AssertionFailedException">Thrown when the value is null.</exception>
        public static void NotNull(object? value, string message)
        {
            if (value == null) throw new AssertionFailedException(message + ": expected a value but got null");
        }

        /// <summary>
        /// Assert that a string contains a fragment (ordinal comparison).
        /// </summary>
        /// <param name="haystack">String to search.</param>
        /// <param name="needle">Fragment that must appear.</param>
        /// <param name="message">Message describing the string.</param>
        /// <exception cref="AssertionFailedException">Thrown when the fragment is missing.</exception>
        public static void Contains(string? haystack, string needle, string message)
        {
            if (haystack == null || haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
                throw new AssertionFailedException(message + ": expected [" + Format(haystack) + "] to contain [" + needle + "]");
        }

        /// <summary>
        /// Assert that an action throws an exception of exactly type T (derived types do not count).
        /// </summary>
        /// <typeparam name="T">Exact exception type.</typeparam>
        /// <param name="action">Action expected to throw.</param>
        /// <param name="message">Message describing the call.</param>
        /// <returns>The thrown exception.</returns>
        /// <exception cref="AssertionFailedException">Thrown when nothing is thrown or a different type is thrown.</exception>
        public static T Throws<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
            }
            catch (Exception e) when (e.GetType() == typeof(T))
            {
                return (T)e;
            }
            catch (Exception e)
            {
                throw new AssertionFailedException(message + ": expected " + typeof(T).Name + " but got " + e.GetType().Name + ": " + e.Message);
            }

            throw new AssertionFailedException(message + ": expected " + typeof(T).Name + " but nothing was thrown");
        }

        private static string Format(object? value)
        {
            if (value == null) return "null";
            return value.ToString() ?? "null";
        }
    }
}
