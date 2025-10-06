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
                var response = await _httpClient.GetStringAsync(GitHubApiUrl);
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                var tagName = root.GetProperty("tag_name").GetString();
                if (string.IsNullOrEmpty(tagName)) return null;

                // Remove 'v' prefix if present
                if (tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    tagName = tagName[1..];

                if (!Version.TryParse(tagName, out var latestVersion))
                    return null;

                var currentVersion = GetCurrentVersion();
                
                if (latestVersion > currentVersion)
                {
                    var releaseUrl = root.GetProperty("html_url").GetString();
                    var releaseNotes = root.GetProperty("body").GetString();
                    var publishedAt = root.GetProperty("published_at").GetString();
                    
                    // Look for download assets (prefer MSI, then ZIP)
                    string? downloadUrl = null;
                    string? updateType = null;
                    
                    if (root.TryGetProperty("assets", out var assetsElement))
                    {
                        // First pass: Look for MSI files
                        foreach (var asset in assetsElement.EnumerateArray())
                        {
                            var assetName = asset.GetProperty("name").GetString();
                            if (assetName != null && 
                                (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) &&
                                 assetName.Contains("EasyToDo", StringComparison.OrdinalIgnoreCase)))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                updateType = "MSI";
                                break;
                            }
                        }

                        // Second pass: Look for ZIP files if no MSI found
                        if (downloadUrl == null)
                        {
                            foreach (var asset in assetsElement.EnumerateArray())
                            {
                                var assetName = asset.GetProperty("name").GetString();
                                if (assetName != null && 
                                    (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                     assetName.Contains("EasyToDo", StringComparison.OrdinalIgnoreCase)))
                                {
                                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                    updateType = "ZIP";
                                    break;
                                }
                            }
                        }
                    }

                    return new UpdateInfo
                    {
                        LatestVersion = latestVersion,
                        CurrentVersion = currentVersion,
                        ReleaseUrl = releaseUrl ?? "",
                        ReleaseNotes = releaseNotes ?? "",
                        PublishedDate = DateTime.TryParse(publishedAt, out var date) ? date : DateTime.Now,
                        DownloadUrl = downloadUrl,
                        UpdateType = updateType ?? "Unknown"
                    };
                }

                return null; // No update available
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
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
                return false;

            try
            {
                // Show progress window
                _progressWindow = new UpdateProgressWindow();
                _progressWindow.Show();
                _progressWindow.UpdateStatus("Preparing update...");

                // Create temp directory for update files
                var tempDir = Path.Combine(Path.GetTempPath(), "EasyToDo_Update");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);

                var fileExtension = updateInfo.UpdateType == "MSI" ? ".msi" : ".zip";
                var downloadPath = Path.Combine(tempDir, $"update{fileExtension}");

                _progressWindow.UpdateStatus("Downloading update...");

                // Download with progress
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    
                    if (totalBytes != 0)
                    {
                        var progress = (int)((totalBytesRead * 100) / totalBytes);
                        _progressWindow.UpdateProgress(progress);
                        _progressWindow.UpdateStatus($"Downloaded {totalBytesRead / 1024 / 1024:F1} MB of {totalBytes / 1024 / 1024:F1} MB");
                    }
                }

                _progressWindow.UpdateStatus("Preparing installation...");

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
            catch (Exception ex)
            {
                _progressWindow?.Close();
                MessageBox.Show(
                    $"Failed to download or install update:\n{ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Installs MSI update
        /// </summary>
        private static async Task<bool> InstallMsiUpdateAsync(string msiPath)
        {
            try
            {
                _progressWindow?.UpdateStatus("Launching installer...");
                
                // Close progress window before launching installer
                _progressWindow?.Close();

                // Show final message
                var result = MessageBox.Show(
                    "MSI installer downloaded successfully!\n\n" +
                    "The installer will now launch to update EasyToDo.\n" +
                    "EasyToDo will close automatically.\n\n" +
                    "Click OK to proceed with the installation.",
                    "Ready to Install",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    // Launch MSI installer
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = $"/i \"{msiPath}\" /passive /norestart",
                        UseShellExecute = true,
                        Verb = "runas" // Request admin privileges
                    };

                    Process.Start(startInfo);

                    // Close the current application
                    await Task.Delay(1000); // Give time for installer to start
                    Application.Current.Shutdown();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to launch MSI installer:\n{ex.Message}\n\n" +
                    $"You can manually run the installer at:\n{msiPath}",
                    "Installation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
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