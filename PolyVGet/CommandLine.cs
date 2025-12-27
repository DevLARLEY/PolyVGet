using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices.Swift;
using PolyVGet.Misc;
using Spectre.Console;

namespace PolyVGet;

public static class CommandLine
{
    private enum Quality
    {
        Best,
        Medium, 
        Worst
    }

    private record QualityItem(int Index, string Label);
    
    private static readonly Argument<string> VideoUriArgument = new("videoUri", "Video URI (e.g. 4e75d3d997e444a48be3913c77d1c8d8_4)") { Arity = ArgumentArity.ExactlyOne };
    private static readonly Argument<string?> TokenArgument = new("token", "PolyV PlaySafe Token (e.g. 85fc0eb0-c3ce-4c80-84ef-dbb3aa1cab99-t0)") { Arity = ArgumentArity.ZeroOrOne };

    private static readonly Option QualityOption = new Option<Quality?>(["--quality", "-q"], () => null, "Set the video quality to download");
    private static readonly Option SubtitlesOption = new Option<bool>(["--subtitles", "-s"], () => false, "Download the video's subtitles");
    private static readonly Option MaxThreadsOption = new Option<int>(["--max-threads", "-t"], () => 4, "Maximum number of threads");
    private static readonly Option OutputDirectoryOption = new Option<string>(["--output-directory", "-o"], () => ".", "Output directory");
    private static readonly Option OverwriteOption = new Option<bool>(["--overwrite", "-y"], () => false, "Overwrite existing file");
    private static readonly Option LogLevelOption = new Option<LogLevel>(["--log-level", "-l"], () => LogLevel.Info, "Level of log output");
    
    public static Parser GetBuilder()
    {
        var rootCommand = new RootCommand("PolyV (Version 11, 12, 13, Mp4) Downloader written in C#")
        {
            Handler = CommandHandler.Create(HandleCommandAsync)
        };

        rootCommand.AddArgument(VideoUriArgument);
        rootCommand.AddArgument(TokenArgument);

        rootCommand.AddOption(QualityOption);
        rootCommand.AddOption(SubtitlesOption);
        rootCommand.AddOption(MaxThreadsOption);
        rootCommand.AddOption(OutputDirectoryOption);
        rootCommand.AddOption(OverwriteOption);
        rootCommand.AddOption(LogLevelOption);

        var builder = new CommandLineBuilder(rootCommand).UseDefaults().Build();
        
        return builder;
    }
    
    private static async Task HandleCommandAsync(Quality? quality, string videoUri, string? token, bool subtitles, int maxThreads, string outputDirectory, bool overwrite, LogLevel logLevel)
    {
        Logger.LogLevel = logLevel;
        
        try
        {
            var polyV = new PolyVGet(videoUri, token, outputDirectory, overwrite);
            await polyV.Initialize();

            if (polyV.PolyVClient.VideoJson.Resolution.Count == 1)
            {
                await polyV.Download(0, maxThreads, subtitles);
                return;
            }
 
            var qualities = Enumerable.Range(0, polyV.PolyVClient.VideoJson.Resolution.Count)
                .Select(i => new QualityItem(i, polyV.PolyVClient.QualityString(i)))
                .ToList();

            var index = quality switch
            {
                Quality.Best => qualities.Count - 1,
                Quality.Medium => qualities.Count / 2,
                Quality.Worst => 0,
                _ => AnsiConsole.Prompt(new SelectionPrompt<QualityItem>().AddChoices(qualities).UseConverter(q => q.Label)).Index
            };

            await polyV.Download(index, maxThreads, subtitles);
        }
        catch (Exception e)
        {
            if (Logger.LogLevel == LogLevel.Debug)
                throw;

            Logger.LogFatal(e.Message);
        }
    }
}