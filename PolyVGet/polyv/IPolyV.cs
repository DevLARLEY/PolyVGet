namespace PolyVGet.polyv;

public interface IPolyV
{
    public byte[] DecryptKey(byte[] key, int mh, string token);
    
    public byte[] DecryptFile(byte[] key, byte[] iv, byte[] encryptedData, int fragmentIndex);
}