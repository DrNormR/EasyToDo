using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using EasyToDo.Models;

namespace EasyToDo.Views
{
    public partial class PopupNoteWindow : Window, INotifyPropertyChanged
    {
        private readonly NoteItem _noteItem;
        private string _noteText;
        private string _itemText;

        public event EventHandler<string> NoteSaved;
        public event EventHandler NoteDeleted;
        public event PropertyChangedEventHandler PropertyChanged;

        public string NoteText
        {
            get => _noteText;
            set
            {
                if (_noteText != value)
                {
                    _noteText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ItemText
        {
            get => _itemText;
            set
            {
                if (_itemText != value)
                {
                    _itemText = value;
                    OnPropertyChanged();
                }
            }
        }

        public PopupNoteWindow(NoteItem noteItem)
        {
            InitializeComponent();
            _noteItem = noteItem ?? throw new ArgumentNullException(nameof(noteItem));
            
            DataContext = this;
            
            // Initialize properties
            ItemText = noteItem.Text;
            NoteText = noteItem.PopupNoteText ?? string.Empty;
            
            // Set up keyboard shortcuts
            KeyDown += PopupNoteWindow_KeyDown;
            
            // Focus the text box when window loads
            Loaded += (s, e) => NoteTextBox.Focus();
            
            // Set window position relative to mouse cursor
            SetWindowPosition();
        }

        private void SetWindowPosition()
        {
            var mousePosition = Mouse.GetPosition(Application.Current.MainWindow);
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                var point = mainWindow.PointToScreen(mousePosition);
                Left = Math.Max(0, point.X - Width / 2);
                Top = Math.Max(0, point.Y - Height / 2);
                
                // Ensure window is within screen bounds
                var workingArea = SystemParameters.WorkArea;
                if (Left + Width > workingArea.Right)
                    Left = workingArea.Right - Width;
                if (Top + Height > workingArea.Bottom)
                    Top = workingArea.Bottom - Height;
            }
        }

        private void PopupNoteWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SaveAndClose();
                e.Handled = true;
            }
            else if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                InsertBulletPoint();
                e.Handled = true;
            }
            else if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                InsertNumberedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                InsertCheckbox();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void BulletButton_Click(object sender, RoutedEventArgs e)
        {
            InsertBulletPoint();
        }

        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            InsertNumberedItem();
        }

        private void CheckboxButton_Click(object sender, RoutedEventArgs e)
        {
            InsertCheckbox();
        }

        private void InsertBulletPoint()
        {
            InsertTextAtCursor("• ");
        }

        private void InsertNumberedItem()
        {
            // Count existing numbered items to get next number
            var lines = NoteText.Split('\n');
            int nextNumber = 1;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.')
                {
                    if (int.TryParse(trimmed.Substring(0, 1), out int num) && num >= nextNumber)
                    {
                        nextNumber = num + 1;
                    }
                }
            }
            
            InsertTextAtCursor($"{nextNumber}. ");
        }

        private void InsertCheckbox()
        {
            InsertTextAtCursor("? ");
        }

        private void InsertTextAtCursor(string text)
        {
            try
            {
                int cursorPosition = NoteTextBox.CaretIndex;
                var currentText = NoteText ?? string.Empty;
                
                // Check if we're at the beginning of a line or after a newline
                bool atLineStart = cursorPosition == 0 || 
                                  (cursorPosition > 0 && currentText[cursorPosition - 1] == '\n');
                
                string textToInsert = text;
                if (!atLineStart)
                {
                    textToInsert = "\n" + text;
                }
                
                // Insert the text
                var newText = currentText.Insert(cursorPosition, textToInsert);
                NoteText = newText;
                
                // Move cursor to end of inserted text
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NoteTextBox.CaretIndex = cursorPosition + textToInsert.Length;
                    NoteTextBox.Focus();
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inserting text: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveAndClose();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear all text?", 
                "Clear Note", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                NoteText = string.Empty;
                NoteTextBox.Focus();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to delete this note completely?\n\nThis action cannot be undone.", 
                "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Clear the note from the item
                    _noteItem.PopupNoteText = null;
                    _noteItem.HasNote = false;
                    
                    // Raise event to notify parent
                    NoteDeleted?.Invoke(this, EventArgs.Empty);
                    
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting note: {ex.Message}", "Delete Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAndClose()
        {
            try
            {
                // Update the note item
                _noteItem.PopupNoteText = string.IsNullOrWhiteSpace(NoteText) ? null : NoteText;
                
                // Raise event to notify parent
                NoteSaved?.Invoke(this, NoteText);
                
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving note: {ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}