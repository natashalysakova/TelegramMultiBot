// See https://aka.ms/new-console-template for more information
internal class LogUtil
{
    public static void Log(string message)
    {
#if DEBUG
        Console.WriteLine($"[{DateTime.Now}] [{Thread.CurrentThread.ManagedThreadId.ToString("0000")}] {message}");
#endif
    }

    public static void LogError(string message)
    {
        Console.WriteLine($"[{DateTime.Now}] [{Thread.CurrentThread.ManagedThreadId.ToString("0000")}] {message}");
    }
}