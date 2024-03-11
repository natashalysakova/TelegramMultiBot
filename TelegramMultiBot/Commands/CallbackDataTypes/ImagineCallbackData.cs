﻿namespace TelegramMultiBot.Commands.CallbackDataTypes
{
    class ImagineCallbackData : CallbackData<ImagineCommands>
    {
        public double? Upscale { get; set; }

        public ImagineCallbackData(string command, ImagineCommands type, string? id = null, double? upscale = null) : base(command, type, id, new object?[] { upscale })
        {
            Upscale = upscale;
        }

        private ImagineCallbackData()
        {
        }

        public static ImagineCallbackData FromString(string? data)
        {
            var callback = new ImagineCallbackData();
            callback.FillBaseFromString(data);
            if (callback.Parameters.Length > 0)
            {
                var value = callback.Parameters[0] as string;
                if(!string.IsNullOrEmpty(value))
                    callback.Upscale = double.Parse(value);
            }
            return callback;
        }

    }
}
