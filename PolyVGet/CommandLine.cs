using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Spectre.Console;

namespace PolyVGet;

public static class CommandLine
{
    private static readonly Argument<string> ServiceArgument = new("service", "Service name") { Arity = ArgumentArity.ExactlyOne };
    private static readonly Argument<string> VideoIdArgument = new("videoId", "Video ID found in the token request or URL") { Arity = ArgumentArity.ExactlyOne };
    private static readonly Argument<string> CookieArgument = new("cookie", "Cookie required for requesting token") { Arity = ArgumentArity.ZeroOrOne };

    private static readonly Option SubtitlesOption = new Option<bool>(["--subtitles", "-s"], () => false, "Download the video's subtitles");
    private static readonly Option MaxThreadsOption = new Option<int>(["--max-threads", "-t"], () => 16, "Maximum number of threads");
    private static readonly Option OutputDirectoryOption = new Option<string>(["--output-directory", "-o"], () => ".", "Output directory");
    
    public static Parser GetBuilder()
    {
        var rootCommand = new RootCommand("Download a video protected by PolyV")
        {
            Handler = CommandHandler.Create<string, string, string, bool, int, string>(HandleCommandAsync)
        };

        rootCommand.AddArgument(ServiceArgument);
        rootCommand.AddArgument(VideoIdArgument);
        rootCommand.AddArgument(CookieArgument);

        rootCommand.AddOption(SubtitlesOption);
        rootCommand.AddOption(MaxThreadsOption);
        rootCommand.AddOption(OutputDirectoryOption);

        var servicesString = "Services:\n" + string.Join("\n", PolyVGet.GetServices().Select(service => $"  {service.Item1}: {service.Item2}").ToList());
        
        var builder = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseHelp(ctx =>
            {
                ctx.HelpBuilder.CustomizeLayout(_ => HelpBuilder.Default
                    .GetLayout()
                    .Append(_ => AnsiConsole.Write(servicesString)));
            })
            .Build();
        
        return builder;
    }
    
    private static async Task HandleCommandAsync(string service, string videoId, string cookie, bool subtitles, int maxThreads, string outputDirectory) 
    {
        var polyV = PolyVGet.WithService(service, videoId, cookie, outputDirectory);
        await polyV.Initialize();

        var qualities = polyV.GetQualities();
        
        var videoQuality = qualities.Count > 1 
            ? AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Select a video quality:")
                .AddChoices(polyV.GetQualities())) 
            : qualities.First();

        await polyV.Download(videoQuality, maxThreads, subtitles);
    }
}