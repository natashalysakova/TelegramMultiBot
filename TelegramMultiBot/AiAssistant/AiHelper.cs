using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.AiAssistant
{
    internal class AiHelper(ISqlConfiguationService configuationService, IConfiguration configuration)
    {
        static string systemPrompt =
@"Ти — корисний помічник зі штучним інтелектом, який узагальнює повідомлення чату. Зроби все можливе, щоб надати корисний короткий виклад того, що обговорювалося в наданих повідомленнях чату. У відповіді короткий абзац підсумовує основні моменти повідомлень чату.
Починай з: ""Ось короткий переказ повідомлень:"".
Ти отримуєш дані про повідомлення в форматі JSON, але не згадуй про це у відповіді. Не згадуй структуру JSON, не розголошуй жодних ідентифікаторів чи технічних деталей. Якщо була картинка, посилання чи відео — можна про це сказати. Якщо був репост з іншого чату — можеш це вказати. Використовуй лише факти, вказуй теми та користувачів, які брали участь у розмові. Якщо є консенсус — розкажи про це. Завжди використовуй мову розмови. 
Дотримуйся таких правил:

Структурованість: Згрупуй інформацію за темами, якщо їх кілька.
Лаконічність: Резюме має бути максимально стислим, без зайвих деталей.
Точність: Використовуй точні терміни, що відповідають змісту обговорення.
Об'єктивність: Уникай власних висновків чи оцінок, описуй тільки те, що було обговорено.
Ясність: Формулюй думки зрозуміло й доступно, щоб будь-який користувач міг легко зрозуміти резюме.""
";

        static string chatSystemPrompt = "Ти — корисний помічник зі штучним інтелектом, який допомагає користувачам. ТИ НЕ ЗНАЄШ РОСІЙСЬКОЇ МОВИ. Відповідай або українською, або англійською. Ніколи не відповідай російською. Відповідай на запитання чітко і по суті. Якщо не знаєш відповіді, чесно скажи, що не знаєш. Ти добре розбираєшся в політиці, історії, програмуванні і загальних науках. А ще ти бро, свій чувак, не боїшся матюкатися, травити жарти, іронізувати, використовувати сарказм. Якщо хтось не правий, то можеш на це вказати. Не обов'язково бути ввічливим. Уяви, що ти в колі друзів і веди себе ільно та невимушено. Не треба офіціозу.";


        internal async Task<string> Summarize(IEnumerable<ChatHistory>? history)
        {
            var token = Environment.GetEnvironmentVariable("LLM_TOKEN");
            if (token == null)
            {
                token = configuration.GetValue<string>("LLM_TOKEN");
            }

            var messages = JsonConvert.SerializeObject(history, new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            });
            var request = new LLMRequest("llama3.1", systemPrompt, messages, false, new LLMOptions(5192));
            var requestBody = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            using HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(configuationService.GeneralSettings.OllamaApiUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpRequestMessage httpRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Content = requestBody,
                RequestUri = new Uri(configuationService.GeneralSettings.OllamaApiUrl + "ollama/api/generate")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var responce = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            if (responce.IsSuccessStatusCode)
            {
                var json = await responce.Content.ReadAsStringAsync();
                var respObject = JsonConvert.DeserializeObject<LLMResponse>(json);

                if (respObject != null)
                {
                    return respObject.response;
                }
            }
            return "Не вдалося отримати відповідь";
        }

        internal async Task<string> Chat(string prompt)
        {
            var token = Environment.GetEnvironmentVariable("LLM_TOKEN");
            if (token == null)
            {
                token = configuration.GetValue<string>("LLM_TOKEN");
            }

            var request = new LLMRequest("gemma3:12b", chatSystemPrompt, prompt, false, new LLMOptions(5192));
            var requestBody = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            using HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(configuationService.GeneralSettings.OllamaApiUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpRequestMessage httpRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Content = requestBody,
                RequestUri = new Uri(configuationService.GeneralSettings.OllamaApiUrl + "ollama/api/generate")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var responce = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            if (responce.IsSuccessStatusCode)
            {
                var json = await responce.Content.ReadAsStringAsync();
                var respObject = JsonConvert.DeserializeObject<LLMResponse>(json);

                if (respObject != null)
                {
                    return respObject.response;
                }
            }
            return "Не вдалося отримати відповідь";
        }
    }

    record LLMRequest(string model, string system, string prompt, bool stream, LLMOptions options);
    record LLMOptions(int num_ctx);
}



public class LLMResponse
{
    public string model { get; set; }
    public string created_at { get; set; }
    public string response { get; set; }
    public bool done { get; set; }
    public string done_reason { get; set; }
    public int[] context { get; set; }
    public long total_duration { get; set; }
    public long load_duration { get; set; }
    public int prompt_eval_count { get; set; }
    public long prompt_eval_duration { get; set; }
    public int eval_count { get; set; }
    public long eval_duration { get; set; }
}
