using Microsoft.Maui.Controls;

namespace RoyalBakeryCashier
{
    public partial class App : Application
    {
        public static string CrashLogPath => Path.Combine(FileSystem.AppDataDirectory, "crash.log");

        public App()
        {
            InitializeComponent();

            // Global exception handlers to prevent silent crashes
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                LogCrash("AppDomain", ex);
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogCrash("Task", e.Exception);
                e.SetObserved();
            };

            MainPage = new NavigationPage(new Pages.LauncherPage())
            {
                BarBackgroundColor = Color.FromArgb("#1A1A1A"),
                BarTextColor = Colors.White
            };
        }

        public static void LogCrash(string source, Exception ex)
        {
            try
            {
                string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\n{ex}\n\n";
                File.AppendAllText(CrashLogPath, msg);
            }
            catch { }
        }
    }
}