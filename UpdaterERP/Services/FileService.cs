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
                    Arguments = $"/C {command}", // Run the command and terminate
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
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
    }
}