using System.Text.Json.Serialization;

namespace PolyVGet;

public class WingFoxVideoUriData
{
    public required string VideoVid { get; set; }
}

public class WingFoxVideoUri
{
    public required WingFoxVideoUriData Data { get; set; }
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
    public required List<string> Resolution { get; set; }
    
    [JsonPropertyName("hlsPrivate")]
    public int? HlsPrivate { get; set; }

    public required List<string> Hls { get; set; }
    public required string Title { get; set; }
    public required List<int> Filesize { get; set; }
    public required string Duration { get; set; }
    public List<Srt>? Srt { get; set; }
}

[JsonSerializable(typeof(WingFoxVideoUri))]
[JsonSerializable(typeof(JsonResponse))]
[JsonSerializable(typeof(VideoJson))]
public partial class JsonContext : JsonSerializerContext;