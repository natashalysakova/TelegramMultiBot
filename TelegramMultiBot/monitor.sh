ssh {username}@{host} -p {port}

tail -f /proc/$(ps -ef | grep '[d]otnet ./TelegramMultiBot.dll' | awk '{print $2}')/fd/1