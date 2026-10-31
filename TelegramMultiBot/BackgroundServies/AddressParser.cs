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
            case "oem": return configuationService.SvitlobotSettings.OemCookie;

            default:
                return null;
        }

    }

    public async Task<Dictionary<string, BuildingInfo>> ParseAddress(AddressJob addressJob, DateTimeOffset date, ChromeSession? session = null)
    {
        var responseContent = string.Empty;
        var requestContent = string.Empty;
        ChromeSession? ownedSession = null;
        try
        {
            var url = addressJob.Location.Url.Replace("shutdowns", "ajax");

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
            requestContent = await content.ReadAsStringAsync();

            if (session is null)
            {
                ownedSession = await new ChromeBrowserClient()
                    .OpenSessionAsync(addressJob.Location.Url, "DisconSchedule.fact");
                session = ownedSession;
            }

            responseContent = await session.PostFormAsync(url, collection);

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
        finally
        {
            if (ownedSession is not null)
            {
                await ownedSession.DisposeAsync();
            }
        }
    }    
}

