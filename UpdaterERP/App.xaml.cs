using Microsoft.Win32;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using UpdaterMsarERP.Services;

namespace UpdaterMsarERP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public string Protocol = "UpdaterMsarERP";
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Handle protocol activation
            if (e.Args.Length > 0 && e.Args[0].StartsWith($"{Protocol}://"))
            {
                HandleProtocolActivation(e.Args[0]);
            }

            // Register protocol if needed
            if (!IsProtocolRegistered())
            {
                ProtocolRegistryManager.RegisterProtocol();
            }
        }

        private bool IsProtocolRegistered()
        {
            
            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey($"{Protocol}\\shell\\open\\command"))
                {
                    if (key == null) return false;

                    // Get the current application path
                    string currentExePath = Process.GetCurrentProcess().MainModule.FileName;

                    // Get the registered command value
                    var registeredCommand = key.GetValue("")?.ToString() ?? string.Empty;

                    // Extract the executable path from the registered command
                    string registeredExePath = registeredCommand
                        .Trim()
                        .Split(' ')[0]                        // Take the first part before any parameters
                        .Trim('"', '\'');                     // Remove surrounding quotes

                    // Compare paths case-insensitive and normalize
                    bool pathsMatch = string.Equals(
                        Path.GetFullPath(registeredExePath).TrimEnd('\\'),
                        Path.GetFullPath(currentExePath).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase
                    );

                    return !string.IsNullOrEmpty(registeredCommand) && pathsMatch;
                }
            }
            catch
            {
                return false;
            }
        }

        private void HandleProtocolActivation(string protocolArgs)
        {
            // Parse the protocol arguments
            // Example: updaterapp://1.2.3
            var version = protocolArgs.Split(new[] { "://" }, StringSplitOptions.None)[1];

            // Launch your updater application
            string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdaterMsarERP.exe");

            if (File.Exists(updaterPath))
            {
                Process.Start(updaterPath, version);
            }
            else
            {
                MessageBox.Show("Updater application not found!");
            }

            // Close the current application if needed
            Application.Current.Shutdown();
        }
    }

}
