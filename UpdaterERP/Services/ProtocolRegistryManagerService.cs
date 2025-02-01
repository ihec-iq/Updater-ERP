using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;

namespace UpdaterMsarERP.Services
{

    public class ProtocolRegistryManager
    {
        private const string Protocol = "UpdaterMsarERP";
        private static readonly string AppName = Assembly.GetExecutingAssembly().GetName().FullName.ToString().Split(",")[0];

        public static void RegisterProtocol()
        {
            try
            {
                // Check if running as administrator
                if (!IsAdministrator())
                {
                    RunAsAdmin();
                    return;
                }

                // Get the path to your application
                string applicationPath = Process.GetCurrentProcess().MainModule.FileName;

                // Create registry keys
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(Protocol))
                {
                    key.SetValue("", "URL:" + AppName + " Protocol");
                    key.SetValue("URL Protocol", "");

                    using (RegistryKey defaultIcon = key.CreateSubKey("DefaultIcon"))
                    {
                        defaultIcon.SetValue("", applicationPath + ",1");
                    }

                    using (RegistryKey commandKey = key.CreateSubKey(@"shell\open\command"))
                    {
                        commandKey.SetValue("", $"\"{applicationPath}\" \"%1\"");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering protocol: {ex.Message}");
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RunAsAdmin()
        {
            ProcessStartInfo proc = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Assembly.GetEntryAssembly().Location,
                Verb = "runas"
            };

            try
            {
                Process.Start(proc);
                Environment.Exit(0);
            }
            catch
            {
                // User canceled the UAC prompt
            }
        }
    }
}
