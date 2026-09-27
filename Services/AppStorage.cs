using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SmartFoldering.Services;

public sealed record StorageProfile(string Directory, bool IsSeparate)
{
    public string MutexName => IsSeparate
        ? "SmartFoldering_Profile_" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(OperatingSystem.IsWindows() ? Directory.ToUpperInvariant() : Directory)))
        : "SmartFoldering_UniqueAppKey_2023";
}

public static class AppStorage
{
    public static StorageProfile Profile { get; private set; } = Resolve(Array.Empty<string>());

    public static void Configure(string[] args) => Profile = Resolve(args);

    public static StorageProfile Resolve(string[] args)
    {
        if (args.Length == 0)
            return new StorageProfile(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartFoldering"), false);
        if (args.Length != 2 || args[0] != "--data-directory" ||
            string.IsNullOrWhiteSpace(args[1]) || !Path.IsPathFullyQualified(args[1]))
            throw new ArgumentException("Use --data-directory followed by one absolute folder path, or launch without arguments.");
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
        MovePlanner.EnsureNoLinks(directory);
        var normal = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Resolve(Array.Empty<string>()).Directory));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return new StorageProfile(directory, !directory.Equals(normal, comparison));
    }
}
