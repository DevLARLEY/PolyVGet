using System.Security.Cryptography;
using System.Text.Json;
using PolyVGet.Misc;

namespace PolyVGet.PolyV;

public class PolyVClient
{
    private const string VideoJsonUrl = "https://player.polyv.net/secure/{0}.json";

    private const string HlsConstant1 = "NTQ1ZjhmY2QtMzk3OS00NWZhLTkxNjktYzk3NTlhNDNhNTQ4#";
    private const string HlsConstant2 = "OWtjN9xcDcc2cwXKxECpRgKw7piD4RwCdfOUlyNHFdSV0gHi=";

    private static readonly byte[] HlsIv1 = [ 1,  1, 2,  3, 5, 8, 13, 21, 34, 21, 13, 8, 5,  3, 2, 1];
    private static readonly byte[] HlsIv2 = [13, 22, 8, 12, 7, 6, 13,  1, 50, 11, 12, 8, 5, 16, 4, 1];

    public VideoJson VideoJson { get; private set; } = null!;
    public IPolyVImpl PolyVImpl { get; private set; } = null!;

    public int HlsVersion => (VideoJson.HlsPrivate ?? 0) + 11;
    public bool IsHls => VideoJson.Seed != 0;

    public string OutFileName => $"{VideoJson.Title}.{(IsHls ? "ts" : "mp4")}";

    public string QualityString(int i)
    {
        var s = VideoJson.Resolution[i];
        
        if (VideoJson.Bitrate != null)
            s += $", {VideoJson.Bitrate.Split(',')[i]} kbps";

        var filesizes = IsHls ? VideoJson.TsFilesize : VideoJson.Filesize;
        s += $" ({filesizes![i] / 1_000_000d:0.##} MB)";

        return s;
    }
    
    public async Task LoadVideoJson(string videoUri)
    {
        var url = string.Format(VideoJsonUrl, videoUri);

        var responseBody = await HttpUtil.GetJsonAsync(url, HttpUtil.Context.JsonResponse);
        var encryptedBody = Convert.FromHexString(responseBody!.Body);

        var uriHash = MD5.HashData(videoUri.Encode()).ToHex();
        var key = uriHash[..16].Encode();
        var iv = uriHash[16..].Encode();

        var decryptedBody = CryptoUtil.DecryptAesCbc(key, iv, encryptedBody);
        var jsonString = Convert.FromBase64String(decryptedBody.Decode()).Decode();

        Logger.LogDebug(jsonString);
        
        VideoJson = JsonSerializer.Deserialize(jsonString, HttpUtil.Context.VideoJson)!;
        PolyVImpl = VideoJson.HlsPrivate switch
        {
            null => new PolyV11(),
            1 => new PolyV12(),
            2 => new PolyV13(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public async Task<string> GetManifest(string url, int seedConst, int? hlsPrivate)
    {
        if (hlsPrivate == null)
            return await HttpUtil.GetStringAsync(url);

        var encryptedData = await HttpUtil.GetJsonAsync(url, HttpUtil.Context.JsonResponse);

        var constant = hlsPrivate == 1 ? HlsConstant1 : HlsConstant2;
        var iv = hlsPrivate == 1 ? HlsIv1 : HlsIv2;

        var aesKey = MD5.HashData((constant + seedConst).Encode()).ToHex()[1..17].Encode();
        var ciphertext = Convert.FromBase64String(encryptedData!.Body);
        var decrypted = CryptoUtil.DecryptAesCbc(aesKey, iv, ciphertext);

        return decrypted.Decode();
    }

    public async Task TryDownloadSubtitles(string outputDir)
    {
        if (VideoJson.Srt != null)
        {
            await Parallel.ForEachAsync(VideoJson.Srt, async (srt, token) =>
            {
                var stringResponse = await HttpUtil.GetStringAsync(srt.Url, null, token);

                var subtitleFileName = $"{VideoJson.Title}.{srt.Title}.srt";
                var subtitleFile = Path.Combine(outputDir, subtitleFileName);
                Logger.LogInfo($"Subtitle: {subtitleFileName}");
                    
                await File.WriteAllTextAsync(subtitleFile, stringResponse, token);
            });
        }
        else
        {
            Logger.LogWarn("Unable to download subtitles, none available");
        }
    }
}