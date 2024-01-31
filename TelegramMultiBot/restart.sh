#!/bin/bash
set -e
kill $(ps -ef | grep '[d]otnet ./TelegramMultiBot.dll' | awk '{print $2}')
cd /volume1/share/Bots/TelegramMultiBot/
dotnet ./TelegramMultiBot.dll prod &
disown