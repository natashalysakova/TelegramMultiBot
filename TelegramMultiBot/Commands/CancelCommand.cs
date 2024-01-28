﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    internal class CancelCommand : ICommand
    {
        private readonly TelegramBotClient _client;
        private readonly DialogManager _dialogManager;

        public bool CanHandle(string textCommand)
        {
            return textCommand.ToLower().StartsWith("/cancel");
        }

        public CancelCommand(TelegramBotClient client, DialogManager dialogManager)
        {
            _client = client;
            _dialogManager = dialogManager;
        }

        public async void Handle(Message message)
        {
            var activeDialog = _dialogManager[message.Chat.Id];
            if(activeDialog != null)
            {
                _dialogManager.Remove(activeDialog);
                await _client.SendTextMessageAsync(message.Chat.Id, "Операцію зупинено", disableNotification: true);
            }
            else
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "Нема активної операції", disableNotification: true);
            }
        }

        public void HandleCallback(CallbackData callbackData)
        {
            return;
        }
    }
}
