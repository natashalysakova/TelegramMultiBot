// See https://aka.ms/new-console-template for more information
public record CallbackData(long chatId, string command, object data)
{
    public override string ToString()
    {
        return $"{chatId}|{command}|{data}";
    }

    public static CallbackData FromData(string data)
    {
        var info = data.Split('|');
        return new CallbackData(long.Parse(info[0]), info[1], info[2]);
    }
}