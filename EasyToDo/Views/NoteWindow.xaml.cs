using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using EasyToDo.Models;

namespace EasyToDo.Views
{
    public partial class NoteWindow : Window
    {
        private readonly Note _note;
        private bool _isPinned = false;
        private Point _startPoint;
        private bool _isDragging = false;
        private NoteItem _draggedItem;
        private bool _nextItemIsHeading = false;

        // Event to notify when a note changes
        public event EventHandler NoteChanged;

        public NoteWindow(Note note)
        {
            InitializeComponent();
            _note = note;
            DataContext = _note;

            // Set initial color
            SetBackgroundColor(_note.BackgroundColor);
            ColorPicker.SelectedIndex = 0; // Default to yellow

            // Set window position slightly offset from cursor
            var mousePosition = Mouse.GetPosition(Application.Current.MainWindow);
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                var point = mainWindow.PointToScreen(mousePosition);
                Left = point.X + 20;
                Top = point.Y + 20;
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            Topmost = _isPinned;
            if (sender is Button pinButton)
            {
                pinButton.Content = _isPinned ? "📍" : "📌";
                pinButton.ToolTip = _isPinned ? "Unpin window" : "Pin window on top";
            }
        }

        private void HeadingButton_Click(object sender, RoutedEventArgs e)
        {
            _nextItemIsHeading = !_nextItemIsHeading;
            UpdateHeadingButtonAppearance();
            
            // Focus the text box when activating heading mode
            if (_nextItemIsHeading)
            {
                NewItemTextBox.Focus();
            }
        }

