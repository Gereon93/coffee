namespace CoffeeTest.Helpers;

internal static class SqliteOutageHelper
{
    private static IEnumerable<string> SidecarPaths(string dbPath)
    {
        yield return dbPath;
        yield return dbPath + "-wal";
        yield return dbPath + "-shm";
    }

    internal static void DenyAccess(string dbPath)
    {
        foreach (var path in SidecarPaths(dbPath))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(path, FileAttributes.ReadOnly);
            }
            else
            {
                File.SetUnixFileMode(path, UnixFileMode.None);
            }
        }
    }

    private static void RestoreAccess(string dbPath)
    {
        foreach (var path in SidecarPaths(dbPath))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            else
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }

    internal static void RestoreAccessAndDelete(string dbPath)
    {
        RestoreAccess(dbPath);
        foreach (var path in SidecarPaths(dbPath))
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
