using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using EasyToDo.Converters;
using EasyToDo.Models;

namespace EasyToDo.Services
{
    public static class NoteStorage
    {
        private static string _customSavePath;
        private static bool _isChangingStorage = false; // Flag to prevent multiple dialogs
        
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyToDo", "settings.json");
        
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new ColorJsonConverter() }
        };

        static NoteStorage()
        {
            LoadSettings();
        }

        /// <summary>
        /// Gets the current save file path, checking for user preferences first
        /// </summary>
        public static string SaveFilePath => _customSavePath ?? GetOptimalSavePath();

        /// <summary>
        /// Determines the best save location: Custom > Dropbox > OneDrive > Local
        /// </summary>
        private static string GetOptimalSavePath()
        {
            // Try Dropbox first
            var dropboxPath = TryGetDropboxPath();
            if (!string.IsNullOrEmpty(dropboxPath))
            {
                System.Diagnostics.Debug.WriteLine($"Using Dropbox storage: {dropboxPath}");
                return dropboxPath;
            }

            // Try OneDrive second
            var oneDrivePath = TryGetOneDrivePath();
            if (!string.IsNullOrEmpty(oneDrivePath))
            {
                System.Diagnostics.Debug.WriteLine($"Using OneDrive storage: {oneDrivePath}");
                return oneDrivePath;
            }

            // Fallback to local storage
            var localPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasyToDo", "notes.json");
            
            System.Diagnostics.Debug.WriteLine($"Using local storage: {localPath}");
            return localPath;
        }

        /// <summary>
        /// Attempts to find a valid Dropbox installation and return the app folder path
        /// </summary>
        private static string TryGetDropboxPath()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                // Common Dropbox folder locations
                var dropboxPaths = new[]
                {
                    Path.Combine(userProfile, "Dropbox"),
                    Path.Combine(userProfile, "Dropbox (Personal)"),
                    Path.Combine(userProfile, "Dropbox Business")
                };

                foreach (var basePath in dropboxPaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        // Create the app folder in Dropbox
                        var appPath = Path.Combine(basePath, "Apps", "EasyToDo");
                        Directory.CreateDirectory(appPath);
                        return Path.Combine(appPath, "notes.json");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting Dropbox: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to find a valid OneDrive installation and return the app folder path
        /// </summary>
        private static string TryGetOneDrivePath()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                // Common OneDrive folder locations
                var oneDrivePaths = new[]
                {
                    Path.Combine(userProfile, "OneDrive"),
                    Path.Combine(userProfile, "OneDrive - Personal"),
                    Path.Combine(userProfile, "OneDrive - Business")
                };

                foreach (var basePath in oneDrivePaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        // Create the app folder in OneDrive
                        var appPath = Path.Combine(basePath, "EasyToDo");
                        Directory.CreateDirectory(appPath);
                        return Path.Combine(appPath, "notes.json");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting OneDrive: {ex.Message}");
            }

            return null;
        }

        private static DateTime _lastChangeAttempt = DateTime.MinValue;
        private static readonly TimeSpan _minimumTimeBetweenChanges = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Shows a folder selection dialog to choose storage location
        /// </summary>
        public static bool ChangeStorageLocation()
        {
            System.Diagnostics.Debug.WriteLine($"ChangeStorageLocation() called at {DateTime.Now:HH:mm:ss.fff}");
            
            // Prevent multiple concurrent dialogs
            if (_isChangingStorage)
            {
                System.Diagnostics.Debug.WriteLine("Storage change already in progress, ignoring request");
                return false;
            }

            _isChangingStorage = true;
            bool dialogShownSuccessfully = false;
            
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog();
                saveDialog.Title = "Choose Storage Folder for EasyToDo Notes";
                saveDialog.FileName = "notes"; // Default filename
                saveDialog.DefaultExt = ".json";
                saveDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                saveDialog.InitialDirectory = GetStorageFolder();
                saveDialog.OverwritePrompt = false; // We'll handle this ourselves

                System.Diagnostics.Debug.WriteLine("Showing SaveFileDialog...");
                var result = saveDialog.ShowDialog();
                dialogShownSuccessfully = true; // Dialog was shown successfully
                
                System.Diagnostics.Debug.WriteLine($"SaveFileDialog result: {result}");
                
                if (result == true)
                {
                    var selectedPath = saveDialog.FileName;
                    System.Diagnostics.Debug.WriteLine($"User selected: {selectedPath}");
                    
                    // Ensure the file has the correct name
                    var selectedFolder = Path.GetDirectoryName(selectedPath);
                    var newPath = Path.Combine(selectedFolder, "notes.json");
                    
                    // Try to move existing notes to new location
                    if (File.Exists(SaveFilePath) && !File.Exists(newPath))
                    {
                        try
                        {
                            File.Copy(SaveFilePath, newPath);
                            System.Diagnostics.Debug.WriteLine($"Copied notes from {SaveFilePath} to {newPath}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error copying notes: {ex.Message}");
                            return false;
                        }
                    }

                    // Update the custom path
                    _customSavePath = newPath;
                    SaveSettings();
                    
                    System.Diagnostics.Debug.WriteLine($"Storage location changed to: {newPath}");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("User cancelled the dialog");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in ChangeStorageLocation: {ex.Message}");
                
                // Only fallback if the dialog itself failed to show
                if (!dialogShownSuccessfully)
                {
                    System.Diagnostics.Debug.WriteLine("Dialog failed to show, trying fallback...");
                    return ShowFallbackFolderDialog();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Dialog showed but operation failed");
                }
            }
            finally
            {
                _isChangingStorage = false; // Always reset the flag
                System.Diagnostics.Debug.WriteLine($"ChangeStorageLocation completed at {DateTime.Now:HH:mm:ss.fff}, flag reset");
            }

            return false;
        }

        /// <summary>
        /// Fallback folder selection using text input (for compatibility)
        /// </summary>
        private static bool ShowFallbackFolderDialog()
        {
            try
            {
                var currentLocation = GetStorageFolder();
                var message = $"?? Change Storage Location\n\n" +
                             $"Current location:\n{currentLocation}\n\n" +
                             $"The folder chooser is not available on this system.\n" +
                             $"Please enter the full path to your desired storage folder.\n\n" +
                             $"?? Examples:\n" +
                             $"• C:\\Users\\YourName\\Documents\\MyNotes\n" +
                             $"• D:\\Backup\\EasyToDo\n" +
                             $"• C:\\Users\\YourName\\Dropbox\\EasyToDo\n\n" +
                             $"Click OK to continue or Cancel to keep current location.";

                var result = System.Windows.MessageBox.Show(
                    message,
                    "Change Storage Location",
                    System.Windows.MessageBoxButton.OKCancel,
                    System.Windows.MessageBoxImage.Information);

                if (result == System.Windows.MessageBoxResult.OK)
                {
                    return ShowFolderInputDialog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in fallback dialog: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Shows an enhanced input dialog for folder path
        /// </summary>
        private static bool ShowFolderInputDialog()
        {
            try
            {
                // Create a simple input dialog window
                var inputDialog = new System.Windows.Window
                {
                    Title = "?? Enter Storage Folder Path",
                    Width = 600,
                    Height = 250,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    ResizeMode = System.Windows.ResizeMode.NoResize
                };

                var stackPanel = new System.Windows.Controls.StackPanel
                {
                    Margin = new System.Windows.Thickness(20)
                };

                var label = new System.Windows.Controls.Label
                {
                    Content = "?? Enter the full path to the folder where you want to store your notes:",
                    FontWeight = System.Windows.FontWeights.Bold,
                    FontSize = 12
                };

                var hintLabel = new System.Windows.Controls.Label
                {
                    Content = "?? Tip: Right-click in Windows Explorer address bar and select 'Copy address as text'",
                    FontSize = 10,
                    FontStyle = System.Windows.FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };

                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = GetStorageFolder(),
                    Margin = new System.Windows.Thickness(0, 10, 0, 10),
                    Height = 30,
                    FontSize = 12,
                    Padding = new System.Windows.Thickness(5)
                };

                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new System.Windows.Thickness(0, 15, 0, 0)
                };

                var browseButton = new System.Windows.Controls.Button
                {
                    Content = "?? Browse...",
                    Width = 85,
                    Height = 30,
                    Margin = new System.Windows.Thickness(0, 0, 10, 0)
                };

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "? OK",
                    Width = 75,
                    Height = 30,
                    Margin = new System.Windows.Thickness(5, 0, 0, 0),
                    IsDefault = true
                };

                var cancelButton = new System.Windows.Controls.Button
                {
                    Content = "? Cancel",
                    Width = 85,
                    Height = 30,
                    IsCancel = true
                };

                bool dialogResult = false;

                browseButton.Click += (s, e) =>
                {
                    try
                    {
                        // Try to open Windows Explorer for the user to copy the path
                        var currentPath = textBox.Text.Trim();
                        if (Directory.Exists(currentPath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", currentPath);
                        }
                        else
                        {
                            System.Diagnostics.Process.Start("explorer.exe");
                        }

                        System.Windows.MessageBox.Show(
                            "Windows Explorer has been opened.\n\n" +
                            "To copy a folder path:\n" +
                            "1. Navigate to your desired folder\n" +
                            "2. Click in the address bar (or press Ctrl+L)\n" +
                            "3. Copy the path (Ctrl+C)\n" +
                            "4. Return here and paste it (Ctrl+V)",
                            "How to Copy Folder Path",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Could not open Windows Explorer:\n{ex.Message}",
                            "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                };

                okButton.Click += (s, e) =>
                {
                    var folderPath = textBox.Text.Trim();
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        System.Windows.MessageBox.Show("Please enter a valid folder path.", "Invalid Path", 
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        // Create directory if it doesn't exist
                        Directory.CreateDirectory(folderPath);
                        
                        var newPath = Path.Combine(folderPath, "notes.json");
                        
                        // Try to move existing notes to new location
                        if (File.Exists(SaveFilePath) && !File.Exists(newPath))
                        {
                            File.Copy(SaveFilePath, newPath);
                            System.Diagnostics.Debug.WriteLine($"Copied notes from {SaveFilePath} to {newPath}");
                        }

                        // Update the custom path
                        _customSavePath = newPath;
                        SaveSettings();
                        
                        dialogResult = true;
                        inputDialog.DialogResult = true;
                        inputDialog.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error setting storage location:\n{ex.Message}", 
                            "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                };

                buttonPanel.Children.Add(browseButton);
                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(okButton);

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(hintLabel);
                stackPanel.Children.Add(textBox);
                stackPanel.Children.Add(buttonPanel);

                inputDialog.Content = stackPanel;
                textBox.Focus();
                textBox.SelectAll();

                inputDialog.ShowDialog();
                return dialogResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing folder input dialog: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets storage to automatic detection (removes custom path)
        /// </summary>
        public static bool ResetToAutoStorage()
        {
            try
            {
                var oldPath = SaveFilePath;
                _customSavePath = null;
                SaveSettings();

                // Try to move notes to new auto-detected location
                var newPath = SaveFilePath;
                if (File.Exists(oldPath) && !File.Exists(newPath) && oldPath != newPath)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                        File.Copy(oldPath, newPath);
                        System.Diagnostics.Debug.WriteLine($"Moved notes from {oldPath} to {newPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error moving notes: {ex.Message}");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting to auto storage: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads user settings including custom storage path
        /// </summary>
        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (settings != null && settings.TryGetValue("CustomStoragePath", out var customPath) && 
                        !string.IsNullOrEmpty(customPath))
                    {
                        // Verify the custom path is still valid
                        var directory = Path.GetDirectoryName(customPath);
                        if (Directory.Exists(directory))
                        {
                            _customSavePath = customPath;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Custom storage path no longer exists: {customPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves user settings including custom storage path
        /// </summary>
        private static void SaveSettings()
        {
            try
            {
                var settings = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(_customSavePath))
                {
                    settings["CustomStoragePath"] = _customSavePath;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current save file path (useful for displaying to user)
        /// </summary>
        public static string GetSaveLocation()
        {
            return SaveFilePath;
        }

        /// <summary>
        /// Gets a user-friendly description of where notes are stored
        /// </summary>
        public static string GetStorageLocationDescription()
        {
            if (!string.IsNullOrEmpty(_customSavePath))
                return "Custom location";
            if (SaveFilePath.Contains("Dropbox"))
                return "Dropbox (syncing across devices)";
            if (SaveFilePath.Contains("OneDrive"))
                return "OneDrive (syncing across devices)";
            return "Local storage (this device only)";
        }

        public static void SaveNotes(ObservableCollection<Note> notes)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));
                string jsonString = JsonSerializer.Serialize(notes, Options);
                File.WriteAllText(SaveFilePath, jsonString);
                
                System.Diagnostics.Debug.WriteLine($"Notes saved to: {SaveFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving notes: {ex.Message}");
            }
        }

        public static ObservableCollection<Note> LoadNotes()
        {
            try
            {
                // Try to migrate from old location if new location is empty
                if (!File.Exists(SaveFilePath))
                {
                    TryMigrateFromOldLocation();
                }

                if (File.Exists(SaveFilePath))
                {
                    string jsonString = File.ReadAllText(SaveFilePath);
                    var notes = JsonSerializer.Deserialize<ObservableCollection<Note>>(jsonString, Options);
                    
                    System.Diagnostics.Debug.WriteLine($"Notes loaded from: {SaveFilePath}");
                    return notes ?? new ObservableCollection<Note>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading notes: {ex.Message}");
            }

            return new ObservableCollection<Note>();
        }

        /// <summary>
        /// Attempts to migrate notes from the old local location to the new cloud location
        /// </summary>
        private static void TryMigrateFromOldLocation()
        {
            try
            {
                var oldPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EasyToDo", "notes.json");

                if (File.Exists(oldPath) && !File.Exists(SaveFilePath))
                {
                    // Copy the old file to the new location
                    Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));
                    File.Copy(oldPath, SaveFilePath);
                    
                    System.Diagnostics.Debug.WriteLine($"Migrated notes from {oldPath} to {SaveFilePath}");
                    
                    // Optionally keep a backup of the old file
                    var backupPath = Path.ChangeExtension(oldPath, ".backup.json");
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(oldPath, backupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error migrating notes: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the folder path where notes are stored (without the filename)
        /// </summary>
        public static string GetStorageFolder()
        {
            return Path.GetDirectoryName(SaveFilePath);
        }

        /// <summary>
        /// Opens the folder containing the notes file in Windows Explorer
        /// </summary>
        public static void OpenStorageFolder()
        {
            try
            {
                var folderPath = GetStorageFolder();
                if (Directory.Exists(folderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Storage folder does not exist: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening storage folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens Windows Explorer and selects the notes file
        /// </summary>
        public static void OpenFileLocation()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    // Use explorer.exe with /select parameter to highlight the file
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{SaveFilePath}\"");
                }
                else
                {
                    // If file doesn't exist yet, just open the folder
                    OpenStorageFolder();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening file location: {ex.Message}");
                // Fallback to opening just the folder
                try
                {
                    OpenStorageFolder();
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening storage folder as fallback: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// Gets information about the current storage setup
        /// </summary>
        public static (string StorageType, string FolderPath, bool IsCloudStorage, bool IsCustomPath) GetStorageInfo()
        {
            var folderPath = GetStorageFolder();
            bool isCustom = !string.IsNullOrEmpty(_customSavePath);
            bool isCloud = !isCustom && (SaveFilePath.Contains("Dropbox") || SaveFilePath.Contains("OneDrive"));
            
            string storageType;
            if (isCustom)
                storageType = "Custom";
            else if (SaveFilePath.Contains("Dropbox"))
                storageType = "Dropbox";
            else if (SaveFilePath.Contains("OneDrive"))
                storageType = "OneDrive";
            else
                storageType = "Local";

            return (storageType, folderPath, isCloud, isCustom);
        }
    }
}