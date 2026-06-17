namespace CB2Toolkit.Core.Models;

public record NewsItem(
    string Tag,
    string Date,
    string Title,
    string Preview,
    string Content,
    string Version
);