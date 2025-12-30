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
        var colonIndex = line.IndexOf(':');
        if (colonIndex >= 0)
            line = line[(colonIndex + 1)..];
        
        var attributes = new Dictionary<string, string>();

        var isMethod = true;
        var method = new StringBuilder();
        var content = new StringBuilder();
        
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            
            if (c == '=' && isMethod)
            {
                isMethod = false;
            }
            else if (c == ',')
            {
                attributes[method.ToString()] = content.ToString().Trim('"');
                method.Clear();
                content.Clear();
                isMethod = true;
            }
            else if (isMethod)
            {
                method.Append(c);
            }
            else
            {
                content.Append(c);
                if (i == line.Length - 1)
                    attributes[method.ToString()] = content.ToString().Trim('"');
            }
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
                var attributes = ParseHlsLine(line);
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

    public static string FixVideoUri(string videoUri)
    {
        var split = videoUri.Split("_");
        return $"{split[0]}_{videoUri[0]}";
    }
    
    public static string FixLegacyMp4Url(string input)
    {
        var uri = new Uri(input);
        
        var hostParts = uri.Host.Split('.');
        if (hostParts.Length < 2)
            return input;

        var builder = new UriBuilder(uri)
        {
            Host = $"{hostParts[0]}.videocc.net"
        };

        return builder.ToString();
    }
    
    public static string GeneratePid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var value = (int)(Random.Shared.NextDouble() * 1_000_000 + 1_000_000);
        
        return timestamp + "X" + value;
    }
    
    public static string AddUrlQueryParams(UriBuilder builder, params (string Key, string Value)[] parameters)
    {
        var query = HttpUtility.ParseQueryString(builder.Query);

        foreach (var (key, value) in parameters)
            query[key] = value;
        
        builder.Query = query.ToString();
        return builder.ToString();
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