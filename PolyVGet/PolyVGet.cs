using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using PolyVGet.polyv;
using PolyVGet.services;
using Spectre.Console;

namespace PolyVGet;

public class PolyVGet(IService service, string videoId, string directory)
{
    private static readonly HttpClientHandler ClientHandler = new()
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
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly JsonContext Context = new(Options);
    private static readonly PolyVClient PolyVClient = new(Client, Context);

    private IPolyV _polyV = null!;
    private VideoJson _videoJson = null!;

    private static readonly List<Func<IService>> Services =
    [
        () => new WingFox(Client, Context),
        () => new Yiihuu(Client)
    ];
    
    public static PolyVGet WithService(string serviceName, string videoId, string cookie, string directory)
    {
        var service = Services.FirstOrDefault(service => service().Name() == serviceName);
        if (service == null)
            throw new ArgumentException("Service not found");

        var serviceInstance = service();
        ClientHandler.CookieContainer.Add(new Cookie(serviceInstance.CookieName(), cookie, "/", serviceInstance.CookieDomain()));

        Directory.CreateDirectory(directory);
        
        return new PolyVGet(serviceInstance, videoId, directory);
    }

    public static List<(string, string)> GetServices() => Services.Select(service => (service().Name(), service().CookieName())).ToList();
    
    public async Task Initialize()
    {
        Logger.LogInfo($"Getting videoUri for {videoId}");
        var videoUri = await service.GetVideoUri(videoId);
        Logger.LogDebug($"VideoUri: {videoUri}");
        
        Logger.LogInfo("Getting video Json...");
        _videoJson = await PolyVClient.GetVideoJson(videoUri);
        _polyV = _videoJson.HlsPrivate switch
        {
            null => new PolyV11(),
            1 => new PolyV12(),
            2 => new PolyV13(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public List<string> GetQualities() => _videoJson.Resolution;
    
    private async Task<byte[]> GetHlsKey(string keyUrl)
    {
        var subpath = _videoJson.HlsPrivate == null ? "/playsafe/v1104" : $"/playsafe/v{_videoJson.HlsPrivate + 11}";
        
        var token = await service.GetToken(videoId);
        var tokenId = Util.ParseToken(token);
        
        var newKeyUrl = Util.ModifyKeyUrl(keyUrl, subpath, token);
        var response = await Client.GetByteArrayAsync(newKeyUrl);
        return _polyV.DecryptKey(response, _videoJson.SeedConst, tokenId);
    }

    private async Task DownloadAndDecrypt(ProgressTask task, byte[] key, byte[] iv, List<string> fragments, string tempDir, int maxThreads)
    {
        Directory.CreateDirectory(tempDir);
        
        await Parallel.ForEachAsync(fragments.WithIndex(), new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, async (fragment,cancellationToken) =>
        {
            var outFile = Path.Combine(tempDir, $"{fragment.Index}.bin");

            var response = await Client.GetByteArrayAsync(fragment.Item, cancellationToken);
            var decrypted = _polyV.DecryptFile(key, iv, response, fragment.Index);

            await File.WriteAllBytesAsync(outFile, decrypted, cancellationToken);
            
            task.Increment(1);
        });
    }
    
    public async Task Download(string resolution, int maxThreads, bool subtitles)
    {
        var resolutionIndex = GetQualities().IndexOf(resolution);

        var manifestUrl = _videoJson.Hls[resolutionIndex];
        var manifest = await PolyVClient.GetManifest(manifestUrl, _videoJson.SeedConst, _videoJson.HlsPrivate);
        var playlist = Util.ParsePlaylist(manifest);

        var hlsKey = await GetHlsKey(playlist.KeyUrl!);
        
        Logger.LogDebug($"Key: {hlsKey.ToHex()} IV: {playlist.Iv!.ToHex()}");

        var progress = AnsiConsole
            .Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .AutoClear(true);

        Logger.LogInfo("Downloading...");

        var tempDirName = Guid.NewGuid().ToString();
        var tempDir = Path.Combine(directory, tempDirName);
        
        await progress.StartAsync(async ctx =>
        {
            var task = ctx.AddTask(resolution, new ProgressTaskSettings { AutoStart = false });
            task.MaxValue = playlist.Fragments.Count;

            await DownloadAndDecrypt(task, hlsKey, playlist.Iv!, playlist.Fragments, tempDir, maxThreads);
        });
        
        Logger.LogInfo("Merging...");

        var tempFileName = $"{tempDirName}.ts";
        var tempFile = Path.Combine(directory, tempFileName);
        Util.MergeFiles(directory, tempDir, tempFileName);
        
        Directory.Delete(tempDir, true);

        var finalFileName = Path.Combine(directory, $"{_videoJson.Title}.ts");
        
        if (_videoJson.HlsPrivate == 2)
        {
            Logger.LogInfo("Deobfuscating...");
            
            var encrypted = await File.ReadAllBytesAsync(tempFile);
            Util.MarsDeobfuscate(encrypted, tempFile);
            
            Logger.LogInfo("Re-encoding (this can take a while)...");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\titus\tmp\build\ffmpeg\ffmpeg.exe",
                Arguments = $"-y -i {tempFile} -c:v libx264 -c:a copy {finalFileName}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            await process.WaitForExitAsync();
            File.Delete(tempFile);
        }
        else
        {
            if (File.Exists(finalFileName))
                File.Delete(finalFileName);
            
            File.Move(tempFile, finalFileName);
        }

        if (subtitles)
        {
            if (_videoJson.Srt != null)
            {
                Logger.LogInfo($"Downloading {_videoJson.Srt.Count} subtitles...");
                await Parallel.ForEachAsync(_videoJson.Srt, async (srt, token) =>
                {
                    var response = await Client.GetAsync(srt.Url, token);
                    var content = await response.Content.ReadAsStringAsync(token);
                    await File.WriteAllTextAsync($"{_videoJson.Title}.{srt.Title}.srt", content, token);
                });
            }
            else
            {
                Logger.LogWarn("Unable to download subtitles, none available");
            }
        }
        
        Logger.LogInfo("Done");
    }
}