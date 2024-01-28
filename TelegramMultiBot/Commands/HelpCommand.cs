using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    internal class HelpCommand : ICommand
    {
        private readonly TelegramBotClient _client;

        public bool CanHandle(string textCommand)
        {
            return textCommand.ToLower().StartsWith("/help");
        }

        public HelpCommand(TelegramBotClient client)
        {
            _client = client;
        }

        public void Handle(Message message)
        {
            var help = 
@$"
⏰ *Reminder*
[/add](/add) \- add new reminder job for chat
[/delete](/delete) \- delete existing reminder
[/list](/list) \- show all active jobs

🛠 *Other*
[/cancel](/cancel) \- cancel current operation
[/help](/help) \- show help

🗒 Beside that *all* links to __twitter__ or __instagram__ will be formatted to show preview in the chat\.
This functionality can be laggy or don't work for some links cause it's heavily depended on 3rd party services\.";

            /*
            enclose your text in double asterisks to make it bold: **text** → text
            enclose your text in double underscore symbols to make it italic: __text__ → text
            enclose your text in triple backquote symbols to make it monospaced: “`text“` → text
            enclose your text in double tilde characters to make it strikethrough: ~~text~~ → text
            enclose your text in double vertical bars to make it hidden: ||text|| →
            */

            _client.SendTextMessageAsync(message.Chat.Id, help, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2);
        }

        public void HandleCallback(CallbackData callbackData)
        {
            return;
        }
    }
}
