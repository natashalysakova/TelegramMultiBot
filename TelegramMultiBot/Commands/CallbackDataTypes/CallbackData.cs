// See https://aka.ms/new-console-template for more information
using System.Text;

namespace TelegramMultiBot.Commands.CallbackDataTypes
{
    public abstract class CallbackData<T> where T : struct
    {
        public string JobId { get; set; }
        public string Command { get; set; }
        public T JobType { get; set; }

        private object?[] _parameters;
        protected object?[] Parameters { get => _parameters; }

        protected CallbackData()
        {
            _parameters = [];
            JobId = string.Empty;
            Command = "unknown";
        }

        protected void FillBaseFromString(string? data)
        {
            ArgumentNullException.ThrowIfNull(data);

            if (!data.Any(x => x == '|'))
            {
                throw new ArgumentException("Invalid data", nameof(data));
            }

            var info = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
            Command = info[0];
            JobType = Enum.Parse<T>(info[1]);
            if (info.Length > 2)
            {
                JobId = info[2];
            }

            if (info.Length > 3)
            {
                _parameters = info[3..info.Length];
            }
        }

        public CallbackData(string command, T type, string id, params object?[] parameters)
        {
            Command = command;
            JobType = type;
            JobId = id;
            _parameters = parameters;
        }

        public static implicit operator string(CallbackData<T> data)
        {
            return data.ToString();
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder($"{Command}|{JobType}");
            if (JobId != null)
            {
                _ = stringBuilder.Append("|" + JobId);
            }
            if (_parameters != null)
            {
                _ = stringBuilder.Append('|');
                _ = stringBuilder.Append(string.Join("|", _parameters));
            }
            return stringBuilder.ToString();
        }
    }
}