using System.CommandLine.Parsing;

namespace PolyVGet;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        var rootCommand = CommandLine.GetBuilder();
        await rootCommand.InvokeAsync(args);
    }
}