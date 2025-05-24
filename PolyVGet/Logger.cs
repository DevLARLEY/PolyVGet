using Spectre.Console;

namespace PolyVGet;

public static class Logger
{
    private static string GetTime() => DateTime.Now.ToString("HH:mm:ss");
    
    private static void Log(string message, string color, string mode) => AnsiConsole.MarkupLineInterpolated($"{GetTime()} [white on {color}]{mode}[/]: {message}");
    
    public static void LogDebug(string message) => Log(message, "grey", "Debg");

    public static void LogInfo(string message) => Log(message, "darkgreen", "Info");
    public static void LogWarn(string message) => Log(message, "darkorange3", "Warn");

    public static void LogFatal(string message)
    {
        Log(message, "darkred_1", "Eror");
        Environment.Exit(0);
    }
}