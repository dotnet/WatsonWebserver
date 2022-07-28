using System;

namespace Test.Routing
{
    class Program
    {
        static string _Hostname = "127.0.0.1";
        static int _Port = 8080;

        static void Main(string[] args)
        {
            IntermediateServer srv = new IntermediateServer(_Hostname, _Port, false);
            RequestProcessor proc = new RequestProcessor(Console.WriteLine, srv);
            Console.WriteLine("Listening on http://" + _Hostname + ":" + _Port);
            Console.WriteLine("Use either GET /static or GET /param/{id}");
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }
    }
}
