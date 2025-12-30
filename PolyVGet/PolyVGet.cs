using PolyVGet.Misc;
using PolyVGet.PolyV;
using Spectre.Console;

namespace PolyVGet;

public class PolyVGet(string videoUri, string? token, string outputDir, bool overwrite)
{
    public readonly PolyVClient PolyVClient = new(token);

    public async Task Initialize()
    {
        Logger.LogInfo($"Loading Video URI {videoUri}...");
        await PolyVClient.LoadVideoJson(videoUri);
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

    private static async Task DownloadMp4(string url, string taskName, string outFile)
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

        await progress.StartAsync(async ctx =>
        {
            var task = ctx.AddTask(taskName, new ProgressTaskSettings { AutoStart = false });

            await HttpUtil.GetProgressAsync(url, outFile, progress: new Progress<double>(value =>
            {
                task.Value = value;
            }));
        });
    }
    
    public async Task Download(int resolutionIndex, int maxThreads, bool subtitles)
    {
        var finalFileName = Path.Combine(outputDir, PolyVClient.OutFileName);

        if (!overwrite && File.Exists(finalFileName))
            throw new Exception($"File \"{finalFileName}\" already exists. Use -y to overwrite");

        if (PolyVClient.IsHls)
        {
            if (token == null)
                throw new Exception("PolyV PlaySafe requires a token");
            
            Logger.LogInfo($"PolyV PlaySafe Version: {PolyVClient.HlsVersion}");
            
            var manifestUrl = PolyVClient.HlsList[resolutionIndex];
            var manifest = await PolyVClient.GetManifest(manifestUrl);
            var playlist = Util.ParsePlaylist(manifest);

            var hlsKey = await PolyVClient.GetHlsKey(playlist.KeyUrl!);

            Logger.LogDebug($"HLS Key: {hlsKey.ToHex()} IV: {playlist.Iv!.ToHex()}");
            Logger.LogInfo("Downloading...");

            var fragmentsDir = Path.Combine(outputDir, videoUri);

            await DownloadFragments(playlist, PolyVClient.QualityString(resolutionIndex), hlsKey, fragmentsDir, maxThreads);

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
        
            File.Move(mergedFile, finalFileName, true);
        }
        else
        {
            Logger.LogInfo("Downloading PolyV MP4...");

            var mp4Url = Util.FixLegacyMp4Url(PolyVClient.Mp4List[resolutionIndex]);
            await DownloadMp4(mp4Url, PolyVClient.QualityString(resolutionIndex), finalFileName);
        }

        Logger.LogInfo($"Saved as: {finalFileName}");

        if (subtitles)
        {
            Logger.LogInfo("Downloading subtitles...");
            await PolyVClient.TryDownloadSubtitles(outputDir);
        }
        
        Logger.LogInfo("Done");
    }
}