using System.Windows;
using System.Windows.Controls;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.CodeEditor.Utils;

public class LifecycleUserControl : UserControl
{
    public LifecycleUserControl()
    {
        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;
    }

    private async void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        try
        {
            await OnViewLoadedAsync();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"LifecycleUserControl Error {ex}");
        }
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e) => OnViewUnloaded();

    protected virtual Task OnViewLoadedAsync() => Task.CompletedTask;
    protected virtual void OnViewUnloaded() { }
}