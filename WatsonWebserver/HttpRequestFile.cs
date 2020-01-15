using System;
using System.IO;
using System.Net.Mime;
using System.Text;

namespace Tfres
{
  public class HttpRequestFile
  {
    private StringBuilder _stb = new StringBuilder();

    public HttpRequestFile(string h1, string h2)
    {
      /* SAMPLE:
     ------WebKitFormBoundarybCVI7Rwr2zly1O5N
     Content-Disposition: form-data; name="files"; filename="underc.jpg"
     Content-Type: image/jpeg
     */

      // en passant cleaning
      var items = h1.Replace("Content-Disposition: ", "")
                    .Split(new[] { "; name=\"", "\"; filename=\"" },
                           StringSplitOptions.RemoveEmptyEntries);

      ContentDisposition = items[0];
      Name = items[1];
      Filename = items[2].Substring(0, items[2].Length - 1);
      ContentType = h2.Replace("Content-Type: ", "").Trim();
    }

    public string ContentDisposition { get; set; }
    public string Name { get; set; }
    public string Filename { get; set; }
    public string ContentType { get; set; }
    public byte[] Data { get; set; }

    internal void AddLine(string line) => _stb.AppendLine(line);

    internal void Finalize(Encoding encoding)
    {
      Data = encoding.GetBytes(_stb.ToString());
      _stb.Clear();
    }
  }
}
