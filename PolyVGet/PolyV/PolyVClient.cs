using System.Security.Cryptography;
using System.Text.Json;
using PolyVGet.Misc;

namespace PolyVGet.PolyV;

public class PolyVClient(string? token)
{
    private const string HlsConstant1 = "NTQ1ZjhmY2QtMzk3OS00NWZhLTkxNjktYzk3NTlhNDNhNTQ4#";
    private const string HlsConstant2 = "OWtjN9xcDcc2cwXKxECpRgKw7piD4RwCdfOUlyNHFdSV0gHi=";

    private static readonly byte[] HlsIv1 = [ 1,  1, 2,  3, 5, 8, 13, 21, 34, 21, 13, 8, 5,  3, 2, 1];
    private static readonly byte[] HlsIv2 = [13, 22, 8, 12, 7, 6, 13,  1, 50, 11, 12, 8, 5, 16, 4, 1];

    public VideoJson VideoJson { get; private set; } = null!;
    public IPolyVImpl PolyVImpl { get; private set; } = null!;

    private static readonly string Pid = Util.GeneratePid();

    public bool IsHls => VideoJson.Seed != 0;
    public int HlsVersion => (VideoJson.HlsPrivate ?? 0) + 11;
    public List<string> HlsList => VideoJson.Hls302 == "1" ? (VideoJson.Hls2Pc ?? VideoJson.Hls2)!: VideoJson.Hls!;
    public List<string> Mp4List => (VideoJson.H5PcMp4 ?? VideoJson.Mp4)!;
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
        var url = $"https://player.polyv.net/secure/{videoUri}.json";

        var uriHash = MD5.HashData(videoUri.Encode()).ToHex();
        var key = uriHash[..16].Encode();
        var iv = uriHash[16..].Encode();
        
        var responseBody = await HttpUtil.GetJsonAsync(url, HttpUtil.Context.JsonResponse);
        var encryptedBody = Convert.FromHexString(responseBody!.Body);

        var decryptedBody = CryptoUtil.DecryptAesCbc(key, iv, encryptedBody);
        var jsonString = Convert.FromBase64String(decryptedBody.Decode()).Decode();

        Logger.LogDebug(jsonString);
        
        VideoJson = JsonSerializer.Deserialize(jsonString, HttpUtil.Context.VideoJson)!;
        
        if (IsHls)
        {
            PolyVImpl = VideoJson.HlsPrivate switch
            {
                null => new PolyV11(),
                1 => new PolyV12(),
                2 => new PolyV13(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public async Task<byte[]> GetHlsKey(string keyUrl)
    {
        var subpath = VideoJson.HlsPrivate == null ? "/playsafe/v1104" : $"/playsafe/v{HlsVersion}";

        var keyUriBuilder = new UriBuilder(keyUrl);
        keyUriBuilder.Path = subpath + keyUriBuilder.Path;
        var newKeyUrl = keyUriBuilder.ToString();

        byte[] responseBytes;
        
        try
        {
            responseBytes = await HttpUtil.GetBytesAsync(newKeyUrl);
        }
        catch (HttpRequestException e)
        {
            throw new Exception("Unable to get HLS key. Did your token expire?", e);
        }
        
        var tokenId = token!.Split('-')[^1][1..];

        return PolyVImpl.DecryptKey(responseBytes, VideoJson.SeedConst, tokenId);
    }
    
    public async Task<string> GetManifest(string url)
    {
        url = Util.AddUrlQueryParams(
            new UriBuilder(url),
            ("pid", Pid),
            ("device", "desktop"),
            ("token", token!)
        );

        url = url.Replace(".m3u8", ".pdx");

        var constant = HlsVersion is 11 or 12 ? HlsConstant1 : HlsConstant2;
        var aesKey = MD5.HashData((constant + VideoJson.SeedConst).Encode()).ToHex()[1..17].Encode();
        
        var iv = HlsVersion is 11 or 12 ? HlsIv1 : HlsIv2;
        
        var encryptedData = await HttpUtil.GetJsonAsync(url, HttpUtil.Context.JsonResponse);
        var ciphertext = Convert.FromBase64String(encryptedData!.Body);
        
        var decrypted = CryptoUtil.DecryptAesCbc(aesKey, iv, ciphertext);

        return decrypted.Decode();
    }

    public async Task TryDownloadSubtitles(string outputDir)
    {
        if (VideoJson.Srt != null)
        {
            await Parallel.ForEachAsync(VideoJson.Srt, async (srt, ct) =>
            {
                var stringResponse = await HttpUtil.GetStringAsync(srt.Url, null, ct);

                var subtitleFileName = $"{VideoJson.Title}.{srt.Title}.srt";
                var subtitleFile = Path.Combine(outputDir, subtitleFileName);
                Logger.LogInfo($"Subtitle: {subtitleFileName}");
                    
                await File.WriteAllTextAsync(subtitleFile, stringResponse, ct);
            });
        }
        else
        {
            Logger.LogWarn("Unable to download subtitles, none available");
        }
    }
}