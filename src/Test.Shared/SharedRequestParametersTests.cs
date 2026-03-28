namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;

    /// <summary>
    /// Shared RequestParameters unit tests that can execute in both runners.
    /// </summary>
    public static class SharedRequestParametersTests
    {
        /// <summary>
        /// Get the shared RequestParameters test cases.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateSync("RequestParameters :: Indexer returns value", TestIndexerReturnsValue));
            tests.Add(CreateSync("RequestParameters :: Indexer returns null when missing", TestIndexerReturnsNullWhenMissing));
            tests.Add(CreateSync("RequestParameters :: GetInt returns value", TestGetIntReturnsValue));
            tests.Add(CreateSync("RequestParameters :: GetInt returns default when invalid", TestGetIntReturnsDefaultWhenInvalid));
            tests.Add(CreateSync("RequestParameters :: GetInt returns default when missing", TestGetIntReturnsDefaultWhenMissing));
            tests.Add(CreateSync("RequestParameters :: GetLong returns value", TestGetLongReturnsValue));
            tests.Add(CreateSync("RequestParameters :: GetDouble returns value", TestGetDoubleReturnsValue));
            tests.Add(CreateSync("RequestParameters :: GetDecimal returns value", TestGetDecimalReturnsValue));
            tests.Add(CreateSync("RequestParameters :: GetBool handles true", delegate { TestGetBoolHandlesVariousFormats("true", true); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles false", delegate { TestGetBoolHandlesVariousFormats("false", false); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles 1", delegate { TestGetBoolHandlesVariousFormats("1", true); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles 0", delegate { TestGetBoolHandlesVariousFormats("0", false); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles yes", delegate { TestGetBoolHandlesVariousFormats("yes", true); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles no", delegate { TestGetBoolHandlesVariousFormats("no", false); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles on", delegate { TestGetBoolHandlesVariousFormats("on", true); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles off", delegate { TestGetBoolHandlesVariousFormats("off", false); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles y", delegate { TestGetBoolHandlesVariousFormats("y", true); }));
            tests.Add(CreateSync("RequestParameters :: GetBool handles n", delegate { TestGetBoolHandlesVariousFormats("n", false); }));
            tests.Add(CreateSync("RequestParameters :: GetGuid returns value", TestGetGuidReturnsValue));
            tests.Add(CreateSync("RequestParameters :: GetGuid returns default when invalid", TestGetGuidReturnsDefaultWhenInvalid));
            tests.Add(CreateSync("RequestParameters :: GetDateTime returns value", TestGetDateTimeReturnsValue));
            tests.Add(CreateSync("RequestParameters :: GetEnum returns value", TestGetEnumReturnsValue));
            tests.Add(CreateSync("RequestParameters :: GetEnum is case insensitive", TestGetEnumIsCaseInsensitive));
            tests.Add(CreateSync("RequestParameters :: GetArray splits on separator", TestGetArraySplitsOnSeparator));
            tests.Add(CreateSync("RequestParameters :: GetArray supports custom separator", TestGetArrayCustomSeparator));
            tests.Add(CreateSync("RequestParameters :: GetArray returns empty when missing", TestGetArrayReturnsEmptyWhenMissing));
            tests.Add(CreateSync("RequestParameters :: Contains returns true when present", TestContainsReturnsTrueWhenPresent));
            tests.Add(CreateSync("RequestParameters :: Contains returns false when missing", TestContainsReturnsFalseWhenMissing));
            tests.Add(CreateSync("RequestParameters :: GetKeys returns all keys", TestGetKeysReturnsAllKeys));
            tests.Add(CreateSync("RequestParameters :: TryGetValue int success", TestTryGetValueIntSuccess));
            tests.Add(CreateSync("RequestParameters :: TryGetValue int failure", TestTryGetValueIntFailure));
            tests.Add(CreateSync("RequestParameters :: TryGetValue guid success", TestTryGetValueGuidSuccess));
            tests.Add(CreateSync("RequestParameters :: TryGetValue returns false when missing", TestTryGetValueReturnsFalseWhenMissing));
            tests.Add(CreateSync("RequestParameters :: Null collection handled gracefully", TestNullCollectionHandledGracefully));

            return tests.ToArray();
        }

        private static SharedNamedTestCase CreateSync(string name, Action action)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return new SharedNamedTestCase(name, delegate
            {
                action();
                return Task.CompletedTask;
            });
        }

        private static void TestIndexerReturnsValue()
        {
            NameValueCollection collection = new NameValueCollection { { "key", "value" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals("value", parameters["key"], "Indexer should return the stored value.");
        }

        private static void TestIndexerReturnsNullWhenMissing()
        {
            RequestParameters parameters = new RequestParameters(new NameValueCollection());
            AssertTrue(parameters["missing"] == null, "Indexer should return null when missing.");
        }

        private static void TestGetIntReturnsValue()
        {
            NameValueCollection collection = new NameValueCollection { { "page", "5" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(5, parameters.GetInt("page"), "GetInt should parse integers.");
        }

        private static void TestGetIntReturnsDefaultWhenInvalid()
        {
            NameValueCollection collection = new NameValueCollection { { "page", "abc" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(1, parameters.GetInt("page", 1), "GetInt should return the supplied default.");
        }

        private static void TestGetIntReturnsDefaultWhenMissing()
        {
            RequestParameters parameters = new RequestParameters(new NameValueCollection());
            AssertEquals(42, parameters.GetInt("missing", 42), "GetInt should return default when missing.");
        }

        private static void TestGetLongReturnsValue()
        {
            NameValueCollection collection = new NameValueCollection { { "id", "9999999999" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(9999999999L, parameters.GetLong("id"), "GetLong should parse long values.");
        }

        private static void TestGetDoubleReturnsValue()
        {
            NameValueCollection collection = new NameValueCollection { { "score", "3.14" } };
            RequestParameters parameters = new RequestParameters(collection);
            double actual = parameters.GetDouble("score");
            AssertTrue(Math.Abs(actual - 3.14d) < 0.01d, "GetDouble should parse double values.");
        }

        private static void TestGetDecimalReturnsValue()
        {
            NameValueCollection collection = new NameValueCollection { { "price", "19.99" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(19.99m, parameters.GetDecimal("price"), "GetDecimal should parse decimal values.");
        }

        private static void TestGetBoolHandlesVariousFormats(string value, bool expected)
        {
            NameValueCollection collection = new NameValueCollection { { "flag", value } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(expected, parameters.GetBool("flag"), "GetBool should parse common boolean formats.");
        }

        private static void TestGetGuidReturnsValue()
        {
            Guid expected = Guid.NewGuid();
            NameValueCollection collection = new NameValueCollection { { "id", expected.ToString() } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(expected, parameters.GetGuid("id"), "GetGuid should parse GUID values.");
        }

        private static void TestGetGuidReturnsDefaultWhenInvalid()
        {
            NameValueCollection collection = new NameValueCollection { { "id", "not-a-guid" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(Guid.Empty, parameters.GetGuid("id"), "GetGuid should return Guid.Empty when invalid.");
        }

        private static void TestGetDateTimeReturnsValue()
        {
            NameValueCollection collection = new NameValueCollection { { "date", "2025-06-15" } };
            RequestParameters parameters = new RequestParameters(collection);
            DateTime value = parameters.GetDateTime("date");
            AssertTrue(value.Year == 2025 && value.Month == 6 && value.Day == 15, "GetDateTime should parse date values.");
        }

        private static void TestGetEnumReturnsValue()
        {
            NameValueCollection collection = new NameValueCollection { { "method", "GET" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(HttpMethod.GET, parameters.GetEnum("method", HttpMethod.POST), "GetEnum should parse enum values.");
        }

        private static void TestGetEnumIsCaseInsensitive()
        {
            NameValueCollection collection = new NameValueCollection { { "method", "post" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertEquals(HttpMethod.POST, parameters.GetEnum("method", HttpMethod.GET), "GetEnum should ignore case.");
        }

        private static void TestGetArraySplitsOnSeparator()
        {
            NameValueCollection collection = new NameValueCollection { { "ids", "a,b,c" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertStringArrayEquals(new string[] { "a", "b", "c" }, parameters.GetArray("ids"), "GetArray should split comma-separated values.");
        }

        private static void TestGetArrayCustomSeparator()
        {
            NameValueCollection collection = new NameValueCollection { { "ids", "a|b|c" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertStringArrayEquals(new string[] { "a", "b", "c" }, parameters.GetArray("ids", '|'), "GetArray should support custom separators.");
        }

        private static void TestGetArrayReturnsEmptyWhenMissing()
        {
            RequestParameters parameters = new RequestParameters(new NameValueCollection());
            AssertEquals(0, parameters.GetArray("missing").Length, "GetArray should return an empty array when missing.");
        }

        private static void TestContainsReturnsTrueWhenPresent()
        {
            NameValueCollection collection = new NameValueCollection { { "key", "val" } };
            RequestParameters parameters = new RequestParameters(collection);
            AssertTrue(parameters.Contains("key"), "Contains should report present keys.");
        }

        private static void TestContainsReturnsFalseWhenMissing()
        {
            RequestParameters parameters = new RequestParameters(new NameValueCollection());
            AssertTrue(!parameters.Contains("missing"), "Contains should report missing keys.");
        }

        private static void TestGetKeysReturnsAllKeys()
        {
            NameValueCollection collection = new NameValueCollection { { "a", "1" }, { "b", "2" } };
            RequestParameters parameters = new RequestParameters(collection);
            string[] keys = parameters.GetKeys();
            AssertTrue(Array.IndexOf(keys, "a") >= 0 && Array.IndexOf(keys, "b") >= 0, "GetKeys should return all keys.");
        }

        private static void TestTryGetValueIntSuccess()
        {
            NameValueCollection collection = new NameValueCollection { { "count", "42" } };
            RequestParameters parameters = new RequestParameters(collection);
            bool success = parameters.TryGetValue<int>("count", out int result);
            AssertTrue(success, "TryGetValue<int> should succeed.");
            AssertEquals(42, result, "TryGetValue<int> should return the parsed value.");
        }

        private static void TestTryGetValueIntFailure()
        {
            NameValueCollection collection = new NameValueCollection { { "count", "abc" } };
            RequestParameters parameters = new RequestParameters(collection);
            bool success = parameters.TryGetValue<int>("count", out int result);
            AssertTrue(!success, "TryGetValue<int> should fail for invalid values.");
            AssertEquals(0, result, "Failed TryGetValue<int> should return default.");
        }

        private static void TestTryGetValueGuidSuccess()
        {
            Guid expected = Guid.NewGuid();
            NameValueCollection collection = new NameValueCollection { { "id", expected.ToString() } };
            RequestParameters parameters = new RequestParameters(collection);
            bool success = parameters.TryGetValue<Guid>("id", out Guid result);
            AssertTrue(success, "TryGetValue<Guid> should succeed.");
            AssertEquals(expected, result, "TryGetValue<Guid> should return the parsed GUID.");
        }

        private static void TestTryGetValueReturnsFalseWhenMissing()
        {
            RequestParameters parameters = new RequestParameters(new NameValueCollection());
            bool success = parameters.TryGetValue<string>("missing", out string result);
            AssertTrue(!success, "TryGetValue should fail when the key is missing.");
            AssertTrue(result == null, "Failed TryGetValue<string> should return null.");
        }

        private static void TestNullCollectionHandledGracefully()
        {
            RequestParameters parameters = new RequestParameters(null);
            AssertTrue(parameters["key"] == null, "Indexer should return null for a null collection.");
            AssertEquals(0, parameters.GetInt("key"), "GetInt should return the default for a null collection.");
            AssertTrue(!parameters.Contains("key"), "Contains should return false for a null collection.");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
            }
        }

        private static void AssertStringArrayEquals(string[] expected, string[] actual, string message)
        {
            if (expected == null && actual == null)
            {
                return;
            }

            if (expected == null || actual == null || expected.Length != actual.Length)
            {
                throw new InvalidOperationException(message);
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (!String.Equals(expected[i], actual[i], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(message);
                }
            }
        }
    }
}
