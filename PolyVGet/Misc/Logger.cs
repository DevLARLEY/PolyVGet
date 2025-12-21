using Spectre.Console;

namespace PolyVGet.Misc;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Fatal
}

public static class Logger
{
    public static LogLevel LogLevel { get; set; } = LogLevel.Info;
    
    private static string GetTime() => DateTime.Now.ToString("HH:mm:ss");

    private static void Log(string message, string mode, string bgColor, string textColor = "white")
    {
        AnsiConsole.MarkupLineInterpolated($"{GetTime()} [white on {bgColor}]{mode}[/]: [{textColor}]{message}[/]");
    }

    public static void LogDebug(string message)
    {
        if (LogLevel == LogLevel.Debug)
        {
            Log(message, "Debug", "grey");
        }
    }

    public static void LogInfo(string message)
    {
        if (LogLevel <= LogLevel.Info)
        {
            Log(message, "Info", "darkgreen");
        }
    }

    public static void LogWarn(string message)
    {
        if (LogLevel <= LogLevel.Warn)
        {
            Log(message, "Warn", "darkorange3", "darkorange3");
        }
    }

    public static void LogFatal(string message)
    {
        if (LogLevel <= LogLevel.Fatal)
        {
            Log(message, "Fatal", "darkred_1", "red");
        }
    }
}