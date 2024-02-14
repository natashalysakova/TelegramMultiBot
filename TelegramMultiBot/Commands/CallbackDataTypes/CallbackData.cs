// See https://aka.ms/new-console-template for more information
using System;
using System.Runtime.CompilerServices;
using System.Text;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Commands.CallbackDataTypes;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Runtime.InteropServices.JavaScript.JSType;

public abstract class CallbackData<T> where T : struct
{
    public string? Id { get; set; }
    public string Command { get; set; }
    public T JobType { get; set; }
    object?[] _parameters;
    protected object?[] Parameters { get => _parameters; }

    protected CallbackData()
    {
        
    }

    protected void FillBaseFromString(string? data)
    {
        if (data is null)
            throw new ArgumentNullException("data");

        if (data.Count(x => x == '|') < 1)
        {
            throw new ArgumentException("data");
        }

        var info = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
        Command = info[0];
        JobType = Enum.Parse<T>(info[1]);
        if (info.Length > 2)
        {
            Id = info[2];
        }

        if (info.Length > 3)
        {
            _parameters = info[3..info.Length];
        }
        else
        {
            _parameters = Array.Empty<object>();
        }
    }



    public CallbackData(string command, T type, string? id, object?[] parameters)
    {
        Command = command;
        JobType = type;
        Id = id;
        _parameters = parameters;
    }
    
    public static implicit operator string(CallbackData<T> data)
    {
        return data.ToString();
    }

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder($"{Command}|{JobType}");
        if (Id != null)
        {
            stringBuilder.Append("|" + Id);
        }
        if (_parameters != null)
        {
            stringBuilder.Append("|");
            stringBuilder.Append(string.Join("|", _parameters));
        }
        return stringBuilder.ToString();
    }
}