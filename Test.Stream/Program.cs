using System;
using System.IO;
using System.Text;
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
            _Server = new Server("127.0.0.1", 8000, false, RequestHandler);
            _Server.ReadInputStream = false;
            Console.ReadLine();
        }

        static HttpResponse RequestHandler(HttpRequest req)
        {
            Console.WriteLine(req.Method + " " + req.RawUrlWithoutQuery);

            FileStream fs = null;
             
            switch (req.Method)
            {
                case HttpMethod.GET:
                    long len = new System.IO.FileInfo("watson.jpg").Length;
                    fs = new FileStream("watson.jpg", FileMode.Open, FileAccess.Read);
                    return new HttpResponse(req, 200, null, "image/jpeg", len, fs);

                case HttpMethod.POST:
                    if (req.RawUrlEntries == null || req.RawUrlEntries.Count != 1)
                    {
                        return new HttpResponse(req, 400, null, "text/plain", Encoding.UTF8.GetBytes("Bad request"));
                    }
                    else if (req.DataStream == null || !req.DataStream.CanRead)
                    {
                        return new HttpResponse(req, 400, null, "text/plain", Encoding.UTF8.GetBytes("Bad request"));
                    }
                    else
                    {
                        fs = new FileStream(_Directory + "/" + req.RawUrlEntries[0], FileMode.OpenOrCreate);
                        int bytesRead = 0;
                        byte[] buffer = new byte[2048];
                        while ((bytesRead = req.DataStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                        }
                        fs.Close();
                        fs.Dispose();
                        return new HttpResponse(req, 201, null);
                    }

                default:
                    return new HttpResponse(req, 400, null, "text/plain", Encoding.UTF8.GetBytes("Bad request")); 
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
