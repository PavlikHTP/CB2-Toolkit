using System.ComponentModel;
using System.Runtime.CompilerServices;
using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Models;

public class UIElementModel : INotifyPropertyChanged
{
    private string _name;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private string _text;
    private int _r;
    private int _g;
    private int _b;
    private double _opacity;
    private string _miscValue;

    public ElementType Type { get; set; }
    public string Icon { get; set; }

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
    public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
    public double Width { get => _width; set { _width = value; OnPropertyChanged(); } }
    public double Height { get => _height; set { _height = value; OnPropertyChanged(); } }
    public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
    public int R { get => _r; set { _r = Math.Clamp(value, 0, 255); OnPropertyChanged(); } }
    public int G { get => _g; set { _g = Math.Clamp(value, 0, 255); OnPropertyChanged(); } }
    public int B { get => _b; set { _b = Math.Clamp(value, 0, 255); OnPropertyChanged(); } }
    public double Opacity { get => _opacity; set { _opacity = Math.Clamp(value, 0.0, 1.0); OnPropertyChanged(); } }
    public string MiscValue { get => _miscValue; set { _miscValue = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}