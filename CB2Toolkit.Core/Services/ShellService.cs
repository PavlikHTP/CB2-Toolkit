using System.Runtime.InteropServices;

namespace CB2Toolkit.Core.Services;

public class ShellService
{
    private static readonly Lazy<ShellService> _instance = new(() => new ShellService());
    public static ShellService Instance => _instance.Value;

    private ShellService() { }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPath(string pszPath);

    [DllImport("shell32.dll")]
    private static extern IntPtr ILClone(IntPtr pidl);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ILRemoveLastID(IntPtr pidl);

    [DllImport("shell32.dll")]
    private static extern IntPtr ILFindLastID(IntPtr pidl);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, uint dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);

    /// <summary>
    /// Opens the parent folder in Windows Explorer and selects the specified file or directory.
    /// </summary>
    public void RevealInExplorer(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (Directory.Exists(path))
            {
                IntPtr pidlFolder = ILCreateFromPath(path);
                if (pidlFolder != IntPtr.Zero)
                {
                    try
                    {
                        SHOpenFolderAndSelectItems(pidlFolder, 0, null, 0);
                    }
                    finally
                    {
                        ILFree(pidlFolder);
                    }
                }
            }
            else if (File.Exists(path))
            {
                IntPtr pidlAbsolute = ILCreateFromPath(path);
                if (pidlAbsolute != IntPtr.Zero)
                {
                    IntPtr pidlParent = ILClone(pidlAbsolute);
                    if (pidlParent != IntPtr.Zero)
                    {
                        try
                        {
                            ILRemoveLastID(pidlParent);
                            IntPtr pidlRelative = ILFindLastID(pidlAbsolute);

                            SHOpenFolderAndSelectItems(pidlParent, 1, new[] { pidlRelative }, 0);
                        }
                        finally
                        {
                            ILFree(pidlParent);
                        }
                    }
                    ILFree(pidlAbsolute);
                }
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Opens the specified directory directly from the inside in Windows Explorer using native Shell API.
    /// Creates the directory if it does not exist.
    /// </summary>
    public void OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            ShellExecute(IntPtr.Zero, "open", folderPath, null, null, 1);
        }
        catch
        {
        }
    }
}