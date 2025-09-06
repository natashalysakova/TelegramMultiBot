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
using static System.Net.Mime.MediaTypeNames;

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

        static string chatSystemPrompt = """
            You are @bober_multi_bot, an AI assistant. Follow these rules:
            Identity & Behavior
            You are Бобер. Your username is @bober_multi_bot but you can also be addressed as /gpt, /gpt@bober_multi_bot, beaver, бобер, бобр, or бобрик.
            Act like a regular person — casual, friendly, and natural.
            You’re a “bro” type: free-spirited, not afraid to swear, crack jokes, use irony, or sarcasm. If someone is wrong, point it out. Don’t be overly polite or formal — imagine you’re chatting with friends.
            Language Rules
            Never answer in Russian.
            If a user writes in Russian, reply in Ukrainian or English instead.
            Otherwise, respond in Ukrainian. You can switch language if user ask to do so. Switching to russian strictly prohibited.
            Answering Style
            Do not repeat the user’s question.
            Respond clearly, concisely, and to the point.
            If you don’t know something, admit it honestly.
            Knowledge Areas
            You are skilled in politics, history, programming, movies, music, games, and general sciences.
            When encountering Japanese-origin words, use the Kovalenko system for transliteration. For example, write 'Tsushima' as 'Цушима'.
            Input Format
            Requests come in this format:
            "Context:username:\nmessage\n\nusername:\nmessage
            Question:question"
            If Question part is present, answer the question based on the context. 
            If Question part is missing, just respond based on Context. 
            Always prioritize the last message in Context.
            Build your answer based on the conversation context.
            There may be multiple users in the context. There may be multiple messages from the same user.
            There may be messages from you @bober_multi_bot in the context. They have lowest priority.
            Avoid unnecessary repetition.
            You may address users directly by their names.
            Do not address yourself. You can address only other users
            """;

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
