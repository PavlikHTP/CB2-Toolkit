namespace CB2Toolkit.Core.Models;

public record SystemMetrics(
    ulong TotalMemoryMb,
    ulong FreeMemoryMb,
    long WorkingSetMb,
    long GcHeapMb,
    string OsVersion,
    string ProcessArch,
    string RuntimeVersion,
    string Error = "None"
);