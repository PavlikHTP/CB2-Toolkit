using System.ComponentModel;
using System.IO;

namespace CB2Toolkit.CodeEditor.Models;

public class EditorTab : INotifyPropertyChanged
{
    private string _filePath;
    private bool _isUnsaved;

    public EditorTab(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Name => Path.GetFileName(FilePath);

    public bool IsUnsaved
    {
        get => _isUnsaved;
        set
        {
            if (_isUnsaved == value) return;
            _isUnsaved = value;
            OnPropertyChanged(nameof(IsUnsaved));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
