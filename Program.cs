using Avalonia;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using SmartFoldering.Services;

namespace SmartFoldering
{
    internal class Program
    {
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr owner, string text, string title, uint type);

        [STAThread]
        public static void Main(string[] args)
        {
            try { AppStorage.Configure(args); }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows())
                    MessageBoxW(IntPtr.Zero, ex.Message, "SmartFoldering startup", 0x10);
                Environment.ExitCode = 2;
                return;
            }
            Mutex? mutex = null;
            bool ownsMutex = false;
           
            string logDirectory = Path.Combine(AppStorage.Profile.Directory, "Logs");
            string logPath = Path.Combine(logDirectory, "log-.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)  
                .CreateLogger();

            try
            {
              
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    Log.Fatal(e.ExceptionObject as Exception, "A critical error has occurred and was caught at the application boundaries!");
                };

                TaskScheduler.UnobservedTaskException += (sender, e) =>
                {
                    Log.Error(e.Exception, "An unobserved Task error has occurred!");
                    e.SetObserved();  
                };

               
                string appName = AppStorage.Profile.MutexName;
                mutex = new Mutex(true, appName, out bool createdNew);
                ownsMutex = createdNew;

                if (!createdNew)
                {
                    Log.Warning("Another instance of the app is already running. A second instance has been blocked.");
                    if (OperatingSystem.IsWindows())
                        MessageBoxW(IntPtr.Zero, "SmartFoldering is already running. Open it from the system tray.", "SmartFoldering", 0x40);
                    return;  
                }

                Log.Information("SmartFoldering is starting...");

              
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The app has unexpectedly closed!");
            }
            finally
            {
               
                Log.Information("SmartFoldering is shutting down...");
                Log.CloseAndFlush();
                if (ownsMutex)
                {
                    mutex?.ReleaseMutex();
                }
                mutex?.Dispose();
            }
        }

       
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
