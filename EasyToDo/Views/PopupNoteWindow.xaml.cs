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
        private string _attachmentText;
        private string _itemText;

        public event EventHandler<string> AttachmentSaved;
        public event EventHandler AttachmentDeleted;
        public event PropertyChangedEventHandler PropertyChanged;

        public string NoteText
        {
            get => _attachmentText;
            set
            {
                if (_attachmentText != value)
                {
                    _attachmentText = value;
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
            NoteText = noteItem.TextAttachment ?? string.Empty;
            
            // Set up keyboard shortcuts (only window-level, not TextBox level)
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

        private void NoteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleEnterKeyForLists(e);
            }
        }

        private void HandleEnterKeyForLists(KeyEventArgs e)
        {
            try
            {
                var textBox = NoteTextBox;
                if (textBox == null) return;

                var text = textBox.Text ?? string.Empty;
                var caretIndex = textBox.CaretIndex;
                
                // Get the current line text where the cursor is
                string currentLine = GetCurrentLine(text, caretIndex);
                string trimmedLine = currentLine.Trim();
                
                System.Diagnostics.Debug.WriteLine($"Current line: '{currentLine}', Trimmed: '{trimmedLine}'");
                
                // Check if current line is a bullet point
                if (trimmedLine.StartsWith("• "))
                {
                    var content = trimmedLine.Substring(2).Trim();
                    if (string.IsNullOrEmpty(content))
                    {
                        // Empty bullet line - stop the list
                        RemoveCurrentListMarker(text, caretIndex, currentLine);
                        e.Handled = true;
                    }
                    else
                    {
                        // Continue bullet list
                        InsertNewLineWithMarker("\n• ");
                        e.Handled = true;
                    }
                }
                // Check if current line is a numbered item
                else if (IsNumberedLine(trimmedLine))
                {
                    var content = GetNumberedLineContent(trimmedLine);
                    if (string.IsNullOrEmpty(content))
                    {
                        // Empty numbered line - stop the list
                        RemoveCurrentListMarker(text, caretIndex, currentLine);
                        e.Handled = true;
                    }
                    else
                    {
                        // Continue numbered list with next number
                        var currentNumber = GetNumberFromLine(trimmedLine);
                        var nextNumber = currentNumber + 1;
                        InsertNewLineWithMarker($"\n{nextNumber}. ");
                        e.Handled = true;
                    }
                }
                // Check if current line is a checkbox
                else if (trimmedLine.StartsWith("? ") || trimmedLine.StartsWith("? "))
                {
                    var content = trimmedLine.Substring(2).Trim();
                    if (string.IsNullOrEmpty(content))
                    {
                        // Empty checkbox line - stop the list
                        RemoveCurrentListMarker(text, caretIndex, currentLine);
                        e.Handled = true;
                    }
                    else
                    {
                        // Continue checkbox list
                        InsertNewLineWithMarker("\n? ");
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling Enter key: {ex.Message}");
                // Let the default behavior happen if there's an error
            }
        }

        private string GetCurrentLine(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            
            // Find the start of the current line
            int lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1));
            if (lineStart == -1) lineStart = 0;
            else lineStart++; // Move past the \n character
            
            // Find the end of the current line
            int lineEnd = text.IndexOf('\n', caretIndex);
            if (lineEnd == -1) lineEnd = text.Length;
            
            return text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
        }

        private bool IsNumberedLine(string line)
        {
            if (line.Length < 3) return false;
            return char.IsDigit(line[0]) && line[1] == '.' && line[2] == ' ';
        }

        private string GetNumberedLineContent(string line)
        {
            if (!IsNumberedLine(line)) return line;
            return line.Substring(3).Trim();
        }

        private int GetNumberFromLine(string line)
        {
            if (!IsNumberedLine(line)) return 1;
            if (int.TryParse(line.Substring(0, 1), out int number))
                return number;
            return 1;
        }

        private void InsertNewLineWithMarker(string marker)
        {
            try
            {
                var currentText = NoteText ?? string.Empty;
                var caretIndex = NoteTextBox.CaretIndex;
                var newText = currentText.Insert(caretIndex, marker);
                NoteText = newText;
                
                // Move cursor to end of inserted text
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NoteTextBox.CaretIndex = caretIndex + marker.Length;
                    NoteTextBox.Focus();
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inserting new line with marker: {ex.Message}");
            }
        }

        private void RemoveCurrentListMarker(string text, int caretIndex, string currentLine)
        {
            try
            {
                // Find the start of the current line
                int lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1));
                if (lineStart == -1) lineStart = 0;
                else lineStart++; // Move past the \n character
                
                // Remove the list marker from the current line, leaving just a newline
                var beforeLine = text.Substring(0, lineStart);
                var afterLine = text.Substring(caretIndex);
                
                NoteText = beforeLine + afterLine;
                
                // Position cursor at the beginning of the now-empty line
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NoteTextBox.CaretIndex = lineStart;
                    NoteTextBox.Focus();
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing list marker: {ex.Message}");
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
            var result = MessageBox.Show("Are you sure you want to delete this text attachment completely?\n\nThis action cannot be undone.", 
                "Delete Text Attachment", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Clear the attachment from the item
                    _noteItem.TextAttachment = null;
                    _noteItem.HasTextAttachment = false;
                    
                    // Raise event to notify parent
                    AttachmentDeleted?.Invoke(this, EventArgs.Empty);
                    
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting text attachment: {ex.Message}", "Delete Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveAndClose()
        {
            try
            {
                // Update the note item
                _noteItem.TextAttachment = string.IsNullOrWhiteSpace(NoteText) ? null : NoteText;
                
                // Raise event to notify parent
                AttachmentSaved?.Invoke(this, NoteText);
                
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving text attachment: {ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}