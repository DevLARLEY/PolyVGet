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

    private static async Task<HttpResponseMessage> GetAsync(string url, Dictionary<string, string>? headers = null, CancellationToken token = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        
        if (headers != null)
        {
            foreach (var item in headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(item.Key, item.Value);
            }
        }

        var httpResponse = await Client.SendAsync(httpRequest, token);
        
        if (httpResponse.Content.Headers.ContentType?.CharSet?.ToLowerInvariant() == "utf8")
            httpResponse.Content.Headers.ContentType.CharSet = "utf-8";
        
        httpResponse.EnsureSuccessStatusCode();

        return httpResponse;
    }

    public static async Task<T> GetJsonAsync<T>(string url, JsonTypeInfo<T> typeInfo, Dictionary<string, string>? headers = null, CancellationToken token = default)
    {
        var httpResponse = await GetAsync(url, headers, token);
        
        var stringResponse = await httpResponse.Content.ReadAsStringAsync(token);
        Logger.LogDebug($"[{httpResponse.StatusCode}] {url}: {stringResponse.Trim()}");

        return await httpResponse.Content.ReadFromJsonAsync(typeInfo, token) ?? throw new JsonException("Empty JSON response");
    }

    public static async Task<string> GetStringAsync(string url, Dictionary<string, string>? headers = null, CancellationToken token = default)
    {
        var httpResponse = await GetAsync(url, headers, token);
        
        var stringResponse = await httpResponse.Content.ReadAsStringAsync(token);
        Logger.LogDebug($"[{httpResponse.StatusCode}] {url}: {stringResponse.Trim()}");
        
        return stringResponse;
    }

    public static async Task<byte[]> GetBytesAsync(string url, Dictionary<string, string>? headers = null, CancellationToken token = default)
    {
        var httpResponse = await GetAsync(url, headers, token);
        
        var byteResponse = await httpResponse.Content.ReadAsByteArrayAsync(token);

        var truncated = byteResponse.Length > 1000;
        var hexLogText = Convert.ToHexString(truncated ? byteResponse[..1000] : byteResponse);
        
        Logger.LogDebug($"[{httpResponse.StatusCode}] {url}: {hexLogText} {(truncated ? "(truncated)" : "")}");
        
        return byteResponse;
    }
}