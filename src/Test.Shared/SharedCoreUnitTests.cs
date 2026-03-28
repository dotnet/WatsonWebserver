namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Settings;

    /// <summary>
    /// Shared core unit-style tests that can execute in both runners.
    /// </summary>
    public static class SharedCoreUnitTests
    {
        /// <summary>
        /// Get the shared core unit tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateSync("ApiErrorResponse :: Status code derived from error", TestApiErrorResponseStatusCodeDerivedFromError));
            tests.Add(CreateSync("ApiErrorResponse :: Description auto-populated", TestApiErrorResponseDescriptionAutoPopulated));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for Success", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.Success, 200); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for Created", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.Created, 201); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for BadRequest", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.BadRequest, 400); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for NotAuthorized", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.NotAuthorized, 401); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for NotFound", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.NotFound, 404); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for RequestTimeout", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.RequestTimeout, 408); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for Conflict", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.Conflict, 409); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for InternalError", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.InternalError, 500); }));

            tests.Add(CreateSync("AuthResult :: IsPermitted true when success and permitted", TestAuthResultPermitted));
            tests.Add(CreateSync("AuthResult :: IsPermitted false when not found", TestAuthResultNotFound));
            tests.Add(CreateSync("AuthResult :: IsPermitted false when denied explicit", TestAuthResultDeniedExplicit));
            tests.Add(CreateSync("AuthResult :: Metadata propagated", TestAuthResultMetadataPropagated));

            tests.Add(CreateSync("TimeoutSettings :: Default is zero", TestTimeoutSettingsDefaultIsZero));
            tests.Add(CreateSync("TimeoutSettings :: Constructor sets timeout", TestTimeoutSettingsConstructorSetsTimeout));
            tests.Add(CreateSync("TimeoutSettings :: Negative timeout throws", TestTimeoutSettingsNegativeTimeoutThrows));

            tests.Add(CreateSync("WebserverException :: Status code maps from result", TestWebserverExceptionStatusCodeMapsFromResult));
            tests.Add(CreateSync("WebserverException :: Message custom message", TestWebserverExceptionMessageCustomMessage));
            tests.Add(CreateSync("WebserverException :: Message default message", TestWebserverExceptionMessageDefaultMessage));
            tests.Add(CreateSync("WebserverException :: Data can be set", TestWebserverExceptionDataCanBeSet));
            tests.Add(CreateSync("WebserverException :: Inner exception preserved", TestWebserverExceptionInnerExceptionPreserved));

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

        private static void TestApiErrorResponseStatusCodeDerivedFromError()
        {
            ApiErrorResponse response = new ApiErrorResponse { Error = ApiResultEnum.NotFound };
            AssertEquals(404, response.StatusCode, "Expected 404 for NotFound.");
        }

        private static void TestApiErrorResponseDescriptionAutoPopulated()
        {
            ApiErrorResponse response = new ApiErrorResponse { Error = ApiResultEnum.NotAuthorized };
            AssertTrue(!String.IsNullOrEmpty(response.Description), "Description should be auto-populated.");
        }

        private static void TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum error, int expectedCode)
        {
            ApiErrorResponse response = new ApiErrorResponse { Error = error };
            AssertEquals(expectedCode, response.StatusCode, "Unexpected status-code mapping.");
        }

        private static void TestAuthResultPermitted()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.Permitted
            };
            AssertTrue(result.IsPermitted(), "AuthResult should be permitted.");
        }

        private static void TestAuthResultNotFound()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.NotFound,
                AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
            };
            AssertTrue(!result.IsPermitted(), "AuthResult should not be permitted when not found.");
        }

        private static void TestAuthResultDeniedExplicit()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.DeniedExplicit
            };
            AssertTrue(!result.IsPermitted(), "AuthResult should not be permitted when explicitly denied.");
        }

        private static void TestAuthResultMetadataPropagated()
        {
            AuthResult result = new AuthResult { Metadata = "metadata" };
            AssertTrue(result.Metadata != null, "Metadata should be retained.");
        }

        private static void TestTimeoutSettingsDefaultIsZero()
        {
            TimeoutSettings settings = new TimeoutSettings();
            AssertEquals(TimeSpan.Zero, settings.DefaultTimeout, "Default timeout should be zero.");
        }

        private static void TestTimeoutSettingsConstructorSetsTimeout()
        {
            TimeoutSettings settings = new TimeoutSettings(TimeSpan.FromSeconds(30));
            AssertEquals(TimeSpan.FromSeconds(30), settings.DefaultTimeout, "Constructor should set the timeout.");
        }

        private static void TestTimeoutSettingsNegativeTimeoutThrows()
        {
            try
            {
                TimeoutSettings settings = new TimeoutSettings();
                settings.DefaultTimeout = TimeSpan.FromSeconds(-1);
                throw new InvalidOperationException("Expected DefaultTimeout setter to reject negative values.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private static void TestWebserverExceptionStatusCodeMapsFromResult()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.NotFound);
            AssertEquals(404, exception.StatusCode, "Unexpected WebserverException status code.");
        }

        private static void TestWebserverExceptionMessageCustomMessage()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.BadRequest, "Invalid input");
            AssertEquals("Invalid input", exception.Message, "Custom message should be preserved.");
        }

        private static void TestWebserverExceptionMessageDefaultMessage()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.NotFound);
            AssertEquals("Not found.", exception.Message, "Default message should be mapped from the result.");
        }

        private static void TestWebserverExceptionDataCanBeSet()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.Conflict);
            exception.Data = "name";
            AssertTrue(exception.Data != null, "Data payload should be assignable.");
        }

        private static void TestWebserverExceptionInnerExceptionPreserved()
        {
            Exception inner = new InvalidOperationException("inner");
            WebserverException exception = new WebserverException(ApiResultEnum.InternalError, "outer", inner);
            AssertTrue(ReferenceEquals(inner, exception.InnerException), "Inner exception should be preserved.");
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
    }
}