        private void NewItemTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text) && 
                    textBox.Text != "Type here to add a new item..." &&
                    textBox.Text != "Type heading text...")
                {
                    var newItem = new NoteItem 
                    { 
                        Text = textBox.Text, 
                        IsChecked = false, 
                        IsCritical = false,
                        IsHeading = _nextItemIsHeading
                    };
                    
                    _note.Items.Add(newItem);
                    textBox.Text = string.Empty;
                    
                    // Reset heading mode after adding item
                    if (_nextItemIsHeading)
                    {
                        _nextItemIsHeading = false;
                        UpdateHeadingButtonAppearance();
                    }
                    
                    e.Handled = true; // Prevent the beep sound
                    OnNoteChanged(); // Notify that the note has changed
                }
            }
            else if (e.Key == Key.Escape && _nextItemIsHeading)
            {
                // Cancel heading mode on Escape
                _nextItemIsHeading = false;
                UpdateHeadingButtonAppearance();
            }
        }

        private void UpdateHeadingButtonAppearance()
        {
            if (_nextItemIsHeading)
            {
                HeadingButton.Background = new SolidColorBrush(Colors.LightBlue);
                HeadingButton.BorderBrush = new SolidColorBrush(Colors.DarkBlue);
                HeadingButton.Opacity = 1.0;
                HeadingButton.ToolTip = "Next item will be a heading (click to cancel)";
                
                // Update placeholder text
                if (NewItemTextBox.Text == "Type here to add a new item...")
                {
                    NewItemTextBox.Text = "Type heading text...";
                }
            }
            else
            {
                HeadingButton.Background = new SolidColorBrush(Colors.Transparent);
                HeadingButton.BorderBrush = new SolidColorBrush(Colors.Gray);
                HeadingButton.Opacity = 0.6;
                HeadingButton.ToolTip = "Add heading";
                
                // Reset placeholder text
                if (NewItemTextBox.Text == "Type heading text...")
                {
                    NewItemTextBox.Text = "Type here to add a new item...";
                }
            }
        }

        private void NewItemTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Text == "Type here to add a new item..." || textBox.Text == "Type heading text...")
                {
                    textBox.Text = string.Empty;
                    textBox.FontStyle = FontStyles.Normal;
                }
            }
        }

        private void NewItemTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (_nextItemIsHeading)
                {
                    textBox.Text = "Type heading text...";
                }
                else
                {
                    textBox.Text = "Type here to add a new item...";
                }
                textBox.FontStyle = FontStyles.Italic;
            }
        }

        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPicker.SelectedItem is ComboBoxItem selectedItem)
            {
                var colorHex = selectedItem.Tag.ToString();
                if (colorHex != null)
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorHex);
                    _note.BackgroundColor = color;
                    SetBackgroundColor(color);
                    OnNoteChanged(); // Notify that the note has changed
                }
            }
        }

        private void SetBackgroundColor(Color color)
        {
            Background = new SolidColorBrush(color);
        }

        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is NoteItem item)
            {
                // Only allow checking for non-heading items
                if (!item.IsHeading)
                {
                    item.IsChecked = !item.IsChecked;
                    OnNoteChanged(); // Notify that the note has changed
                }
                e.Handled = true; // Prevent further event processing
            }
        }

        private void DeleteItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is NoteItem item)
            {
                _note.Items.Remove(item);
                OnNoteChanged();
                e.Handled = true; // Prevent further event processing
            }
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                var grid = sender as Grid;
                if (grid != null)
                {
                    var textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
                    var textBox = grid.Children.OfType<TextBox>().FirstOrDefault();
                    
                    if (textBlock != null && textBox != null)
                    {
                        textBlock.Visibility = Visibility.Collapsed;
                        textBox.Visibility = Visibility.Visible;
                        textBox.Focus();
                        textBox.SelectAll();
                        e.Handled = true;
                    }
                }
            }
        }

        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var parent = textBox.Parent as Grid;
                if (parent != null)
                {
                    var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                    if (textBlock != null)
                    {
                        textBox.Visibility = Visibility.Collapsed;
                        textBlock.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (e.Key == Key.Enter)
                {
                    var parent = textBox.Parent as Grid;
                    if (parent != null)
                    {
                        var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                        if (textBlock != null)
                        {
                            textBox.Visibility = Visibility.Collapsed;
                            textBlock.Visibility = Visibility.Visible;
                            OnNoteChanged(); // Notify that the note has changed
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    if (textBox.DataContext is NoteItem item)
                    {
                        // Revert changes
                        textBox.Text = item.Text;
                    }
                    var parent = textBox.Parent as Grid;
                    if (parent != null)
                    {
                        var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                        if (textBlock != null)
                        {
                            textBox.Visibility = Visibility.Collapsed;
                            textBlock.Visibility = Visibility.Visible;
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        private void OnNoteChanged()
        {
            // Raise the event to notify that the note has changed
            NoteChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement dragHandle && dragHandle.DataContext is NoteItem item)
            {
                _startPoint = e.GetPosition(null);
                _isDragging = true;
                _draggedItem = item;
                dragHandle.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ItemBorder_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                Point position = e.GetPosition(null);
                Vector diff = _startPoint - position;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is FrameworkElement element)
                    {
                        if (Mouse.Captured is FrameworkElement captured)
                        {
                            captured.ReleaseMouseCapture();
                        }

                        DataObject dragData = new DataObject("NoteItem", _draggedItem);
                        DragDrop.DoDragDrop(element, dragData, DragDropEffects.Move);
                        _isDragging = false;
                        OnNoteChanged();
                    }
                }
            }
        }

        private void ItemBorder_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            if (Mouse.Captured is FrameworkElement captured)
            {
                captured.ReleaseMouseCapture();
            }
        }

        private void ItemBorder_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("NoteItem"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ItemBorder_Drop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.DataContext is NoteItem targetItem &&
                e.Data.GetDataPresent("NoteItem"))
            {
                NoteItem draggedItem = e.Data.GetData("NoteItem") as NoteItem;
                if (draggedItem != null && !ReferenceEquals(draggedItem, targetItem))
                {
                    int draggedIndex = _note.Items.IndexOf(draggedItem);
                    int targetIndex = _note.Items.IndexOf(targetItem);

                    if (draggedIndex != -1 && targetIndex != -1)
                    {
                        _note.Items.Move(draggedIndex, targetIndex);
                        OnNoteChanged();
                    }
                }
            }
        }

        private void CriticalButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.DataContext is NoteItem item)
            {
                // Only allow critical marking for non-heading items
                if (!item.IsHeading)
                {
                    item.IsCritical = !item.IsCritical;
                    OnNoteChanged(); // Notify that the note has changed
                }
                e.Handled = true; // Prevent further event processing
            }
        }
    }
}