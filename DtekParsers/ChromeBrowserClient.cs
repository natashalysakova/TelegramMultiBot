using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Text.Json;

namespace DtekParsers;

/// <summary>
/// Talks to DTEK sites through a remote Chrome (separate container) so that the Incapsula
/// challenge, cookies, TLS fingerprint and user agent all belong to one real browser.
/// </summary>
public class ChromeBrowserClient(ILogger? logger = null)
{
    public const string EndpointEnvironmentVariable = "CHROME_WS_ENDPOINT";
    public const string ProxyEnvironmentVariable = "CHROME_PROXY_SERVER";
    public const string DefaultEndpoint = "ws://localhost:5301";

    private static readonly TimeSpan ChallengeTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public static string ResolveEndpoint()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = DefaultEndpoint;
        }

        // optional proxy (e.g. http://host:port) passed to browserless as a Chrome launch flag
        var proxy = Environment.GetEnvironmentVariable(ProxyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(proxy))
        {
            var separator = endpoint.Contains('?') ? '&' : '?';
            endpoint = $"{endpoint}{separator}--proxy-server={Uri.EscapeDataString(proxy)}";
        }

        return endpoint;
    }

    /// <summary>
    /// Opens the page and waits until <paramref name="readyMarker"/> shows up in its HTML,
    /// i.e. the Incapsula challenge has been passed.
    /// </summary>
    public async Task<ChromeSession> OpenSessionAsync(string url, string readyMarker, CancellationToken token = default)
    {
        var endpoint = ResolveEndpoint();
        logger?.LogDebug("Connecting to browser {endpoint}", endpoint);

        var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = endpoint,
            DefaultViewport = new ViewPortOptions { Width = 1920, Height = 1080 },
        });

        try
        {
            var page = await browser.NewPageAsync();

            // Keep the real browser's UA (consistent with its sec-ch-ua / platform), only drop the headless marker.
            var userAgent = (await browser.GetUserAgentAsync()).Replace("HeadlessChrome", "Chrome");
            await page.SetUserAgentAsync(userAgent);
            await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["Accept-Language"] = "uk-UA,uk;q=0.9,en-US;q=0.8,en;q=0.7"
            });

            await page.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = (int)ChallengeTimeout.TotalMilliseconds
            });

            var html = await WaitForMarkerAsync(page, readyMarker, token);
            return new ChromeSession(browser, page, url, html);
        }
        catch
        {
            await browser.DisposeAsync();
            throw;
        }
    }

    private async Task<string> WaitForMarkerAsync(IPage page, string marker, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + ChallengeTimeout;
        var content = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                content = await page.GetContentAsync();
                if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return content;
                }
            }
            catch (PuppeteerException ex)
            {
                // the challenge reloads the page while we poll
                logger?.LogDebug(ex, "Page is navigating, retrying");
            }

            await Task.Delay(PollInterval, token);
        }

        if (content.Contains("Incapsula", StringComparison.OrdinalIgnoreCase)
            || content.Contains("incap_ses", StringComparison.OrdinalIgnoreCase)
            || content.Contains("noindex,nofollow", StringComparison.OrdinalIgnoreCase))
        {
            throw new IncapsulaException("Incapsula protection detected. Cannot parse the schedule.");
        }

        throw new ParseException($"Page did not contain '{marker}' within {ChallengeTimeout.TotalSeconds}s");
    }
}

public sealed class ChromeSession : IAsyncDisposable
{
    private const string PostScript = @"async (url, pairs) => {
        const headers = {
            'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
            'X-Requested-With': 'XMLHttpRequest'
        };
        const csrf = document.querySelector('meta[name=""csrf-token""]');
        if (csrf) headers['X-CSRF-Token'] = csrf.content;

        const response = await fetch(url, {
            method: 'POST',
            credentials: 'include',
            headers,
            body: new URLSearchParams(pairs).toString()
        });
        return JSON.stringify({ status: response.status, body: await response.text() });
    }";

    private readonly IBrowser _browser;
    private readonly IPage _page;

    internal ChromeSession(IBrowser browser, IPage page, string url, string html)
    {
        _browser = browser;
        _page = page;
        Url = url;
        Html = html;
    }

    public string Url { get; }

    public string Html { get; }

    /// <summary>
    /// Sends a form POST from inside the page, so cookies and fingerprint match the browser that solved the challenge.
    /// </summary>
    public async Task<string> PostFormAsync(string ajaxUrl, IEnumerable<KeyValuePair<string, string>> form)
    {
        var pairs = form.Select(x => new[] { x.Key, x.Value }).ToArray();
        var json = await _page.EvaluateFunctionAsync<string>(PostScript, ajaxUrl, pairs);

        using var document = JsonDocument.Parse(json);
        var status = document.RootElement.GetProperty("status").GetInt32();
        var body = document.RootElement.GetProperty("body").GetString() ?? string.Empty;

        if (status is < 200 or >= 300)
        {
            if (body.Contains("Incapsula", StringComparison.OrdinalIgnoreCase))
            {
                throw new IncapsulaException("Incapsula protection detected on ajax request.");
            }

            throw new ParseException($"Ajax request failed with status {status}");
        }

        return body;
    }

    public ValueTask DisposeAsync() => _browser.DisposeAsync();
}
