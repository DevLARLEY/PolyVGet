using System.Security.Cryptography;
using System.Text;
using MediaFormatLibrary.Mpeg2;
using MediaFormatLibrary.Mpeg2.Enums;
using MediaFormatLibrary.Mpeg2.Helpers;
using MediaFormatLibrary.Mpeg2.PES;
using MediaFormatLibrary.Mpeg2.PES.Enums;
using MediaFormatLibrary.Mpeg2.PSI;


namespace PolyVGet.Misc;

public static class CryptoUtil
{
    public static byte[] DecryptAesCbc(byte[] key, byte[] iv, byte[] encryptedData)
    {
        using var aes = Aes.Create();
        
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var cryptoTransform = aes.CreateDecryptor();
        
        return cryptoTransform.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }
    
    public static byte[] DecryptHeader(byte[] data, byte[] key, byte[] iv, int fragmentIndex, int blockSize)
    {
        var dataSize = data.Length;
        var cipherSize = (fragmentIndex % 5 + 1) * blockSize;
        var headerSize = cipherSize + 16;

        var remainingSize = dataSize - headerSize;
        var xorSize = Math.Min(remainingSize, 16);

        var decrypted = new byte[dataSize - xorSize];
        
        using var aes = Aes.Create();
        
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        var cryptoTransform = aes.CreateDecryptor();
        var decryptedHeader = cryptoTransform.TransformFinalBlock(data, 0, headerSize);
        decryptedHeader[0] = 0x47;
        cryptoTransform.Dispose();
        
        Buffer.BlockCopy(decryptedHeader, 0, decrypted, 0, headerSize);
        
        /* XOR trailing 16 bytes and replace padding of decrypted ciphertext */
        var xorValue = fragmentIndex % 19;
        if (xorSize > 0)
            for (var i = 0; i < xorSize; i++)
                decrypted[cipherSize + i] = (byte)(data[headerSize + i] ^ xorValue);

        if (remainingSize - xorSize > 0)
        {
            Buffer.BlockCopy(data, headerSize + xorSize, decrypted, headerSize, remainingSize - xorSize);
        }

        return decrypted;
    }
    
    public static string UnshuffleToken(string encodedString)
    {
        const string cipherChars = "lpmkenjibhuvgycftxdrzsoawq0126783459";
        const string plainChars = "abcdofghijklnmepqrstuvwxyz0123456789";
        
        var result = new StringBuilder();
        
        foreach (var currentChar in encodedString)
        {
            result.Append(plainChars[cipherChars.IndexOf(currentChar)]);
        }
        
        return result.ToString();
    }
    
    public static byte[] CaesarShift(byte[] inputBytes, int mhShift)
    {
        var output = new byte[inputBytes.Length];
        const int shift = 97;

        for (var i = 0; i < inputBytes.Length; i++)
        {
            var inputByte = inputBytes[i];
            
            var shifted = (mhShift + inputByte - shift) % 26;
            if (shifted < 0) 
                shifted += 26;

            output[i] = (byte)(shift + shifted);
        }

        return output;
    }
    
     private static int FindNextNalUnit(byte[] data, int start)
    {
        var length = data.Length;

        for (var i = start; i < length - 3; i++)
        {
            if (data[i] != 0 || data[i + 1] != 0) 
                continue;
            
            if (data[i + 2] == 1 || i + 3 < length && data[i + 2] == 0 && data[i + 3] == 1) 
                return i;
        }

        return -1;
    }

    private static List<(int Start, int End)> FindNalUnits(byte[] data)
    {
        var nalUnits = new List<(int Start, int End)>();
        var start = FindNextNalUnit(data, 0);

        while (start != -1)
        {
            var nextStart = FindNextNalUnit(data, start + 3);
            var end = nextStart != -1 ? nextStart : data.Length;
            nalUnits.Add((start, end));
            start = nextStart;
        }

        return nalUnits;
    }

    private static sbyte Signed(byte val) => unchecked((sbyte)val);
    
