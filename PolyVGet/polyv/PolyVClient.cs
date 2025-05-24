using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace PolyVGet.polyv;

public class PolyVClient(HttpClient client, JsonContext context)
{
    private const string VideoJsonUrl = "https://player.polyv.net/secure/{0}.json";

    private const string HlsConstant1 = "NTQ1ZjhmY2QtMzk3OS00NWZhLTkxNjktYzk3NTlhNDNhNTQ4#";
    private const string HlsConstant2 = "OWtjN9xcDcc2cwXKxECpRgKw7piD4RwCdfOUlyNHFdSV0gHi=";

    private static readonly byte[] HlsIv1 = [ 1,  1, 2,  3, 5, 8, 13, 21, 34, 21, 13, 8, 5,  3, 2, 1];
    private static readonly byte[] HlsIv2 = [13, 22, 8, 12, 7, 6, 13,  1, 50, 11, 12, 8, 5, 16, 4, 1];
    
    public async Task<VideoJson> GetVideoJson(string videoUri)
    {
        var url = string.Format(VideoJsonUrl, videoUri);

        var response = await client.GetAsync(url);
        
        if (response.Content.Headers.ContentType?.CharSet?.ToLowerInvariant() == "utf8")
            response.Content.Headers.ContentType.CharSet = "utf-8";
        
        var responseBody = await response.Content.ReadFromJsonAsync(context.JsonResponse);
        var encryptedBody = Convert.FromHexString(responseBody!.Body);

        var uriHash = MD5.HashData(videoUri.Encode()).ToHex();
        var key = uriHash[..16].Encode();
        var iv = uriHash[16..].Encode();

        var decryptedBody = Util.DecryptAesCbc(key, iv, encryptedBody);
        var jsonString = Convert.FromBase64String(decryptedBody.Decode()).Decode();

        return JsonSerializer.Deserialize(jsonString, context.VideoJson)!;
    }

    public async Task<string> GetManifest(string url, int seedConst, int? hlsPrivate)
    {
        var response = await client.GetAsync(url);

        if (hlsPrivate == null)
            return await response.Content.ReadAsStringAsync();

        var encryptedData = await response.Content.ReadFromJsonAsync(context.JsonResponse);
        
        var constant = hlsPrivate == 1 ? HlsConstant1 : HlsConstant2;
        var iv = hlsPrivate == 1 ? HlsIv1 : HlsIv2;

        var aesKey = MD5.HashData((constant + seedConst).Encode()).ToHex()[1..17].Encode();
        var ciphertext = Convert.FromBase64String(encryptedData!.Body);
        var decrypted = Util.DecryptAesCbc(aesKey, iv, ciphertext);

        return decrypted.Decode();
    }
}