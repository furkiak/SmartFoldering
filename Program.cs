using Avalonia;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartFoldering
{
    internal class Program
    {
        
        private static Mutex _mutex = null;

        [STAThread]
        public static void Main(string[] args)
        {
           
            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartFoldering", "Logs");
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

               
                const string appName = "SmartFoldering_UniqueAppKey_2023";
                _mutex = new Mutex(true, appName, out bool createdNew);

                if (!createdNew)
                {
                    Log.Warning("Another instance of the app is already running. A second instance has been blocked.");
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
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
        }

       
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}