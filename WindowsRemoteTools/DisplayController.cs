using System;
using System.Runtime.InteropServices;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Controller for display brightness and power management on Windows
    /// </summary>
    public class DisplayController : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);

        private const int HWND_BROADCAST = 0xffff;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int MONITOR_ON = -1;
        private const int MONITOR_OFF = 2;

        /// <summary>
        /// Sets the display brightness (0-100)
        /// Note: This works on laptops with WMI support
        /// </summary>
        public bool SetBrightness(int brightness)
        {
            try
            {
                // Clamp brightness to 0-100
                brightness = Math.Max(0, Math.Min(100, brightness));

                // Use WMI to set brightness
                using (var mClass = new System.Management.ManagementClass("WmiMonitorBrightnessMethods"))
                {
                    mClass.Scope = new System.Management.ManagementScope(@"\\.\root\wmi");
                    using (var instances = mClass.GetInstances())
                    {
                        foreach (System.Management.ManagementObject instance in instances)
                        {
                            var args = new object[] { 1, brightness };
                            instance.InvokeMethod("WmiSetBrightness", args);
                            Console.WriteLine($"Brightness set to {brightness}%");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting brightness: {ex.Message}");
                Console.WriteLine("Brightness control may not be supported on this system");
            }
            return false;
        }

        /// <summary>
        /// Gets the current display brightness (0-100)
        /// </summary>
        public int GetBrightness()
        {
            try
            {
                using (var mClass = new System.Management.ManagementClass("WmiMonitorBrightness"))
                {
                    mClass.Scope = new System.Management.ManagementScope(@"\\.\root\wmi");
                    using (var instances = mClass.GetInstances())
                    {
                        foreach (System.Management.ManagementObject instance in instances)
                        {
                            var brightness = Convert.ToInt32(instance.GetPropertyValue("CurrentBrightness"));
                            return brightness;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting brightness: {ex.Message}");
            }
            return 50; // Default fallback
        }

        /// <summary>
        /// Turns the display off
        /// </summary>
        public void TurnDisplayOff()
        {
            try
            {
                SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
                Console.WriteLine("Display turned off");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error turning display off: {ex.Message}");
            }
        }

        /// <summary>
        /// Turns the display on
        /// </summary>
        public void TurnDisplayOn()
        {
            try
            {
                SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_ON);
                Console.WriteLine("Display turned on");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error turning display on: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the display is currently on
        /// </summary>
        public bool IsDisplayOn()
        {
            // Note: Windows doesn't provide a direct API to check monitor state
            // This is a simplified implementation
            return true; // Default assumption
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
