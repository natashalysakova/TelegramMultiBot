using DtekParsers;
using System.Text.Json;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.BackgroundServies;

public class AddressParser(ISqlConfiguationService configuationService)
{

    private string? GetCookie(string url)
    {
        if (configuationService == null)
            return null;

        var location = LocationNameUtility.GetRegionByUrl(url);
        switch (location)
        {
            case "kem": return configuationService.SvitlobotSettings.KemCookie;
            case "krem": return configuationService.SvitlobotSettings.KremCookie;

            default:
                return null;
        }

    }

    public async Task<Dictionary<string, BuildingInfo>> ParseAddress(AddressJob addressJob, DateTimeOffset date)
    {
        var responseContent = string.Empty;
        var requestContent = string.Empty;
        try
        {
            var url = addressJob.Location.Url.Replace("shutdowns", "ajax");
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var dtekCookie = GetCookie(url);
            if (!string.IsNullOrEmpty(dtekCookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", dtekCookie);
            }

            // Validate and trim city and street before adding to collection
            var validatedCity = addressJob.City.ValidateAndTrimCyrillicText();
            var validatedStreet = addressJob.Street.ValidateAndTrimCyrillicText();

            var collection = new List<KeyValuePair<string, string>>
            {
                new("method", "getHomeNum"),
                new("data[0][name]", "city"),
                new("data[0][value]", validatedCity),
                new("data[1][name]", "street"),
                new("data[1][value]", validatedStreet),
                new("data[2][name]", "updateFact"),
                new("data[2][value]", date.ToString("dd.MM.yyyy HH:mm"))
            };
            var content = new FormUrlEncodedContent(collection);
            request.Content = content;
            requestContent = await content.ReadAsStringAsync();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            responseContent = await response.Content.ReadAsStringAsync();
            var addressResponse = JsonSerializer.Deserialize<AddressResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Check if response is successful and has data
            if (addressResponse?.Result == true && addressResponse.Data != null)
            {                
                return addressResponse.Data;
            }

            throw new ParseException("Invalid response from server");
        }
        catch (Exception ex)
        {
            throw new ParseException($"Failed to fetch HTML: {ex.Message}. Request: {requestContent} Response content: {responseContent}");
        }
    }    
}


