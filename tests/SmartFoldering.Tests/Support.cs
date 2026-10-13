namespace SmartFoldering.Tests;

/// <summary>A temporary folder tree, deleted after the test.</summary>
public sealed class TempFolder : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "smartfoldering-tests-" + Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Root);

    public string Dir(string relative)
    {
        string path = Path.Combine(Root, relative);
        Directory.CreateDirectory(path);
        return path;
    }

    public string File(string relative, string content = "data", DateTime? writtenUtc = null)
    {
        string path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        System.IO.File.SetLastWriteTimeUtc(path, writtenUtc ?? DateTime.UtcNow.AddMinutes(-10));
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestClock
{
    public DateTime Now { get; private set; } = DateTime.UtcNow;
    public void Advance(TimeSpan by) => Now += by;
    public Func<DateTime> Func => () => Now;
}
