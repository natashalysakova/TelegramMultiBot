namespace TelegramMultiBot.BackgroundServies;

public class AddressResponse
{
    public bool Result { get; set; }
    public Dictionary<string, BuildingInfo>? Data { get; set; }
    // Add other properties if needed (showCurOutageParam, fact, preset, etc.)
}


