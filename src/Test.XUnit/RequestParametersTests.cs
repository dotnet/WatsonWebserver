namespace Test.XUnit
{
    using System;
    using System.Collections.Specialized;
    using WatsonWebserver.Core;
    using Xunit;

    public class RequestParametersTests
    {
        [Fact]
        public void Indexer_ReturnsValue()
        {
            NameValueCollection nvc = new NameValueCollection { { "key", "value" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal("value", p["key"]);
        }

        [Fact]
        public void Indexer_ReturnsNull_WhenMissing()
        {
            RequestParameters p = new RequestParameters(new NameValueCollection());
            Assert.Null(p["missing"]);
        }

        [Fact]
        public void GetInt_ReturnsValue()
        {
            NameValueCollection nvc = new NameValueCollection { { "page", "5" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(5, p.GetInt("page"));
        }

        [Fact]
        public void GetInt_ReturnsDefault_WhenInvalid()
        {
            NameValueCollection nvc = new NameValueCollection { { "page", "abc" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(1, p.GetInt("page", 1));
        }

        [Fact]
        public void GetInt_ReturnsDefault_WhenMissing()
        {
            RequestParameters p = new RequestParameters(new NameValueCollection());
            Assert.Equal(42, p.GetInt("missing", 42));
        }

        [Fact]
        public void GetLong_ReturnsValue()
        {
            NameValueCollection nvc = new NameValueCollection { { "id", "9999999999" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(9999999999L, p.GetLong("id"));
        }

        [Fact]
        public void GetDouble_ReturnsValue()
        {
            NameValueCollection nvc = new NameValueCollection { { "score", "3.14" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(3.14, p.GetDouble("score"), 2);
        }

        [Fact]
        public void GetDecimal_ReturnsValue()
        {
            NameValueCollection nvc = new NameValueCollection { { "price", "19.99" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(19.99m, p.GetDecimal("price"));
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("1", true)]
        [InlineData("0", false)]
        [InlineData("yes", true)]
        [InlineData("no", false)]
        [InlineData("on", true)]
        [InlineData("off", false)]
        [InlineData("y", true)]
        [InlineData("n", false)]
        public void GetBool_HandlesVariousFormats(string value, bool expected)
        {
            NameValueCollection nvc = new NameValueCollection { { "flag", value } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(expected, p.GetBool("flag"));
        }

        [Fact]
        public void GetGuid_ReturnsValue()
        {
            Guid expected = Guid.NewGuid();
            NameValueCollection nvc = new NameValueCollection { { "id", expected.ToString() } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(expected, p.GetGuid("id"));
        }

        [Fact]
        public void GetGuid_ReturnsDefault_WhenInvalid()
        {
            NameValueCollection nvc = new NameValueCollection { { "id", "not-a-guid" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(Guid.Empty, p.GetGuid("id"));
        }

        [Fact]
        public void GetDateTime_ReturnsValue()
        {
            NameValueCollection nvc = new NameValueCollection { { "date", "2025-06-15" } };
            RequestParameters p = new RequestParameters(nvc);
            DateTime result = p.GetDateTime("date");
            Assert.Equal(2025, result.Year);
            Assert.Equal(6, result.Month);
            Assert.Equal(15, result.Day);
        }

        [Fact]
        public void GetEnum_ReturnsValue()
        {
            NameValueCollection nvc = new NameValueCollection { { "method", "GET" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(HttpMethod.GET, p.GetEnum("method", HttpMethod.POST));
        }

        [Fact]
        public void GetEnum_IsCaseInsensitive()
        {
            NameValueCollection nvc = new NameValueCollection { { "method", "post" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.Equal(HttpMethod.POST, p.GetEnum("method", HttpMethod.GET));
        }

        [Fact]
        public void GetArray_SplitsOnSeparator()
        {
            NameValueCollection nvc = new NameValueCollection { { "ids", "a,b,c" } };
            RequestParameters p = new RequestParameters(nvc);
            string[] result = p.GetArray("ids");
            Assert.Equal(new[] { "a", "b", "c" }, result);
        }

        [Fact]
        public void GetArray_CustomSeparator()
        {
            NameValueCollection nvc = new NameValueCollection { { "ids", "a|b|c" } };
            RequestParameters p = new RequestParameters(nvc);
            string[] result = p.GetArray("ids", '|');
            Assert.Equal(new[] { "a", "b", "c" }, result);
        }

        [Fact]
        public void GetArray_ReturnsEmpty_WhenMissing()
        {
            RequestParameters p = new RequestParameters(new NameValueCollection());
            Assert.Empty(p.GetArray("missing"));
        }

        [Fact]
        public void Contains_ReturnsTrue_WhenPresent()
        {
            NameValueCollection nvc = new NameValueCollection { { "key", "val" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.True(p.Contains("key"));
        }

        [Fact]
        public void Contains_ReturnsFalse_WhenMissing()
        {
            RequestParameters p = new RequestParameters(new NameValueCollection());
            Assert.False(p.Contains("missing"));
        }

        [Fact]
        public void GetKeys_ReturnsAllKeys()
        {
            NameValueCollection nvc = new NameValueCollection { { "a", "1" }, { "b", "2" } };
            RequestParameters p = new RequestParameters(nvc);
            string[] keys = p.GetKeys();
            Assert.Contains("a", keys);
            Assert.Contains("b", keys);
        }

        [Fact]
        public void TryGetValue_Int_Success()
        {
            NameValueCollection nvc = new NameValueCollection { { "count", "42" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.True(p.TryGetValue<int>("count", out int result));
            Assert.Equal(42, result);
        }

        [Fact]
        public void TryGetValue_Int_Failure()
        {
            NameValueCollection nvc = new NameValueCollection { { "count", "abc" } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.False(p.TryGetValue<int>("count", out int _));
        }

        [Fact]
        public void TryGetValue_Guid_Success()
        {
            Guid expected = Guid.NewGuid();
            NameValueCollection nvc = new NameValueCollection { { "id", expected.ToString() } };
            RequestParameters p = new RequestParameters(nvc);
            Assert.True(p.TryGetValue<Guid>("id", out Guid result));
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TryGetValue_ReturnsFalse_WhenMissing()
        {
            RequestParameters p = new RequestParameters(new NameValueCollection());
            Assert.False(p.TryGetValue<string>("missing", out string _));
        }

        [Fact]
        public void NullCollection_HandledGracefully()
        {
            RequestParameters p = new RequestParameters(null);
            Assert.Null(p["key"]);
            Assert.Equal(0, p.GetInt("key"));
            Assert.False(p.Contains("key"));
        }
    }
}
