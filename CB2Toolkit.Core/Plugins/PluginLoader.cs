using System.Reflection;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.Core.Plugins;

public sealed class PluginLoader
{
    private static readonly Lazy<PluginLoader> _instance = new(() => new PluginLoader());
    public static PluginLoader Instance => _instance.Value;

    private readonly List<PluginInfo> _plugins = new();
    private readonly object _lock = new();
    
    public IReadOnlyList<PluginInfo> Plugins
    {
        get { lock (_lock) return _plugins.AsReadOnly(); }
    }

    public string PluginsFolder => Path.Combine(AppMetadata.AppDataFolder, "Plugins");
    
    private PluginLoader() { }

    public void EnsurePluginsFolder()
    {
        if (!Directory.Exists(PluginsFolder))
        {
            Directory.CreateDirectory(PluginsFolder);
        }
    }

    public List<PluginInfo> DiscoverPlugins()
    {
        EnsurePluginsFolder();
        var discovered = new List<PluginInfo>();

        foreach (var dll in Directory.GetFiles(PluginsFolder, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var info = TryLoadPluginInfo(dll);
                if (info != null)
                {
                    discovered.Add(info);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarn($"Failed to inspect plugin: {Path.GetFileName(dll)} - {ex.Message}");
            }
        }

        return discovered;
    }

    public async Task LoadAllPluginsAsync()
    {
        var discovered = DiscoverPlugins();
        
        foreach (var info in discovered)
        {
            try
            {
                await LoadPluginAsync(info);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"Failed to load plugin '{info.Name}': {ex.Message}");
            }
        }
    }

    public async Task LoadPluginAsync(PluginInfo info)
    {
        var assembly = Assembly.LoadFrom(info.AssemblyPath);
        
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) 
                               && !t.IsInterface 
                               && !t.IsAbstract 
                               && t.GetConstructor(Type.EmptyTypes) != null);

        if (pluginType == null)
        {
            LoggerService.Instance.LogWarn($"No IPlugin implementation found in {Path.GetFileName(info.AssemblyPath)}");
            return;
        }

        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
        var context = new PluginContext(plugin.Name, AppMetadata.AppDataFolder);
        
        await plugin.InitializeAsync(context);
        
        info.Instance = plugin;
        info.IsEnabled = true;
        
        lock (_lock)
        {
            _plugins.Add(info);
        }

        LoggerService.Instance.LogInfo($"Plugin loaded: {plugin.Name} v{plugin.Version} by {plugin.Author}");
    }

    public async Task UnloadPluginAsync(PluginInfo info)
    {
        if (info.Instance != null)
        {
            await info.Instance.ShutdownAsync();
        }

        lock (_lock)
        {
            info.IsEnabled = false;
            _plugins.Remove(info);
        }

        LoggerService.Instance.LogInfo($"Plugin unloaded: {info.Name}");
    }

    public async Task UnloadAllPluginsAsync()
    {
        List<PluginInfo> snapshot;
        lock (_lock)
        {
            snapshot = new List<PluginInfo>(_plugins);
        }

        foreach (var plugin in snapshot)
        {
            await UnloadPluginAsync(plugin);
        }
    }

    private PluginInfo? TryLoadPluginInfo(string dllPath)
    {
        var assembly = Assembly.LoadFrom(dllPath);
        
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) 
                               && !t.IsInterface 
                               && !t.IsAbstract 
                               && t.GetConstructor(Type.EmptyTypes) != null);

        if (pluginType == null) return null;

        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;

        return new PluginInfo
        {
            Name = plugin.Name,
            Description = plugin.Description,
            Author = plugin.Author,
            Version = plugin.Version,
            AssemblyPath = dllPath,
            LoadedAt = DateTime.Now,
            Instance = null
        };
    }
}
