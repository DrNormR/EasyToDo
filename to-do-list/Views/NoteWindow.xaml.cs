using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using to_do_list.Models;

namespace to_do_list.Views
{
    public partial class NoteWindow : Window
    {
        private readonly Note _note;
        private bool _isPinned = false;
        private Point _startPoint;
        private bool _isDragging = false;
        private NoteItem _draggedItem;

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

        private void NewItemTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text) && 
                    textBox.Text != "Type here to add a new item...")
                {
                    _note.Items.Add(new NoteItem { Text = textBox.Text, IsChecked = false });
                    textBox.Text = string.Empty;
                    e.Handled = true; // Prevent the beep sound
                    OnNoteChanged(); // Notify that the note has changed
                }
            }
        }

        private void NewItemTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Text == "Type here to add a new item...")
            {
                textBox.Text = string.Empty;
                textBox.FontStyle = FontStyles.Normal;
            }
        }

        private void NewItemTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Type here to add a new item...";
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

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            // The checkbox state is automatically updated through binding
            OnNoteChanged(); // Notify that the note has changed
        }

        private void DeleteItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("DeleteItem_PreviewMouseLeftButtonDown called!");
            
            if (sender is FrameworkElement element && element.DataContext is NoteItem item)
            {
                System.Diagnostics.Debug.WriteLine($"Found NoteItem: {item.Text}");
                System.Diagnostics.Debug.WriteLine($"Items count before removal: {_note.Items.Count}");
                
                bool removed = _note.Items.Remove(item);
                System.Diagnostics.Debug.WriteLine($"Item removed successfully: {removed}");
                System.Diagnostics.Debug.WriteLine($"Items count after removal: {_note.Items.Count}");
                
                OnNoteChanged();
                e.Handled = true; // Prevent further event processing
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Could not find NoteItem in DataContext");
            }
        }

        // Keep the old method for reference but it shouldn't be called anymore
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("DeleteItem_Click called!");
            
            if (sender is FrameworkElement element)
            {
                System.Diagnostics.Debug.WriteLine($"Sender is FrameworkElement: {element.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"DataContext type: {element.DataContext?.GetType().Name ?? "null"}");
                
                if (element.DataContext is NoteItem item)
                {
                    System.Diagnostics.Debug.WriteLine($"Found NoteItem: {item.Text}");
                    System.Diagnostics.Debug.WriteLine($"Items count before removal: {_note.Items.Count}");
                    
                    bool removed = _note.Items.Remove(item);
                    System.Diagnostics.Debug.WriteLine($"Item removed successfully: {removed}");
                    System.Diagnostics.Debug.WriteLine($"Items count after removal: {_note.Items.Count}");
                    
                    OnNoteChanged();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("DataContext is not NoteItem");
                    
                    // Let's try finding it through visual tree
                    var parent = element.Parent;
                    System.Diagnostics.Debug.WriteLine($"Parent type: {parent?.GetType().Name ?? "null"}");
                    
                    if (parent is Grid grid)
                    {
                        System.Diagnostics.Debug.WriteLine($"Grid DataContext: {grid.DataContext?.GetType().Name ?? "null"}");
                        
                        var border = grid.Parent as Border;
                        if (border != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Border DataContext: {border.DataContext?.GetType().Name ?? "null"}");
                            
                            if (border.DataContext is NoteItem borderItem)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found item through border: {borderItem.Text}");
                                bool removed = _note.Items.Remove(borderItem);
                                System.Diagnostics.Debug.WriteLine($"Item removed through border: {removed}");
                                OnNoteChanged();
                            }
                        }
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Sender is not FrameworkElement");
            }
        }

        // Helper method to find parent of specific type
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return parent as T;
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
    }
}