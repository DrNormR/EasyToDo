using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EasyToDo.Models;
using EasyToDo.Services;

namespace EasyToDo.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Note> Notes { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            // Load existing notes
            Notes = NoteStorage.LoadNotes();

            NotesListBox.ItemsSource = Notes;
            CreateNoteButton.Click += CreateNoteButton_Click;
            DeleteNoteButton.Click += DeleteNoteButton_Click;
            DuplicateNoteButton.Click += DuplicateNoteButton_Click;
            NotesListBox.MouseDoubleClick += NotesListBox_MouseDoubleClick;

            // Set up handlers to detect changes
            Notes.CollectionChanged += Notes_CollectionChanged;

            // Register window closing event to save notes
            Closing += MainWindow_Closing;

            // Subscribe to external file changes for real-time sync
            NoteStorage.ExternalFileChanged += OnExternalFileChanged;

            // Update storage status display
            UpdateStorageStatus();
        }

        private void OnExternalFileChanged(object sender, ObservableCollection<Note> updatedNotes)
        {
            // This runs when another device/instance changes the notes file
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 External file changes detected - updating UI with {updatedNotes.Count} notes");
                
                // Update the Notes collection on the UI thread
                Dispatcher.Invoke(() =>
                {
                    // Keep track of which note windows are currently open
                    var openWindows = Application.Current.Windows.OfType<NoteWindow>().ToList();
                    var windowNoteMapping = openWindows.ToDictionary(w => w.DataContext as Note, w => w);
                    
                    // Temporarily unsubscribe from change events to prevent save loops
                    Notes.CollectionChanged -= Notes_CollectionChanged;
                    
                    // Clear and reload all notes
                    Notes.Clear();
                    foreach (var note in updatedNotes)
                    {
                        Notes.Add(note);
                        
                        // Re-subscribe to property changes for the new notes
                        note.Items.CollectionChanged += NoteItems_CollectionChanged;
                        if (note is System.ComponentModel.INotifyPropertyChanged notifyNote)
                        {
                            notifyNote.PropertyChanged += Note_PropertyChanged;
                        }
                        
                        // Subscribe to item property changes
                        foreach (var item in note.Items)
                        {
                            if (item is System.ComponentModel.INotifyPropertyChanged notifyItem)
                            {
                                notifyItem.PropertyChanged += NoteItem_PropertyChanged;
                            }
                        }
                    }
                    
                    // Update any open note windows with the refreshed data
                    foreach (var window in openWindows)
                    {
                        try
                        {
                            var oldNote = window.DataContext as Note;
                            if (oldNote != null)
                            {
                                // Find the corresponding updated note by title (or could use ID if available)
                                var updatedNote = updatedNotes.FirstOrDefault(n => 
                                    n.Title == oldNote.Title && 
                                    n.BackgroundColor.Equals(oldNote.BackgroundColor));
                                
                                if (updatedNote != null)
                                {
                                    // Update the window's DataContext to the new note instance
                                    window.DataContext = updatedNote;
                                    System.Diagnostics.Debug.WriteLine($"🪟 Updated note window for: {updatedNote.Title}");
                                }
                                else
                                {
                                    // The note might have been deleted externally
                                    System.Diagnostics.Debug.WriteLine($"⚠️ Note window for '{oldNote.Title}' - corresponding note not found in update");
                                    
                                    // Show a message to the user and close the window
                                    MessageBox.Show(
                                        $"The note '{oldNote.Title}' has been modified or deleted on another device.\n\n" +
                                        $"This window will now close to prevent conflicts.",
                                        "Note Synchronized",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                    
                                    window.Close();
                                }
                            }
                        }
                        catch (Exception windowEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Error updating note window: {windowEx.Message}");
                        }
                    }
                    
                    // Re-subscribe to collection changes
                    Notes.CollectionChanged += Notes_CollectionChanged;
                    
                    // Show a subtle notification
                    ShowStatusMessage("🔄 Notes synchronized", TimeSpan.FromSeconds(2));
                    
                    System.Diagnostics.Debug.WriteLine($"✅ UI updated with synchronized notes - {openWindows.Count} windows refreshed");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling external file changes: {ex.Message}");
                
                // Show user-friendly error message
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowStatusMessage("⚠️ Sync error - check Debug Output", TimeSpan.FromSeconds(3));
                }));
            }
        }

        private void UpdateStorageStatus()
        {
            var (storageType, folderPath, isCloudStorage, isCustomPath) = NoteStorage.GetStorageInfo();
            
            string icon = storageType switch
            {
                "Dropbox" => "📦",
                "OneDrive" => "☁️",
                "Custom" => "📂",
                _ => "💾"
            };

            // Update the menu button icon (only if it exists)
            if (StorageMenuIcon != null)
            {
                StorageMenuIcon.Text = icon;
            }
            
            // Update the status label (now just informational)
            if (StorageStatusLabel != null)
            {
                StorageStatusLabel.Text = $"{icon} {NoteStorage.GetStorageLocationDescription()}";
                StorageStatusLabel.ToolTip = $"Current storage: {NoteStorage.GetSaveLocation()}";
            }
        }

        private void StorageMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // Open the context menu on left-click
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void CreateBackupNow_Click(object sender, RoutedEventArgs e)
        {
            if (NoteStorage.CreateBackupNow())
            {
                ShowStatusMessage("✅ Backup created successfully!", TimeSpan.FromSeconds(3));
            }
            else
            {
                ShowStatusMessage("❌ Failed to create backup", TimeSpan.FromSeconds(3));
            }
        }

        private void ManageBackups_Click(object sender, RoutedEventArgs e)
        {
            ShowBackupFileChooser();
        }

        private void RestoreFromBackup_Click(object sender, RoutedEventArgs e)
        {
            ShowBackupFileChooser();
        }

        private void ResetToAutoStorage_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset to automatic storage detection?\n\nThis will move your notes back to the automatically detected location (Dropbox, OneDrive, or local storage).",
                "Reset Storage",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (NoteStorage.ResetToAutoStorage())
                {
                    UpdateStorageStatus();
                    MessageBox.Show(
                        "Storage reset successfully!\n\nYour notes have been moved to the automatically detected location.",
                        "Storage Reset",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Failed to reset storage location.\n\nPlease check that the target location is accessible.",
                        "Reset Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private void OpenStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            NoteStorage.OpenStorageFolder();
        }

        private void StorageDetails_Click(object sender, RoutedEventArgs e)
        {
            ShowStorageDetailsDialog();
        }

        private void StorageStatusLabel_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ShowStorageDetailsDialog();
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                // Right-click shows backup management menu
                ShowBackupMenu();
            }
        }

        private void ShowStorageDetailsDialog()
        {
            var (storageType, folderPath, isCloudStorage, isCustomPath) = NoteStorage.GetStorageInfo();
            var location = NoteStorage.GetSaveLocation();
            var (lastBackup, backupCount, nextBackup) = NoteStorage.GetBackupInfo();
            
            string backupInfo = lastBackup != DateTime.MinValue 
                ? $"Last backup: {lastBackup:MMM dd, yyyy 'at' HH:mm}\nBackups available: {backupCount}\nNext backup: {nextBackup}"
                : "No backups created yet\nFirst backup will be created automatically";
            
            string message = $"📍 Storage Details\n\n" +
                           $"Storage Type: {storageType}\n" +
                           $"Custom Location: {(isCustomPath ? "✅ Yes" : "❌ No")}\n" +
                           $"Cloud Sync: {(isCloudStorage ? "✅ Enabled" : "❌ Local only")}\n" +
                           $"Folder: {folderPath}\n" +
                           $"File: {Path.GetFileName(location)}\n\n" +
                           $"💾 Backup Information\n" +
                           $"{backupInfo}\n\n";

            if (isCustomPath)
            {
                message += $"📂 You've selected a custom storage location.\n" +
                          $"Use 'Reset to Auto Storage' to return to automatic detection.";
            }
            else if (isCloudStorage)
            {
                message += $"✨ Your notes sync automatically across devices!\n" +
                          $"Install EasyToDo on other devices to access your notes everywhere.";
            }
            else
            {
                message += $"💡 Install Dropbox or OneDrive to enable automatic syncing across devices,\n" +
                          $"or use 'Change Storage Location' to choose a custom folder.";
            }

            MessageBox.Show(
                message,
                "Storage Information",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ChangeStorageLocation_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ChangeStorageLocation_Click called (from context menu)");
            
            // Disable the menu temporarily to prevent double-clicks
            if (sender is MenuItem menuItem)
            {
                menuItem.IsEnabled = false;
            }
            
            try
            {
                if (NoteStorage.ChangeStorageLocation())
                {
                    // Update status after successful change
                    UpdateStorageStatus();
                    
                    // Show success message in the window instead of popup
                    var (storageType, folderPath, isCloudStorage, isCustomPath) = NoteStorage.GetStorageInfo();
                    ShowStatusMessage($"✅ Storage changed to {storageType}", TimeSpan.FromSeconds(4));
                }
            }
            finally
            {
                // Re-enable the menu item after a short delay
                if (sender is MenuItem menu)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        menu.IsEnabled = true;
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        /// <summary>
        /// Shows a temporary status message in the main window
        /// </summary>
        private void ShowStatusMessage(string message, TimeSpan duration)
        {
            StatusMessage.Text = message;
            StatusMessage.Visibility = Visibility.Visible;
            
            // Hide the message after the specified duration
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = duration;
            timer.Tick += (s, e) =>
            {
                StatusMessage.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void ShowBackupFileChooser()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.Title = "Choose Backup File to Restore";
                openFileDialog.Filter = "Backup files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                
                // Set initial directory to the storage folder where backups are located
                var storageFolder = NoteStorage.GetStorageFolder();
                if (Directory.Exists(storageFolder))
                {
                    openFileDialog.InitialDirectory = storageFolder;
                }

                var result = openFileDialog.ShowDialog();
                
                if (result == true && !string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    var selectedFile = openFileDialog.FileName;
                    
                    // Show confirmation dialog
                    var confirmResult = MessageBox.Show(
                        $"🔄 Restore from Backup\n\n" +
                        $"Selected file: {Path.GetFileName(selectedFile)}\n" +
                        $"Location: {Path.GetDirectoryName(selectedFile)}\n\n" +
                        $"⚠️ This will replace ALL your current notes!\n\n" +
                        $"💡 Consider creating a backup of your current notes first.\n\n" +
                        $"Do you want to continue?",
                        "Confirm Backup Restoration", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Warning);

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        RestoreFromBackupFile(selectedFile);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing backup file chooser: {ex.Message}");
                MessageBox.Show(
                    $"Error opening file chooser:\n{ex.Message}",
                    "File Selection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RestoreFromBackupFile(string backupPath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Restoring from backup: {backupPath}");
                
                // Load notes from backup
                var backupNotes = NoteStorage.LoadBackup(backupPath);
                
                // Temporarily unsubscribe from change events to prevent save loops during restoration
                Notes.CollectionChanged -= Notes_CollectionChanged;
                
                // Clear current notes and replace with backup
                Notes.Clear();
                foreach (var note in backupNotes)
                {
                    Notes.Add(note);
                    
                    // Re-subscribe to property changes for the restored notes
                    note.Items.CollectionChanged += NoteItems_CollectionChanged;
                    if (note is INotifyPropertyChanged notifyNote)
                    {
                        notifyNote.PropertyChanged += Note_PropertyChanged;
                    }
                    
                    // Subscribe to item property changes
                    foreach (var item in note.Items)
                    {
                        if (item is INotifyPropertyChanged notifyItem)
                        {
                            notifyItem.PropertyChanged += NoteItem_PropertyChanged;
                        }
                    }
                }

                // Re-subscribe to collection changes
                Notes.CollectionChanged += Notes_CollectionChanged;

                // Save immediately to persist the restoration
                NoteStorage.SaveImmediately(Notes);
                
                var fileName = Path.GetFileName(backupPath);
                ShowStatusMessage($"✅ Restored from: {fileName}", TimeSpan.FromSeconds(4));
                
                System.Diagnostics.Debug.WriteLine($"Successfully restored {backupNotes.Count} notes from backup");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring from backup: {ex.Message}");
                ShowStatusMessage("❌ Failed to restore backup", TimeSpan.FromSeconds(3));
                
                MessageBox.Show(
                    $"Error restoring from backup:\n{ex.Message}\n\n" +
                    $"Your current notes have not been changed.",
                    "Restore Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowBackupMenu()
        {
            var backups = NoteStorage.GetAvailableBackups();
            
            if (backups.Count == 0)
            {
                var createResult = MessageBox.Show(
                    "📋 No Backups Available\n\n" +
                    "No backup files found. Backups are created automatically daily and when the app closes.\n\n" +
                    "Would you like to create a backup now?",
                    "Backup Management",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                    
                if (createResult == MessageBoxResult.Yes)
                {
                    if (NoteStorage.CreateBackupNow())
                    {
                        ShowStatusMessage("✅ Backup created successfully!", TimeSpan.FromSeconds(3));
                    }
                    else
                    {
                        ShowStatusMessage("❌ Failed to create backup", TimeSpan.FromSeconds(3));
                    }
                }
                return;
            }

            string backupList = "📋 Available Backups:\n\n";
            for (int i = 0; i < Math.Min(backups.Count, 5); i++)
            {
                var fileName = Path.GetFileName(backups[i]);
                var fileDate = File.GetCreationTime(backups[i]);
                backupList += $"• {fileName} ({fileDate:MMM dd, HH:mm})\n";
            }

            if (backups.Count > 5)
            {
                backupList += $"... and {backups.Count - 5} more\n";
            }

            backupList += "\n💡 Use 'Restore from Backup' in Settings Menu to choose a backup file\n" +
                         "⚠️ Restoring will replace your current notes!";

            var chooseResult = MessageBox.Show(
                backupList + "\n\nWould you like to choose a backup to restore?",
                "Backup Management",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (chooseResult == MessageBoxResult.Yes)
            {
                ShowBackupFileChooser();
            }
        }

        private void CreateNoteButton_Click(object sender, RoutedEventArgs e)
        {
            var note = new Note { Title = $"Note {Notes.Count + 1}" };
            Notes.Add(note);
            var noteWindow = new NoteWindow(note);
            noteWindow.Show();
        }

        private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note note)
                Notes.Remove(note);
        }

        private void DuplicateNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note note)
            {
                var copy = new Note
                {
                    Title = note.Title + " (Copy)",
                    BackgroundColor = note.BackgroundColor,
                    Items = new ObservableCollection<NoteItem>(note.Items.Select(i => new NoteItem 
                    { 
                        Text = i.Text, 
                        IsChecked = i.IsChecked, 
                        IsCritical = i.IsCritical,
                        IsHeading = i.IsHeading
                    }))
                };
                Notes.Add(copy);
            }
        }

        private void NotesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (NotesListBox.SelectedItem is Note note)
            {
                var noteWindow = new NoteWindow(note);
                noteWindow.NoteChanged += NoteWindow_NoteChanged;
                noteWindow.Show();
            }
        }

        private void Notes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Save whenever notes are added, removed, or replaced
            SaveNotes();

            // Subscribe to property changes for new items
            if (e.NewItems != null)
            {
                foreach (Note note in e.NewItems)
                {
                    // Subscribe to the Items collection changes
                    note.Items.CollectionChanged += NoteItems_CollectionChanged;

                    // Subscribe to property changes on the note itself
                    if (note is INotifyPropertyChanged notifyNote)
                    {
                        notifyNote.PropertyChanged += Note_PropertyChanged;
                    }
                }
            }
        }

        private void NoteWindow_NoteChanged(object sender, EventArgs e)
        {
            // Save when a note is modified in the note window
            SaveNotes();
        }

        private void Note_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Save when a note property changes
            SaveNotes();
        }

        private void NoteItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Save when items within a note change
            SaveNotes();

            // Subscribe to property changes for new items
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged += NoteItem_PropertyChanged;
                    }
                }
            }
        }

        private void NoteItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Save when an item property changes
            SaveNotes();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Unsubscribe from external file changes
            NoteStorage.ExternalFileChanged -= OnExternalFileChanged;
            
            // Stop file monitoring
            NoteStorage.StopFileMonitoring();
            
            // Force immediate save when application is closing
            NoteStorage.SaveImmediately(Notes);
        }

        private void SaveNotes()
        {
            // Use the new throttled auto-save system
            NoteStorage.RequestSave(Notes);
        }

        private void CheckForSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Manual sync check requested");
                ShowStatusMessage("🔄 Checking for updates...", TimeSpan.FromSeconds(1));
                
                // Perform a more thorough sync check for manual requests
                // This is especially useful after wake-from-sleep scenarios
                for (int i = 0; i < 3; i++)
                {
                    NoteStorage.ForceCheckSync();
                    if (i < 2) // Don't delay after the last check
                    {
                        System.Threading.Thread.Sleep(300); // Brief delay between checks
                    }
                }
                
                // Also check sync health to ensure monitoring is working
                var diagnostics = NoteStorage.GetSyncDiagnostics();
                System.Diagnostics.Debug.WriteLine($"Sync health status:\n{diagnostics}");
                
                // Show brief success message
                ShowStatusMessage("✅ Sync check completed", TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during manual sync check: {ex.Message}");
                ShowStatusMessage("❌ Sync check failed", TimeSpan.FromSeconds(2));
            }
        }

        private void ShowSyncDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var diagnostics = NoteStorage.GetSyncDiagnostics();
                
                MessageBox.Show(
                    diagnostics,
                    "Sync Diagnostics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing sync diagnostics: {ex.Message}");
                MessageBox.Show(
                    $"Error generating diagnostics:\n{ex.Message}",
                    "Diagnostics Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🚀 Manual update check requested from UI");
                
                // Run version parsing test for debugging
                UpdateService.TestVersionParsing();
                
                ShowStatusMessage("🚀 Checking for updates...", TimeSpan.FromSeconds(2));
                
                var currentVersion = UpdateService.GetCurrentVersion();
                System.Diagnostics.Debug.WriteLine($"📍 Current version from UI: {currentVersion}");
                
                // Check for updates using the UpdateService
                var updateInfo = await UpdateService.CheckForUpdatesAsync();
                
                if (updateInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Update found from UI check: {updateInfo.LatestVersion}");
                    ShowStatusMessage("✅ Update available!", TimeSpan.FromSeconds(2));
                    await UpdateService.ShowUpdateDialogAsync(updateInfo);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No update found from UI check");
                    ShowStatusMessage("✅ You're up to date!", TimeSpan.FromSeconds(3));
                    
                    // Show detailed diagnostic information
                    var diagnosticMessage = $"You're running the latest version!\n\n" +
                                          $"Current version: {currentVersion}\n" +
                                          $"GitHub repository: DrNormR/EasyToDo\n" +
                                          $"Checking: https://api.github.com/repos/DrNormR/EasyToDo/releases/latest\n\n" +
                                          $"No updates available at this time.\n\n" +
                                          $"💡 Debug Info:\n" +
                                          $"Assembly version: {currentVersion}\n" +
                                          $"Version components: {currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}.{currentVersion.Revision}\n\n" +
                                          $"Latest GitHub tag found: Check Debug Output window for detailed parsing info.\n\n" +
                                          $"If you just published 'easytodov2.3', the parser should now handle this format correctly.";

                    MessageBox.Show(
                        diagnosticMessage,
                        "EasyToDo - Update Check Results",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in UI update check: {ex.Message}");
                ShowStatusMessage("❌ Update check failed", TimeSpan.FromSeconds(2));
                
                var currentVersion = UpdateService.GetCurrentVersion();
                var errorMessage = $"Unable to check for updates at this time.\n\n" +
                                 $"Current version: {currentVersion}\n" +
                                 $"Repository: DrNormR/EasyToDo\n\n" +
                                 $"Error details:\n{ex.Message}\n\n" +
                                 $"Please check:\n" +
                                 $"• Your internet connection\n" +
                                 $"• GitHub.com accessibility\n" +
                                 $"• Repository permissions\n\n" +
                                 $"Try again later or check GitHub directly.";
                
                MessageBox.Show(
                    errorMessage,
                    "Update Check Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void AboutApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentVersion = UpdateService.GetCurrentVersion();
                var (storageType, folderPath, isCloudStorage, isCustomPath) = NoteStorage.GetStorageInfo();
                
                string syncStatus = isCloudStorage ? "✅ Cloud Sync Enabled" : "📱 Local Storage Only";
                
                string aboutMessage = $"📝 EasyToDo - Sticky Notes Todo App\n\n" +
                                     $"Version: {currentVersion}\n" +
                                     $"Storage: {storageType}\n" +
                                     $"Sync Status: {syncStatus}\n\n" +
                                     $"🚀 Features:\n" +
                                     $"• Create and manage todo notes\n" +
                                     $"• Drag & drop to reorder items\n" +
                                     $"• Mark items as critical or headings\n" +
                                     $"• Color-coded notes\n" +
                                     $"• Pin notes on top\n" +
                                     $"• Automatic cloud sync (Dropbox/OneDrive)\n" +
                                     $"• Daily automatic backups\n" +
                                     $"• Real-time sync across devices\n" +
                                     $"• Auto-update system\n\n" +
                                     $"💾 Your notes are safely stored at:\n" +
                                     $"{folderPath}\n\n" +
                                     $"© 2024 EasyToDo - Simple, Fast, Reliable\n\n" +
                                     $"🔧 Having update issues? Click 'Show Details' for diagnostics.";

                var result = MessageBox.Show(
                    aboutMessage,
                    "About EasyToDo",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                // If user clicks Cancel (which we'll use as "Show Details"), show diagnostics
                if (result == MessageBoxResult.Cancel)
                {
                    var diagnostics = UpdateService.GetInstallationDiagnostics();
                    MessageBox.Show(
                        diagnostics,
                        "EasyToDo - Installation Diagnostics",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing about dialog: {ex.Message}");
                MessageBox.Show(
                    $"📝 EasyToDo - Sticky Notes Todo App\n\n" +
                    $"Version: {UpdateService.GetCurrentVersion()}\n\n" +
                    $"A simple and intuitive todo application\n" +
                    $"with cloud sync capabilities.\n\n" +
                    $"© 2024 EasyToDo",
                    "About EasyToDo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}