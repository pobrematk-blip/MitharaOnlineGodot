using System.Globalization;

namespace Mithara.Server;

public static class Logger
{
    private static readonly string _logPath;
    private static readonly object _lock = new();

    static Logger()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"server_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
    }

    public static void Info(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        lock (_lock)
        {
            Console.WriteLine(line);
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }

    public static void Error(string msg, Exception? ex = null)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [ERRO] {msg}";
        if (ex != null)
            line += $"\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        lock (_lock)
        {
            Console.WriteLine(line);
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }
}
