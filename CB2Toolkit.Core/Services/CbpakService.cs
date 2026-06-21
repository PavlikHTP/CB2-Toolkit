using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.Core.Services;

public class CbpakService
{
    private static readonly Lazy<CbpakService> _instance = new(() => new CbpakService());
    public static CbpakService Instance => _instance.Value;

    private const int PAK_VERSION = 26534;
    private const long MAX_FILE_SIZE = 268435456;
    private const long MAX_PACK_SIZE = 1431655765;

    private readonly HashSet<string> _blockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "exe", "dll", "vbs", "vbe", "wsf", "ws", "bat", "cmd", "chm", "com", "out", "paf",
        "pex", "ps1", "run", "prg", "pif", "rtf", "pdf", "msc", "msp", "jar", "js",
        "jse", "scf", "lnk", "inf", "reg", "doc", "xls", "ppt", "docm", "dotm", "xlsm",
        "xltm", "xlam", "pptm", "potm", "ppam", "ppsm", "sldm", "wsc", "wsh", "vb"
    };

    private CbpakService() { }

    public async Task PackDirectoryAsync(string directory, string destName, string baseUrl, string serverFolder)
    {
        string[] allFiles = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        int repeatCount = 1;

        List<object> fileEntries = new();
        string finalPak = Path.Combine(directory, $"{destName}.cbpak");

        FileStream pakStream = new FileStream(finalPak, FileMode.Create, FileAccess.Write);
        BinaryWriter writer = new BinaryWriter(pakStream);
        writer.Write(PAK_VERSION);

        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');

            if (fileName.Equals("addons.jsonc", StringComparison.OrdinalIgnoreCase)) continue;
            if (ext.Equals("cbpak", StringComparison.OrdinalIgnoreCase)) continue;

            if (_blockedExtensions.Contains(ext)) continue;

            FileInfo info = new FileInfo(filePath);
            if (info.Length >= MAX_FILE_SIZE) continue;

            if (pakStream.Position + info.Length >= MAX_PACK_SIZE)
            {
                writer.Write(0);
                writer.Close();
                pakStream.Close();

                byte[] compressedBytes = await File.ReadAllBytesAsync(finalPak);
                string hash = CryptoBuffer.GetMd5FromBytes(compressedBytes).ToLowerInvariant();

                fileEntries.Add(new
                {
                    url = $"{baseUrl}/{serverFolder}/{Path.GetFileName(finalPak)}",
                    export = Path.GetFileName(finalPak),
                    hash = hash
                });

                repeatCount++;
                finalPak = Path.Combine(directory, $"{destName}{repeatCount}.cbpak");

                pakStream = new FileStream(finalPak, FileMode.Create, FileAccess.Write);
                writer = new BinaryWriter(pakStream);
                writer.Write(PAK_VERSION);
            }

            string relativePath = Path.GetRelativePath(directory, filePath).Replace('\\', '/').ToLowerInvariant();

            byte[] fileBytes;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var ms = new MemoryStream())
                {
                    await fs.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }
            }

            string fileHash = CryptoBuffer.GetMd5FromBytes(fileBytes).ToLowerInvariant();

            writer.Write(1);

            string meta = $"{relativePath}:::{fileHash}\r\n";
            byte[] metaBytes = Encoding.UTF8.GetBytes(meta);
            writer.Write(metaBytes);

            writer.Write((int)info.Length);
            writer.Write(fileBytes);
        }

        writer.Write(0);
        writer.Close();
        pakStream.Close();

        byte[] finalCompressedBytes = await File.ReadAllBytesAsync(finalPak);
        string finalHash = CryptoBuffer.GetMd5FromBytes(finalCompressedBytes).ToLowerInvariant();

        fileEntries.Add(new
        {
            url = $"{baseUrl}/{serverFolder}/{Path.GetFileName(finalPak)}",
            export = Path.GetFileName(finalPak),
            hash = finalHash
        });

        var jsonObject = new
        {
            serverfolder = serverFolder,
            files = fileEntries
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(jsonObject, options);
        string outputPath = Path.Combine(directory, "addons.jsonc");
        await File.WriteAllTextAsync(outputPath, jsonString);
    }

    public async Task UnpackFileAsync(string cbpakPath, string destFolder)
    {
        using FileStream fs = new FileStream(cbpakPath, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new BinaryReader(fs);

        int version = reader.ReadInt32();
        if (version != PAK_VERSION)
        {
            reader.Close();
            return;
        }

        Directory.CreateDirectory(destFolder);

        while (fs.Position < fs.Length)
        {
            int continueFlag = reader.ReadInt32();
            if (continueFlag == 0) break;

            List<byte> lineBytes = new();
            while (true)
            {
                byte b = reader.ReadByte();
                if (b == '\n') break;
                if (b != '\r') lineBytes.Add(b);
            }

            string line = Encoding.UTF8.GetString(lineBytes.ToArray());
            int sepPos = line.IndexOf(":::");
            string filename = sepPos != -1 ? line.Substring(0, sepPos) : line;

            int size = reader.ReadInt32();
            size = size & 0x7FFFFFFF;

            if (size < MAX_FILE_SIZE)
            {
                string fullOutPath = Path.Combine(destFolder, filename);
                string dir = Path.GetDirectoryName(fullOutPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                byte[] fileData = reader.ReadBytes(size);
                await File.WriteAllBytesAsync(fullOutPath, fileData);
            }
            else
            {
                fs.Seek(size, SeekOrigin.Current);
            }
        }

        reader.Close();
    }
}