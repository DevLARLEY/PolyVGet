using System.Text;
using System.Web;

namespace PolyVGet.Misc;

public static class Util
{
    public static string Decode(this byte[] input) => Encoding.UTF8.GetString(input);
    public static byte[] Encode(this string input) => Encoding.UTF8.GetBytes(input);
    public static string ToHex(this byte[] input) => Convert.ToHexStringLower(input);

    private static Dictionary<string, string> ParseHlsLine(string line)
    {
        var attributes = new Dictionary<string, string>();
        
        var split = line.Split(',');
        foreach (var s in split)
        {
            var subSplit = s.Split("=");
            attributes[subSplit[0]] = subSplit[1].Trim('"');
        }

        return attributes;
    }
    
    public static Playlist ParsePlaylist(string content)
    {
        var fragments = new List<string>();
        
        string? keyUrl = null;
        byte[]? iv = null;
        
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("#EXT-X-KEY:"))
            {
                var attributes = ParseHlsLine(line[11..]);
                keyUrl = attributes["URI"];
                iv = Convert.FromHexString(attributes["IV"][2..]);
            } 
            else if (line.StartsWith("http"))
            {
                fragments.Add(line);
            }
        }

        return new Playlist
        {
            Fragments = fragments,
            KeyUrl = keyUrl,
            Iv = iv
        };
    }

    public static string ParseToken(string token)
    {
        var split = token.Split('-');
        return split[^1][1..];
    }
    
    public static string ModifyKeyUrl(string originalUrl, string path, string token)
    {
        var uri = new Uri(originalUrl);

        var uriBuilder = new UriBuilder(uri)
        {
            Path = path + uri.AbsolutePath,
        };

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["token"] = token;
        uriBuilder.Query = query.ToString();

        return uriBuilder.Uri.ToString();
    }

    public static void MergeFiles(string filesDir, string outFile)
    {
        var files = Directory.GetFiles(filesDir, "*.bin")
            .Select(f => new
            {
                Path = f,
                Number = int.TryParse(Path.GetFileNameWithoutExtension(f), out var n) ? n : int.MaxValue
            })
            .Where(f => f.Number != int.MaxValue)
            .OrderBy(f => f.Number)
            .Select(f => f.Path)
            .ToList();

        using var output = File.Create(outFile);
        foreach (var file in files)
        {
            using var input = File.OpenRead(file);
            input.CopyTo(output);
        }
    }
}