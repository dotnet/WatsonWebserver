using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WatsonWebserver;

namespace Test.Routing
{
    public class RequestProcessor
    {
        private Action<string> _Logger = null;
        private IntermediateServer _Server = null;

        public RequestProcessor()
        {

        }

        public RequestProcessor(Action<string> logger, IntermediateServer server)
        {
            _Logger = logger;
            _Server = server;
        }

        [StaticRoute(HttpMethod.GET, "/static")]
        public async Task StaticRoute1(HttpContext ctx)
        {
            try
            {
                string msg = "Static route /static";
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send(msg);
                _Logger.Invoke(msg);

                /*
                 * 
                 * This gets thrown, because routing invokes a new instance of RequestProcessor where the parameterless constructor is used, therefore, _Logger is never set.
                 * 
                 * 
{
  "ClassName": "System.NullReferenceException",
  "Message": "Object reference not set to an instance of an object.",
  "Data": null,
  "InnerException": null,
  "HelpURL": null,
  "StackTraceString": "   at Test.Routing.RequestProcessor.ParamRoute1(HttpContext ctx) in C:\\Code\\Watson\\WatsonWebserver-4.2-main\\Test.Routing\\RequestProcessor.cs:line 52",
  "RemoteStackTraceString": null,
  "RemoteStackIndex": 0,
  "ExceptionMethod": null,
  "HResult": -2147467261,
  "Source": "Test.Routing",
  "WatsonBuckets": null
}
                 *
                 */

                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(SerializeJson(e, true));
            }
        }

        [ParameterRoute(HttpMethod.GET, "/param/{id}")]
        public async Task ParamRoute1(HttpContext ctx)
        {
            try
            {
                string msg = "Parameter route /param/" + ctx.Request.Url.Parameters["id"];
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send(msg);
                _Logger.Invoke(msg);

                /*
                 * 
                 * This gets thrown, because routing invokes a new instance of RequestProcessor where the parameterless constructor is used, therefore, _Logger is never set.
                 * 
                 * 
{
  "ClassName": "System.NullReferenceException",
  "Message": "Object reference not set to an instance of an object.",
  "Data": null,
  "InnerException": null,
  "HelpURL": null,
  "StackTraceString": "   at Test.Routing.RequestProcessor.ParamRoute1(HttpContext ctx) in C:\\Code\\Watson\\WatsonWebserver-4.2-main\\Test.Routing\\RequestProcessor.cs:line 52",
  "RemoteStackTraceString": null,
  "RemoteStackIndex": 0,
  "ExceptionMethod": null,
  "HResult": -2147467261,
  "Source": "Test.Routing",
  "WatsonBuckets": null
}
                 *
                 */

                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(SerializeJson(e, true));
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static string SerializeJson(object obj, bool pretty)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (obj == null) return null;
            string json;

            if (pretty)
            {
                json = JsonConvert.SerializeObject(
                  obj,
                  Newtonsoft.Json.Formatting.Indented,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                  });
            }
            else
            {
                json = JsonConvert.SerializeObject(obj,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc
                  });
            }

            return json;
        }
    }
}
