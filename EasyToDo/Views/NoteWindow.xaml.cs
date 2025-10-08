using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        // Available color options
        private readonly List<ColorOption> _colorOptions = new List<ColorOption>
        {
            new ColorOption("Yellow", "#FFFFE0"),
            new ColorOption("Pink", "#FFE4E1"),
            new ColorOption("Blue", "#E0FFFF"),
            new ColorOption("Green", "#E0FFE0"),
            new ColorOption("Orange", "#FFE4C4"),
            new ColorOption("Purple", "#E6E6FA"),
            new ColorOption("Peach", "#FFDAB9")
        };

        // Event to notify when a note changes
        public event EventHandler NoteChanged;

        public NoteWindow(Note note)
        {
            InitializeComponent();
            _note = note;
            DataContext = _note;

            // Setup color picker
            ColorPicker.ItemsSource = _colorOptions;
            
            // Set initial color and find matching color option
            SetBackgroundColor(_note.BackgroundColor);
            SetColorPickerSelection(_note.BackgroundColor);

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

        private void SetColorPickerSelection(Color noteColor)
        {
            // Find the color option that matches the note's background color
            var matchingOption = _colorOptions.FirstOrDefault(option => 
                option.ColorValue.R == noteColor.R && 
                option.ColorValue.G == noteColor.G && 
                option.ColorValue.B == noteColor.B);

            if (matchingOption != null)
            {
                ColorPicker.SelectedItem = matchingOption;
            }
            else
            {
                // Default to first color if no match found
                ColorPicker.SelectedIndex = 0;
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            Topmost = _isPinned;
            
            if (sender is Button pinButton)
            {
                // Add debug information
                System.Diagnostics.Debug.WriteLine($"Pin state changed to: {_isPinned}");
                
                // Try emoji first, fallback to simple symbols if needed
                string emojiContent = _isPinned ? "\U0001F4CD" : "\U0001F4CC"; // ?? : ??
                string fallbackContent = _isPinned ? "?" : "?"; // Filled circle : Empty circle
                string newTooltip = _isPinned ? "Unpin window" : "Pin window on top";
                
                System.Diagnostics.Debug.WriteLine($"Setting button content to: {emojiContent}");
                
                // Set content with emoji first
                pinButton.Content = emojiContent;
                pinButton.ToolTip = newTooltip;
                
                // Ensure proper font fallback for emoji rendering
                pinButton.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI, Arial Unicode MS");
                
                // Force visual refresh
                pinButton.InvalidateVisual();
                pinButton.UpdateLayout();
                
                // If we still get rendering issues, we could add a timer to check and fallback
                // This is a backup approach in case emoji rendering fails completely
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Check if content appears to be rendered correctly by verifying it's not showing as "?"

                    var currentContent = pinButton.Content?.ToString();
                    if (string.IsNullOrEmpty(currentContent) || currentContent.Contains("?") || currentContent.Contains("?"))
                    {
                        System.Diagnostics.Debug.WriteLine("Emoji rendering failed, using fallback symbols");
                        pinButton.Content = fallbackContent;
                        pinButton.FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Arial");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
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
            if (ColorPicker.SelectedItem is ColorOption selectedOption)
            {
                _note.BackgroundColor = selectedOption.ColorValue;
                SetBackgroundColor(selectedOption.ColorValue);
                OnNoteChanged(); // Notify that the note has changed
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
                            OnNoteChanged(); // Save on Enter
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
                        OnNoteChanged(); // Save when losing focus
                    }
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