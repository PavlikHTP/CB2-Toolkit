using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CB2Toolkit.Core.Models;

public class FileNode : INotifyPropertyChanged
{
    public string Key { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }

    public bool IsExpanded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsUnsaved
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public List<FileNode> Value { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Extension => Path.GetExtension(Key)?.ToLower() ?? string.Empty;
    
    public override string ToString() => Key;
}