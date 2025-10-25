using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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
        private bool _isInitialized = false;
        
        // Health monitoring
        private System.Windows.Threading.DispatcherTimer _healthCheckTimer;
        private DateTime _lastActivity = DateTime.Now;

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

            // Restore window size from note
            RestoreWindowSize();

            // Hook up window events for size persistence and health monitoring
            SizeChanged += NoteWindow_SizeChanged;
            Loaded += NoteWindow_Loaded;
            Activated += NoteWindow_Activated; // Add activation monitoring
            
            // Initialize health monitoring
            InitializeHealthMonitoring();
        }

        private void InitializeHealthMonitoring()
        {
            try
            {
                // Set up a timer to periodically check the health of the note window
                _healthCheckTimer = new System.Windows.Threading.DispatcherTimer();
                _healthCheckTimer.Interval = TimeSpan.FromMinutes(5); // Check every 5 minutes
                _healthCheckTimer.Tick += HealthCheckTimer_Tick;
                _healthCheckTimer.Start();
                
                System.Diagnostics.Debug.WriteLine("?? Health monitoring initialized - checking every 5 minutes");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error initializing health monitoring: {ex.Message}");
            }
        }

        private void HealthCheckTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("?? Performing periodic health check");
                
                // Check if the note is still valid
                if (_note?.Items == null)
                {
                    System.Diagnostics.Debug.WriteLine("?? Health check failed: Note or Items collection is null");
                    RefreshNoteBinding();
                    return;
                }
                
                // Check if DataContext is still correct
                if (DataContext != _note)
                {
                    System.Diagnostics.Debug.WriteLine("?? Health check failed: DataContext mismatch");
                    RefreshNoteBinding();
                    return;
                }
                
                // Check if we can still interact with the TextBox
                if (NewItemTextBox != null && !NewItemTextBox.IsEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("?? Health check failed: TextBox is disabled");
                    NewItemTextBox.IsEnabled = true;
                }
                
                // Update last activity if there's been recent interaction
                _lastActivity = DateTime.Now;
                
                System.Diagnostics.Debug.WriteLine("? Health check passed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error during health check: {ex.Message}");
                RefreshNoteBinding();
            }
        }

        private void NoteWindow_Activated(object sender, EventArgs e)
        {
            try
            {
                // When window becomes active, validate the state
                System.Diagnostics.Debug.WriteLine("?? Note window activated - performing health check");
                
                // Check if our note reference is still valid
                if (_note?.Items == null || DataContext != _note)
                {
                    System.Diagnostics.Debug.WriteLine("?? Note state inconsistency detected on activation");
                    RefreshNoteBinding();
                }
                
                // Ensure the text box is ready for input
                if (NewItemTextBox != null && !NewItemTextBox.IsFocused)
                {
                    // Small delay to ensure the window is fully activated
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        NewItemTextBox.IsEnabled = true;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error during window activation health check: {ex.Message}");
            }
        }

        private void NoteWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitialized = true;
        }

        private void NoteWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only save size after window is fully initialized to avoid saving intermediate sizes during window creation
            if (_isInitialized && WindowState == WindowState.Normal)
            {
                SaveWindowSize();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Stop health monitoring
                if (_healthCheckTimer != null)
                {
                    _healthCheckTimer.Stop();
                    _healthCheckTimer = null;
                    System.Diagnostics.Debug.WriteLine("?? Health monitoring stopped");
                }
                
                // Ensure we save the window size one final time when closing
                SaveWindowSize();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error during window closing: {ex.Message}");
            }
            finally
            {
                base.OnClosing(e);
            }
        }

        private void RestoreWindowSize()
        {
            // Set window size from note properties
            Width = _note.WindowWidth;
            Height = _note.WindowHeight;
        }

        private void SaveWindowSize()
        {
            // Save current window size to note properties
            if (WindowState == WindowState.Normal) // Only save size when not minimized/maximized
            {
                _note.WindowWidth = ActualWidth;
                _note.WindowHeight = ActualHeight;
                OnNoteChanged(); // Trigger save
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
            if (sender is TextBox textBox)
            {
                try
                {
                    // Add diagnostic logging for debugging
                    System.Diagnostics.Debug.WriteLine($"?? NewItemTextBox_KeyDown: Key={e.Key}, Text='{textBox.Text}', Items.Count={_note?.Items?.Count ?? -1}");
                    
                    // Validate that our note and its collection are still valid
                    if (_note?.Items == null)
                    {
                        System.Diagnostics.Debug.WriteLine("?? Note or Items collection is null - attempting recovery");
                        RefreshNoteBinding();
                        return;
                    }
                    
                    if (e.Key == Key.Enter)
                    {
                        bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                        bool isSingleLine = !textBox.Text.Contains('\n') && !textBox.Text.Contains('\r');
                        
                        // Add item if: Ctrl+Enter pressed, OR regular Enter with single-line text
                        if (isCtrlPressed || isSingleLine)
                        {
                            if (!string.IsNullOrWhiteSpace(textBox.Text) && 
                                textBox.Text != "Type here to add a new item..." &&
                                textBox.Text != "Type heading text..." &&
                                textBox.Text != "Type here to add a new item... (Ctrl+Enter to add)" &&
                                textBox.Text != "Type heading text... (Ctrl+Enter to add)")
                            {
                                try
                                {
                                    var newItem = new NoteItem 
                                    { 
                                        Text = textBox.Text.Trim(), 
                                        IsChecked = false, 
                                        IsCritical = false,
                                        IsHeading = _nextItemIsHeading
                                    };
                                    
                                    // Add with error handling
                                    _note.Items.Add(newItem);
                                    System.Diagnostics.Debug.WriteLine($"? Successfully added item: '{newItem.Text}'");
                                    
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
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"? Error adding item: {ex.Message}");
                                    // Attempt to refresh the binding and try again
                                    RefreshNoteBinding();
                                }
                            }
                            else if (isSingleLine)
                            {
                                // For empty text with single line, still handle to prevent beep
                                e.Handled = true;
                            }
                        }
                        // If it's multi-line text and no Ctrl, let Enter create a new line (don't handle)
                    }
                    else if (e.Key == Key.Escape && _nextItemIsHeading)
                    {
                        // Cancel heading mode on Escape
                        _nextItemIsHeading = false;
                        UpdateHeadingButtonAppearance();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? Critical error in NewItemTextBox_KeyDown: {ex.Message}");
                    // Last resort: refresh everything
                    RefreshNoteBinding();
                }
            }
        }

        /// <summary>
        /// Refreshes the note binding and data context to recover from sync issues
        /// </summary>
        private void RefreshNoteBinding()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("?? Attempting to refresh note binding...");
                
                // Force refresh the DataContext
                var currentNote = DataContext as Note;
                if (currentNote != null && currentNote != _note)
                {
                    System.Diagnostics.Debug.WriteLine("?? DataContext mismatch detected - updating reference");
                    // The note has been updated by sync, update our reference
                    DataContext = currentNote;
                }
                else if (currentNote == null)
                {
                    System.Diagnostics.Debug.WriteLine("?? DataContext is null - restoring");
                    DataContext = _note;
                }
                
                // Force refresh the UI bindings
                if (ItemsListBox != null)
                {
                    ItemsListBox.Items.Refresh();
                }
                
                // Ensure focus is on the text box
                NewItemTextBox?.Focus();
                
                System.Diagnostics.Debug.WriteLine("? Note binding refresh completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error during note binding refresh: {ex.Message}");
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
                    NewItemTextBox.Text = "Type heading text... (Ctrl+Enter to add)";
                }
            }
            else
            {
                HeadingButton.Background = new SolidColorBrush(Colors.Transparent);
                HeadingButton.BorderBrush = new SolidColorBrush(Colors.Gray);
                HeadingButton.Opacity = 0.6;
                HeadingButton.ToolTip = "Add heading";
                
                // Reset placeholder text
                if (NewItemTextBox.Text == "Type heading text... (Ctrl+Enter to add)")
                {
                    NewItemTextBox.Text = "Type here to add a new item... (Ctrl+Enter to add)";
                }
            }
        }

        private void NewItemTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Text == "Type here to add a new item... (Ctrl+Enter to add)" || 
                    textBox.Text == "Type heading text... (Ctrl+Enter to add)" ||
                    textBox.Text == "Type here to add a new item..." || 
                    textBox.Text == "Type heading text...")
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
                    textBox.Text = "Type heading text... (Ctrl+Enter to add)";
                }
                else
                {
                    textBox.Text = "Type here to add a new item... (Ctrl+Enter to add)";
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
                // Use Ctrl+Enter to save when editing multi-line text, allowing Enter for new lines
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    var parent = textBox.Parent as Grid;
                    if (parent != null)
                    {
                        var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                        if (textBlock != null)
                        {
                            textBox.Visibility = Visibility.Collapsed;
                            textBlock.Visibility = Visibility.Visible;
                            OnNoteChanged(); // Save on Ctrl+Enter
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
            try
            {
                // Update last activity timestamp
                _lastActivity = DateTime.Now;
                
                // Validate that we can still notify changes
                if (NoteChanged != null)
                {
                    // Raise the event to notify that the note has changed
                    NoteChanged?.Invoke(this, EventArgs.Empty);
                    System.Diagnostics.Debug.WriteLine("?? Note change notification sent successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("?? No change event handlers registered");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error notifying note changes: {ex.Message}");
                // Don't let this fail silently - the note changes should still be saved
                // The auto-save system should pick this up anyway
            }
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
            
            // Hide all drop indicators when drag ends
            HideAllDropIndicators();
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
            
            // Hide all drop indicators
            HideAllDropIndicators();
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

        private void PaperNoteButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.DataContext is NoteItem item)
            {
                // Only allow text attachment creation for non-heading items
                if (!item.IsHeading)
                {
                    try
                    {
                        var textAttachmentWindow = new PopupNoteWindow(item);
                        
                        // Subscribe to the AttachmentSaved event
                        textAttachmentWindow.AttachmentSaved += (s, attachmentText) =>
                        {
                            OnNoteChanged(); // Trigger save when text attachment is saved
                        };
                        
                        // Subscribe to the AttachmentDeleted event
                        textAttachmentWindow.AttachmentDeleted += (s, args) =>
                        {
                            OnNoteChanged(); // Trigger save when text attachment is deleted
                        };
                        
                        // Show the text attachment editor window
                        textAttachmentWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error opening text attachment editor: {ex.Message}");
                        MessageBox.Show($"Error opening text attachment editor: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                e.Handled = true; // Prevent further event processing
            }
        }

        private void ItemBorder_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("NoteItem") && sender is FrameworkElement element)
            {
                // Find the drop indicator in this item's template
                if (element.Parent is StackPanel stackPanel)
                {
                    var dropIndicator = stackPanel.Children.OfType<Rectangle>()
                        .FirstOrDefault(r => r.Name == "DropIndicator");
                    
                    if (dropIndicator != null)
                    {
                        dropIndicator.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void ItemBorder_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                // Find the drop indicator in this item's template
                if (element.Parent is StackPanel stackPanel)
                {
                    var dropIndicator = stackPanel.Children.OfType<Rectangle>()
                        .FirstOrDefault(r => r.Name == "DropIndicator");
                    
                    if (dropIndicator != null)
                    {
                        dropIndicator.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void HideAllDropIndicators()
        {
            // Find all drop indicators in the ItemsControl and hide them
            if (ItemsListBox != null)
            {
                foreach (var item in ItemsListBox.Items)
                {
                    var container = ItemsListBox.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container != null)
                    {
                        var stackPanel = FindChild<StackPanel>(container);
                        if (stackPanel != null)
                        {
                            var dropIndicator = stackPanel.Children.OfType<Rectangle>()
                                .FirstOrDefault(r => r.Name == "DropIndicator");
                            
                            if (dropIndicator != null)
                            {
                                dropIndicator.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                }
            }
        }

        private T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void RefreshNote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("?? Manual refresh requested by user");
                RefreshNoteBinding();
                
                // Show user feedback with a simple message box for now
                MessageBox.Show("?? Note refreshed successfully!", "Refresh Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error during manual refresh: {ex.Message}");
                MessageBox.Show($"Error refreshing note: {ex.Message}", "Refresh Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HealthCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("?? Manual health check requested by user");
                
                bool isHealthy = true;
                var issues = new List<string>();
                
                // Check note validity
                if (_note?.Items == null)
                {
                    issues.Add("Note or Items collection is null");
                    isHealthy = false;
                }
                
                // Check DataContext
                if (DataContext != _note)
                {
                    issues.Add("DataContext mismatch detected");
                    isHealthy = false;
                }
                
                // Check TextBox
                if (NewItemTextBox != null && !NewItemTextBox.IsEnabled)
                {
                    issues.Add("Input TextBox is disabled");
                    isHealthy = false;
                }
                
                string message;
                if (isHealthy)
                {
                    message = $"? Note Health Check Passed\n\n" +
                             $"• Note Items: {_note?.Items?.Count ?? 0} items\n" +
                             $"• DataContext: Valid\n" +
                             $"• Input TextBox: Enabled\n" +
                             $"• Last Activity: {_lastActivity:HH:mm:ss}\n" +
                             $"• Window State: Normal";
                }
                else
                {
                    message = $"?? Note Health Issues Detected\n\n" +
                             $"Issues found:\n" + string.Join("\n", issues.Select(i => $"• {i}")) +
                             $"\n\nClick 'Refresh Note' to attempt automatic recovery.";
                             
                    // Auto-attempt recovery
                    RefreshNoteBinding();
                }
                
                MessageBox.Show(message, "Note Health Check", MessageBoxButton.OK, 
                    isHealthy ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error during health check: {ex.Message}");
                MessageBox.Show($"Error during health check: {ex.Message}", "Health Check Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}