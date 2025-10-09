using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using EasyToDo.Converters;
using EasyToDo.Models;

namespace EasyToDo.Services
{
    public static class NoteStorage
    {
        private static string _customSavePath;
        private static bool _isChangingStorage = false; // Flag to prevent multiple dialogs
        
        // Auto-save throttling
        private static DispatcherTimer _saveTimer;
        private static bool _hasPendingSave = false;
        private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500); // 500ms delay
        
        // File monitoring for real-time sync
        private static FileSystemWatcher _fileWatcher;
        private static DateTime _lastFileWrite = DateTime.MinValue;
        private static DateTime _lastSaveTime = DateTime.MinValue;
        private static bool _isExternalChange = false;
        
        // Sync detection timer
        private static DispatcherTimer _syncCheckTimer;
        private static readonly TimeSpan SyncCheckInterval = TimeSpan.FromSeconds(2); // Check every 2 seconds
        
        // Daily backup system
        private static DispatcherTimer _backupTimer;
        private static DateTime _lastBackupDate = DateTime.MinValue;
        private static readonly TimeSpan BackupInterval = TimeSpan.FromHours(24); // Daily backup
        private static readonly int MaxBackupFiles = 7; // Keep 7 days worth of backups
        
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyToDo", "settings.json");
        
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new ColorJsonConverter() }
        };

        // Event for notifying the UI when external changes are detected
        public static event EventHandler<ObservableCollection<Note>> ExternalFileChanged;

        static NoteStorage()
        {
            LoadSettings();
            InitializeSaveTimer();
            InitializeBackupSystem();
            InitializeFileMonitoring();
        }

        private static void InitializeSaveTimer()
        {
            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = SaveDelay;
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                if (_hasPendingSave)
                {
                    PerformSave();
                    _hasPendingSave = false;
                }
            };
        }

        private static void InitializeFileMonitoring()
        {
            try
            {
                // Set up file system watcher to monitor the notes file
                var notesFolder = GetStorageFolder();
                if (Directory.Exists(notesFolder))
                {
                    _fileWatcher = new FileSystemWatcher(notesFolder, "notes.json");
                    _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;
                    _fileWatcher.Changed += OnNotesFileChanged;
                    _fileWatcher.Created += OnNotesFileChanged; // Also monitor file creation
                    _fileWatcher.IncludeSubdirectories = false;
                    
                    // Increase internal buffer size to prevent missed events
                    _fileWatcher.InternalBufferSize = 8192 * 4; // 32KB buffer
                    
                    _fileWatcher.EnableRaisingEvents = true;
                    
                    System.Diagnostics.Debug.WriteLine($"?? File monitoring started for: {Path.Combine(notesFolder, "notes.json")}");
                    System.Diagnostics.Debug.WriteLine($"?? Monitoring folder: {notesFolder}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"?? Storage folder doesn't exist yet: {notesFolder}");
                }
                
                // Also set up a periodic sync check timer as a fallback
                _syncCheckTimer = new DispatcherTimer();
                _syncCheckTimer.Interval = SyncCheckInterval;
                _syncCheckTimer.Tick += (s, e) => CheckForExternalChanges();
                _syncCheckTimer.Start();
                
                System.Diagnostics.Debug.WriteLine("?? Sync monitoring initialized - checking every 2 seconds");
                System.Diagnostics.Debug.WriteLine($"?? Current save file path: {SaveFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error initializing file monitoring: {ex.Message}");
            }
        }

        private static void OnNotesFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
                {
                    System.Diagnostics.Debug.WriteLine($"?? File change detected: {e.FullPath} ({e.ChangeType}) at {DateTime.Now:HH:mm:ss.fff}");
                    
                    // Debounce file change events (wait a bit to ensure file is fully written)
                    var currentTime = DateTime.Now;
                    _lastFileWrite = currentTime;
                    
                    // Use dispatcher to check after a delay
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            await System.Threading.Tasks.Task.Delay(200); // Wait 200ms to ensure file is complete
                            
                            // Only process if this is still the most recent change
                            if (DateTime.Now - _lastFileWrite < TimeSpan.FromMilliseconds(400))
                            {
                                System.Diagnostics.Debug.WriteLine($"?? Processing file change from {currentTime:HH:mm:ss.fff}");
                                CheckForExternalChanges();
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"?? Skipping outdated file change from {currentTime:HH:mm:ss.fff}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"? Error in file change handler: {ex.Message}");
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error in OnNotesFileChanged: {ex.Message}");
            }
        }

        private static void CheckForExternalChanges()
        {
            try
            {
                if (!File.Exists(SaveFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"?? Save file doesn't exist: {SaveFilePath}");
                    return;
                }

                var fileInfo = new FileInfo(SaveFilePath);
                var fileLastWrite = fileInfo.LastWriteTime;
                var timeSinceLastSave = fileLastWrite - _lastSaveTime;
                var timeSinceFileWrite = DateTime.Now - fileLastWrite;

                System.Diagnostics.Debug.WriteLine($"?? Sync Check - File: {fileLastWrite:HH:mm:ss.fff}, Our Save: {_lastSaveTime:HH:mm:ss.fff}, Diff: {timeSinceLastSave.TotalSeconds:F1}s");

                // Check if file was modified externally (not by our own save operation)
                if (fileLastWrite > _lastSaveTime.AddSeconds(1) && // More than 1 second after our last save
                    timeSinceFileWrite < TimeSpan.FromMinutes(10)) // But within the last 10 minutes
                {
                    System.Diagnostics.Debug.WriteLine($"?? External file change detected! File modified at {fileLastWrite:HH:mm:ss.fff}, our last save at {_lastSaveTime:HH:mm:ss.fff}");
                    
                    // Load the updated notes and notify the UI
                    var updatedNotes = LoadNotesFromFile();
                    if (updatedNotes != null)
                    {
                        _isExternalChange = true;
                        ExternalFileChanged?.Invoke(null, updatedNotes);
                        System.Diagnostics.Debug.WriteLine($"? External changes loaded and UI notified - {updatedNotes.Count} notes");
                        
                        // Update our last save time to prevent re-triggering
                        _lastSaveTime = fileLastWrite;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"?? Failed to load updated notes from file");
                    }
                }
                else if (timeSinceLastSave.TotalSeconds > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"?? File change detected but ignoring - likely our own save or too old");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error checking for external changes: {ex.Message}");
            }
        }

        private static ObservableCollection<Note> LoadNotesFromFile()
        {
            const int maxRetries = 3;
            const int retryDelayMs = 100;
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    if (File.Exists(SaveFilePath))
                    {
                        // Check if file is accessible
                        using (var fileStream = new FileStream(SaveFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (var reader = new StreamReader(fileStream))
                            {
                                string jsonString = reader.ReadToEnd();
                                
                                if (string.IsNullOrWhiteSpace(jsonString))
                                {
                                    System.Diagnostics.Debug.WriteLine($"?? Empty or whitespace JSON content on attempt {retry + 1}");
                                    if (retry < maxRetries - 1)
                                    {
                                        System.Threading.Thread.Sleep(retryDelayMs);
                                        continue;
                                    }
                                    return new ObservableCollection<Note>();
                                }
                                
                                var notes = JsonSerializer.Deserialize<ObservableCollection<Note>>(jsonString, Options);
                                System.Diagnostics.Debug.WriteLine($"?? Successfully loaded {notes?.Count ?? 0} notes from file on attempt {retry + 1}");
                                return notes ?? new ObservableCollection<Note>();
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"?? File doesn't exist: {SaveFilePath}");
                        return new ObservableCollection<Note>();
                    }
                }
                catch (IOException ex) when (retry < maxRetries - 1)
                {
                    System.Diagnostics.Debug.WriteLine($"?? File access error on attempt {retry + 1}, retrying: {ex.Message}");
                    System.Threading.Thread.Sleep(retryDelayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? Error loading notes from file on attempt {retry + 1}: {ex.Message}");
                    if (retry == maxRetries - 1)
                    {
                        break; // Don't retry on final attempt
                    }
                    System.Threading.Thread.Sleep(retryDelayMs);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"? Failed to load notes after {maxRetries} attempts");
            return new ObservableCollection<Note>();
        }

        /// <summary>
        /// Force immediate save (for critical operations)
        /// </summary>
        public static void SaveImmediately(ObservableCollection<Note> notes)
        {
            _saveTimer.Stop();
            _hasPendingSave = false;
            SaveNotes(notes);
            System.Diagnostics.Debug.WriteLine($"Immediate save completed at {DateTime.Now:HH:mm:ss.fff}");
            
            // Check if we need to create a backup after immediate save
            CheckAndCreateBackup();
        }

        /// <summary>
        /// Manually create a backup (can be called from UI)
        /// </summary>
        public static bool CreateBackupNow()
        {
            try
            {
                if (!File.Exists(SaveFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("No notes file exists to backup");
                    return false;
                }

                CreateManualBackup();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating manual backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a manual backup with timestamp (always creates a new file)
        /// </summary>
        private static void CreateManualBackup()
        {
            try
            {
                var backupFolder = Path.GetDirectoryName(SaveFilePath);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var backupFileName = $"notes-backup-{timestamp}.json";
                var backupPath = Path.Combine(backupFolder, backupFileName);

                // Always create a new backup file with timestamp (no duplicate prevention)
                File.Copy(SaveFilePath, backupPath, false); // Don't overwrite if somehow exists
                
                System.Diagnostics.Debug.WriteLine($"?? Manual backup created: {backupPath}");

                // Clean up old backups (keep only last MaxBackupFiles)
                CleanupOldBackups(backupFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating manual backup: {ex.Message}");
                throw; // Re-throw to let CreateBackupNow handle it
            }
        }

        /// <summary>
        /// Get backup information for display
        /// </summary>
        public static (DateTime LastBackup, int BackupCount, string NextBackup) GetBackupInfo()
        {
            var backups = GetAvailableBackups();
            var nextBackup = _lastBackupDate.Date < DateTime.Now.Date ? "Today" : 
                           (_lastBackupDate.AddDays(1).Date == DateTime.Now.Date ? "Tomorrow" : 
                            _lastBackupDate.AddDays(1).ToString("MMM dd"));

            return (_lastBackupDate, backups.Count, nextBackup);
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
                var settings = LoadSettingsInternal();
                if (settings != null)
                {
                    // Load custom storage path
                    if (settings.TryGetValue("CustomStoragePath", out var customPath) && 
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

                    // Load last backup date
                    if (settings.TryGetValue("LastBackupDate", out var backupDateString) && 
                        DateTime.TryParse(backupDateString, out var backupDate))
                    {
                        _lastBackupDate = backupDate;
                        System.Diagnostics.Debug.WriteLine($"Last backup date loaded: {_lastBackupDate:yyyy-MM-dd HH:mm:ss}");
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
                var settings = LoadSettingsInternal() ?? new Dictionary<string, string>();
                
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
                // Track when we're saving to avoid detecting our own changes as external
                _lastSaveTime = DateTime.Now;
                
                Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath));
                string jsonString = JsonSerializer.Serialize(notes, Options);
                File.WriteAllText(SaveFilePath, jsonString);
                
                System.Diagnostics.Debug.WriteLine($"Notes saved to: {SaveFilePath} at {_lastSaveTime:HH:mm:ss.fff}");
                
                // Update file monitoring if folder changed
                UpdateFileMonitoring();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving notes: {ex.Message}");
            }
        }

        private static void UpdateFileMonitoring()
        {
            try
            {
                var currentFolder = GetStorageFolder();
                
                // If the folder changed, restart file monitoring
                if (_fileWatcher?.Path != currentFolder || !_fileWatcher.EnableRaisingEvents)
                {
                    System.Diagnostics.Debug.WriteLine($"?? Updating file monitoring - Old: {_fileWatcher?.Path ?? "null"}, New: {currentFolder}");
                    
                    _fileWatcher?.Dispose();
                    
                    if (Directory.Exists(currentFolder))
                    {
                        _fileWatcher = new FileSystemWatcher(currentFolder, "notes.json");
                        _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;
                        _fileWatcher.Changed += OnNotesFileChanged;
                        _fileWatcher.Created += OnNotesFileChanged;
                        _fileWatcher.IncludeSubdirectories = false;
                        _fileWatcher.InternalBufferSize = 8192 * 4; // 32KB buffer
                        _fileWatcher.EnableRaisingEvents = true;
                        
                        System.Diagnostics.Debug.WriteLine($"? File monitoring restarted for: {Path.Combine(currentFolder, "notes.json")}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"?? Cannot monitor - folder doesn't exist: {currentFolder}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"?? File monitoring already active for: {currentFolder}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error updating file monitoring: {ex.Message}");
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
                    
                    // Track the last file modification time
                    var fileInfo = new FileInfo(SaveFilePath);
                    _lastSaveTime = fileInfo.LastWriteTime;
                    
                    System.Diagnostics.Debug.WriteLine($"Notes loaded from: {SaveFilePath}");
                    System.Diagnostics.Debug.WriteLine($"File last modified: {_lastSaveTime:HH:mm:ss.fff}");
                    
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
        /// Forces an immediate check for external file changes (manual sync)
        /// </summary>
        public static void ForceCheckSync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("?? Force sync check requested");
                
                // Ensure file monitoring is active
                UpdateFileMonitoring();
                
                // Perform immediate sync check
                CheckForExternalChanges();
                
                System.Diagnostics.Debug.WriteLine("? Force sync check completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error in force sync check: {ex.Message}");
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

        /// <summary>
        /// Gets detailed diagnostic information about the sync system
        /// </summary>
        public static string GetSyncDiagnostics()
        {
            try
            {
                var fileExists = File.Exists(SaveFilePath);
                var folderExists = Directory.Exists(GetStorageFolder());
                var fileInfo = fileExists ? new FileInfo(SaveFilePath) : null;
                
                var diagnostics = "?? EasyToDo Sync Diagnostics\n" +
                                "=" + new string('=', 35) + "\n\n" +
                                $"?? Storage Path: {SaveFilePath}\n" +
                                $"?? Folder Exists: {(folderExists ? "? Yes" : "? No")}\n" +
                                $"?? File Exists: {(fileExists ? "? Yes" : "? No")}\n";
                
                if (fileExists && fileInfo != null)
                {
                    diagnostics += $"?? File Size: {fileInfo.Length:N0} bytes\n" +
                                 $"?? File Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss.fff}\n" +
                                 $"?? Our Last Save: {_lastSaveTime:yyyy-MM-dd HH:mm:ss.fff}\n" +
                                 $"?? Time Difference: {(fileInfo.LastWriteTime - _lastSaveTime).TotalSeconds:F2} seconds\n\n";
                }
                else
                {
                    diagnostics += "?? File not available for analysis\n\n";
                }
                
                diagnostics += $"?? File Watcher Status:\n" +
                             $"  • Active: {(_fileWatcher?.EnableRaisingEvents == true ? "? Yes" : "? No")}\n" +
                             $"  • Monitoring: {_fileWatcher?.Path ?? "None"}\n" +
                             $"  • Filter: {_fileWatcher?.Filter ?? "None"}\n\n" +
                             $"? Sync Timer: {(_syncCheckTimer?.IsEnabled == true ? "? Running" : "? Stopped")}\n" +
                             $"?? Check Interval: {SyncCheckInterval.TotalSeconds} seconds\n\n";
                
                // Test file access
                try
                {
                    if (fileExists)
                    {
                        using (var stream = new FileStream(SaveFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            diagnostics += "? File Access: Read access confirmed\n";
                        }
                    }
                }
                catch (Exception ex)
                {
                    diagnostics += $"? File Access Error: {ex.Message}\n";
                }
                
                diagnostics += "\n?? Troubleshooting Tips:\n" +
                             "• Ensure both computers have EasyToDo in the same cloud folder\n" +
                             "• Check that Dropbox/OneDrive is syncing properly\n" +
                             "• Verify file permissions in the storage folder\n" +
                             "• Try using 'Check for Sync' button to force a manual check\n" +
                             "• Look at Debug Output window for real-time sync messages";
                
                return diagnostics;
            }
            catch (Exception ex)
            {
                return $"? Error generating diagnostics: {ex.Message}";
            }
        }

        private static void PerformSave()
        {
            if (_currentNotes != null)
            {
                SaveNotes(_currentNotes);
                System.Diagnostics.Debug.WriteLine($"Auto-save completed at {DateTime.Now:HH:mm:ss.fff}");
            }
        }

        private static void InitializeBackupSystem()
        {
            // Check for backup on startup
            CheckAndCreateBackup();
            
            // Set up backup timer to check every hour
            _backupTimer = new DispatcherTimer();
            _backupTimer.Interval = TimeSpan.FromHours(1); // Check hourly
            _backupTimer.Tick += (s, e) => CheckAndCreateBackup();
            _backupTimer.Start();
            
            System.Diagnostics.Debug.WriteLine("Backup system initialized - checking every hour for daily backups");
        }

        private static Dictionary<string, string> LoadSettingsInternal()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
            return new Dictionary<string, string>();
        }

        private static void CheckAndCreateBackup()
        {
            try
            {
                var now = DateTime.Now.Date; // Get today's date (without time)
                
                // Create backup if:
                // 1. We haven't created one today, AND
                // 2. The main notes file exists, AND  
                // 3. At least 24 hours have passed since last backup
                if (_lastBackupDate.Date < now && 
                    File.Exists(SaveFilePath) && 
                    (DateTime.Now - _lastBackupDate) >= BackupInterval)
                {
                    CreateDailyBackup();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking backup: {ex.Message}");
            }
        }

        private static void CreateDailyBackup()
        {
            try
            {
                var backupFolder = Path.GetDirectoryName(SaveFilePath);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
                var backupFileName = $"notes-backup-{timestamp}.json";
                var backupPath = Path.Combine(backupFolder, backupFileName);

                // Don't create duplicate backups for the same day
                if (File.Exists(backupPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Backup already exists for today: {backupPath}");
                    _lastBackupDate = DateTime.Now;
                    SaveBackupSettings();
                    return;
                }

                // Copy the current notes file to backup
                File.Copy(SaveFilePath, backupPath, true);
                _lastBackupDate = DateTime.Now;
                SaveBackupSettings();

                System.Diagnostics.Debug.WriteLine($"Daily backup created: {backupPath}");

                // Clean up old backups (keep only last MaxBackupFiles)
                CleanupOldBackups(backupFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating daily backup: {ex.Message}");
            }
        }

        private static void CleanupOldBackups(string backupFolder)
        {
            try
            {
                var backupFiles = Directory.GetFiles(backupFolder, "notes-backup-*.json");
                
                if (backupFiles.Length <= MaxBackupFiles)
                    return; // No cleanup needed

                // Sort by creation time (oldest first)
                Array.Sort(backupFiles, (f1, f2) => 
                    File.GetCreationTime(f1).CompareTo(File.GetCreationTime(f2)));

                // Delete oldest files to keep only MaxBackupFiles
                int filesToDelete = backupFiles.Length - MaxBackupFiles;
                for (int i = 0; i < filesToDelete; i++)
                {
                    File.Delete(backupFiles[i]);
                    System.Diagnostics.Debug.WriteLine($"Deleted old backup: {Path.GetFileName(backupFiles[i])}");
                }
                
                System.Diagnostics.Debug.WriteLine($"Backup cleanup complete - keeping {MaxBackupFiles} most recent backups");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up old backups: {ex.Message}");
            }
        }

        public static List<string> GetAvailableBackups()
        {
            try
            {
                var backupFolder = Path.GetDirectoryName(SaveFilePath);
                var backupFiles = Directory.GetFiles(backupFolder, "notes-backup-*.json");
                
                // Sort by creation time (newest first)
                Array.Sort(backupFiles, (f1, f2) => 
                    File.GetCreationTime(f2).CompareTo(File.GetCreationTime(f1)));

                return new List<string>(backupFiles);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting available backups: {ex.Message}");
                return new List<string>();
            }
        }

        public static ObservableCollection<Note> LoadBackup(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    string jsonString = File.ReadAllText(backupPath);
                    var notes = JsonSerializer.Deserialize<ObservableCollection<Note>>(jsonString, Options);
                    
                    System.Diagnostics.Debug.WriteLine($"Backup loaded from: {backupPath}");
                    return notes ?? new ObservableCollection<Note>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading backup: {ex.Message}");
            }

            return new ObservableCollection<Note>();
        }

        public static void StopFileMonitoring()
        {
            try
            {
                _fileWatcher?.Dispose();
                _syncCheckTimer?.Stop();
                System.Diagnostics.Debug.WriteLine("File monitoring stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping file monitoring: {ex.Message}");
            }
        }

        private static ObservableCollection<Note> _currentNotes;

        public static void RequestSave(ObservableCollection<Note> notes)
        {
            _currentNotes = notes;
            _hasPendingSave = true;
            
            // Restart the timer - this provides throttling for rapid changes
            _saveTimer.Stop();
            _saveTimer.Start();
            
            System.Diagnostics.Debug.WriteLine($"Auto-save requested at {DateTime.Now:HH:mm:ss.fff}");
        }

        private static void SaveBackupSettings()
        {
            try
            {
                var settings = LoadSettingsInternal() ?? new Dictionary<string, string>();
                settings["LastBackupDate"] = _lastBackupDate.ToString("O"); // ISO 8601 format
                
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving backup settings: {ex.Message}");
            }
        }
    }
}