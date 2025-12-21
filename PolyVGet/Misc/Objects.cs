namespace PolyVGet.Misc;

public class Playlist
{
    public required List<string> Fragments { get; set; }
    public string? KeyUrl { get; set; }
    public byte[]? Iv { get; set; }
}