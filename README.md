## PolyVGet

Modular PolyV (Version 11, 12, 13) Downloader written in C# \
Currently supports the following services:
+ Wingfox (wingfox.com, yiihuu.cc)
+ Yiihuu (yiihuu.com)

### Compiling
Make you've got all [Native AOT Prerequisities](https://aka.ms/nativeaot-prerequisites) installed. Then run:
```shell
dotnet publish -c Release
```

### Running
Specify the service name, video ID and, if required, a cookie value to download a video. 
The video ID is either in the URL (e.g. `https://www.wingfox.com/p/.../163118`) or a token network request (e.g. `https://www.yiihuu.com/polyv/polyv_get_token.php?vid=342770`).
`<cookie>` is the value of a cookie sent in the polyv token request (e.g. `ule0khj2hl0b51j94stmhi2oeb`). Their respective names are given in the `--help` output.

To download, for example, a wingfox video at **https://www.wingfox.com/p/.../163118** that requires a login (so a token, which is `ule0khj2hl0b51j94stmhi2oeb`) and download the resulting video to a folder called `download`, run:
```shell
PolyVGet wingfox 163118 ule0khj2hl0b51j94stmhi2oeb -o download 
```

> [!IMPORTANT]  
> The downloader will inform you to use v13text.exe for playback for v13 videos. Read this for more [info](#playback-of-v13-videos).

Commandline syntax:
```shell
Usage:
  PolyVGet <service> <videoId> [<cookie>] [options]

Arguments:
  <service>  Service name
  <videoId>  Video ID found in the token request or URL
  <cookie>   Cookie required for requesting token

Options:
  -s, --subtitles                            Download the video's subtitles [default: False]
  -t, --max-threads <max-threads>            Maximum number of threads [default: 4]
  -o, --output-directory <output-directory>  Output directory [default: .]
  -l, --log-level <Debug|Fatal|Info|Warn>    Level of log output [default: Info]
  --version                                  Show version information
  -?, -h, --help                             Show help and usage information
```

### Playback of v13 videos
There are two methods of playing these videos:
+ [v13test.exe](https://nicaicai.lanzouo.com/ifpNd2537aad), a tool taken from [a thread on 52PoJie.cn](https://www.52pojie.cn/thread-1945942-1-1.html) that can **play** downloaded files without any issues. This is only a player and conversion to a playable video format using this program is not possible. To play, run `v13test.exe` and enter the file path to the downloaded video. mpv will open, allowing you to play the video normally. This program was originally written in Python and then compiled to C using Nuitka, making .pyc file recovery impossible.
+ [PolyVGet-ffmpeg](https://github.com/DevLARLEY/PolVGet-ffmpeg) (and [PolyVGet-x264](https://github.com/DevLARLEY/PolVGet-x264)), a modified version of ffmpeg to match the changes made to the wasm runtime running in the browser. This would obviously be my preferred option since it code is public, but I have not been able to fully reverse engineer everything. Some (or all) intra-frames do not decode properly which causes a grey-ish frame to show up every 10-30 seconds when re-encoding. The re-implementation will use libavcodec's decoder to read the H.264 data and x264 to encode it to a playable video. Technically, the video data is not actually encoded differently but only moved around in the file, but since there is no such option we need to completely re-encode the file. To do this, run `ffmpeg.exe -y -i <downloaded file> -c:v libx264 -c:a copy <output file>`.
