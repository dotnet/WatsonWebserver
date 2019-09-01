using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test
{
    static class Program
    {
        static void Main()
        {
            List<string> hostnames = new List<string>();
            hostnames.Add("127.0.0.1");

            Server server = new Server(hostnames, 9000, false, DefaultRoute);

            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }
         
        static async Task DefaultRoute(HttpContext ctx)
        {
            try
            {
                if ((ctx.Request.Method == HttpMethod.POST
                    || ctx.Request.Method == HttpMethod.PUT)
                    && ctx.Request.Data != null
                    && ctx.Request.ChunkedTransfer)
                {
                    Console.WriteLine("Received request for " + ctx.Request.Method.ToString() + " " + ctx.Request.RawUrlWithoutQuery);

                    while (true)
                    {
                        Console.Write("Reading chunk: ");
                        Chunk chunk = await ctx.Request.ReadChunk();

                        Console.Write("[" + chunk.Length + " bytes] ");
                        if (chunk.Length > 0) Console.WriteLine(Encoding.UTF8.GetString(chunk.Data));
                        else Console.WriteLine("");

                        if (chunk.IsFinalChunk)
                        {
                            Console.WriteLine("*** Final chunk ***");
                            break;
                        }
                    }

                    ctx.Response.StatusCode = 200;
                    await ctx.Response.Send();
                    return;
                }
                else
                {
                    if (ctx.Request.RawUrlWithoutQuery.Equals("/img/watson.jpg"))
                    {
                        Console.WriteLine("- User requested /img/watson.jpg");
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "image/jpeg";
                        ctx.Response.ChunkedTransfer = true;
                        ctx.Response.Headers.Add("Cache-Control", "no-cache");

                        long fileSize = new FileInfo("./img/watson.jpg").Length;
                        Console.WriteLine("Sending file of size " + fileSize + " bytes");

                        long bytesSent = 0;

                        using (FileStream fs = new FileStream("./img/watson.jpg", FileMode.Open, FileAccess.Read))
                        {
                            byte[] buffer = new byte[16384];
                            long bytesRemaining = fileSize;

                            while (bytesRemaining > 0)
                            {
                                Thread.Sleep(500);
                                int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);

                                if (bytesRead > 0)
                                {
                                    bytesRemaining -= bytesRead;

                                    if (bytesRemaining > 0)
                                    {
                                        Console.WriteLine("- Sending chunk of size " + bytesRead);

                                        if (bytesRead == buffer.Length)
                                        {
                                            await ctx.Response.SendChunk(buffer);
                                        }
                                        else
                                        {
                                            byte[] temp = new byte[bytesRead];
                                            Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                                            await ctx.Response.SendChunk(temp);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("- Sending final chunk of size " + bytesRead);

                                        if (bytesRead == buffer.Length)
                                        {
                                            await ctx.Response.SendFinalChunk(buffer);
                                        }
                                        else
                                        {
                                            byte[] temp = new byte[bytesRead];
                                            Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                                            await ctx.Response.SendFinalChunk(temp);
                                        }
                                    }

                                    bytesSent += bytesRead;
                                }
                            }
                        }

                        Console.WriteLine("Sent " + bytesSent + " bytes");
                        return;
                    }
                    if (ctx.Request.RawUrlWithoutQuery.Equals("/txt/test.txt"))
                    {
                        Console.WriteLine("- User requested /txt/test.txt");
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        ctx.Response.ChunkedTransfer = true;
                        ctx.Response.Headers.Add("Cache-Control", "no-cache");

                        long fileSize = new FileInfo("./txt/test.txt").Length;
                        Console.WriteLine("Sending file of size " + fileSize + " bytes");

                        long bytesSent = 0;

                        using (FileStream fs = new FileStream("./txt/test.txt", FileMode.Open, FileAccess.Read))
                        {
                            byte[] buffer = new byte[16];
                            long bytesRemaining = fileSize;

                            while (bytesRemaining > 0)
                            {
                                Thread.Sleep(500);
                                int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);

                                if (bytesRead > 0)
                                {
                                    bytesRemaining -= bytesRead;

                                    if (bytesRemaining > 0)
                                    {
                                        Console.WriteLine("- Sending chunk of size " + bytesRead);

                                        if (bytesRead == buffer.Length)
                                        {
                                            await ctx.Response.SendChunk(buffer);
                                        }
                                        else
                                        {
                                            byte[] temp = new byte[bytesRead];
                                            Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                                            await ctx.Response.SendChunk(temp);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("- Sending final chunk of size " + bytesRead);

                                        if (bytesRead == buffer.Length)
                                        {
                                            await ctx.Response.SendFinalChunk(buffer);
                                        }
                                        else
                                        {
                                            byte[] temp = new byte[bytesRead];
                                            Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                                            await ctx.Response.SendFinalChunk(temp);
                                        }
                                    }

                                    bytesSent += bytesRead;
                                }
                            }
                        }

                        Console.WriteLine("Sent " + bytesSent + " bytes");
                        return;
                    }
                    else
                    {
                        ctx.Response.StatusCode = 200;
                        await ctx.Response.Send("Watson says try using GET /img/watson.jpg or /txt/test.txt to see what happens!");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        } 
    }
}
