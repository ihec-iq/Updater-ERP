using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using UpdaterERP.Services; // Add this namespace

namespace UpdaterERP
{
    public partial class MainWindow : Window
    {
        // GitHub repository details
        private readonly string repoOwner = "ihec-iq";
        private readonly string repoName = "masr-erp";

        // URL for the latest release
        private string frontUrl;

        // Hardcoded backend URL
        private readonly string backUrl = @"https://github.com/ihec-iq/ihec-backend-11/archive/refs/heads/main.zip";

        // Path to the local file for saving paths
        private readonly string pathsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "paths.txt");

        // Services
        private readonly GitHubReleaseService githubService;
        private readonly PathStorageService pathStorageService;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            githubService = new GitHubReleaseService(repoOwner, repoName);
            pathStorageService = new PathStorageService(pathsFilePath);

            // Load saved paths when the application starts
            LoadPaths();

            // Fetch the latest release URL for the frontend
            FetchLatestReleaseUrl();
        }

        private async void FetchLatestReleaseUrl()
        {
            try
            {
                // Fetch the latest release URL
                frontUrl = await githubService.FetchLatestReleaseUrlAsync();

                if (string.IsNullOrEmpty(frontUrl))
                {
                    lblState.Content = "No .zip asset found in the latest release.";
                }
            }
            catch (Exception ex)
            {
                lblState.Content = $"Error fetching latest release URL: {ex.Message}";
            }
        }

        private void LoadPaths()
        {
            try
            {
                // Load paths from the file
                string[] paths = pathStorageService.LoadPaths();

                // Set the paths in the text boxes
                if (paths.Length >= 1)
                    txtPathFront.Text = paths[0];
                if (paths.Length >= 2)
                    txtPathBack.Text = paths[1];
            }
            catch (Exception ex)
            {
                lblState.Content = ex.Message;
            }
        }

        private void SavePaths()
        {
            try
            {
                // Save the paths to the file
                pathStorageService.SavePaths(txtPathFront.Text, txtPathBack.Text);
            }
            catch (Exception ex)
            {
                lblState.Content = ex.Message;
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            string frontFolderPath = txtPathFront.Text;
            string backFolderPath = txtPathBack.Text;

            // Validate folder paths if the corresponding checkbox is checked
            if (ChPathFront.IsChecked == true && string.IsNullOrEmpty(frontFolderPath))
            {
                lblState.Content = "Please enter the frontend folder path.";
                return;
            }

            if (ChPathBack.IsChecked == true && string.IsNullOrEmpty(backFolderPath))
            {
                lblState.Content = "Please enter the backend folder path.";
                return;
            }

            if (ChPathFront.IsChecked == true && !Directory.Exists(frontFolderPath))
            {
                lblState.Content = "The frontend folder path is invalid or does not exist.";
                return;
            }

            if (ChPathBack.IsChecked == true && !Directory.Exists(backFolderPath))
            {
                lblState.Content = "The backend folder path is invalid or does not exist.";
                return;
            }

            try
            {
                // Disable the button during the process
                BtnDownload.IsEnabled = false;

                // Save the paths to the local file
                SavePaths();

                // Show the progress bar and update the label
                ProgressBarLoading.Visibility = Visibility.Visible;
                lblState.Content = "Starting download...";

                // Download and process front file if the checkbox is checked
                if (ChPathFront.IsChecked == true)
                {
                    if (string.IsNullOrEmpty(frontUrl))
                    {
                        lblState.Content = "Failed to fetch the latest frontend release URL.";
                        return;
                    }

                    lblState.Content = "Downloading front files...";
                    string frontZipPath = await DownloadFileAsync(frontUrl, "front.zip", (progress, downloadedBytes, totalBytes) =>
                    {
                        ProgressBarLoading.Value = progress;
                        lblState.Content = $"Downloading front files... {FormatBytes(downloadedBytes)}/{FormatBytes(totalBytes)}";
                    });
                    await ExtractAndMoveFilesAsync(frontZipPath, frontFolderPath);
                    lblState.Content = "Front files downloaded and extracted successfully!";
                }

                // Reset progress bar
                ProgressBarLoading.Value = 0;

                // Download and process back file if the checkbox is checked
                if (ChPathBack.IsChecked == true)
                {
                    lblState.Content = "Downloading back files...";
                    string backZipPath = await DownloadFileAsync(backUrl, "back.zip", (progress, downloadedBytes, totalBytes) =>
                    {
                        ProgressBarLoading.Value = progress;
                        lblState.Content = $"Downloading back files... {FormatBytes(downloadedBytes)}/{FormatBytes(totalBytes)}";
                    });
                    await ExtractAndMoveFilesAsync(backZipPath, backFolderPath);
                    lblState.Content = "Back files downloaded and extracted successfully!";
                }

                // Final message
                lblState.Content = "Update completed successfully!";
            }
            catch (Exception ex)
            {
                lblState.Content = $"An error occurred: {ex.Message}";
            }
            finally
            {
                // Re-enable the button and hide the progress bar
                BtnDownload.IsEnabled = true;
                ProgressBarLoading.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<string> DownloadFileAsync(string url, string fileName, Action<int, long, long> progressCallback)
        {
            using (HttpClient client = new HttpClient())
            {
                // Start the download
                HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // Get the total file size
                long totalBytes = response.Content.Headers.ContentLength ?? 0;

                // Create a stream for the file
                using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                              fileStream = new FileStream(System.IO.Path.Combine(Path.GetTempPath(), fileName), FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    byte[] buffer = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;

                        // Report progress
                        if (totalBytes > 0)
                        {
                            int progressPercentage = (int)((double)totalBytesRead / totalBytes * 100);
                            progressCallback(progressPercentage, totalBytesRead, totalBytes);
                        }
                    }
                }

                return System.IO.Path.Combine(Path.GetTempPath(), fileName);
            }
        }

        private async Task ExtractAndMoveFilesAsync(string zipFilePath, string destinationFolderPath)
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

        private string RemoveTopLevelFolder(string fullName)
        {
            // Split the full name into parts
            string[] parts = fullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            // If there are no parts, return the original full name
            if (parts.Length == 0)
                return fullName;

            // Remove the first part (top-level folder)
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(1));
        }

        private string FormatBytes(long bytes)
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
    }
}