using System.Globalization;
using System.Reflection;
using CB2Toolkit.Core.Console.Abstraction;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Settings;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.Core.Console.Commands;

public class ConfigCommand : IConsoleCommand
{
    public string Name => "config";
    public string Description => "Views, gets, or modifies application settings dynamically.";
    public string Usage => "config <list|get|set> [property_path] [value]";
    public IReadOnlyList<string> Aliases => new[] { "cfg" };

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var action = context.GetArg(0)?.ToLower();
        var propPath = context.GetArg(1);

        if (string.IsNullOrEmpty(action) || action == "list")
        {
            ListSettings(context);
            return CommandResult.Success();
        }

        if (string.IsNullOrEmpty(propPath))
        {
            return CommandResult.Fail("Property path is required for get/set actions.");
        }

        if (action == "get")
        {
            return GetSetting(context, propPath);
        }

        if (action == "set")
        {
            var valueStr = context.GetArg(2);
            if (valueStr == null)
            {
                return CommandResult.Fail("Value argument is required for set action.");
            }
            return await SetSettingAsync(context, propPath, valueStr);
        }

        return CommandResult.Fail($"Unknown action '{action}'. Use list, get, or set.");
    }

    private void ListSettings(CommandContext context)
    {
        context.Output.WriteInfo("=== Current Settings ===");
        var settings = SettingsService.Instance.Current;
        
        foreach (var prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = prop.GetValue(settings);
            if (val is string || val is ValueType)
            {
                context.Output.WriteLine($"{prop.Name} = {val}");
            }
        }
    }

    private CommandResult GetSetting(CommandContext context, string path)
    {
        if (TryResolveProperty(path, out var targetObj, out var propInfo) && propInfo != null)
        {
            var val = propInfo.GetValue(targetObj);
            context.Output.WriteInfo($"{path} = {val}");
            return CommandResult.Success();
        }

        return CommandResult.Fail($"Property '{path}' not found.");
    }

    private async Task<CommandResult> SetSettingAsync(CommandContext context, string path, string valueStr)
    {
        if (TryResolveProperty(path, out var targetObj, out var propInfo) && propInfo != null && targetObj != null)
        {
            if (!propInfo.CanWrite)
            {
                return CommandResult.Fail($"Property '{path}' is read-only.");
            }

            try
            {
                object convertedVal;
                var targetType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

                if (targetType.IsEnum)
                {
                    convertedVal = Enum.Parse(targetType, valueStr, ignoreCase: true);
                }
                else
                {
                    convertedVal = Convert.ChangeType(valueStr, targetType, CultureInfo.InvariantCulture);
                }

                propInfo.SetValue(targetObj, convertedVal);
                bool saved = await SettingsService.Instance.SaveAsync();

                if (saved)
                {
                    context.Output.WriteSuccess($"Successfully set '{path}' to '{convertedVal}'.");
                    return CommandResult.Success();
                }

                return CommandResult.Fail("Failed to write settings to disk.");
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"Cannot convert '{valueStr}' to {propInfo.PropertyType.Name}: {ex.Message}");
            }
        }

        return CommandResult.Fail($"Property '{path}' not found.");
    }

    private bool TryResolveProperty(string path, out object? targetObj, out PropertyInfo? propInfo)
    {
        targetObj = SettingsService.Instance.Current;
        propInfo = null;

        var parts = path.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            if (targetObj == null) return false;

            var type = targetObj.GetType();
            propInfo = type.GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (propInfo == null) return false;

            if (i < parts.Length - 1)
            {
                targetObj = propInfo.GetValue(targetObj);
            }
        }

        return propInfo != null;
    }
}