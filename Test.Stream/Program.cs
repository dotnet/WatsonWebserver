using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test
{
    class Program
    {
        static string _Directory = "Uploads";
        static Server _Server;

        static void Main(string[] args)
        {
            if (!Directory.Exists(_Directory)) Directory.CreateDirectory(_Directory);
            _Server = new Server("127.0.0.1", 9000, false, DefaultRoute);
            _Server.Start();

            Console.ReadLine();
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            Console.WriteLine(ctx.Request.Method + " " + ctx.Request.RawUrlWithoutQuery);

            FileStream fs = null;
             
            switch (ctx.Request.Method)
            {
                case HttpMethod.GET:
                    long len = new System.IO.FileInfo("watson.jpg").Length;
                    fs = new FileStream("watson.jpg", FileMode.Open, FileAccess.Read);
                    ctx.Response.StatusCode = 200;
                    await ctx.Response.Send(len, fs);
                    return; 

                case HttpMethod.POST:
                    if (ctx.Request.RawUrlEntries == null || ctx.Request.RawUrlEntries.Count != 1)
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send("Bad request");
                        return;
                    }
                    else if (ctx.Request.Data == null || !ctx.Request.Data.CanRead)
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send("Bad request");
                        return;
                    }
                    else
                    {
                        fs = new FileStream(_Directory + "/" + ctx.Request.RawUrlEntries[0], FileMode.OpenOrCreate);
                        int bytesRead = 0;
                        byte[] buffer = new byte[2048];
                        while ((bytesRead = ctx.Request.Data.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                        }
                        fs.Close();
                        fs.Dispose();
                        ctx.Response.StatusCode = 201;
                        await ctx.Response.Send();
                        return;
                    }

                default:
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send("Bad request");
                    return;
            } 
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }
    }
}
