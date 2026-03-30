namespace Test.Parameters
{
    using System.Threading.Tasks;
    using WatsonWebserver;

    /// <summary>
    /// Instance API controller for parameter route tests.
    /// </summary>
    [RoutePrefix("MyApi")]
    public class MyApiController : ApiControllerBase
    {
        /// <summary>
        /// GET test with query parameters.
        /// </summary>
        [HttpGet]
        public async Task<MyClass> GetTest1(int x, int y)
        {
            return new MyClass()
            {
                X = x * y,
                Message = base.Context.Request.Url.Full
            };
        }

        /// <summary>
        /// GET test with HttpContext and query parameters.
        /// </summary>
        [HttpGet]
        public async Task<MyClass> GetTest2(HttpContext ctx, int x, int y)
        {
            return new MyClass()
            {
                X = x * y,
                Message = ctx.Request.Url.Full
            };
        }

        /// <summary>
        /// POST test with body deserialization.
        /// </summary>
        [HttpPost]
        public async Task<MyClass> PostTest1(MyClass mc)
        {
            return new MyClass()
            {
                X = mc.X * 2,
                Message = Context.Request.Url.Full
            };
        }

        /// <summary>
        /// POST test with HttpContext and body deserialization.
        /// </summary>
        [HttpPost]
        public async Task<MyClass> PostTest2(HttpContext ctx, MyClass mc)
        {
            return new MyClass()
            {
                X = mc.X * 2,
                Message = ctx.Request.Url.Full
            };
        }

        /// <summary>
        /// POST test with HttpContext, body deserialization, and query parameter.
        /// </summary>
        [HttpPost]
        public async Task<MyClass> PostTest3(HttpContext ctx, MyClass mc, int multiplier)
        {
            return new MyClass()
            {
                X = mc.X * multiplier,
                Message = ctx.Request.Url.Full
            };
        }
    }
}
