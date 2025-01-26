using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace UpdaterERP.Services
{
    public class FileService
    {
        public async Task ExtractAndMoveFilesAsync(string zipFilePath, string destinationFolderPath)
        {
            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException("Zip file not found.");
            }

            // Ensure the destination folder exists
            if (!Directory.Exists(destinationFolderPath))
            {
                Directory.CreateDirectory(destinationFolderPath); // Create the folder if it doesn't exist
            }

            // Extract the zip file
            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Skip directories (they are represented as empty entries)
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        continue;

                    // Remove the top-level folder from the entry's full name
                    string entryPath = RemoveTopLevelFolder(entry.FullName);

                    // Combine the destination folder path with the modified entry path
                    string destinationPath = System.IO.Path.Combine(destinationFolderPath, entryPath);

                    // Ensure the parent directory exists
                    string parentDirectory = System.IO.Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(parentDirectory))
                    {
                        Directory.CreateDirectory(parentDirectory); // Create the parent directory if it doesn't exist
                    }

                    // Extract the file
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            // Delete the zip file after extraction
            File.Delete(zipFilePath);
        }

        public string RemoveTopLevelFolder(string fullName)
        {
            // Split the full name into parts
            string[] parts = fullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            // If there are no parts, return the original full name
            if (parts.Length == 0)
                return fullName;

            // Remove the first part (top-level folder)
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(1));
        }

        public string FormatBytes(long bytes)
        {
            // Convert bytes to a human-readable format (KB, MB, GB)
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
        public void RunCommand(string workingDirectory, string command)
        {
            try
            {
                // Create a new process to run the command
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = workingDirectory, // Set the working directory
                    Arguments = $"/C {command}", // Run the command and close the window when done
                    UseShellExecute = true, // Enable shell execute to allow visible windows
                    CreateNoWindow = false // Ensure the window is visible
                };

                process.StartInfo = startInfo;

                // Start the process
                process.Start();

                // Wait for the process to exit (optional if you want synchronous execution)
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error running command: {ex.Message}");
            }
        }


        public void RunCommandOld(string workingDirectory, string command)
        {
            try
            {
                // Create a new process to run the command
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = workingDirectory, // Set the working directory
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Arguments = $"/K {command}", // Run the command and keep the window open
                    UseShellExecute = true, // Enable shell execute to allow visible windows
                    CreateNoWindow = false // Set to true if you don't want a visible window
                };

                process.StartInfo = startInfo;

                // Start the process
                process.Start();

                // Read the output (optional)
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                // Wait for the process to exit
                process.WaitForExit();

                // Log the output and errors (optional)
                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine("Output: " + output);
                }
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine("Error: " + error);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error running command: {ex.Message}");
            }
        }
        public void RenameFile(string directoryPath, string oldFileName, string newFileName)
        {
            try
            {
                string oldFilePath = Path.Combine(directoryPath, oldFileName);
                string newFilePath = Path.Combine(directoryPath, newFileName);

                // Check if the old file exists
                if (!File.Exists(oldFilePath))
                {
                    throw new FileNotFoundException($"File not found: {oldFilePath}");
                }

                // Rename the file
                File.Move(oldFilePath, newFilePath,true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error renaming file: {ex.Message}");
            }
        }

        public static (bool state, string message) SetPhpExtensionState(string phpIniPath, string extensionName, bool enable = true)
        {
            // Check if the php.ini file exists
            if (!File.Exists(phpIniPath))
            {
                return (false, "php.ini file not found at the specified path.");
            }

            // Check if the filename is "php.ini"
            string fileName = Path.GetFileName(phpIniPath);
            if (!fileName.Equals("php.ini", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "The specified file is not named 'php.ini'.");
            }

            // Read the php.ini file
            string[] lines = File.ReadAllLines(phpIniPath);

            // Flags to track if the extension exists and its current state
            bool extensionExists = false;
            bool extensionModified = false;

            // Search for the extension
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();

                // Check if the line is related to the extension
                if (trimmedLine.StartsWith($"extension={extensionName}", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith($"extension=php_{extensionName}.dll", StringComparison.OrdinalIgnoreCase))
                {
                    extensionExists = true;

                    // Check if the line is commented out
                    bool isCommented = trimmedLine.StartsWith(";");

                    // Enable the extension
                    if (enable)
                    {
                        if (isCommented)
                        {
                            // Uncomment the line
                            lines[i] = lines[i].TrimStart(';').Trim();
                            extensionModified = true;
                        }
                    }
                    // Disable the extension
                    else
                    {
                        if (!isCommented)
                        {
                            // Comment out the line
                            lines[i] = $";{lines[i]}";
                            extensionModified = true;
                        }
                    }
                }
            }

            // If the extension does not exist and we want to enable it, add it
            if (!extensionExists && enable)
            {
                Array.Resize(ref lines, lines.Length + 1);
                lines[lines.Length - 1] = $"extension={extensionName}"; // or $"extension=php_{extensionName}.dll" for Windows
                extensionModified = true;
            }

            // Save the changes if the extension was modified
            if (extensionModified)
            {
                File.WriteAllLines(phpIniPath, lines);
                return (true, $"The '{extensionName}' extension has been {(enable ? "enabled" : "disabled")} in php.ini.");
            }

            // Return appropriate message if no changes were made
            if (extensionExists)
            {
                return (true, $"The '{extensionName}' extension is already {(enable ? "enabled" : "disabled")} in php.ini.");
            }

            return (false, $"The '{extensionName}' extension could not be modified.");
        }

    }
}