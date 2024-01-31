using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("help")]

    internal class HelpCommand : BaseCommand
    {
        private readonly TelegramBotClient _client;

        public HelpCommand(TelegramBotClient client)
        {
            _client = client;
        }

        public override Task Handle(Message message)
        {
            var html =
@"
⏰ *Reminder*
[/add](/add) \- add new reminder job for chat
[/delete](/delete) \- delete existing reminder
[/list](/list) \- show all active jobs

🤖 Image Generation
[/imagine](/imagine cat driving a bike) \- generate image using the prompt \(not always awailable\)\. 
>Use `\#xl` in your prompt to run slower but better SDXL model
>Use `\#file` to get original output without compression
 
🛠 *Other*
[/cancel](/cancel) \- cancel current operation
[/help](/help) \- show help

🗒 Beside that, *all* links to __twitter__ or __instagram__ will be formatted to show preview in the chat\.
This functionality can be laggy or don't work for some links cause it's heavily depended on 3rd party services\.
";


            _client.SendTextMessageAsync(message.Chat.Id, html, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2, messageThreadId: message.MessageThreadId);

            return Task.CompletedTask;
        }
    }
}
