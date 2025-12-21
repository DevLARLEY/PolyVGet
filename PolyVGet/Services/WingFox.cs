using PolyVGet.Misc;

namespace PolyVGet.Services;

public class WingFox : IService
{
    private const string TokenUrl = "https://www.wingfox.com/polyv/polyv_get_token.php?video_id={0}";
    private const string VideoUrl = "https://api.wingfox.com/api/album/get_video_url?play_video_id={0}";

    public string Name() => "wingfox";
    
    public string CookieName() => "yiihuu_s_c_d";
    public string CookieDomain() => ".wingfox.com";

    public async Task<string> GetToken(string videoId)
    {
        var url = string.Format(TokenUrl, videoId);
        
        return await HttpUtil.GetStringAsync(url);
    }

    public async Task<string> GetVideoUri(string videoId)
    {
        var url = string.Format(VideoUrl, videoId);
        
        var responseBody = await HttpUtil.GetJsonAsync<WingFoxVideoUri>(url, HttpUtil.Context.WingFoxVideoUri);
        return responseBody!.Data.VideoVid;
    }
}