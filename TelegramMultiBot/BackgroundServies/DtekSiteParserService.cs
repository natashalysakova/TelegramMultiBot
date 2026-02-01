namespace TelegramMultiBot.BackgroundServies;

public class DtekSiteParserService : IDtekSiteParserService
{
    private readonly DtekSiteParser _dtekSiteParser;

    public DtekSiteParserService(DtekSiteParser dtekSiteParser)
    {
        _dtekSiteParser = dtekSiteParser;
    }

    public async Task ParseImmediately()
    {
        _dtekSiteParser.CancelDelay();
        await Task.CompletedTask;
    }
}


