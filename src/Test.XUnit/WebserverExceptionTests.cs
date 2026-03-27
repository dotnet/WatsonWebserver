namespace Test.XUnit
{
    using System;
    using WatsonWebserver.Core;
    using Xunit;

    public class WebserverExceptionTests
    {
        [Fact]
        public void StatusCode_MapsFromResult()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.NotFound);
            Assert.Equal(404, ex.StatusCode);
        }

        [Fact]
        public void Message_CustomMessage()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.BadRequest, "Invalid input");
            Assert.Equal("Invalid input", ex.Message);
        }

        [Fact]
        public void Message_DefaultMessage()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.NotFound);
            Assert.Equal("Not found.", ex.Message);
        }

        [Fact]
        public void Data_CanBeSet()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.Conflict);
            ex.Data = new { Field = "name" };
            Assert.NotNull(ex.Data);
        }

        [Fact]
        public void InnerException_Preserved()
        {
            Exception inner = new InvalidOperationException("inner");
            WebserverException ex = new WebserverException(ApiResultEnum.InternalError, "outer", inner);
            Assert.Same(inner, ex.InnerException);
        }
    }
}
