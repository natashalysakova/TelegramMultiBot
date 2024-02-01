// See https://aka.ms/new-console-template for more information
using System.Runtime.CompilerServices;

public record CallbackData
{
    public string[] Data { get; set; }
    public string Command { get; set; }
    public string DataString { get => $"{Command}|{string.Join('|', Data)}"; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="chatId"></param>
    /// <param name="command"></param>
    /// <param name="data">data parameters are separated by '|' sign</param>

    public CallbackData(string command, string data)
    {
        Command = command;
        Data = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }

    public static CallbackData FromString(string data)
    {
        var info = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return new CallbackData(info[0], info[1]);
    }
}