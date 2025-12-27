namespace TelegramMultiBot.BackgroundServies;

public interface ISvitlobotClient
{
    Task<string> GetTimetable(string channelKey);
    Task<bool> UpdateTimetable(string channelKey, string timetableData);
}

public class SvitlobotClient : ISvitlobotClient
{
    private readonly HttpClient _httpClient;

    public SvitlobotClient()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string> GetTimetable(string channelKey)
    {
        var response = await _httpClient.GetAsync($"https://api.svitlobot.in.ua/website/getChannelTimetable?channel_key={channelKey}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> UpdateTimetable(string channelKey, string timetableData)
    {
        var response = await _httpClient.GetAsync($"https://api.svitlobot.in.ua/website/timetableEditEvent?&channel_key={channelKey}&timetableData={timetableData}");
        return response.IsSuccessStatusCode;
    }
}
