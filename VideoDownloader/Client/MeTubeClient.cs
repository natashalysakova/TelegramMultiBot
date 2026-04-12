using System.Net.Http.Json;
using TelegramMultiBot.Database.Interfaces;

namespace VideoDownloader.Client;

public class MeTubeClient
{
    // POST at /add
    // POST at /delete
    // POST at /start
    // GET at /history
    HttpClient _httpClient = new HttpClient();
    public MeTubeClient(ISqlConfiguationService sqlConfigurationService)
    {
        var url = sqlConfigurationService.VideoDownloaderSettings.MeTubeUrl;
        if (!string.IsNullOrEmpty(url))
            _httpClient.BaseAddress = new Uri(url);
    }

    public async Task<MeTubeGenericResponse?> AddDownload(string url)
    {
        var requestBody = new MeTubeAddRequest
        {
            Url = url,
            Quality = "best",
            Format = "any",
            AutoStart = true,
            SplitByChapters = false
        };

        var content = JsonContent.Create(requestBody);
        var response = await _httpClient.PostAsync("/add", content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content
            .ReadFromJsonAsync<MeTubeGenericResponse>();
        return responseContent;
    }

    public async Task<MeTubeHistoryResponse?> GetHistory()
    {
        var response = await _httpClient.GetAsync("/history");
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content
            .ReadFromJsonAsync<MeTubeHistoryResponse>();
        return responseContent;
    }

    public async Task<MeTubeGenericResponse?> DeleteDownload(string id)
    {
        return await DeleteDownloads([id]);
    }

    public async Task<MeTubeGenericResponse?> DeleteDownloads(string[] ids)
    {
        // {"where":"done","ids":["https://www.youtube.com/watch?v=bxOLkxR6_6c"]}
        var requestBody = new MeTubeDeleteRequest
        {
            Where = "done",
            Ids = ids
        };
        var content = JsonContent.Create(requestBody);
        var response = await _httpClient.PostAsync("/delete", content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content
            .ReadFromJsonAsync<MeTubeGenericResponse>();
        return responseContent;
    }

    public async Task<MeTubeGenericResponse?> StartDownload(string[] ids)
    {
        var requestBody = new MeTubeStartRequest
        {
            Ids = ids
        };
        var content = JsonContent.Create(requestBody);
        var response = await _httpClient.PostAsync("/start", content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content
            .ReadFromJsonAsync<MeTubeGenericResponse>();
        return responseContent!;
    }

    public async Task<HttpResponseMessage> GetFileResponseAsync(string filename)
    {
        var response = await _httpClient.GetAsync($"/download/{Uri.EscapeDataString(filename)}", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
