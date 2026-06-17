using System.Runtime.InteropServices;

namespace CB2Toolkit.Core.Services;

public class ZstdService
{
    private const string DllName = "zstd_native.dll";

    private static readonly Lazy<ZstdService> _instance = new(() => new ZstdService());
    public static ZstdService Instance => _instance.Value;

    private ZstdService()
    {
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr zstd_compress(byte[] srcPtr, nuint srcLen, int compressionLevel, out nuint outLen);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr zstd_decompress_safe(byte[] srcPtr, nuint srcLen, out nuint outLen);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int zstd_compress_file(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string srcPath, 
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destPath, 
        int compressionLevel);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int zstd_decompress_file_safe(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string srcPath, 
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destPath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void zstd_free_buffer(IntPtr ptr, nuint len);

    public byte[]? CompressBytes(byte[] data, int compressionLevel = 3)
    {
        if (data == null || data.Length == 0) return Array.Empty<byte>();

        IntPtr compressedPtr = zstd_compress(data, (nuint)data.Length, compressionLevel, out nuint outLen);
        if (compressedPtr == IntPtr.Zero) return null;

        try
        {
            byte[] result = new byte[(long)outLen];
            Marshal.Copy(compressedPtr, result, 0, (int)outLen);
            return result;
        }
        finally
        {
            zstd_free_buffer(compressedPtr, outLen);
        }
    }

    public byte[]? DecompressBytes(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0) return Array.Empty<byte>();

        IntPtr decompressedPtr = zstd_decompress_safe(compressedData, (nuint)compressedData.Length, out nuint outLen);
        if (decompressedPtr == IntPtr.Zero) return null;

        try
        {
            byte[] result = new byte[(long)outLen];
            Marshal.Copy(decompressedPtr, result, 0, (int)outLen);
            return result;
        }
        finally
        {
            zstd_free_buffer(decompressedPtr, outLen);
        }
    }

    public Task<int> CompressFileAsync(string sourcePath, string destinationPath, int compressionLevel = 3)
    {
        return Task.Run(() => zstd_compress_file(sourcePath, destinationPath, compressionLevel));
    }

    public Task<int> DecompressFileAsync(string sourceArchivePath, string destinationFilePath)
    {
        return Task.Run(() => zstd_decompress_file_safe(sourceArchivePath, destinationFilePath));
    }
}