namespace Test.XUnit
{
    using System;
    using WatsonWebserver.Core;
    using Xunit;

    public class ApiErrorResponseTests
    {
        [Fact]
        public void StatusCode_DerivedFromError()
        {
            ApiErrorResponse resp = new ApiErrorResponse { Error = ApiResultEnum.NotFound };
            Assert.Equal(404, resp.StatusCode);
        }

        [Fact]
        public void Description_AutoPopulated()
        {
            ApiErrorResponse resp = new ApiErrorResponse { Error = ApiResultEnum.NotAuthorized };
            Assert.False(String.IsNullOrEmpty(resp.Description));
        }

        [Theory]
        [InlineData(ApiResultEnum.Success, 200)]
        [InlineData(ApiResultEnum.Created, 201)]
        [InlineData(ApiResultEnum.BadRequest, 400)]
        [InlineData(ApiResultEnum.NotAuthorized, 401)]
        [InlineData(ApiResultEnum.NotFound, 404)]
        [InlineData(ApiResultEnum.RequestTimeout, 408)]
        [InlineData(ApiResultEnum.Conflict, 409)]
        [InlineData(ApiResultEnum.InternalError, 500)]
        public void StatusCode_MapsCorrectly(ApiResultEnum error, int expectedCode)
        {
            ApiErrorResponse resp = new ApiErrorResponse { Error = error };
            Assert.Equal(expectedCode, resp.StatusCode);
        }
    }
}
