using System.Text.Json.Serialization;

namespace PolyVGet.Misc;

public class VideoUriData
{
    public required string VideoVid { get; set; }
}

public class VideoUri
{
    public required VideoUriData Data { get; set; }
}

public class JsonResponse
{
    public required string Body { get; set; }
}

public class Srt
{
    public required string Title { get; set; }
    public required string Url { get; set; }
}

public class VideoJson
{
    public required int SeedConst { get; set; }
    public required int Seed { get; set; }
    
    public required List<string> Resolution { get; set; }

    [JsonPropertyName("out_br")]
    public string? Bitrate { get; set; }

    [JsonPropertyName("tsfilesize")]
    public List<int>? TsFilesize { get; set; }
    
    [JsonPropertyName("filesize")]
    public List<int>? Filesize { get; set; }
    
    [JsonPropertyName("hlsPrivate")]
    public int? HlsPrivate { get; set; }

    public List<string>? Hls { get; set; }
    
    [JsonPropertyName("h5pcmp4")]
    public List<string>? H5PcMp4 { get; set; }
    
    public List<string>? Mp4 { get; set; }

    public required string Title { get; set; }
    public required string Duration { get; set; }
    
    public List<Srt>? Srt { get; set; }
}

[JsonSerializable(typeof(VideoUri))]
[JsonSerializable(typeof(JsonResponse))]
[JsonSerializable(typeof(VideoJson))]
public partial class JsonContext : JsonSerializerContext;