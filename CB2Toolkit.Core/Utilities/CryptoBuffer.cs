using System.Security.Cryptography;
using System.Text;

namespace CB2Toolkit.Core.Utilities;

public static class CryptoBuffer
{
    public static string GetMd5FromBytes(ReadOnlySpan<byte> source)
    {
        Span<byte> hashBuffer = stackalloc byte[16];
        MD5.HashData(source, hashBuffer);
        return Convert.ToHexString(hashBuffer);
    }
    
    public static string GetMd5(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = MD5.HashData(inputBytes);
        return Convert.ToHexString(hashBytes);
    }
}