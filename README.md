## PolyVGet

PolyV (Version 11, 12, 13, Mp4) Downloader written in C#

### Compiling
Make sure you've got all [Native AOT Prerequisities](https://aka.ms/nativeaot-prerequisites) installed. Then run:
```shell
dotnet publish -c Release
```

### Running
The downloader requires both a PolyV video URI and optionally a token found in the network tab (for HLS):
+ Video URI: In the URL of this request: `https://player.polyv.net/secure/<HERE>.json`
+ Token: In the URL query of this request: `https://hls.videocc.net/playsafe/../../../...key?token=<HERE>`

Then run:
```shell
PolyVGet 4e75d3d997e444a48be3913c77d1c8d8_4 85fc0eb0-c3ce-4c80-84ef-dbb3aa1cab99-t0
```

> [!IMPORTANT]  
> The downloader will inform you to use v13text.exe for playback for v13 videos. Read this for more [info](#playback-of-v13-videos).

Commandline syntax:
```shell
Usage:
  PolyVGet <videoUri> [<token>] [options]

Arguments:
  <videoUri>  Video URI (e.g. 4e75d3d997e444a48be3913c77d1c8d8_4)
  <token>     PolyV PlaySafe Token (e.g. 85fc0eb0-c3ce-4c80-84ef-dbb3aa1cab99-t0)

Options:
  -q, --quality <Best|Medium|Worst>          Set the video quality to download []
  -s, --subtitles                            Download the video's subtitles [default: False]
  -t, --max-threads <max-threads>            Maximum number of threads [default: 4]
  -o, --output-directory <output-directory>  Output directory [default: .]
  -y, --overwrite                            Overwrite existing file [default: False]
  -l, --log-level <Debug|Fatal|Info|Warn>    Level of log output [default: Info]
  --version                                  Show version information
  -?, -h, --help                             Show help and usage information
```

### Playback of v13 videos
There are two methods of playing these videos:
+ [v13test.exe](https://nicaicai.lanzouo.com/ifpNd2537aad), a tool taken from [a thread on 52PoJie.cn](https://www.52pojie.cn/thread-1945942-1-1.html) that can **play** downloaded files without any issues. This is only a player and conversion to a playable video format using this program is not possible. To play, run `v13test.exe` and enter the file path to the downloaded video. mpv will open, allowing you to play the video normally. This program was originally written in Python and then compiled to C using Nuitka, making .pyc file recovery impossible.
+ [PolyVGet-ffmpeg](https://github.com/DevLARLEY/PolyVGet-ffmpeg), a modified version of ffmpeg to match the changes made to the wasm runtime in the browser. This would obviously be my preferred option since its code is public, but I have not been able to reverse engineer it 100%. This leads to about 1 in 1000 B/P frames to be corrupt (?) when re-encoding. The re-implementation will use libavcodec's decoder to read the H.264 data and x264 to encode it to a playable video. Technically, the video data is not actually encoded differently but only moved around in the file, but since there is no such option we need to completely re-encode the file. To do this, run `ffmpeg.exe -y -i <downloaded file> -c:v libx264 -c:a copy <output file>`. If you want to help with reverse engineering, use [libpolyvffmpeg.so](https://www.mediafire.com/file/ofxnpahf64sbmz5/libpolyvffmpeg.so/file) (from the Wingfox Android App) because it still contains functions names.
