using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

using UpdaterERP.Services;

namespace UpdaterERP
{
    public partial class MainWindow : Window
    {
        // GitHub repository details
        private readonly string repoOwner = "ihec-iq";
        private readonly string repoName = "msar-erp";

        // URL for the latest release
        private string frontUrl = "";

        // Hardcoded backend URL
        private readonly string backUrl = @"https://github.com/ihec-iq/msar-backend-11/archive/refs/heads/main.zip";

        // Path to the local file for saving paths
        private readonly string pathsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "paths.txt");

        // Services
        private readonly GitHubReleaseService gitHubService;
        private readonly PathStorageService pathStorageService;
        private readonly FileService fileService;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            gitHubService = new GitHubReleaseService(repoOwner, repoName);
            pathStorageService = new PathStorageService(pathsFilePath);
            fileService = new FileService();

            // Load saved paths when the application starts
            LoadPaths();

            // Fetch the latest release URL for the frontend
            FetchLatestReleaseUrl();
            VersionLabel.Text = GetApplicationVersion();
        }

        private string GetApplicationVersion()
        {
            // Get the version from the assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"v{version.Major}.{version.Minor}.{version.Build}";
        }

        private async void FetchLatestReleaseUrl()
        {
            try
            {
                // Fetch the latest release URL
                frontUrl = await gitHubService.FetchLatestReleaseUrlAsync();
                lblVersion.Content = "Last MSAR ERP Version: " + gitHubService.GitHubName;
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
                if (paths.Length >= 3)
                    txtPathPhpIni.Text = paths[2];
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
                pathStorageService.SavePaths(txtPathFront.Text, txtPathBack.Text, txtPathPhpIni.Text);
            }
            catch (Exception ex)
            {
                lblState.Content = ex.Message;
            }
        }

 
private void BtnBrowseFront_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        dialog.Title = "Select Frontend Folder";

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            txtPathFront.Text = dialog.FileName;
        }
    }

    private void BtnBrowseBack_Click(object sender, RoutedEventArgs e)
        {
            // Open folder browser dialog
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Title = "Select Backend Folder";

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                txtPathBack.Text = dialog.FileName;
            }  
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            string frontFolderPath = txtPathFront.Text;
            string backFolderPath = txtPathBack.Text;
            lblState.Style = (Style)FindResource("NormalLabel");
            // Validate folder paths if the corresponding checkbox is checked
            if (ChPathFront.IsChecked == true && string.IsNullOrEmpty(frontFolderPath))
            {
                lblState.Content = "Please enter the frontend folder path.";
                lblState.Style = (Style)FindResource("ErrorLabel");
                return;
            }

            if (ChPathBack.IsChecked == true && string.IsNullOrEmpty(backFolderPath))
            {
                lblState.Content = "Please enter the backend folder path.";
                lblState.Style = (Style)FindResource("ErrorLabel");
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
                        lblState.Content = $"Downloading front files... {fileService.FormatBytes(downloadedBytes)}/{fileService.FormatBytes(totalBytes)}";
                    });
                    await fileService.ExtractAndMoveFilesAsync(frontZipPath, frontFolderPath);
                    lblState.Content = "Front files downloaded and extracted successfully!";
                }

                // Reset progress bar
                ProgressBarLoading.Value = 0;

                // Download and process back file if the checkbox is checked
                if (ChPathBack.IsChecked == true)
                {
                    var result = FileService.SetPhpExtensionState(phpIniPath: txtPathPhpIni.Text, extensionName: "zip", enable: ChEnableZip.IsChecked==true);
                    if (!result.state)
                    {
                        lblState.Content = "Failed to enable the zip extension in php.ini." + result.message;
                        lblState.Style = (Style)FindResource("ErrorLabel");
                        return;
                    }
                    lblState.Content = "Downloading back files...";
                    string backZipPath = await DownloadFileAsync(backUrl, "back.zip", (progress, downloadedBytes, totalBytes) =>
                    {
                        ProgressBarLoading.Value = progress;
                        lblState.Content = $"Downloading back files... {fileService.FormatBytes(downloadedBytes)}/{fileService.FormatBytes(totalBytes)}";
                    });
                    await fileService.ExtractAndMoveFilesAsync(backZipPath, backFolderPath);
                    lblState.Content = "Back files downloaded and extracted successfully!";

                    // Run "php artisan migrate" in the backend folder after download and extraction
                    lblState.Content = "Running database operations...";
                    if (ChFirstSetup.IsChecked == true)
                    {
                        // Rename .env.example to .env
                        fileService.RenameFile(backFolderPath, ".env.example", ".env");
                        fileService.RunCommand(backFolderPath, "composer update");
                        fileService.RunCommand(backFolderPath, "php artisan migrate --seed");
                        fileService.RunCommand(backFolderPath, "php artisan storage:link");
                        lblState.Content = "Database setup completed successfully!";
                    }
                    else
                    {
                        fileService.RunCommand(backFolderPath, "php artisan migrate");
                        lblState.Content = "Database migrations completed successfully!";
                    }
                }

                // Final message
                lblState.Content = "Download and migration completed successfully!";
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

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            // Get the last word from txtPathFront
            string path = txtPathFront.Text.Trim();
            string lastWord = path.Split('\\').LastOrDefault() ?? string.Empty;

            if (!string.IsNullOrEmpty(lastWord))
            {
                // Construct the URL
                string url = $"http://localhost/{lastWord}";

                // Open the URL in the default browser
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                lblState.Content = "Invalid path. Please check the Frontend Path.";
            }
        }
        private void BtnPhpIni_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "PHP Configuration File (*.ini)|*.ini";
            if (openFileDialog.ShowDialog() == true)
            {
                txtPathPhpIni.Text = openFileDialog.FileName;
                SavePaths();
            }
        }

    }
}