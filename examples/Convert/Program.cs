using System;
using System.IO;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Exceptions;

// Minimal end-to-end example.
//
//   API2CONVERT_API_KEY=your-key dotnet run --project examples/Convert -- path/to/file.docx pdf
//
// With no arguments it converts a sample remote JPG to PNG.
string input = args.Length > 0
    ? args[0]
    : "https://example-files.online-convert.com/raster%20image/jpg/example.jpg";
string target = args.Length > 1 ? args[1] : "png";

using var client = new Api2ConvertClient(""); // reads API2CONVERT_API_KEY

try
{
    ConversionResult result = await client.ConvertAsync(input, target);
    string path = await result.SaveAsync(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar);
    Console.WriteLine($"Saved: {path}");
}
catch (Api2ConvertException e)
{
    Console.Error.WriteLine($"Conversion failed: {e.Message}");
    Environment.Exit(1);
}
