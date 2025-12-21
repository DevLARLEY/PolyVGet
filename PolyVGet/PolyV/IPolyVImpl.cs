namespace PolyVGet.PolyV;

public interface IPolyVImpl
{
    public byte[] DecryptKey(byte[] key, int mh, string token);
    
    public byte[] DecryptFile(byte[] key, byte[] iv, byte[] encryptedData, int fragmentIndex);
}