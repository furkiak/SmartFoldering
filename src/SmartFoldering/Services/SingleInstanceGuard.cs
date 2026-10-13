using SmartFoldering.Core;

namespace SmartFoldering.Services;

/// <summary>
/// Only one SmartFoldering may run per user session: two engines would race to move the same files. A second launch
/// asks the running instance to show its window instead.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\SmartFoldering.SingleInstance";
    private const string ShowEventName = @"Local\SmartFoldering.ShowWindow";

    private readonly Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private volatile bool _disposed;

    public bool IsPrimary { get; }

    /// <summary>Raised on a background thread when another launch asked this instance to show itself.</summary>
    public event Action? ShowRequested;

    public SingleInstanceGuard()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            IsPrimary = createdNew;
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
            }
        }
        catch (Exception ex)
        {
            // If the platform cannot create named mutexes, run anyway rather than refusing to start.
            Log.Warn("Single-instance check unavailable", ex);
            IsPrimary = true;
        }
    }

    /// <summary>Starts listening for "show window" requests (Windows only; other platforms have no named events).</summary>
    public void StartListening()
    {
        if (!OperatingSystem.IsWindows() || !IsPrimary) return;
        try
        {
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _showEvent = showEvent;
            new Thread(() =>
            {
                while (!_disposed && showEvent.WaitOne())
                {
                    if (_disposed) break;
                    ShowRequested?.Invoke();
                }
            })
            { IsBackground = true, Name = "SmartFoldering.SingleInstance" }.Start();
        }
        catch (Exception ex)
        {
            Log.Warn("Cannot listen for second launches", ex);
        }
    }

    /// <summary>Called by a second launch: wakes the running instance.</summary>
    public static void SignalPrimary()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var handle = EventWaitHandle.OpenExisting(ShowEventName);
            handle.Set();
        }
        catch
        {
            // The running instance is not listening (yet); nothing else to do.
        }
    }

    public void Dispose()
    {
        _disposed = true;
        try { _showEvent?.Set(); } catch { /* closing */ }
        _showEvent?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { /* not owned */ }
        _mutex?.Dispose();
    }
}
