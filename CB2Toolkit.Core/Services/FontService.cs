using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using CB2Toolkit.Core.Models;
using FontEnum = CB2Toolkit.Core.Models.Enums.Fonts;

namespace CB2Toolkit.Core.Services;

public class FontService
{
    public static FontService Instance { get; } = new();

    private readonly Dictionary<FontEnum, (string file, double size)> _fontMappings = new()
    {
        [FontEnum.FontDefault] = ("OPPOSans-M.ttf", 14),
        [FontEnum.FontDefaultBig] = ("OPPOSans-M.ttf", 43),
        [FontEnum.FontDigital] = ("DS-Digital.ttf", 20),
        [FontEnum.FontDigitalBig] = ("DS-Digital.ttf", 60),
        [FontEnum.FontJournal] = ("Journal.ttf", 58),
        [FontEnum.FontConsole] = ("Andale Mono.ttf", 16),
        [FontEnum.FontCredits] = ("OPPOSans-M.ttf", 18),
        [FontEnum.FontCreditsBig] = ("OPPOSans-M.ttf", 30),
        [FontEnum.FontTahoma] = ("tahoma.ttf", 34),
        [FontEnum.FontIcons] = ("Icons.ttf", 15),
        [FontEnum.FontDefaultMedium] = ("OPPOSans-M.ttf", 17),
        [FontEnum.FontIconsBig] = ("Icons.ttf", 32),
        [FontEnum.FontConsoleSmall] = ("Andale Mono.ttf", 13.6),
        [FontEnum.FontDefaultSmall] = ("OPPOSans-M.ttf", 10.5),
    };

    private readonly Dictionary<FontEnum, FontInfo> _cache = new();
    private readonly Dictionary<string, FontFamily> _loaded = new();

    public double PreviewHeight { get; set; } = 1024;

    private static readonly Dictionary<string, string> SystemFallback = new()
    {
        ["tahoma.ttf"] = "Tahoma",
    };

    public void Initialize(string baseDirectory = null)
    {
        _cache.Clear();
        _loaded.Clear();
        
        TryLoadFontsFromResources();


        if (_loaded.Count == 0 && !string.IsNullOrEmpty(baseDirectory))
        {
            TryLoadFontsFromDirectory(baseDirectory);
        }
    }

    private void TryLoadFontsFromResources()
    {
        string[] asmNames = { "CB2Toolkit.UIEditor" };

        foreach (string asmName in asmNames)
        {
            Assembly assembly;
            try
            {
                assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == asmName);
            }
            catch
            {
                continue;
            }

            if (assembly == null) continue;

            var fontResources = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            string prefix = asmName + ".Fonts.";

            foreach (string resName in assembly.GetManifestResourceNames())
            {
                if (!resName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string fileName = resName.Substring(prefix.Length);

                try
                {
                    using var stream = assembly.GetManifestResourceStream(resName);
                    if (stream == null) continue;
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    fontResources[fileName] = ms.ToArray();
                }
                catch { }
            }

            if (fontResources.Count == 0) continue;

            string tempDir = Path.Combine(Path.GetTempPath(), "CB2Toolkit-Fonts");
            Directory.CreateDirectory(tempDir);
            var dirUri = new Uri(tempDir + "\\", UriKind.Absolute);

            var uniqueFonts = _fontMappings.Values
                .Select(m => m.file)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string fileName in uniqueFonts)
            {
                if (!fontResources.TryGetValue(fileName, out var fontData)) continue;

                try
                {
                    string tempFile = Path.Combine(tempDir, fileName);

                    if (!File.Exists(tempFile))
                        File.WriteAllBytes(tempFile, fontData);

                    var glyph = new GlyphTypeface(new Uri(tempFile, UriKind.Absolute));
                    string familyName = glyph.Win32FamilyNames.Values
                        .FirstOrDefault(v => !string.IsNullOrEmpty(v))
                        ?? glyph.FamilyNames.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v));

                    if (string.IsNullOrEmpty(familyName)) continue;

                    var family = new FontFamily(dirUri, "./#" + familyName);

                    string realKey = familyName.ToLowerInvariant();
                    if (!_loaded.ContainsKey(realKey))
                        _loaded[realKey] = family;

                    string fileNameKey = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
                    if (!_loaded.ContainsKey(fileNameKey))
                        _loaded[fileNameKey] = family;
                }
                catch { }
            }
        }
    }

    private void TryLoadFontsFromDirectory(string baseDirectory)
    {
        string fontsDir = Path.Combine(baseDirectory, "Fonts");
        if (!Directory.Exists(fontsDir)) return;

        var dirUri = new Uri(fontsDir + "\\", UriKind.Absolute);

        foreach (string ttf in Directory.GetFiles(fontsDir, "*.ttf"))
        {
            string familyName = null;

            try
            {
                var glyph = new GlyphTypeface(new Uri(ttf, UriKind.Absolute));
                familyName = glyph.Win32FamilyNames.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v))
                          ?? glyph.FamilyNames.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
            }
            catch { }

            if (string.IsNullOrEmpty(familyName)) continue;

            var family = new FontFamily(dirUri, "./#" + familyName);

            string realKey = familyName.ToLowerInvariant();
            if (!_loaded.ContainsKey(realKey))
                _loaded[realKey] = family;

            string fileNameKey = Path.GetFileNameWithoutExtension(ttf).ToLowerInvariant();
            if (!_loaded.ContainsKey(fileNameKey))
                _loaded[fileNameKey] = family;
        }
    }

    public void InvalidateCache()
    {
        _cache.Clear();
    }

    public FontInfo GetFontInfo(FontEnum font)
    {
        if (_cache.TryGetValue(font, out var cached))
            return cached;

        if (!_fontMappings.TryGetValue(font, out var mapping))
        {
            _cache[font] = new FontInfo { Family = new FontFamily("Segoe UI"), Size = 14 };
            return _cache[font];
        }

        double scale = PreviewHeight / 1024.0;
        double scaled = mapping.size * scale;
        int gameSize = (int)scaled;
        var info = ResolveFont(mapping.file, Math.Max(1, gameSize));
        _cache[font] = info;
        return info;
    }

    private FontInfo ResolveFont(string fileName, double size)
    {
        string key = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

        if (_loaded.TryGetValue(key, out var family))
            return new FontInfo { Family = family, Size = size };

        var match = _loaded.Keys.FirstOrDefault(k => k.Contains(key));
        if (match != null)
            return new FontInfo { Family = _loaded[match], Size = size };

        if (SystemFallback.TryGetValue(fileName, out var sysName))
            return new FontInfo { Family = new FontFamily(sysName), Size = size };

        return new FontInfo { Family = new FontFamily("Segoe UI"), Size = size };
    }
}