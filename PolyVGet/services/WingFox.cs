using System.Net.Http.Json;

namespace PolyVGet.services;

public class WingFox(HttpClient client, JsonContext context) : IService
{
    private const string TokenUrl = "https://www.wingfox.com/polyv/polyv_get_token.php?video_id={0}";
    private const string VideoUrl = "https://api.wingfox.com/api/album/get_video_url?play_video_id={0}";

    public string Name() => "wingfox";
    
    public string CookieName() => "yiihuu_s_c_d";
    public string CookieDomain() => ".wingfox.com";

    public async Task<string> GetToken(string videoId)
    {
        var url = string.Format(TokenUrl, videoId);

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            Logger.LogFatal($"Unable to get Token: {response.StatusCode}. Is the cookie valid?");
        
        var responseBody = await response.Content.ReadAsStringAsync();

        return responseBody;
    }

    public async Task<string> GetVideoUri(string videoId)
    {
        var url = string.Format(VideoUrl, videoId);

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            Logger.LogFatal($"Unable to get Video URI: {response.StatusCode}. Is the cookie valid?");
        
        var responseBody = await response.Content.ReadFromJsonAsync(context.WingFoxVideoUri);

        return responseBody!.Data.VideoVid;
    }
}