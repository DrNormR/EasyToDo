using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace EasyToDo.Services
{
    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/DrNormR/EasyToDo/releases/latest";
        private const string UserAgent = "EasyToDo-UpdateChecker/1.0";
        private static readonly HttpClient _httpClient = new();
        private static UpdateProgressWindow? _progressWindow;

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            _httpClient.Timeout = TimeSpan.FromMinutes(10); // Allow longer downloads
        }

        /// <summary>
        /// Tests version parsing with various tag formats (for debugging)
        /// </summary>
        public static void TestVersionParsing()
        {
            var testTags = new[]
            {
                "easytodov2.3",
                "v2.3.0",
                "2.3.0",
                "easytodo-v2.3.1",
                "EasyToDoV2.4",
                "release-2.5.0"
            };

            System.Diagnostics.Debug.WriteLine("?? Testing version parsing...");
            
            foreach (var tag in testTags)
            {
                System.Diagnostics.Debug.WriteLine($"\n??? Testing tag: '{tag}'");
                
                var versionString = tag.ToLowerInvariant();
                var prefixes = new[] { "easytodov", "easytodo-v", "easytodo_v", "easytodo", "v", "version" };
                
                foreach (var prefix in prefixes)
                {
                    if (versionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        versionString = versionString[prefix.Length..];
                        System.Diagnostics.Debug.WriteLine($"   ?? Removed '{prefix}' prefix: '{versionString}'");
                        break;
                    }
                }
                
                versionString = versionString.TrimStart('-', '_', '.');
                
                var versionMatch = System.Text.RegularExpressions.Regex.Match(versionString, @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?");
                if (versionMatch.Success)
                {
                    var major = versionMatch.Groups[1].Value;
                    var minor = versionMatch.Groups[2].Success ? versionMatch.Groups[2].Value : "0";
                    var build = versionMatch.Groups[3].Success ? versionMatch.Groups[3].Value : "0";
                    var revision = versionMatch.Groups[4].Success ? versionMatch.Groups[4].Value : "0";
                    
                    versionString = $"{major}.{minor}.{build}.{revision}";
                    
                    if (Version.TryParse(versionString, out var parsedVersion))
                    {
                        System.Diagnostics.Debug.WriteLine($"   ? Parsed successfully: {parsedVersion}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"   ? Parse failed: '{versionString}'");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"   ? Regex match failed for: '{versionString}'");
                }
            }
        }

        /// <summary>
        /// Gets the current version of the application
        /// </summary>
        public static Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
        }

        /// <summary>
        /// Checks if a newer version is available on GitHub
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"?? Starting update check...");
                System.Diagnostics.Debug.WriteLine($"?? GitHub API URL: {GitHubApiUrl}");
                
                var response = await _httpClient.GetStringAsync(GitHubApiUrl);
                System.Diagnostics.Debug.WriteLine($"? GitHub API response received (length: {response.Length} chars)");
                
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                var tagName = root.GetProperty("tag_name").GetString();
                System.Diagnostics.Debug.WriteLine($"??? GitHub tag_name: '{tagName}'");
                
                if (string.IsNullOrEmpty(tagName)) 
                {
                    System.Diagnostics.Debug.WriteLine("? Tag name is null or empty");
                    return null;
                }

                // More robust version extraction
                var versionString = tagName.ToLowerInvariant();
                
                // Remove common prefixes (including your specific format)
                var prefixes = new[] { "easytodov", "easytodo-v", "easytodo_v", "easytodo", "v", "version" };
                foreach (var prefix in prefixes)
                {
                    if (versionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        versionString = versionString[prefix.Length..];
                        System.Diagnostics.Debug.WriteLine($"?? Removed '{prefix}' prefix: '{versionString}'");
                        break;
                    }
                }
                
                // Remove common separators after prefix
                versionString = versionString.TrimStart('-', '_', '.');
                
                // Extract version numbers using regex (handles formats like "2.3.0-beta", "2.3", etc.)
                var versionMatch = System.Text.RegularExpressions.Regex.Match(versionString, @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?");
                if (versionMatch.Success)
                {
                    var major = versionMatch.Groups[1].Value;
                    var minor = versionMatch.Groups[2].Success ? versionMatch.Groups[2].Value : "0";
                    var build = versionMatch.Groups[3].Success ? versionMatch.Groups[3].Value : "0";
                    var revision = versionMatch.Groups[4].Success ? versionMatch.Groups[4].Value : "0";
                    
                    versionString = $"{major}.{minor}.{build}.{revision}";
                    System.Diagnostics.Debug.WriteLine($"?? Normalized version: '{versionString}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"? Could not extract version numbers from: '{versionString}'");
                }

                if (!Version.TryParse(versionString, out var latestVersion))
                {
                    System.Diagnostics.Debug.WriteLine($"? Failed to parse version string: '{versionString}'");
                    return null;
                }

                var currentVersion = GetCurrentVersion();
                
                System.Diagnostics.Debug.WriteLine($"?? Version Comparison:");
                System.Diagnostics.Debug.WriteLine($"   Current: {currentVersion} ({currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}.{currentVersion.Revision})");
                System.Diagnostics.Debug.WriteLine($"   Latest:  {latestVersion} ({latestVersion.Major}.{latestVersion.Minor}.{latestVersion.Build}.{latestVersion.Revision})");
                System.Diagnostics.Debug.WriteLine($"   Comparison result: {latestVersion.CompareTo(currentVersion)} (>0 means update available)");
                
                if (latestVersion > currentVersion)
                {
                    System.Diagnostics.Debug.WriteLine("? Update available - processing release info...");
                    
                    var releaseUrl = root.GetProperty("html_url").GetString();
                    var releaseNotes = root.GetProperty("body").GetString();
                    var publishedAt = root.GetProperty("published_at").GetString();
                    
                    System.Diagnostics.Debug.WriteLine($"?? Release URL: {releaseUrl}");
                    System.Diagnostics.Debug.WriteLine($"?? Published: {publishedAt}");
                    System.Diagnostics.Debug.WriteLine($"?? Release notes length: {releaseNotes?.Length ?? 0} chars");
                    
                    // Look for download assets (prefer MSI, then ZIP)
                    string? downloadUrl = null;
                    string? updateType = null;
                    
                    if (root.TryGetProperty("assets", out var assetsElement))
                    {
                        var assetCount = assetsElement.GetArrayLength();
                        System.Diagnostics.Debug.WriteLine($"?? Found {assetCount} assets");
                        
                        // First pass: Look for MSI files
                        foreach (var asset in assetsElement.EnumerateArray())
                        {
                            var assetName = asset.GetProperty("name").GetString();
                            System.Diagnostics.Debug.WriteLine($"   Asset: '{assetName}'");
                            
                            if (assetName != null && 
                                (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) &&
                                 assetName.Contains("EasyToDo", StringComparison.OrdinalIgnoreCase)))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                updateType = "MSI";
                                System.Diagnostics.Debug.WriteLine($"? Found MSI asset: {assetName}");
                                break;
                            }
                        }

                        // Second pass: Look for ZIP files if no MSI found
                        if (downloadUrl == null)
                        {
                            System.Diagnostics.Debug.WriteLine("?? No MSI found, looking for ZIP files...");
                            foreach (var asset in assetsElement.EnumerateArray())
                            {
                                var assetName = asset.GetProperty("name").GetString();
                                if (assetName != null && 
                                    (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                     assetName.Contains("EasyToDo", StringComparison.OrdinalIgnoreCase)))
                                {
                                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                    updateType = "ZIP";
                                    System.Diagnostics.Debug.WriteLine($"? Found ZIP asset: {assetName}");
                                    break;
                                }
                            }
                        }
                        
                        if (downloadUrl == null)
                        {
                            System.Diagnostics.Debug.WriteLine("?? No suitable download assets found");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("?? No assets property found in release");
                    }

                    var updateInfo = new UpdateInfo
                    {
                        LatestVersion = latestVersion,
                        CurrentVersion = currentVersion,
                        ReleaseUrl = releaseUrl ?? "",
                        ReleaseNotes = releaseNotes ?? "",
                        PublishedDate = DateTime.TryParse(publishedAt, out var date) ? date : DateTime.Now,
                        DownloadUrl = downloadUrl,
                        UpdateType = updateType ?? "Unknown"
                    };
                    
                    System.Diagnostics.Debug.WriteLine($"?? Update info created successfully");
                    return updateInfo;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("?? No update available - current version is up to date or newer");
                }

                return null; // No update available
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error checking for updates: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"?? Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Shows update dialog and handles user choice
        /// </summary>
        public static async Task<bool> ShowUpdateDialogAsync(UpdateInfo updateInfo)
        {
            var updateTypeDisplay = updateInfo.UpdateType switch
            {
                "MSI" => "MSI Installer (Recommended)",
                "ZIP" => "ZIP Archive",
                _ => "Download"
            };

            var message = $"?? New Version Available!\n\n" +
                         $"Current version: {updateInfo.CurrentVersion}\n" +
                         $"Latest version: {updateInfo.LatestVersion}\n" +
                         $"Released: {updateInfo.PublishedDate:MMM dd, yyyy}\n" +
                         $"Update type: {updateTypeDisplay}\n\n";

            if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
            {
                var notes = updateInfo.ReleaseNotes.Length > 200 
                    ? updateInfo.ReleaseNotes[..200] + "..."
                    : updateInfo.ReleaseNotes;
                message += $"What's new:\n{notes}\n\n";
            }

            if (!string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                if (updateInfo.UpdateType == "MSI")
                {
                    message += "Would you like to download and install the update now?\n\n" +
                              "?? The MSI installer will guide you through the update process.";
                }
                else
                {
                    message += "Would you like to download and install the update now?\n\n" +
                              "?? The application will close during update.";
                }
                
                var result = MessageBox.Show(
                    message,
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // Test the download URL first
                    System.Diagnostics.Debug.WriteLine("?? Testing download URL before full download...");
                    var (isValid, testMessage, contentLength) = await TestDownloadUrlAsync(updateInfo.DownloadUrl);
                    
                    if (!isValid)
                    {
                        System.Diagnostics.Debug.WriteLine($"? Download URL test failed: {testMessage}");
                        MessageBox.Show(
                            $"Download URL Validation Failed\n\n" +
                            $"The update download URL appears to be invalid:\n" +
                            $"{testMessage}\n\n" +
                            $"This could be due to:\n" +
                            $"• Temporary GitHub server issues\n" +
                            $"• Network connectivity problems\n" +
                            $"• Firewall/antivirus blocking access\n\n" +
                            $"URL: {updateInfo.DownloadUrl}\n\n" +
                            $"Please try again later or visit GitHub manually.",
                            "Download Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return false;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"? Download URL test passed: {testMessage}");
                    return await DownloadAndInstallUpdateAsync(updateInfo);
                }
            }
            else
            {
                message += "Click OK to visit the release page to download manually.";
                
                var result = MessageBox.Show(
                    message,
                    "Update Available",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    Process.Start(new ProcessStartInfo(updateInfo.ReleaseUrl) { UseShellExecute = true });
                }
            }

            return false;
        }

        /// <summary>
        /// Downloads and installs the update with progress indication
        /// </summary>
        private static async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
        {
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                System.Diagnostics.Debug.WriteLine("? Download URL is null or empty");
                return false;
            }

            UpdateProgressWindow? progressWindow = null;
            string? downloadPath = null;

            try
            {
                System.Diagnostics.Debug.WriteLine($"?? Starting download from: {updateInfo.DownloadUrl}");
                
                // Show progress window
                progressWindow = new UpdateProgressWindow();
                _progressWindow = progressWindow;
                progressWindow.Show();
                progressWindow.UpdateStatus("Preparing update...");

                // Create temp directory for update files
                var tempDir = Path.Combine(Path.GetTempPath(), "EasyToDo_Update");
                System.Diagnostics.Debug.WriteLine($"?? Temp directory: {tempDir}");
                
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                    System.Diagnostics.Debug.WriteLine("??? Cleaned existing temp directory");
                }
                Directory.CreateDirectory(tempDir);

                var fileExtension = updateInfo.UpdateType == "MSI" ? ".msi" : ".zip";
                downloadPath = Path.Combine(tempDir, $"update{fileExtension}");
                System.Diagnostics.Debug.WriteLine($"?? Download path: {downloadPath}");

                progressWindow.UpdateStatus("Downloading update...");

                // Enhanced download with better error handling
                System.Diagnostics.Debug.WriteLine("?? Starting HTTP request...");
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                
                System.Diagnostics.Debug.WriteLine($"?? HTTP Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"?? Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"HTTP request failed with status {response.StatusCode}: {response.ReasonPhrase}");
                }

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                System.Diagnostics.Debug.WriteLine($"?? Content length: {totalBytes} bytes ({totalBytes / 1024 / 1024:F2} MB)");
                
                if (totalBytes == 0)
                {
                    System.Diagnostics.Debug.WriteLine("?? Warning: Content-Length is 0 or not provided");
                }

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                int bytesRead;
                var lastProgressUpdate = DateTime.Now;

                System.Diagnostics.Debug.WriteLine("?? Starting download stream...");

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    
                    // Update progress more frequently for debugging
                    var now = DateTime.Now;
                    if (now - lastProgressUpdate > TimeSpan.FromMilliseconds(500) || totalBytes == 0)
                    {
                        lastProgressUpdate = now;
                        
                        if (totalBytes != 0)
                        {
                            var progress = (int)((totalBytesRead * 100) / totalBytes);
                            progressWindow.UpdateProgress(progress);
                            progressWindow.UpdateStatus($"Downloaded {totalBytesRead / 1024 / 1024:F1} MB of {totalBytes / 1024 / 1024:F1} MB");
                            System.Diagnostics.Debug.WriteLine($"?? Progress: {progress}% ({totalBytesRead}/{totalBytes} bytes)");
                        }
                        else
                        {
                            progressWindow.UpdateStatus($"Downloaded {totalBytesRead / 1024 / 1024:F1} MB (size unknown)");
                            System.Diagnostics.Debug.WriteLine($"?? Downloaded: {totalBytesRead} bytes (total size unknown)");
                        }
                    }
                }

                // Ensure file is written to disk
                await fileStream.FlushAsync();
                fileStream.Close();
                
                System.Diagnostics.Debug.WriteLine($"? Download completed. Total bytes read: {totalBytesRead}");

                // Verify the downloaded file
                if (File.Exists(downloadPath))
                {
                    var downloadedFileInfo = new FileInfo(downloadPath);
                    System.Diagnostics.Debug.WriteLine($"?? Downloaded file size: {downloadedFileInfo.Length} bytes ({downloadedFileInfo.Length / 1024 / 1024:F2} MB)");
                    
                    if (downloadedFileInfo.Length == 0)
                    {
                        throw new InvalidOperationException("Downloaded file is 0 bytes. The download may have failed or been interrupted.");
                    }
                    
                    if (totalBytes > 0 && downloadedFileInfo.Length != totalBytes)
                    {
                        System.Diagnostics.Debug.WriteLine($"?? Warning: Downloaded file size ({downloadedFileInfo.Length}) doesn't match expected size ({totalBytes})");
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Downloaded file not found at: {downloadPath}");
                }

                progressWindow.UpdateStatus("Preparing installation...");

                // Handle different update types
                if (updateInfo.UpdateType == "MSI")
                {
                    return await InstallMsiUpdateAsync(downloadPath);
                }
                else
                {
                    return await InstallZipUpdateAsync(downloadPath, tempDir);
                }
            }
            catch (HttpRequestException httpEx)
            {
                System.Diagnostics.Debug.WriteLine($"? HTTP Error downloading update: {httpEx.Message}");
                progressWindow?.Close();
                
                MessageBox.Show(
                    $"Network Error During Download\n\n" +
                    $"Failed to download the update file:\n{httpEx.Message}\n\n" +
                    $"Please check:\n" +
                    $"• Your internet connection\n" +
                    $"• Firewall or antivirus blocking the download\n" +
                    $"• GitHub.com accessibility\n\n" +
                    $"Download URL: {updateInfo.DownloadUrl}",
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error downloading/installing update: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"?? Stack trace: {ex.StackTrace}");
                
                progressWindow?.Close();
                
                var errorMessage = $"Update Download/Installation Failed\n\n" +
                                 $"Error: {ex.Message}\n\n";
                
                if (!string.IsNullOrEmpty(downloadPath) && File.Exists(downloadPath))
                {
                    var fileSize = new FileInfo(downloadPath).Length;
                    errorMessage += $"Downloaded file: {downloadPath}\n" +
                                   $"File size: {fileSize} bytes\n\n";
                }
                
                errorMessage += $"You can try:\n" +
                               $"• Running EasyToDo as Administrator\n" +
                               $"• Temporarily disabling antivirus\n" +
                               $"• Downloading manually from GitHub\n\n" +
                               $"Download URL: {updateInfo.DownloadUrl}";
                
                MessageBox.Show(errorMessage, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Installs MSI update with enhanced error handling and logging
        /// </summary>
        private static async Task<bool> InstallMsiUpdateAsync(string msiPath)
        {
            try
            {
                _progressWindow?.UpdateStatus("Preparing MSI installation...");
                
                // Get current application directory for validation
                var currentAppDir = GetApplicationDirectory();
                System.Diagnostics.Debug.WriteLine($"?? Current app directory: {currentAppDir}");
                System.Diagnostics.Debug.WriteLine($"?? MSI path: {msiPath}");
                
                // Verify MSI file exists and is valid
                if (!File.Exists(msiPath))
                {
                    throw new FileNotFoundException($"MSI file not found: {msiPath}");
                }
                
                var msiFileSize = new FileInfo(msiPath).Length;
                System.Diagnostics.Debug.WriteLine($"?? MSI file size: {msiFileSize / 1024 / 1024:F1} MB");
                
                // Close progress window before showing user dialog
                _progressWindow?.Close();

                // Enhanced user confirmation with more details
                var confirmMessage = $"?? Ready to Install EasyToDo Update\n\n" +
                                   $"Update file: {Path.GetFileName(msiPath)}\n" +
                                   $"File size: {msiFileSize / 1024 / 1024:F1} MB\n" +
                                   $"Current location: {currentAppDir}\n\n" +
                                   $"The MSI installer will:\n" +
                                   $"• Close EasyToDo automatically\n" +
                                   $"• Update the application files\n" +
                                   $"• Require administrator privileges\n" +
                                   $"• Launch the updated version\n\n" +
                                   $"?? Important: Save any unsaved work first!\n\n" +
                                   $"Continue with the installation?";

                var result = MessageBox.Show(
                    confirmMessage,
                    "Confirm Update Installation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    System.Diagnostics.Debug.WriteLine("? User cancelled MSI installation");
                    return false;
                }

                // Try multiple MSI installation approaches
                System.Diagnostics.Debug.WriteLine("?? Starting MSI installation process...");

                // Approach 1: Standard MSI install with logging
                var success = await TryMsiInstallWithLogging(msiPath);
                
                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("?? Standard MSI install failed, trying interactive mode...");
                    // Approach 2: Interactive install as fallback
                    success = await TryInteractiveMsiInstall(msiPath);
                }
                
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("? MSI installation initiated successfully");
                    
                    // Give the installer a moment to start before closing the app
                    await Task.Delay(2000);
                    
                    // Close the current application
                    Application.Current.Shutdown();
                    return true;
                }
                else
                {
                    // If MSI installation fails, offer manual installation
                    await OfferManualInstallation(msiPath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error in MSI installation: {ex.Message}");
                _progressWindow?.Close();
                
                MessageBox.Show(
                    $"Update Installation Error\n\n" +
                    $"The automatic update installation failed:\n" +
                    $"{ex.Message}\n\n" +
                    $"You can manually run the installer at:\n" +
                    $"{msiPath}\n\n" +
                    $"Or download it again from GitHub.",
                    "Installation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        /// <summary>
        /// Attempts MSI installation with detailed logging
        /// </summary>
        private static async Task<bool> TryMsiInstallWithLogging(string msiPath)
        {
            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "EasyToDo_Update_Log.txt");
                System.Diagnostics.Debug.WriteLine($"?? MSI log file: {logPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{msiPath}\" /qb /norestart /l*v \"{logPath}\"",
                    UseShellExecute = true,
                    Verb = "runas" // Request admin privileges
                };

                System.Diagnostics.Debug.WriteLine($"?? MSI command: {startInfo.FileName} {startInfo.Arguments}");

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    System.Diagnostics.Debug.WriteLine($"? MSI process started with PID: {process.Id}");
                    
                    // Don't wait for completion - let it run in background while we close the app
                    await Task.Delay(1000); // Give it a moment to start
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("? Failed to start MSI process");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? MSI installation with logging failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts interactive MSI installation
        /// </summary>
        private static async Task<bool> TryInteractiveMsiInstall(string msiPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{msiPath}\" /qr /norestart", // Reduced UI with modal dialogs at the end
                    UseShellExecute = true,
                    Verb = "runas"
                };

                System.Diagnostics.Debug.WriteLine($"?? Interactive MSI command: {startInfo.FileName} {startInfo.Arguments}");

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    System.Diagnostics.Debug.WriteLine($"? Interactive MSI process started with PID: {process.Id}");
                    await Task.Delay(1000);
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("? Failed to start interactive MSI process");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Interactive MSI installation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Offers manual installation as a fallback
        /// </summary>
        private static async Task OfferManualInstallation(string msiPath)
        {
            try
            {
                var message = $"?? Automatic Installation Failed\n\n" +
                             $"The automatic update couldn't complete, but you can install manually:\n\n" +
                             $"Option 1: Double-click to run manually\n" +
                             $"• File location: {msiPath}\n" +
                             $"• Right-click ? 'Run as administrator'\n\n" +
                             $"Option 2: Open file location\n" +
                             $"• Click 'Open Folder' below\n" +
                             $"• Double-click the MSI file\n\n" +
                             $"Option 3: Download fresh copy\n" +
                             $"• Click 'Download Again' to get a new copy\n\n" +
                             $"The installer will update EasyToDo to the latest version.";

                var dialogResult = MessageBox.Show(
                    message + "\n\nWould you like to open the folder containing the installer?",
                    "Manual Installation Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    // Open the folder containing the MSI file
                    var folderPath = Path.GetDirectoryName(msiPath);
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{msiPath}\"");
                        System.Diagnostics.Debug.WriteLine($"?? Opened folder: {folderPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error offering manual installation: {ex.Message}");
            }
        }

        /// <summary>
        /// Installs ZIP update (existing method)
        /// </summary>
        private static async Task<bool> InstallZipUpdateAsync(string zipPath, string tempDir)
        {
            try
            {
                _progressWindow?.UpdateStatus("Extracting update...");

                // Extract the update
                var extractPath = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractPath);
                
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Find the executable in extracted files
                var exeFiles = Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories);
                if (exeFiles.Length == 0)
                {
                    _progressWindow?.Close();
                    MessageBox.Show(
                        "Update file does not contain a valid executable.",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                _progressWindow?.UpdateStatus("Preparing installation...");

                // Create and launch updater script
                await CreateUpdaterAsync(extractPath, GetApplicationDirectory());

                return true;
            }
            catch (Exception ex)
            {
                _progressWindow?.Close();
                MessageBox.Show(
                    $"Failed to extract or install ZIP update:\n{ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Creates and launches the updater script for ZIP updates
        /// </summary>
        private static async Task CreateUpdaterAsync(string sourcePath, string targetPath)
        {
            var updaterScript = Path.Combine(Path.GetTempPath(), "EasyToDo_Updater.bat");
            var currentProcess = Process.GetCurrentProcess();

            var script = $@"@echo off
title EasyToDo Updater
echo.
echo ========================================
echo          EasyToDo Auto Updater
echo ========================================
echo.
echo Waiting for EasyToDo to close...
timeout /t 2 /nobreak > nul

:wait_loop
tasklist /fi ""pid eq {currentProcess.Id}"" 2>nul | find ""{currentProcess.Id}"" >nul
if not errorlevel 1 (
    echo Application still running, waiting...
    timeout /t 1 /nobreak > nul
    goto wait_loop
)

echo.
echo Backing up current version...
if exist ""{targetPath}\backup"" rmdir /s /q ""{targetPath}\backup""
mkdir ""{targetPath}\backup"" 2>nul
xcopy ""{targetPath}\*.exe"" ""{targetPath}\backup\"" /Y /Q 2>nul
xcopy ""{targetPath}\*.dll"" ""{targetPath}\backup\"" /Y /Q 2>nul

echo.
echo Installing update...
xcopy ""{sourcePath}\*"" ""{targetPath}\"" /E /H /C /I /Y /Q

if errorlevel 1 (
    echo.
    echo Update failed! Restoring backup...
    xcopy ""{targetPath}\backup\*"" ""{targetPath}\"" /Y /Q
    echo Update was rolled back due to an error.
    pause
    goto cleanup
)

echo.
echo Update completed successfully!
echo Starting EasyToDo...
start """" ""{Path.Combine(targetPath, "EasyToDo.exe")}""

:cleanup
echo.
echo Cleaning up...
timeout /t 2 /nobreak > nul
rmdir /s /q ""{Path.GetDirectoryName(sourcePath)}"" 2>nul
rmdir /s /q ""{targetPath}\backup"" 2>nul
del ""%~f0"" 2>nul
";

            await File.WriteAllTextAsync(updaterScript, script);

            // Close progress window
            _progressWindow?.Close();

            // Show final message
            MessageBox.Show(
                "Update ready to install!\n\nEasyToDo will now close and the update will be applied automatically.",
                "Update Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Launch updater and exit current application
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterScript,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal // Show the updater window
            });

            // Exit current application
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Tests the download URL to verify it's accessible and returns valid content
        /// </summary>
        public static async Task<(bool IsValid, string Message, long ContentLength)> TestDownloadUrlAsync(string downloadUrl)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"?? Testing download URL: {downloadUrl}");
                
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
                using var response = await _httpClient.SendAsync(headRequest);
                
                System.Diagnostics.Debug.WriteLine($"?? HEAD Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"?? Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                
                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"HTTP {response.StatusCode}: {response.ReasonPhrase}", 0);
                }
                
                var contentLength = response.Content.Headers.ContentLength ?? 0;
                System.Diagnostics.Debug.WriteLine($"?? Content Length from HEAD: {contentLength} bytes");
                
                if (contentLength == 0)
                {
                    return (false, "Content-Length is 0 bytes", 0);
                }
                
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
                System.Diagnostics.Debug.WriteLine($"?? Content-Type: {contentType}");

                // Additional validation: Perform a ranged GET request to check content validity
                using (var getRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                {
                    getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 1023); // First 1024 bytes

                    using var getResponse = await _httpClient.SendAsync(getRequest);
                    if (!getResponse.IsSuccessStatusCode)
                    {
                        return (false, $"HTTP {getResponse.StatusCode}: {getResponse.ReasonPhrase} (Content check)", contentLength);
                    }

                    var responseBody = await getResponse.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(responseBody) || responseBody.Length < 100)
                    {
                        return (false, "Downloaded content is empty or too short", contentLength);
                    }

                    System.Diagnostics.Debug.WriteLine($"?? Content preview: {responseBody.Substring(0, 100)}... (truncated for debug)");
                }
                
                return (true, $"URL is valid. Size: {contentLength / 1024 / 1024:F2} MB, Type: {contentType}", contentLength);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error testing download URL: {ex.Message}");
                return (false, $"Error testing URL: {ex.Message}", 0);
            }
        }

        /// <summary>
        /// Gets diagnostic information about the current installation
        /// </summary>
        public static string GetInstallationDiagnostics()
        {
            try
            {
                var appDir = GetApplicationDirectory();
                var exePath = Assembly.GetExecutingAssembly().Location;
                var currentVersion = GetCurrentVersion();
                
                var diagnostics = $"?? EasyToDo Installation Diagnostics\n\n" +
                                $"Current Version: {currentVersion}\n" +
                                $"Executable Path: {exePath}\n" +
                                $"Application Directory: {appDir}\n" +
                                $"Process Name: {Process.GetCurrentProcess().ProcessName}\n" +
                                $"Process ID: {Process.GetCurrentProcess().Id}\n" +
                                $"Running as Admin: {IsRunningAsAdministrator()}\n" +
                                $"Windows Version: {Environment.OSVersion}\n" +
                                $".NET Version: {Environment.Version}\n\n" +
                                $"File System Access:\n" +
                                $"• Can write to app dir: {CanWriteToDirectory(appDir)}\n" +
                                $"• Can write to temp: {CanWriteToDirectory(Path.GetTempPath())}\n\n" +
                                $"Registry Access:\n" +
                                $"• Installed Programs Key Accessible: {CanAccessInstalledPrograms()}\n\n" +
                                $"MSI Installation Tips:\n" +
                                $"• Close all instances of EasyToDo before updating\n" +
                                $"• Run MSI installer as Administrator\n" +
                                $"• Check Windows Event Log if installation fails\n" +
                                $"• Ensure antivirus is not blocking the installation";
                
                return diagnostics;
            }
            catch (Exception ex)
            {
                return $"Error generating diagnostics: {ex.Message}";
            }
        }
        
        private static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        
        private static bool CanWriteToDirectory(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return false;
                    
                var testFile = Path.Combine(directoryPath, $"test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static bool CanAccessInstalledPrograms()
        {
            try
            {
                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                key?.Close();
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the directory where the application is installed
        /// </summary>
        private static string GetApplicationDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? 
                   Environment.CurrentDirectory;
        }

        /// <summary>
        /// Checks for updates silently on startup
        /// </summary>
        public static async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                // Only check if it's been more than a day since last check
                var lastCheck = GetLastUpdateCheck();
                if (DateTime.Now - lastCheck < TimeSpan.FromDays(1))
                    return;

                var updateInfo = await CheckForUpdatesAsync();
                if (updateInfo != null)
                {
                    SetLastUpdateCheck(DateTime.Now);
                    
                    var updateTypeDisplay = updateInfo.UpdateType == "MSI" ? "MSI installer" : "ZIP package";
                    
                    // Show notification but don't auto-install
                    var result = MessageBox.Show(
                        $"A new version of EasyToDo is available!\n\n" +
                        $"Current: {updateInfo.CurrentVersion}\n" +
                        $"Latest: {updateInfo.LatestVersion}\n" +
                        $"Type: {updateTypeDisplay}\n\n" +
                        $"Would you like to update now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        await ShowUpdateDialogAsync(updateInfo);
                    }
                }
                else
                {
                    SetLastUpdateCheck(DateTime.Now);
                }
            }
            catch
            {
                // Silent failure for startup check
            }
        }

        /// <summary>
        /// Gets the last update check timestamp
        /// </summary>
        private static DateTime GetLastUpdateCheck()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var configFile = Path.Combine(appData, "EasyToDo", "update_config.txt");
                
                if (File.Exists(configFile))
                {
                    var content = File.ReadAllText(configFile);
                    if (DateTime.TryParse(content, out var date))
                        return date;
                }
            }
            catch { }
            
            return DateTime.MinValue;
        }

        /// <summary>
        /// Sets the last update check timestamp
        /// </summary>
        private static void SetLastUpdateCheck(DateTime timestamp)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var configDir = Path.Combine(appData, "EasyToDo");
                Directory.CreateDirectory(configDir);
                
                var configFile = Path.Combine(configDir, "update_config.txt");
                File.WriteAllText(configFile, timestamp.ToString("O"));
            }
            catch { }
        }
    }

    public class UpdateInfo
    {
        public Version CurrentVersion { get; set; } = new(1, 0, 0, 0);
        public Version LatestVersion { get; set; } = new(1, 0, 0, 0);
        public string ReleaseUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public DateTime PublishedDate { get; set; }
        public string? DownloadUrl { get; set; }
        public string UpdateType { get; set; } = "Unknown";
    }
}