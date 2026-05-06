using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("image", "Download images from urls", false)]
    internal class ImageCommand : BaseCommand
    {
        public async override Task Handle(Message message)
        {
            var urls = ExtractUrls(message.Text);


        }

        private IEnumerable<string> ExtractUrls(string? text)
        {
            var splitted = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            return splitted.Where(x => Uri.IsWellFormedUriString(x, UriKind.Absolute)).ToList();
        }
    }
}
