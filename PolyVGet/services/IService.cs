namespace PolyVGet.services;

public interface IService
{
    public string Name();
    public string CookieName();
    public string CookieDomain();
    
    public Task<string> GetToken(string videoId);

    public Task<string> GetVideoUri(string videoId);
}