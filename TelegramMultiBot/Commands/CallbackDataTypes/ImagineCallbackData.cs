using TelegramMultiBot.ImageGenerators.Automatic1111;

namespace TelegramMultiBot.Commands.CallbackDataTypes
{
    class ImagineCallbackData : TelegramCallbackData<ImagineCommands>
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
                callback.Upscale = double.Parse(callback.Parameters[0].ToString());
            }
            return callback;
        }
    }
}
