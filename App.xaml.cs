using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CSD
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private const string TokenSettingsKey = "Token";
        private Window? _window;

        public static TrayService? TrayService { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            // Unhandled exceptions from WinUI XAML
            UnhandledException += (sender, e) =>
            {
                WriteCrashLog(e.Exception, "WinUI.UnhandledException", message: e.Message);
                e.Handled = true;
            };

            // Unhandled exceptions from other threads (catch-all before process kill)
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                WriteCrashLog(ex, "AppDomain.UnhandledException", isTerminating: e.IsTerminating);
            };

            // Unobserved task exceptions (fire-and-forget tasks)
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                WriteCrashLog(e.Exception, "UnobservedTaskException", observed: e.Observed);
                e.SetObserved();
            };
        }

        private static void WriteCrashLog(Exception? ex, string source,
            bool? isTerminating = null, bool? observed = null, string? message = null)
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CSD", "CrashLogs");
                Directory.CreateDirectory(dir);
                var logPath = System.IO.Path.Combine(dir, "crash.log");

                var sb = new StringBuilder();
                sb.AppendLine($"=== CRASH LOG [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ===");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
                sb.AppendLine($"ProcessId: {Environment.ProcessId}");

                if (isTerminating.HasValue)
                    sb.AppendLine($"IsTerminating: {isTerminating.Value}");
                if (observed.HasValue)
                    sb.AppendLine($"Observed: {observed.Value}");
                if (message != null)
                    sb.AppendLine($"WinUI.Message: {message}");

                if (ex != null)
                {
                    sb.AppendLine($"Exception: {ex.GetType().FullName}");
                    sb.AppendLine($"Message: {ex.Message}");
                    sb.AppendLine($"Stack: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        sb.AppendLine($"Inner: {ex.InnerException.GetType().FullName}");
                        sb.AppendLine($"Inner Msg: {ex.InnerException.Message}");
                        sb.AppendLine($"Inner Stack: {ex.InnerException.StackTrace}");
                    }
                }

                // Log loaded modules (useful for detecting third-party injections like dockmod64.dll)
                try
                {
                    var modules = Process.GetCurrentProcess().Modules
                        .Cast<ProcessModule>()
                        .Select(m => $"{m.ModuleName} @ 0x{m.BaseAddress.ToInt64():X8}");
                    sb.AppendLine("Modules:");
                    foreach (var m in modules) sb.AppendLine($"  {m}");
                }
                catch { /* suppress */ }

                sb.AppendLine("========================================");
                File.AppendAllText(logPath, sb.ToString());
            }
            catch
            {
                // Do not rethrow — we are in an error handler
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var savedToken = AppSettings.Values[TokenSettingsKey] as string;
            _window = string.IsNullOrWhiteSpace(savedToken) ? new InitializationWindow() : new MainWindow();

            _ = Task.Delay(400).ContinueWith(_ => { try { SoundService.PlaySound("startsound.wav"); } catch { } });

            _window.Activate();

            TrayService = new TrayService(_window);
            TrayService.Initialize();

            NotificationService.Instance.Initialize();
            NotificationService.Instance.OpenWindowRequested += () =>
            {
                _window?.DispatcherQueue.TryEnqueue(() =>
                {
                    _window?.Activate();
                });
            };
        }
    }
}
