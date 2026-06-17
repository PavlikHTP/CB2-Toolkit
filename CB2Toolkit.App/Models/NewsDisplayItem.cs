using System.Windows;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Models;

public class NewsDisplayItem
{
    public NewsItem Data { get; set; } = null!;
    public string Tag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public Visibility WarningVisibility { get; set; }
}