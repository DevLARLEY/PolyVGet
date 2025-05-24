namespace PolyVGet.services;

public class Yiihuu(HttpClient client) : IService
{
    private const string TokenUrl = "https://www.yiihuu.com/polyv/polyv_get_token.php?vid={0}";
    private const string VideoUrl = "https://www.yiihuu.com/get_video_uri.php?play_video_id={0}";

    public string Name() => "yiihuu";
    
    public string CookieName() => "PHPSESSID";
    public string CookieDomain() => "www.yiihuu.com";

    public async Task<string> GetToken(string videoId)
    {
        var url = string.Format(TokenUrl, videoId);

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            Logger.LogFatal($"Unable to get Token: {response.StatusCode}. Is the cookie valid?");
        
        var responseBody = await response.Content.ReadAsStringAsync();

        return responseBody.Trim();
    }

    public async Task<string> GetVideoUri(string videoId)
    {
        var url = string.Format(VideoUrl, videoId);

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            Logger.LogFatal($"Unable to get Video URI: {response.StatusCode}. Is the cookie valid?");
        
        var responseBody = await response.Content.ReadAsStringAsync();

        return responseBody.Trim().Split('#')[0];
    }
}