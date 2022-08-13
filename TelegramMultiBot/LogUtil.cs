// See https://aka.ms/new-console-template for more information
class LogUtil
{
    public static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now}] [{Thread.CurrentThread.ManagedThreadId.ToString("0000")}] {message}");
    }
}