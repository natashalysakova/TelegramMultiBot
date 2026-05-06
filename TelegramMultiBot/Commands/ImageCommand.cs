using ImageDownloader;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramMultiBot.Database;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("image", "Download images from urls", false)]
    internal class ImageCommand : BaseCommand
    {
        private readonly IImageProcessHandler _imageProcessHandler;
        private readonly TelegramBotClient _botClient;
        private readonly ILogger<ImageCommand> _logger;
        private readonly BoberDbContext _boberDb;

        public ImageCommand(IImageProcessHandler imageProcessHandler, TelegramBotClient botClient, ILogger<ImageCommand> logger, BoberDbContext boberDb)
        {
            _imageProcessHandler = imageProcessHandler;
            _botClient = botClient;
            _logger = logger;
            _boberDb = boberDb;
        }

        public async override Task Handle(Message message)
        {
            var urls = ExtractUrls(message.Text);

            foreach (var url in urls)
            {
                await _imageProcessHandler.HandleImageDownload(url, new ReplyTo(message), CancellationToken.None);
            }
        }


        private IEnumerable<string> ExtractUrls(string? text)
        {
            var splitted = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            return splitted.Where(x => Uri.IsWellFormedUriString(x, UriKind.Absolute)).ToList();
        }
    }
}
