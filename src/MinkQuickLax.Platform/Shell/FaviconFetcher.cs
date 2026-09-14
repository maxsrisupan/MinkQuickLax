using System.Net.Http;
using MinkQuickLax.Core.Launch;

namespace MinkQuickLax.Platform.Shell;

/// <summary>
/// Downloads a site's own icon: the page's declared icons first, then /favicon.ico. No third-party icon services
/// (SPEC 4.1, 6 privacy). Every request gives up after 5 seconds.
/// </summary>
public sealed class FaviconFetcher : IDisposable
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const int MaxBytes = 1_000_000;

    private readonly HttpClient _http;

    public FaviconFetcher()
        : this(new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All, AllowAutoRedirect = true, MaxAutomaticRedirections = 5 }))
    {
    }

    public FaviconFetcher(HttpClient http)
    {
        _http = http;
        _http.Timeout = Timeout;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MinkQuickLax/1.0 (+https://github.com/maxsrisupan/MinkQuickLax)");
    }

    /// <summary>Image bytes (PNG, ICO, JPEG, GIF, BMP) of the best icon found, or null.</summary>
    public async Task<byte[]?> FetchAsync(Uri page, CancellationToken cancel = default)
    {
        if (page.Scheme is not ("http" or "https"))
        {
            return null;
        }
        string? html = null;
        try
        {
            using var response = await _http.GetAsync(page, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            {
                html = await ReadLimitedStringAsync(response, cancel).ConfigureAwait(false);
                page = response.RequestMessage?.RequestUri ?? page;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Fall through to /favicon.ico on the original host.
        }

        foreach (var candidate in FaviconParser.Candidates(html, page))
        {
            cancel.ThrowIfCancellationRequested();
            try
            {
                using var response = await _http.GetAsync(candidate, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }
                var bytes = await ReadLimitedBytesAsync(response, cancel).ConfigureAwait(false);
                if (bytes is not null && LooksLikeImage(bytes))
                {
                    return bytes;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                // Try the next candidate.
            }
        }
        return null;
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Checks magic numbers so an HTML error page is not treated as an icon.</summary>
    internal static bool LooksLikeImage(ReadOnlySpan<byte> b) =>
        b.Length > 8 && (
            (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) // PNG
            || (b[0] == 0x00 && b[1] == 0x00 && (b[2] == 0x01 || b[2] == 0x02) && b[3] == 0x00) // ICO / CUR
            || (b[0] == 0xFF && b[1] == 0xD8) // JPEG
            || (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) // GIF
            || (b[0] == 0x42 && b[1] == 0x4D)); // BMP

    private static async Task<string?> ReadLimitedStringAsync(HttpResponseMessage response, CancellationToken cancel)
    {
        var bytes = await ReadLimitedBytesAsync(response, cancel).ConfigureAwait(false);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static async Task<byte[]?> ReadLimitedBytesAsync(HttpResponseMessage response, CancellationToken cancel)
    {
        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            return null;
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancel).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
            {
                return null;
            }
        }
        return buffer.ToArray();
    }
}
