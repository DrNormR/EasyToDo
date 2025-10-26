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

        private void NoteTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                System.Diagnostics.Debug.WriteLine("?? Enter key detected in PreviewKeyDown");
                
                if (TryHandleListContinuation())
                {
                    System.Diagnostics.Debug.WriteLine("? List continuation handled, preventing default Enter");
                    e.Handled = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("?? No list detected, allowing normal Enter");
                }
            }
        }

        private bool TryHandleListContinuation()
        {
            try
            {
                var textBox = NoteTextBox;
                if (textBox == null) return false;

                // Get current text and cursor position
                string text = textBox.Text ?? "";
                int caretIndex = textBox.CaretIndex;
                
                System.Diagnostics.Debug.WriteLine($"?? Processing text at caret {caretIndex}: '{text}'");
                
                // Find the current line
                string currentLine = GetLineAtCaret(text, caretIndex);
                string trimmed = currentLine.Trim();
                
                System.Diagnostics.Debug.WriteLine($"?? Current line: '{currentLine}' | Trimmed: '{trimmed}'");
                
                // Check for bullet point
                if (trimmed.StartsWith("• "))
                {
                    string content = trimmed.Substring(2);
                    System.Diagnostics.Debug.WriteLine($"?? Found bullet, content: '{content}'");
                    
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        // Remove bullet and don't continue
                        RemoveBulletFromCurrentLine(text, caretIndex, currentLine);
                        return true;
                    }
                    else
                    {
                        // Add new bullet
                        InsertAtCaret("\n• ");
                        return true;
                    }
                }
                
                // Check for numbered list
                var numberMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+)\.\s(.*)");
                if (numberMatch.Success)
                {
                    int currentNumber = int.Parse(numberMatch.Groups[1].Value);
                    string content = numberMatch.Groups[2].Value;
                    
                    System.Diagnostics.Debug.WriteLine($"?? Found number {currentNumber}, content: '{content}'");
                    
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        // Remove number and don't continue
                        RemoveNumberFromCurrentLine(text, caretIndex, currentLine);
                        return true;
                    }
                    else
                    {
                        // Add next number
                        int nextNumber = currentNumber + 1;
                        InsertAtCaret($"\n{nextNumber}. ");
                        return true;
                    }
                }
                
                // Check for checkbox
                if (trimmed.StartsWith("? ") || trimmed.StartsWith("? "))
                {
                    string content = trimmed.Substring(2);
                    System.Diagnostics.Debug.WriteLine($"? Found checkbox, content: '{content}'");
                    
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        // Remove checkbox and don't continue
                        RemoveCheckboxFromCurrentLine(text, caretIndex, currentLine);
                        return true;
                    }
                    else
                    {
                        // Add new checkbox
                        InsertAtCaret("\n? ");
                        return true;
                    }
                }
                
                return false; // No list found
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error in TryHandleListContinuation: {ex.Message}");
                return false;
            }
        }

        private string GetLineAtCaret(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // Find line start
            int lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1));
            if (lineStart < 0) lineStart = 0;
            else lineStart++; // Skip the \n
            
            // Find line end
            int lineEnd = text.IndexOf('\n', caretIndex);
            if (lineEnd < 0) lineEnd = text.Length;
            
            return text.Substring(lineStart, lineEnd - lineStart);
        }

        private void InsertAtCaret(string textToInsert)
        {
            try
            {
                var textBox = NoteTextBox;
                int caretPos = textBox.CaretIndex;
                
                System.Diagnostics.Debug.WriteLine($"?? Inserting '{textToInsert}' at position {caretPos}");
                
                // Insert text directly into TextBox
                textBox.Text = textBox.Text.Insert(caretPos, textToInsert);
                
                // Move caret to end of inserted text
                textBox.CaretIndex = caretPos + textToInsert.Length;
                
                System.Diagnostics.Debug.WriteLine($"? Text inserted, caret moved to {textBox.CaretIndex}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error inserting text: {ex.Message}");
            }
        }

        private void RemoveBulletFromCurrentLine(string text, int caretIndex, string currentLine)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("??? Removing bullet from current line");
                
                // Find where current line starts in the full text
                int lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1));
                if (lineStart < 0) lineStart = 0;
                else lineStart++; // Skip the \n
                
                // Replace the bullet with nothing
                string lineWithoutBullet = currentLine.Replace("• ", "");
                
                // Rebuild the text
                string before = text.Substring(0, lineStart);
                string after = text.Substring(lineStart + currentLine.Length);
                
                NoteTextBox.Text = before + lineWithoutBullet + after;
                NoteTextBox.CaretIndex = lineStart;
                
                System.Diagnostics.Debug.WriteLine("? Bullet removed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error removing bullet: {ex.Message}");
            }
        }

        private void RemoveNumberFromCurrentLine(string text, int caretIndex, string currentLine)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("??? Removing number from current line");
                
                // Find where current line starts in the full text
                int lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1));
                if (lineStart < 0) lineStart = 0;
                else lineStart++; // Skip the \n
                
                // Remove the number pattern
                string lineWithoutNumber = System.Text.RegularExpressions.Regex.Replace(currentLine, @"^\d+\.\s", "");
                
                // Rebuild the text
                string before = text.Substring(0, lineStart);
                string after = text.Substring(lineStart + currentLine.Length);
                
                NoteTextBox.Text = before + lineWithoutNumber + after;
                NoteTextBox.CaretIndex = lineStart;
                
                System.Diagnostics.Debug.WriteLine("? Number removed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error removing number: {ex.Message}");
            }
        }

        private void RemoveCheckboxFromCurrentLine(string text, int caretIndex, string currentLine)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("??? Removing checkbox from current line");
                
                // Find where current line starts in the full text
                int lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1));
                if (lineStart < 0) lineStart = 0;
                else lineStart++; // Skip the \n
                
                // Replace the checkbox with nothing
                string lineWithoutCheckbox = currentLine.Replace("? ", "").Replace("? ", "");
                
                // Rebuild the text
                string before = text.Substring(0, lineStart);
                string after = text.Substring(lineStart + currentLine.Length);
                
                NoteTextBox.Text = before + lineWithoutCheckbox + after;
                NoteTextBox.CaretIndex = lineStart;
                
                System.Diagnostics.Debug.WriteLine("? Checkbox removed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error removing checkbox: {ex.Message}");
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
                var numberMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+)\.\s");
                if (numberMatch.Success)
                {
                    int num = int.Parse(numberMatch.Groups[1].Value);
                    if (num >= nextNumber)
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