using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
⏰ *Бобер\-нагадувач*
[/reminder](/reminder)
Бобер\-нагадувач може нагадувати про щось в чаті, згідно зі встановленим графіком нагадування \(CRON\)
Більше про CRON можна дізнатися за посиланням 
https\:\/\/crontab\.guru

🤖 *Бобер\-художник*
[/imagine](/imagine cat driving a bike) \- малює картинки за вказаним описом \(не завжди доступно\)\. 
>Додай `\#negative` і увесь текст після хештегу \(окрім інших хештегів\) буде сприйнято як негативний запит

>Використовуй `\#1`, `\#2`, `\#3` або `\#4` щоб вказати кількість зображень, які ти хочеш отримати

>Використовуй хештеги формату, щоб керувати форматом зображення
>1024x 1024: default \(1\:1\)
>768x 1344: `\#vertical` \(9\:16\)
>915x 1144: `\#portrait` \(4\:5\)
>1182x 886: `\#photo` \(4\:3\)
>1254x 836: `\#landscape` \(3\:2\)
>1365x 768: `\#widescreen` \(16\:9\)
>1564x 670: `\#cinematic` \(21\:9\)

>Якщо бажаєш попрацювати над деталями зображення \- використуй повторно його seed
>Додай хештег `\#seed\:\<SEED\>` де `\<SEED\>` \- це число, що міститься в описі попереднього рендера\. 
>Інші параметрові хештеги повинні бути тими самими, але можна змінювати деталі опису, та negative промпт

>Використовуй хештег `#model_` щоб обрати модель для рендеру\.
>Наразі доступні моделі\:
>`#model_dreamshaper`
>`#model_juggernaut`
>`#model_unstable`

>Додай `\#info` щоб побачити в описі параметри генерації

>На згенерованому зображенні також можна виконати 
>`Upscale x2` або `Upscale x4` \- Збільшення розподільної здатності зображення без перегенерації \(Швидко\)
>`Hires Fix` \- Перегенерація зображення з вищею розподільною здатністю, зображення може мати нові деталі і мінімально відрізнятися від оригінального \(Повільно\)
 
🛠 *Інше*
[/delete](/delete) \- використай цю команду у відповіді до бота, щоб той видалив своє повідомлення\.
[/cancel](/cancel) \- перервати поточний діалог
[/help](/help) \- показати допомогу

🗒 Окрім того, *усі* посилання на __twitter__ та __instagram__ будуть відформатовані, щоб показати превью в чат\.
Цей функціонал мже не працювати як потрібно або не працювати взагалі, бо він залежить від сторонніх сервісів\.
";


            _client.SendTextMessageAsync(message.Chat.Id, html, parseMode: ParseMode.MarkdownV2, messageThreadId: message.MessageThreadId, disableWebPagePreview: true);

            return Task.CompletedTask;
        }
    }
}
