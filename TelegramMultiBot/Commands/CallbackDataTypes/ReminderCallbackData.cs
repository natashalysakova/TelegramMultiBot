namespace TelegramMultiBot.Commands.CallbackDataTypes
{
    class ReminderCallbackData : CallbackData<ReminderCommands>
    {
        public ReminderCallbackData(string command, ReminderCommands type, int id) : base(command, type, id.ToString(), null)
        {
        }
        public ReminderCallbackData(string command, ReminderCommands type) : base(command, type, null, null)
        {
        }

        private ReminderCallbackData()
        {

        }

        public static ReminderCallbackData FromString(string? data)
        {
            var callback = new ReminderCallbackData();
            callback.FillBaseFromString(data);
            return callback;
        }
    }
}
