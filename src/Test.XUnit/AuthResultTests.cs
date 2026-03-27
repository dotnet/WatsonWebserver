namespace Test.XUnit
{
    using WatsonWebserver.Core;
    using Xunit;

    public class AuthResultTests
    {
        [Fact]
        public void IsPermitted_TrueWhenSuccessAndPermitted()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.Permitted
            };
            Assert.True(result.IsPermitted());
        }

        [Fact]
        public void IsPermitted_FalseWhenNotFound()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.NotFound,
                AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
            };
            Assert.False(result.IsPermitted());
        }

        [Fact]
        public void IsPermitted_FalseWhenDeniedExplicit()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.DeniedExplicit
            };
            Assert.False(result.IsPermitted());
        }

        [Fact]
        public void Metadata_Propagated()
        {
            AuthResult result = new AuthResult { Metadata = new { UserId = 42 } };
            Assert.NotNull(result.Metadata);
        }
    }
}
