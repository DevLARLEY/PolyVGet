using PolyVGet.Misc;

namespace PolyVGet.Services;

public class Yiihuu : IService
{
    private const string TokenUrl = "https://www.yiihuu.com/polyv/polyv_get_token.php?vid={0}";
    private const string VideoUrl = "https://www.yiihuu.com/get_video_uri.php?play_video_id={0}";

    public string Name() => "yiihuu";
    
    public string CookieName() => "PHPSESSID";
    public string CookieDomain() => "www.yiihuu.com";

    public async Task<string> GetToken(string videoId)
    {
        var url = string.Format(TokenUrl, videoId);

        var stringResponse = await HttpUtil.GetStringAsync(url);
        return stringResponse.Trim();
    }

    public async Task<string> GetVideoUri(string videoId)
    {
        var url = string.Format(VideoUrl, videoId);

        var stringResponse = await HttpUtil.GetStringAsync(url);
        return stringResponse.Trim().Split('#')[0];
    }
}