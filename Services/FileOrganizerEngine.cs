using SmartFoldering.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SmartFoldering.Services
{
    public class FileOrganizerEngine
    {
        private readonly ISettingsManager _settingsManager;
        private Timer _fallbackTimer;
        private int _isRunning = 0;

        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();

        public event Action<int> OnScanCompleted;
        public event Action<string> OnError;

        public FileOrganizerEngine(ISettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        public void Start()
        {
            Log.Information("The file system has been started.");
            RestartWatchers();

            _fallbackTimer = new Timer(ExecuteScan, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        public void Stop()
        {
            Log.Information("The file system has been stopped.");
            _fallbackTimer?.Change(Timeout.Infinite, 0);
            StopWatchers();
        }

        public void ForceScanNow()
        {
            Log.Information("A manual scan was triggered by the user.");
            ThreadPool.QueueUserWorkItem(ExecuteScan);
        }

        public void RestartWatchers()
        {
            StopWatchers();

            var settings = _settingsManager.GetSettings();
            if (settings.WatchFolders == null) return;

            foreach (var folder in settings.WatchFolders)
            {
                if (Directory.Exists(folder.Path))
                {
                    var watcher = new FileSystemWatcher(folder.Path)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };


                    watcher.Created += async (s, e) => await HandleNewFileAsync(e.FullPath, folder);
                    watcher.Renamed += async (s, e) => await HandleNewFileAsync(e.FullPath, folder);

                    _watchers.Add(watcher);
                    Log.Information($"Live streaming has started: {folder.Path}");
                }
            }
        }

        private void StopWatchers()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }


        private async Task HandleNewFileAsync(string filePath, WatchFolder folder)
        {
            Log.Information($"A new file has been detected: {filePath}");
            bool isFileReady = await WaitForFileUnlockAsync(filePath, TimeSpan.FromSeconds(60));

            if (isFileReady)
            {
                if (ProcessFile(filePath, folder))
                {
                    OnScanCompleted?.Invoke(1);
                }
            }
            else
            {
                Log.Warning($"The file is locked or inaccessible. The process has switched to the fallback scan: {filePath}");
            }
        }

        private async Task<bool> WaitForFileUnlockAsync(string filePath, TimeSpan timeout)
        {
            var delay = TimeSpan.FromMilliseconds(500);
            var expire = DateTime.Now.Add(timeout);

            while (DateTime.Now < expire)
            {
                try
                {

                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {

                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {

                    Log.Error(ex, $"Unexpected error during file lock check: {filePath}");
                    return false;
                }
            }
            return false;
        }

        private void ExecuteScan(object state)
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 1) return;

            int movedFilesCount = 0;
            try
            {
                var settings = _settingsManager.GetSettings();
                if (settings.WatchFolders == null || settings.WatchFolders.Count == 0) return;

                foreach (var watchFolder in settings.WatchFolders)
                {
                    if (!Directory.Exists(watchFolder.Path)) continue;

                    var files = Directory.GetFiles(watchFolder.Path);
                    foreach (var file in files)
                    {
                        if (ProcessFile(file, watchFolder))
                        {
                            movedFilesCount++;
                        }
                    }
                }

                if (movedFilesCount > 0)
                {
                    Log.Information($"Fallback scan: {movedFilesCount} files moved.");
                    OnScanCompleted?.Invoke(movedFilesCount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred during the scan.");
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }
        private bool ProcessFile(string filePath, WatchFolder watchFolder)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string fileExtension = Path.GetExtension(filePath);

                foreach (var target in watchFolder.TargetFolders)
                {
                    bool matchFound = false;

                    foreach (var rule in target.Rules)
                    {
                        if (rule.Type == RuleType.Extension && fileExtension.Equals(rule.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            matchFound = true; break;
                        }
                        else if (rule.Type == RuleType.Keyword && fileName.IndexOf(rule.Value, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchFound = true; break;
                        }
                    }

                    if (matchFound)
                    {
                        return MoveFile(filePath, target.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Skipped in file rule matching: {Path.GetFileName(filePath)} - {ex.Message}");
            }
            return false;
        }


        private bool MoveFile(string sourceFilePath, string targetDirectory)
        {
            try
            {
                if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);

                string fileName = Path.GetFileName(sourceFilePath);
                string destinationPath = Path.Combine(targetDirectory, fileName);

                int count = 1;
                while (File.Exists(destinationPath))
                {
                    string tempFileName = $"{Path.GetFileNameWithoutExtension(fileName)} ({count++}){Path.GetExtension(fileName)}";
                    destinationPath = Path.Combine(targetDirectory, tempFileName);
                }

                File.Move(sourceFilePath, destinationPath);
                Log.Information($"Transfer Successful: {fileName} -> {targetDirectory}");
                return true;
            }
            catch (IOException)
            {

                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Transfer Error: {Path.GetFileName(sourceFilePath)}");
                return false;
            }
        }
    }
}