using Microsoft.Extensions.Configuration;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("help")]
    internal class HelpCommand(TelegramClientWrapper client, ISqlConfiguationService configuration) : BaseCommand
    {
        public override async Task Handle(Message message)
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

>SDXL
>Стандартний формат зображення "
+ $"{GenerationParams.defaultResolution[ModelVersion.SDXL].Width}x{GenerationParams.defaultResolution[ModelVersion.SDXL].Height}: \\({GenerationParams.defaultResolution[ModelVersion.SDXL].Ar}\\)"
+ @"
>Використовуй хештеги формату, щоб керувати форматом зображення
" +
string.Join("\n", GenerationParams.supportedResolutions[ModelVersion.SDXL].Select(x => $">{x.Width}x{x.Height}: `\\{x.Hashtag}` \\({x.Ar}\\)"))
+ @"

>1\.5
>Стандартний формат зображення "
+ $"{GenerationParams.defaultResolution[ModelVersion.OneFive].Width}x{GenerationParams.defaultResolution[ModelVersion.OneFive].Height}: \\({GenerationParams.defaultResolution[ModelVersion.OneFive].Ar}\\)"
+ @"
>Використовуй хештеги формату, щоб керувати форматом зображення
" +
string.Join("\n", GenerationParams.supportedResolutions[ModelVersion.OneFive].Select(x => $">{x.Width}x{x.Height}: `\\{x.Hashtag}` \\({x.Ar}\\)"))
+ @"

>Якщо бажаєш попрацювати над деталями зображення \- використуй повторно його seed
>Додай хештег `\#seed\:\<SEED\>` де `\<SEED\>` \- це число, що міститься в описі попереднього рендера\.
>Інші параметрові хештеги повинні бути тими самими, але можна змінювати деталі опису, та negative промпт

>Використовуй хештег `#model_` щоб обрати модель для рендеру\.
>Наразі доступні моделі\:
" +
string.Join("\n", configuration.Models.Select(x => $">`#model_{x.Name}`"))
+ @"

>Використовуй хештеги `#auto` або `#comfy` щоб обрати API генерації Automatic1111 або ComfyUI відповідно

>Додай `\#info` щоб побачити в описі параметри генерації

Викорисовуй команду [/buttons](/buttons) в реплаї до згенерованої картинки, щоб відобразити кнопки для маніпуляції з картинкою
>На згенерованому зображенні також можна виконати
>`Upscale x2` або `Upscale x4` \- Збільшення розподільної здатності зображення без перегенерації \(Швидко\)
>`Hires Fix` \- Перегенерація зображення з вищею розподільною здатністю, зображення може мати нові деталі і мінімально відрізнятися від оригінального \(Повільно\)
>Додати `він\'єтку` або `шум`, щоб придати зображенню реалізму\.

⏰ *Моніторинг графиків світла в Київський області*
`/monitor add\-dtek\-krem` \- Додати задачу моніторінгу до поточного чату\. 
`/monitor del\-dtek\-krem` \- Видалити задачу моніторінгу з поточного чату\. 
`/monitor list` \- Список активних задач\. 

🛠 *Інше*
[/delete](/delete) \- використай цю команду у відповіді до бота, щоб той видалив своє повідомлення\.
[/cancel](/cancel) \- перервати поточний діалог
[/help](/help) \- показати допомогу

🗒 Окрім того, *усі* посилання на __twitter__ та __instagram__ будуть відформатовані, щоб показати превью в чат\.
Цей функціонал може не працювати як потрібно або не працювати взагалі, бо він залежить від сторонніх сервісів\.
[/edit](/edit) \- відредагувати текст вашого повідомлення, в котрому бот заміняв URL
";

            
            await client.SendMessageAsync(message, html, parseMode: ParseMode.MarkdownV2, linkPreviewOptions: new LinkPreviewOptions() { IsDisabled = true });
            //_client.SendTextMessageAsync(message.Chat.Id, html, parseMode: ParseMode.MarkdownV2, messageThreadId: message.MessageThreadId, disableWebPagePreview: true);
        }
    }
}
/*
reminder - Бобер-Нагадувач
imagine - Бобер-художник
status - Статус доступних генераторів
buttons - Показати кнопки для згенерованого зображення
help - Допомога
delete - Видалити повідомлення від бота
cancel - Зупинити поточний діалог
 */