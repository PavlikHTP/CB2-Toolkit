using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Models;

public class UIElementModel : INotifyPropertyChanged
{
    public ElementType Type
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string GroupId
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public string Name
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public double X
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double Y
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double Width
    {
        get;
        set
        {
            field = Math.Round(value, 3);
            OnPropertyChanged();
        }
    }

    public double Height
    {
        get;
        set
        {
            field = Math.Round(value, 3);
            OnPropertyChanged();
        }
    }

    public string Text
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public int R
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, 255);
            OnPropertyChanged();
        }
    }

    public int G
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, 255);
            OnPropertyChanged();
        }
    }

    public int B
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, 255);
            OnPropertyChanged();
        }
    }

    public double Opacity
    {
        get;
        set
        {
            field = Math.Clamp(value, 0.0, 1.0);
            OnPropertyChanged();
        }
    }

    public string MiscValue
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Rounded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }
    
    public Fonts Font
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}