using System.Security.Cryptography;
using PolyVGet.Misc;

namespace PolyVGet.PolyV;

public class PolyV13 : IPolyVImpl
{
    private const string Md5Salt = "bWztsdNi0XOa3q8D";
    private static readonly byte[] KeyIv = [1, 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 7, 5, 3, 2, 1];
    
    private const string HeaderKeyConstant = "VCFGeyK0YRjsMSLwGhMQAiMhcSj3NFhsLyb1LCHxNCb1Xyi2=";
    private readonly byte[] _headerKey;
    private readonly byte[] _headerIv = [0, 8, 2, 7, 1, 9, 1, 4, 1, 2, 1, 3, 12, 1, 3, 1];

    public PolyV13()
    {
        var constantHash = MD5.HashData(HeaderKeyConstant.Encode()).ToHex();
        _headerKey = constantHash.Substring(4, 16).Encode();
    }

    private static byte[] UnshuffleKey(byte[] shuffled)
    {
        var keyLength = shuffled.Length;

        var blockSide = Math.Sqrt(keyLength);
        var width = (int)Math.Ceiling(blockSide);
        var height = (int)Math.Floor(blockSide);
        var surface = height * width;

        var output = new byte[surface];
        
        var temp = new byte[surface];
        for (var i = 0; i < surface; i++)
            temp[i] = 0x20;

        var counter = 0;
        for (var f = 0; f < width; f++)
        {
            var row = height * f;
            for (var i = 0; i < height; i++)
            {
                if (counter < keyLength)
                {
                    temp[row + i] = shuffled[counter];
                }
                counter++;
            }
        }

        var counter2 = 0;
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                output[counter2] = temp[col * height + row];
                counter2++;
            }
        }

        return output;
    }
    
    private static byte[] UnshuffleHash(byte[] shuffled)
    {
        const int size = 32;
        
        if (shuffled.Length != size)
            throw new ArgumentException($"Shuffled hash must be {size} bytes long");
        
        int[] indices = [
             0,  4,  8, 12, 16, 20, 24, 28, 
             1,  3,  5,  7,  9, 11, 13, 15, 
            17, 19, 21, 23, 25, 27, 29, 31, 
             2,  6, 10, 14, 18, 22, 26, 30
        ];

        var unshuffled = new byte[size];

        for (var i = 0; i < size; i++)
        {
            unshuffled[i] = shuffled[indices[i]];
        }

        return unshuffled;
    }

    public byte[] DecryptKey(byte[] key, int mh, string token)
    {
        var mhHash = MD5.HashData(mh.ToString().Encode()).ToHex();
        var shiftedKey = CryptoUtil.CaesarShift(mhHash.Encode(), mh);

        var unshuffledToken = CryptoUtil.UnshuffleToken(token);
        var tokenHash = MD5.HashData(unshuffledToken.Encode()).ToHex();
        var unshuffledTokenHash = UnshuffleHash(tokenHash.Encode());

        var keyHash = MD5.HashData([..unshuffledTokenHash, ..Md5Salt.Encode(), ..shiftedKey]).ToHex();
        var decryptedKey = CryptoUtil.DecryptAesCbc(keyHash.Substring(9, 16).Encode(), KeyIv, key);
        var unshuffledKey = UnshuffleKey(decryptedKey)[..16];
        
        return unshuffledKey;
    }

    public byte[] DecryptFile(byte[] key, byte[] iv, byte[] encryptedData, int fragmentIndex)
    {
        var decrypted = CryptoUtil.DecryptAesCbc(key, iv, encryptedData);
        var headerDecrypted = CryptoUtil.DecryptHeader(decrypted, _headerKey, _headerIv, fragmentIndex, 1024);

        return headerDecrypted;
    }
}