using System.Text.Json;
using CB2Toolkit.Core.Models.Settings;

namespace CB2Toolkit.Core.Services;

public class SettingsService
{
    public static SettingsService Instance { get; } = new();
    
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly string SettingsFilePath = Path.Combine(AppMetadata.AppDataFolder, "settings.json");

    public AppSettings Current { get; private set; } = new();

    private SettingsService() { }

    public async Task LoadAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                Current = new AppSettings();
                await SaveInternalAsync();
                return;
            }

            using FileStream openStream = File.OpenRead(SettingsFilePath);
            var imported = await JsonSerializer.DeserializeAsync<AppSettings>(openStream);
            Current = imported ?? throw new InvalidDataException();
        }
        catch
        {
            Current = new AppSettings();
            await SaveInternalAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> SaveAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await SaveInternalAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ImportAsync(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        await _semaphore.WaitAsync();
        try
        {
            using FileStream openStream = File.OpenRead(filePath);
            var imported = await JsonSerializer.DeserializeAsync<AppSettings>(openStream);
            
            if (imported != null)
            {
                Current = imported;
                await SaveInternalAsync(); 
                return true;
            }
        }
        catch
        {
        }
        finally
        {
            _semaphore.Release();
        }
        return false;
    }

    public async Task<bool> ExportAsync(string targetPath)
    {
        await _semaphore.WaitAsync();
        try
        {
            using FileStream createStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(createStream, Current, new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> SaveInternalAsync()
    {
        try
        {
            if (!Directory.Exists(AppMetadata.AppDataFolder))
            {
                Directory.CreateDirectory(AppMetadata.AppDataFolder);
            }

            using FileStream createStream = new FileStream(SettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(createStream, Current, new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}