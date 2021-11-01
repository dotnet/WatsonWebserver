using System;

namespace Test.Routing
{
    class Program
    {
        static void Main(string[] args)
        {
            IntermediateServer srv = new IntermediateServer("localhost", 9000, false);
            RequestProcessor proc = new RequestProcessor(Console.WriteLine, srv);
            Console.WriteLine("Listening on http://localhost:9000, use either GET /static or GET /param/{id}");
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }
    }
}
