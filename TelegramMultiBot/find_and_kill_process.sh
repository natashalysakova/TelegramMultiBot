$ ps aux | grep -i "TelegramMultiBot"
$kill #

#kill
kill $(ps -ef | grep '[d]otnet ./Telegram' | awk '{print $2}')


#run
cd /volume1/share/Bots/TelegramMultiBot/
dotnet ./TelegramMultiBot.dll &
disown