    public static void MarsDeobfuscate(byte[] data, string outFile)
    {
        var inStream = new MemoryStream(data);
        var readStream = new Mpeg2TransportStream(inStream, false);
        var streamReader = new Mpeg2TransportStreamReader(readStream);
        
        var writeStream = Mpeg2TransportStream.OpenTs(outFile, FileMode.OpenOrCreate, FileAccess.Write);
        var streamWriter = new Mpeg2TransportStreamWriter(writeStream);

        var pidMap = new Dictionary<ushort, StreamType>();

        while (streamReader.ReadPacketSequence() is { } tsPayload)
        {
            var packets = tsPayload.Packets!;
            var first = packets.First();

            if (tsPayload.Type == TsPayloadType.Pmt)
            {
                if (ProgramSpecificInformationSection.ReadSection(first.Payload!) is ProgramMapSection pmt)
                    foreach (var entry in pmt.StreamEntries)
                        pidMap[entry.Pid] = entry.StreamType;

                writeStream.WritePacket(first);
            } 
            else if (tsPayload.Type == TsPayloadType.Pes)
            {
                var payload = TransportPacketHelper.AssemblePayload(packets);
                        
                if (!pidMap.TryGetValue(first.Header.Pid, out var streamType))
                    continue;
                
                var pesPacket = new PesPacket(payload);
                            
                if (streamType != StreamType.H264)
                {
                    streamWriter.WritePesPacket(first.Header.Pid, pesPacket);
                    continue;
                }
                var pesData = pesPacket.Data!;
                                
                var outStream = new MemoryStream();
                foreach ((int Start, int End) nalUnit in FindNalUnits(pesData))
                {
                    var nalUnitLength = pesData[nalUnit.Start + 2] == 1 ? 3 : 4;
                    var dataStart = nalUnit.Start + nalUnitLength;
                                    
                    outStream.Write(pesData, nalUnit.Start, nalUnitLength);

                    var marsByte = pesData[dataStart] | -128;
                    if ((byte)(marsByte ^ pesData[dataStart + 1]) != (byte)'m') goto skip;
                    if ((byte)(marsByte ^ pesData[dataStart + 2]) != (byte)'a') goto skip;
                    if ((byte)(marsByte ^ pesData[dataStart + 3]) != (byte)'r') goto skip;
                    if ((byte)(marsByte ^ pesData[dataStart + 4]) != (byte)'s') goto skip; 

                    var xorByte = (Signed(pesData[dataStart + 10]) & 12) | (Signed(pesData[dataStart + 12]) & 3);
                    var nalXorByte = xorByte | (Signed(pesData[dataStart + 6]) & -64) | (Signed(pesData[dataStart + 8]) & 48);

                    var nalByte = (byte)(nalXorByte ^ pesData[dataStart + 5]);
                    var nalHeaderByte = (byte)(nalByte >> 3 | nalByte << 5);

                    if (Signed(pesData[dataStart]) >= 0) goto skip;
                                    
                    outStream.WriteByte(nalHeaderByte);

                    var obfuscationType = ((nalByte >> 3) & 31) - 1;

                    for (var i = dataStart + 14; i < nalUnit.End; i++)
                    {
                        var newByte = pesData[i];
                        switch (obfuscationType)
                        { 
                            case 6:
                                newByte = (byte)(newByte ^ xorByte);
                                break;
                            case 7: 
                                var shiftByte = (byte)(newByte ^ nalXorByte);
                                newByte = (byte)(shiftByte << 4 | shiftByte >> 4);
                                break;
                            case 0:
                                var newShiftByte = (byte)(newByte ^ nalXorByte ^ -1);
                                newByte = (byte)(newShiftByte << 4 | newShiftByte >> 4);
                                break;
                        }
                                            
                        outStream.WriteByte(newByte);
                    }

                    goto end;
                                    
                    skip:
                    outStream.Write(pesData, dataStart, nalUnit.End - dataStart);
                                    
                    end:;
                }

                pesPacket.Data = outStream.ToArray();
                            
                streamWriter.WritePesPacket(first.Header.Pid, pesPacket);
            }
            else
            {
                writeStream.WritePacket(first);
            }
        }

        readStream.BaseStream.Dispose();
        writeStream.BaseStream.Dispose();
    }
}