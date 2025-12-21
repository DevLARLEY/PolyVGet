using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using PolyVGet.Misc;
using Spectre.Console;

namespace PolyVGet;

public static class CommandLine
{
    private static readonly Argument<string> VideoUriArgument = new("videoUri", "Video URI (e.g. 4e75d3d997e444a48be3913c77d1c8d8_4)") { Arity = ArgumentArity.ExactlyOne };
    private static readonly Argument<string> TokenArgument = new("token", "PolyV Token (e.g. 85fc0eb0-c3ce-4c80-84ef-dbb3aa1cab99-t0)") { Arity = ArgumentArity.ExactlyOne };

    private static readonly Option SubtitlesOption = new Option<bool>(["--subtitles", "-s"], () => false, "Download the video's subtitles");
    private static readonly Option MaxThreadsOption = new Option<int>(["--max-threads", "-t"], () => 4, "Maximum number of threads");
    private static readonly Option OutputDirectoryOption = new Option<string>(["--output-directory", "-o"], () => ".", "Output directory");
    private static readonly Option LogLevelOption = new Option<LogLevel>(["--log-level", "-l"], () => LogLevel.Info, "Level of log output");
    
    public static Parser GetBuilder()
    {
        var rootCommand = new RootCommand("PolyV (Version 11, 12, 13) Downloader written in C#")
        {
            Handler = CommandHandler.Create<string, string, bool, int, string, LogLevel>(HandleCommandAsync)
        };

        rootCommand.AddArgument(VideoUriArgument);
        rootCommand.AddArgument(TokenArgument);

        rootCommand.AddOption(SubtitlesOption);
        rootCommand.AddOption(MaxThreadsOption);
        rootCommand.AddOption(OutputDirectoryOption);
        rootCommand.AddOption(LogLevelOption);

        var builder = new CommandLineBuilder(rootCommand).UseDefaults().Build();
        
        return builder;
    }
    
    private static async Task HandleCommandAsync(string videoUri, string token, bool subtitles, int maxThreads, string outputDirectory, LogLevel logLevel)
    {
        Logger.LogLevel = logLevel;

        try
        {
            var polyV = new PolyVGet(videoUri, token, outputDirectory);
            await polyV.Initialize();

            var qualities = polyV.PolyVClient.VideoJson.Resolution;

            var videoQuality = qualities.Count > 1
                ? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select quality:").AddChoices(qualities))
                : qualities.First();

            await polyV.Download(videoQuality, maxThreads, subtitles);
        }
        catch (Exception e)
        {
            if (Logger.LogLevel == LogLevel.Debug)
                throw;

            Logger.LogFatal(e.Message);
        }
    }
}