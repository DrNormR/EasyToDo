using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
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
            StorageStatusLabel.MouseLeftButtonUp += StorageStatusLabel_Click;
            LocateFileButton.Click += LocateFileButton_Click;
            // Removed: ChangeStorageButton.Click += ChangeStorageButton_Click; (already in XAML)

            // Set up handlers to detect changes
            Notes.CollectionChanged += Notes_CollectionChanged;

            // Register window closing event to save notes
            Closing += MainWindow_Closing;

            // Update storage status display
            UpdateStorageStatus();
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

            StorageStatusLabel.Text = $"{icon} {NoteStorage.GetStorageLocationDescription()}";
            StorageStatusLabel.ToolTip = $"Notes saved to: {NoteStorage.GetSaveLocation()}\n\nLeft-click for storage details\nRight-click for backup management";
        }

        private void ChangeStorageButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ChangeStorageButton_Click called");
            
            // Disable the button temporarily to prevent double-clicks
            ChangeStorageButton.IsEnabled = false;
            
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
                // Re-enable the button after a short delay
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ChangeStorageButton.IsEnabled = true;
                }), System.Windows.Threading.DispatcherPriority.Background);
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

        private void LocateFileButton_Click(object sender, RoutedEventArgs e)
        {
            NoteStorage.OpenFileLocation();
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            NoteStorage.OpenFileLocation();
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
                : "No backups created yet\nFirst backup: When you close the app or tomorrow";
            
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

            // Show storage details with backup options
            var result = MessageBox.Show(
                message + $"\n\n📋 Would you like to create a backup now?",
                "Storage Information",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
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
        }

        private void ChangeStorageLocation_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ChangeStorageLocation_Click called (from context menu)");
            ChangeStorageButton_Click(sender, e);
        }

        private void ShowBackupMenu()
        {
            var backups = NoteStorage.GetAvailableBackups();
            
            if (backups.Count == 0)
            {
                MessageBox.Show(
                    "📋 No Backups Available\n\n" +
                    "No backup files found. Backups are created automatically daily and when the app closes.\n\n" +
                    "Would you like to create a backup now?",
                    "Backup Management",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
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

            backupList += "\n💡 Tip: Right-click the storage status to manage backups\n" +
                         "⚠️ Restoring will replace your current notes!";

            var result = MessageBox.Show(
                backupList + "\n\nWould you like to restore from a backup?",
                "Backup Management",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // For now, restore from the most recent backup
                // In a full implementation, you'd show a file picker
                RestoreFromBackup(backups[0]);
            }
        }

        private void RestoreFromBackup(string backupPath)
        {
            var result = MessageBox.Show(
                $"⚠️ Restore Confirmation\n\n" +
                $"This will replace ALL your current notes with the backup from:\n" +
                $"{Path.GetFileName(backupPath)}\n" +
                $"Created: {File.GetCreationTime(backupPath):MMM dd, yyyy 'at' HH:mm}\n\n" +
                $"Your current notes will be lost unless you create a backup first.\n\n" +
                $"Are you sure you want to continue?",
                "Restore from Backup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Load notes from backup
                    var backupNotes = NoteStorage.LoadBackup(backupPath);
                    
                    // Replace current notes
                    Notes.Clear();
                    foreach (var note in backupNotes)
                    {
                        Notes.Add(note);
                    }

                    // Save immediately to persist the restoration
                    NoteStorage.SaveImmediately(Notes);
                    
                    ShowStatusMessage($"✅ Restored from backup: {Path.GetFileName(backupPath)}", TimeSpan.FromSeconds(4));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error restoring backup: {ex.Message}");
                    ShowStatusMessage("❌ Failed to restore backup", TimeSpan.FromSeconds(3));
                }
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
            // Force immediate save when application is closing
            NoteStorage.SaveImmediately(Notes);
        }

        private void SaveNotes()
        {
            // Use the new throttled auto-save system
            NoteStorage.RequestSave(Notes);
        }
    }
}