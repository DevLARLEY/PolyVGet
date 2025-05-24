using System.Security.Cryptography;
using System.Text;

namespace PolyVGet.polyv;

public class PolyV11 : IPolyV
{
    private const string Md5Salt = "FgzVfucSJUWkSIPYgiua";
    
    private static readonly byte[] KeyIv = [1, 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 7, 5, 3, 2, 1];

    private static byte[] UnshuffleKey(byte[] shuffled)
    {
        const int size = 16;
        
        if (shuffled.Length != size)
            throw new ArgumentException($"Shuffled key must be {size} bytes long");
        
        int[] indices = [
            0,  4,  8, 12, 
            1,  5,  9, 13, 
            2,  6, 10, 14, 
            3,  7, 11, 15
        ];

        var unshuffled = new byte[size];
        
        for (var i = 0; i < size; i++)
        {
            unshuffled[i] = shuffled[indices[i]];
        }

        return unshuffled;
    }

    private static byte[] UnshuffleHash(byte[] shuffled)
    {
        const int size = 32;
        
        if (shuffled.Length != size)
            throw new ArgumentException($"Shuffled hash must be {size} bytes long");
        
        int[] indices = [
            0,  6, 12, 18, 24, 30,  1,  5, 
            7, 11, 13, 17, 19, 23, 25, 29, 
            31,  2,  4,  8, 10, 14, 16, 20, 
            22, 26, 28,  3,  9, 15, 21, 27
        ];

        var unshuffled = new byte[size];

        for (var i = 0; i < size; i++)
        {
            unshuffled[i] = shuffled[indices[i]];
        }

        return unshuffled;
    }
    
    /*private static byte[] CaesarShift(byte[] inputBytes, int mhShift)
    {
        var size = inputBytes.Length;
        var output = new byte[size];

        for (var i = 0; i < size; i++)
        {
            var inputByte = inputBytes[i];

            var shift = inputByte - 65 < 26 ? 65 : 97;
            var shifted = (mhShift + inputByte - shift) % 26;
            
            output[i] = (byte)(shift + shifted);
        }

        return output;
    }*/
    
    private static byte[] CaesarShift(byte[] inputBytes, int mhShift)
    {
        var size = inputBytes.Length;
        var output = new byte[size];

        for (var i = 0; i < size; i++)
        {
            var inputByte = inputBytes[i];

            var shift = inputByte is < 65 or > 90 ? 97 : 65;
            var shifted = (mhShift + inputByte - shift) % 26;
            
            output[i] = (byte)(shift + shifted);
        }

        return output;
    }
    
    public byte[] DecryptKey(byte[] key, int mh, string token)
    {
        var mhHash = MD5.HashData([..Md5Salt.Encode(), ..mh.ToString().Encode()]).ToHex();
        var shiftedKey = CaesarShift(mhHash.Encode(), mh);

        var unshuffledToken = Util.UnshuffleToken(token);
        var tokenHash = MD5.HashData(unshuffledToken.Encode()).ToHex();
        var unshuffledTokenHash = UnshuffleHash(tokenHash.Encode());

        var keyHash = MD5.HashData([..Md5Salt.Encode(), ..shiftedKey, ..unshuffledTokenHash]).ToHex();
        var decryptedKey = Util.DecryptAesCbc(keyHash.Substring(7, 16).Encode(), KeyIv, key);
        var unshuffledKey = UnshuffleKey(decryptedKey);
        
        return unshuffledKey;
    }

    public byte[] DecryptFile(byte[] key, byte[] iv, byte[] encryptedData, int fragmentIndex)
    {
        return Util.DecryptAesCbc(key, iv, encryptedData);
    }
}