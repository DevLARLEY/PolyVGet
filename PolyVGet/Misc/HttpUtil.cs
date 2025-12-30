using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PolyVGet.Misc;

public static class HttpUtil
{
    public static readonly HttpClientHandler ClientHandler = new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        MaxConnectionsPerServer = 1024,
        UseCookies = true,
        CookieContainer = new CookieContainer()
    };

    private static readonly HttpClient Client = new(ClientHandler)
    {
        Timeout = TimeSpan.FromSeconds(100),
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:147.0) Gecko/20100101 Firefox/147.0" },
            { "Referer", "https://www.google.com/" }
        }
    };
    
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public static readonly JsonContext Context = new(Options);

    private static async Task<HttpResponseMessage> SendAsync(
        string url, 
        Dictionary<string, string>? headers = null,
        CancellationToken token = default, 
        HttpMethod? method = null,
        HttpCompletionOption httpCompletionOption = HttpCompletionOption.ResponseContentRead)
    {
        Logger.LogDebug($"[{method ?? HttpMethod.Get}]: {url}");
        
        using var httpRequest = new HttpRequestMessage(method ?? HttpMethod.Get, url);
        
        if (headers != null)
        {
            foreach (var item in headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(item.Key, item.Value);
            }
        }

        var httpResponse = await Client.SendAsync(httpRequest, httpCompletionOption, token);
        
        if (httpResponse.Content.Headers.ContentType?.CharSet?.ToLowerInvariant() == "utf8")
            httpResponse.Content.Headers.ContentType.CharSet = "utf-8";
        
        httpResponse.EnsureSuccessStatusCode();

        return httpResponse;
    }

    public static async Task<T> GetJsonAsync<T>(string url, JsonTypeInfo<T> typeInfo, Dictionary<string, string>? headers = null, CancellationToken token = default)
    {
        var httpResponse = await SendAsync(url, headers, token);
        
        var stringResponse = await httpResponse.Content.ReadAsStringAsync(token);
        Logger.LogDebug($"[{httpResponse.StatusCode}] {url}: {stringResponse.Trim()}");

        return await httpResponse.Content.ReadFromJsonAsync(typeInfo, token) ?? throw new JsonException("Empty JSON response");
    }

    public static async Task<string> GetStringAsync(string url, Dictionary<string, string>? headers = null, CancellationToken token = default)
    {
        var httpResponse = await SendAsync(url, headers, token);
        
        var stringResponse = await httpResponse.Content.ReadAsStringAsync(token);
        Logger.LogDebug($"[{httpResponse.StatusCode}] {url}: {stringResponse.Trim()}");
        
        return stringResponse;
    }

    public static async Task<byte[]> GetBytesAsync(string url, Dictionary<string, string>? headers = null, CancellationToken token = default)
    {
        var httpResponse = await SendAsync(url, headers, token);
        
        var byteResponse = await httpResponse.Content.ReadAsByteArrayAsync(token);

        var truncated = byteResponse.Length > 1000;
        var hexLogText = Convert.ToHexString(truncated ? byteResponse[..1000] : byteResponse);
        
        Logger.LogDebug($"[{httpResponse.StatusCode}] {url}: {hexLogText} {(truncated ? "(truncated)" : "")}");
        
        return byteResponse;
    }

    public static async Task GetProgressAsync(
        string url,
        string outputPath,
        Dictionary<string, string>? headers = null,
        IProgress<double>? progress = null,
        CancellationToken token = default)
    {
        using var response = await SendAsync(url, headers, token, httpCompletionOption: HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;

        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = File.Create(outputPath);

        var buffer = new byte[81920];
        long totalRead = 0;

        int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            totalRead += read;

            if (contentLength.HasValue)
            {
                var percent = (double)totalRead / contentLength.Value * 100;
                progress?.Report(percent);
            }
        }
        
        progress?.Report(100);
    }
}