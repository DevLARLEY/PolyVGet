using System.CommandLine.Parsing;

namespace PolyVGet;

class Program
{
    static async Task Main(string[] args)
    {
        var rootCommand = CommandLine.GetBuilder();
        await rootCommand.InvokeAsync(args);
    }
}