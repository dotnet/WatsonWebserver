namespace Test.Loopback
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    
    static class Program
    {
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;

        static void Main(string[] args)
        {

            _Settings = new WebserverSettings
            {
                Hostname = _Hostname,
                Port = _Port
            };
            Console.WriteLine("Initializing webserver");
            _Server = new WatsonWebserver.Webserver(_Settings, DefaultRoute);

            Console.WriteLine("Listening on " + _Settings.Prefix);
            _Server.Start();

            Console.WriteLine("ENTER to exit");
            Console.ReadLine();
        }
         
        static async Task DefaultRoute(HttpContextBase ctx)
        { 
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send();
            return; 
        } 
    }
}


