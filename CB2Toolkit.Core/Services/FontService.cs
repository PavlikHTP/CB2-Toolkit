using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
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
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));

        foreach (var assembly in assemblies)
        {
            string asmName = assembly.GetName().Name;
            string resName = asmName + ".g.resources";

            using var stream = assembly.GetManifestResourceStream(resName);
            if (stream == null) continue;

            using var reader = new ResourceReader(stream);

            foreach (DictionaryEntry entry in reader)
            {
                string resourcePath = entry.Key?.ToString();
                if (string.IsNullOrEmpty(resourcePath) || !resourcePath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileName(resourcePath);
                var packUri = new Uri($"pack://application:,,,/{asmName};component/{resourcePath}");

                string familyName = null;

                try
                {
                    var glyph = new GlyphTypeface(packUri);
                    familyName = glyph.Win32FamilyNames.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v))
                              ?? glyph.FamilyNames.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
                }
                catch
                {
                }

                if (string.IsNullOrEmpty(familyName))
                    continue;
                
                int lastSlash = resourcePath.LastIndexOf('/');
                string folderPath = lastSlash >= 0 ? resourcePath.Substring(0, lastSlash + 1) : "";
                var baseUri = new Uri($"pack://application:,,,/{asmName};component/{folderPath}");

                var family = new FontFamily(baseUri, "./#" + familyName);

                string realKey = familyName.ToLowerInvariant();
                if (!_loaded.ContainsKey(realKey))
                    _loaded[realKey] = family;

                string fileNameKey = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
                if (!_loaded.ContainsKey(fileNameKey))
                    _loaded[fileNameKey] = family;
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