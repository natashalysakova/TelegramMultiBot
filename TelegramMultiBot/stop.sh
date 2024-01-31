#!/bin/bash
set -e
kill $(ps -ef | grep '[d]otnet ./TelegramMultiBot.dll' | awk '{print $2}')