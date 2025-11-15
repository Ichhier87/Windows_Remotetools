using System;
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
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Console.WriteLine("Starting Windows Remote Tools...");

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
