using System.Windows;
using System.Windows.Controls;

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
        await OnViewLoadedAsync();
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        OnViewUnloaded();
    }

    protected virtual Task OnViewLoadedAsync() => Task.CompletedTask;

    protected virtual void OnViewUnloaded()
    {
    }
}