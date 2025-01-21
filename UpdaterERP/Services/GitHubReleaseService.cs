using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpdaterERP.Services
{
    public class GitHubReleaseService
    {
        private readonly string repoOwner;
        private readonly string repoName;

        public GitHubReleaseService(string repoOwner, string repoName)
        {
            this.repoOwner = repoOwner;
            this.repoName = repoName;
        }

        public async Task<string> FetchLatestReleaseUrlAsync()
        {
            try
            {
                // GitHub API endpoint for the latest release
                string apiUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";

                using (HttpClient client = new HttpClient())
                {
                    // Set user agent header (required by GitHub API)
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("UpdaterERP");

                    // Fetch the latest release details
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();

                    // Parse the JSON response
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        JsonElement root = doc.RootElement;

                        // Get the assets array
                        JsonElement assets = root.GetProperty("assets");

                        // Find the .zip asset
                        foreach (JsonElement asset in assets.EnumerateArray())
                        {
                            string assetName = asset.GetProperty("name").GetString();
                            if (assetName.EndsWith(".zip"))
                            {
                                return asset.GetProperty("browser_download_url").GetString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                throw new Exception($"Error fetching latest release URL: {ex.Message}");
            }

            return null; // Return null if no .zip asset is found
        }
    }
}