using System.Runtime.InteropServices;
using Microsoft.Win32;

public static class FileAssociationService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static void Register()
    {
        try
        {
            string exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.as"))
            {
                key.SetValue("", "CB2Toolkit.AngelScript");
            }

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CB2Toolkit.AngelScript"))
            {
                key.SetValue("", "AngelScript File");
            }

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CB2Toolkit.AngelScript\shell\open\command"))
            {
                key.SetValue("", $"\"{exePath}\" \"%1\"");
            }

            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
        }
    }
}