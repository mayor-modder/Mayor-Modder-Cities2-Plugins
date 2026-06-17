namespace SaveInvestigator;

public static class GamePathLocator
{
    private const string Cities2AppId = "1363080";
    private const string DefaultGameFolderName = "Cities Skylines II";
    private const string RelativeManagedDirectory = @"Cities2_Data\Managed";
    private const string DefaultRelativeSavesRoot = @"AppData\LocalLow\Colossal Order\Cities Skylines II\Saves";

    public static string ResolveManagedPath()
    {
        return ResolveManagedPath(Environment.GetEnvironmentVariable, EnumerateDefaultSteamAppsDirectories());
    }

    internal static string ResolveManagedPath(
        Func<string, string?> environmentVariableProvider,
        IEnumerable<string> steamAppsDirectories)
    {
        var managedPath = environmentVariableProvider("CSII_MANAGEDPATH");
        if (!string.IsNullOrWhiteSpace(managedPath) && Directory.Exists(managedPath))
        {
            return managedPath;
        }

        var installPath = environmentVariableProvider("CSII_INSTALLATIONPATH");
        if (TryResolveManagedPathFromInstallRoot(installPath, out managedPath))
        {
            return managedPath;
        }

        foreach (var steamAppsDirectory in EnumerateSteamAppsDirectories(steamAppsDirectories))
        {
            if (TryResolveManagedPathFromSteamAppsDirectory(steamAppsDirectory, out managedPath))
            {
                return managedPath;
            }
        }

        throw new DirectoryNotFoundException("Could not resolve the CS2 managed assemblies path.");
    }

    public static string? ResolveSavesRoot()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return null;
        }

        var savesRoot = Path.Combine(userProfile, DefaultRelativeSavesRoot);
        return Directory.Exists(savesRoot) ? savesRoot : null;
    }

    public static string ResolveSavePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var savesRoot = ResolveSavesRoot()
            ?? throw new DirectoryNotFoundException("Could not locate the default CS2 saves directory.");
        var latestSave = Directory.EnumerateFiles(savesRoot, "*.cok", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Length)
            .FirstOrDefault();

        return latestSave?.FullName
            ?? throw new FileNotFoundException("No .cok save files were found under the default CS2 saves directory.", savesRoot);
    }

    private static bool TryResolveManagedPathFromInstallRoot(string? installPath, out string managedPath)
    {
        managedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return false;
        }

        var candidate = Path.Combine(installPath, RelativeManagedDirectory);
        if (!Directory.Exists(candidate))
        {
            return false;
        }

        managedPath = candidate;
        return true;
    }

    private static bool TryResolveManagedPathFromSteamAppsDirectory(string steamAppsDirectory, out string managedPath)
    {
        managedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(steamAppsDirectory) || !Directory.Exists(steamAppsDirectory))
        {
            return false;
        }

        var appManifestPath = Path.Combine(steamAppsDirectory, $"appmanifest_{Cities2AppId}.acf");
        if (!File.Exists(appManifestPath))
        {
            return false;
        }

        var candidate = Path.Combine(
            steamAppsDirectory,
            "common",
            DefaultGameFolderName,
            RelativeManagedDirectory);
        if (!Directory.Exists(candidate))
        {
            return false;
        }

        managedPath = candidate;
        return true;
    }

    private static IEnumerable<string> EnumerateDefaultSteamAppsDirectories()
    {
        var steamRoots = new[]
        {
            Environment.GetEnvironmentVariable("STEAMROOT"),
            CombineIfSet(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            CombineIfSet(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        };

        foreach (var steamRoot in steamRoots)
        {
            if (string.IsNullOrWhiteSpace(steamRoot))
            {
                continue;
            }

            var steamAppsDirectory = Path.Combine(steamRoot, "steamapps");
            if (Directory.Exists(steamAppsDirectory))
            {
                yield return steamAppsDirectory;
            }
        }
    }

    private static IEnumerable<string> EnumerateSteamAppsDirectories(IEnumerable<string> seedSteamAppsDirectories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();

        foreach (var seedSteamAppsDirectory in seedSteamAppsDirectories)
        {
            if (string.IsNullOrWhiteSpace(seedSteamAppsDirectory))
            {
                continue;
            }

            var normalizedPath = Path.GetFullPath(seedSteamAppsDirectory);
            if (!Directory.Exists(normalizedPath) || !seen.Add(normalizedPath))
            {
                continue;
            }

            pending.Enqueue(normalizedPath);
        }

        while (pending.Count > 0)
        {
            var steamAppsDirectory = pending.Dequeue();
            yield return steamAppsDirectory;

            foreach (var librarySteamAppsDirectory in EnumerateLibrarySteamAppsDirectories(steamAppsDirectory))
            {
                var normalizedPath = Path.GetFullPath(librarySteamAppsDirectory);
                if (!Directory.Exists(normalizedPath) || !seen.Add(normalizedPath))
                {
                    continue;
                }

                pending.Enqueue(normalizedPath);
            }
        }
    }

    private static IEnumerable<string> EnumerateLibrarySteamAppsDirectories(string steamAppsDirectory)
    {
        var libraryFoldersPath = Path.Combine(steamAppsDirectory, "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(libraryFoldersPath))
        {
            var pathIndex = line.IndexOf("\"path\"", StringComparison.Ordinal);
            if (pathIndex < 0)
            {
                continue;
            }

            var firstQuote = line.IndexOf('"', pathIndex + "\"path\"".Length);
            if (firstQuote < 0)
            {
                continue;
            }

            var secondQuote = line.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0)
            {
                continue;
            }

            var libraryRoot = line.Substring(firstQuote + 1, secondQuote - firstQuote - 1)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(libraryRoot))
            {
                continue;
            }

            yield return Path.Combine(libraryRoot, "steamapps");
        }
    }

    private static string? CombineIfSet(string root, string child)
    {
        return string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine(root, child);
    }
}
