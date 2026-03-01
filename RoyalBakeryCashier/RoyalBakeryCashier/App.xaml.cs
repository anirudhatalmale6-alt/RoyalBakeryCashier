using Microsoft.Maui.Controls;

namespace RoyalBakeryCashier
{
    public partial class App : Application
    {
        public static string CrashLogPath => Path.Combine(FileSystem.AppDataDirectory, "crash.log");

        /// <summary>
        /// Terminal mode: "Cashier", "Salesman", or empty (launcher).
        /// Set via compile constant or terminal.config file next to the EXE.
        /// </summary>
        public static string TerminalMode { get; set; } = "";

        /// <summary>
        /// For salesman terminals: the display name (e.g., "Salesman 1", "Salesman 2").
        /// </summary>
        public static string TerminalName { get; set; } = "Salesman";

        /// <summary>
        /// The logged-in user's display name (set after login).
        /// </summary>
        public static string LoggedInUserName { get; set; } = "";

        /// <summary>
        /// The logged-in user's ID (set after login).
        /// </summary>
        public static int LoggedInUserId { get; set; }

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

            // Determine mode from config file next to the EXE
            LoadTerminalConfig();

#if CASHIER_MODE
            TerminalMode = "Cashier";
#elif SALESMAN_MODE
            TerminalMode = "Salesman";
#endif

            // All modes start with login page
            MainPage = new NavigationPage(new Pages.LoginPage())
            {
                BarBackgroundColor = Color.FromArgb("#1A1A1A"),
                BarTextColor = Colors.White
            };
        }

        /// <summary>
        /// Reads terminal.config from the app's base directory.
        /// Format: Mode=Cashier or Mode=Salesman and Name=Salesman 1
        /// </summary>
        private void LoadTerminalConfig()
        {
            try
            {
                // Look for config file next to executable
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDir, "terminal.config");

                if (!File.Exists(configPath))
                {
                    // Also check app data directory
                    configPath = Path.Combine(FileSystem.AppDataDirectory, "terminal.config");
                }

                if (File.Exists(configPath))
                {
                    var lines = File.ReadAllLines(configPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length != 2) continue;
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();

                        if (key.Equals("Mode", StringComparison.OrdinalIgnoreCase))
                            TerminalMode = val;
                        else if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                            TerminalName = val;
                    }
                }
            }
            catch (Exception ex)
            {
                LogCrash("LoadConfig", ex);
            }
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
