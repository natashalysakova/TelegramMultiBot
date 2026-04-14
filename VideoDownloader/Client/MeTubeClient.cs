using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;

namespace VideoDownloader.Client;

public class MeTubeClient
{
    // POST at /add
    // POST at /delete
    // POST at /start
    // GET at /history
    HttpClient _httpClient;
    private readonly ISqlConfiguationService _sqlConfigurationService;
    private readonly ILogger<MeTubeClient> _logger;
    private readonly string defaultUrl = "http://metube:8081";

    public MeTubeClient(ISqlConfiguationService sqlConfigurationService, ILogger<MeTubeClient> logger, IHttpClientFactory httpClientFactory)
    {
        _sqlConfigurationService = sqlConfigurationService;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();

        var url = sqlConfigurationService.VideoDownloaderSettings.MeTubeUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("MeTube URL is not configured. Using default {DefaultUrl}", defaultUrl);
            url = defaultUrl;
        }

        var baseUrl = new Uri(url);
        _httpClient.BaseAddress = baseUrl;
    }

    public async Task<MeTubeGenericResponse?> AddDownload(string url, string idPrefix)
    {
        var settings = _sqlConfigurationService.VideoDownloaderSettings;
        var requestBody = new MeTubeAddRequest
        {
            Url = url,
            Quality = settings.VideoFormat == VideoFormat.iosCompatible ? VideoQuality.best.GetDescription() : settings.VideoQuality.GetDescription(),
            Format = settings.VideoFormat.GetDescription(),
            Codec = settings.VideoCodec.GetDescription(),
            DownloadType = "video",
            AutoStart = true,
            SplitByChapters = false,
            CustomNamePrefix = idPrefix
        };

        var content = JsonContent.Create(requestBody);
        _logger.LogTrace("Sending add download request to MeTube. Request: {content}", await content.ReadAsStringAsync());
        var response = await _httpClient.PostAsync("/add", content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content
            .ReadFromJsonAsync<MeTubeGenericResponse>();
        return responseContent;
    }

    public async Task<MeTubeHistoryResponse> GetHistory()
    {
        var response = await _httpClient.GetAsync("/history");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        _logger.LogTrace("Getting history from MeTube. Response: {content}", content);
        var responseContent = JsonSerializer.Deserialize<MeTubeHistoryResponse>(content);

        if(responseContent == null)
        {
            throw new Exception($"Failed to deserialize MeTube history response. Response: {content}");
        }

        return responseContent;
    }

    public async Task<MeTubeGenericResponse?> DeleteDownload(string id)
    {
        return await DeleteDownloads([id]);
    }

    public async Task<MeTubeGenericResponse?> DeleteDownloads(IEnumerable<string> ids)
    {
        // {"where":"done","ids":["https://www.youtube.com/watch?v=bxOLkxR6_6c"]}
        var requestBody = new MeTubeDeleteRequest
        {
            Where = "done",
            Ids = ids.ToArray()
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

    public async Task<HttpResponseMessage> GetFileResponseAsync(string filename, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/download/{Uri.EscapeDataString(filename)}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
