using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CB2Toolkit.Core.Models;

public class FileNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isExpanded;
    private bool _isUnsaved;
    
    public string Key { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsUnsaved
    {
        get => _isUnsaved;
        set
        {
            _isUnsaved = value;
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