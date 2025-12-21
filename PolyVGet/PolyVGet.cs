using PolyVGet.Misc;
using PolyVGet.PolyV;
using Spectre.Console;

namespace PolyVGet;

public class PolyVGet(string videoUri, string token, string outputDir)
{
    public readonly PolyVClient PolyVClient = new();

    public async Task Initialize()
    {
        Logger.LogInfo($"Loading Video URI {videoUri}...");
        await PolyVClient.LoadVideoJson(videoUri);

        Logger.LogInfo($"PolyV Version: {PolyVClient.Version}");
    }
    
    private async Task<byte[]> GetHlsKey(string keyUrl)
    {
        var subpath = PolyVClient.VideoJson.HlsPrivate == null ? "/playsafe/v1104" : $"/playsafe/v{PolyVClient.Version}";
        var tokenId = Util.ParseToken(token);
        
        var newKeyUrl = Util.ModifyKeyUrl(keyUrl, subpath, token);
        Logger.LogDebug($"Key URL: {newKeyUrl}");
        
        var responseBytes = await HttpUtil.GetBytesAsync(newKeyUrl);
        return PolyVClient.PolyVImpl.DecryptKey(responseBytes, PolyVClient.VideoJson.SeedConst, tokenId);
    }

    private async Task DownloadFragments(Playlist playlist, string taskName, byte[] key, string tempDir, int maxThreads)
    {
        var progress = AnsiConsole
            .Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .AutoClear(true);
        
        Directory.CreateDirectory(tempDir);
        
        await progress.StartAsync(async ctx =>
        {
            var task = ctx.AddTask(taskName, new ProgressTaskSettings { AutoStart = false });
            task.MaxValue = playlist.Fragments.Count;

            await Parallel.ForEachAsync(playlist.Fragments.Select((item, index) => (Item: item, Index: index)), new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, async (fragment, cToken) =>
            {
                var outFile = Path.Combine(tempDir, $"{fragment.Index}.bin");

                var response = await HttpUtil.GetBytesAsync(fragment.Item, null, cToken);
                Logger.LogDebug($"Downloaded fragment {fragment.Index}");
            
                var decrypted = PolyVClient.PolyVImpl.DecryptFile(key, playlist.Iv!, response, fragment.Index);

                await File.WriteAllBytesAsync(outFile, decrypted, cToken);
            
                task.Increment(1);
            });
        });
    }
    
    public async Task Download(string resolution, int maxThreads, bool subtitles)
    {
        var resolutionIndex = PolyVClient.VideoJson.Resolution.IndexOf(resolution);

        var manifestUrl = PolyVClient.VideoJson.Hls[resolutionIndex];
        var manifest = await PolyVClient.GetManifest(manifestUrl, PolyVClient.VideoJson.SeedConst, PolyVClient.VideoJson.HlsPrivate);
        var playlist = Util.ParsePlaylist(manifest);

        var hlsKey = await GetHlsKey(playlist.KeyUrl!);
        
        Logger.LogDebug($"HLS Key: {hlsKey.ToHex()} IV: {playlist.Iv!.ToHex()}");
        Logger.LogInfo("Downloading...");

        var fragmentsDir = Path.Combine(outputDir, videoUri);

        await DownloadFragments(playlist, resolution, hlsKey, fragmentsDir, maxThreads);

        Logger.LogInfo("Merging...");

        var mergedFile = Path.Combine(outputDir, $"{videoUri}.ts");
        
        Util.MergeFiles(fragmentsDir, mergedFile);
        Directory.Delete(fragmentsDir, true);

        if (PolyVClient.VideoJson.HlsPrivate == 2)
        {
            Logger.LogInfo("Deobfuscating...");
            
            var encrypted = await File.ReadAllBytesAsync(mergedFile);
            CryptoUtil.MarsDeobfuscate(encrypted, mergedFile);
            
            Logger.LogWarn("Use v13test.exe to play");
            
            /*Logger.LogInfo("Re-encoding (this can take a while)...");

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
            File.Delete(tempFile);*/
        }

        var finalFileName = Path.Combine(outputDir, $"{PolyVClient.VideoJson.Title}.ts");
        File.Move(mergedFile, finalFileName, true);

        Logger.LogInfo($"Saved as: {finalFileName}");

        if (subtitles)
        {
            Logger.LogInfo("Downloading subtitles...");
            await PolyVClient.DownloadSubtitles(outputDir);
        }
        
        Logger.LogInfo("Done");
    }
}