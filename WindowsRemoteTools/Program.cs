using System;
using System.Linq;
using System.ServiceProcess;
using System.Windows.Forms;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Main entry point for Windows Remote Tools
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Supports the following command-line arguments:
        /// --service: Run as Windows Service
        /// --console: Run in console mode (headless, no GUI)
        /// (no args): Run with GUI (System Tray)
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Check if running as service
            bool runAsService = args.Contains("--service", StringComparer.OrdinalIgnoreCase);
            bool runAsConsole = args.Contains("--console", StringComparer.OrdinalIgnoreCase);

            if (runAsService)
            {
                // Run as Windows Service
                RunAsService();
            }
            else if (runAsConsole)
            {
                // Run in console mode (headless)
                RunAsConsole();
            }
            else
            {
                // Run with GUI (default)
                RunWithGUI();
            }
        }

        /// <summary>
        /// Runs the application as a Windows Service
        /// </summary>
        private static void RunAsService()
        {
            Console.WriteLine("Starting as Windows Service...");
            ServiceBase[] servicesToRun = new ServiceBase[]
            {
                new WindowsRemoteToolsService()
            };
            ServiceBase.Run(servicesToRun);
        }

        /// <summary>
        /// Runs the application in console mode (headless, no GUI)
        /// </summary>
        private static void RunAsConsole()
        {
            Console.WriteLine("Starting in console mode (headless)...");

            // Console mode uses overlay directly (runs in user context)
            using (var core = new RemoteToolsCore(useOverlayDirectly: true))
            {
                core.Start();

                Console.WriteLine("Press Ctrl+C or Q to quit...");

                // Wait for user input to exit
                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q || (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.C))
                    {
                        break;
                    }
                }

                Console.WriteLine("Shutting down...");
            }
        }

        /// <summary>
        /// Runs the application with GUI (System Tray)
        /// </summary>
        private static void RunWithGUI()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Console.WriteLine("Starting Windows Remote Tools with GUI...");

            try
            {
                Application.Run(new TrayApp());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                MessageBox.Show(
                    $"Fatal error: {ex.Message}\n\nSee console for details.",
                    "Windows Remote Tools Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}
